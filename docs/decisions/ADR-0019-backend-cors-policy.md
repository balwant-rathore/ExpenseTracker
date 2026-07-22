# ADR-0019: Backend CORS Policy Alongside the Vite Dev Proxy

## Status

Accepted

## Context

ET016's frontend design (D1 in the ET016 change, now archived) deliberately avoided adding CORS
to the backend, relying instead on a Vite dev-server proxy (`server.proxy` for `/api` in
`frontend/vite.config.ts`) so the browser only ever sees same-origin requests during
`pnpm --filter frontend dev`. That was a correct, minimal-blast-radius choice for the ticket at
the time — but it left a real, explicitly-flagged gap: the proxy only applies to the Vite dev
server. `vite preview`, a built `dist/` served from a static host, or any setup where the frontend
and backend genuinely run on different origins (e.g. a future production deployment) would have
no way to reach the API — a direct `fetch` would either 404 (relative `/api/...` resolving against
the frontend's own origin) or hit a hard CORS wall (zero server-side CORS headers configured).

This was surfaced explicitly as an open question in ET016's `design.md` and confirmed as a live,
unresolved gap by two independent `/review` passes.

## Decision

Add a real CORS policy to the backend (`Api.Cors.CorsPolicyNames.Frontend`), configured via a new
`Cors:AllowedOrigins` configuration section (array of origin strings), following the same
Options-pattern-plus-extension-method structure already used for auth (`AddAuthFoundation`),
attachments, notifications, etc.:

- `backend/src/Api/Cors/CorsOptions.cs` — `AllowedOrigins` string array, empty by default.
- `backend/src/Api/Cors/CorsPolicyNames.cs` — the single named policy, `"Frontend"`.
- `backend/src/Api/Extensions/CorsServiceCollectionExtensions.cs` — `AddCorsFoundation`, reads
  `Cors:AllowedOrigins` and builds a policy allowing those origins, any header, and
  `GET/POST/PUT/DELETE` methods. No `AllowCredentials()` — the app carries the access token via
  the `Authorization` header and response bodies, never cookies, so credentialed CORS is
  unnecessary (confirmed by `/review`: no `credentials: 'include'`/`withCredentials` anywhere in
  `frontend/src`).
- `Program.cs` wires `app.UseCors(CorsPolicyNames.Frontend)` **before** `app.UseRateLimiter()` and
  `app.UseAuthentication()`, so CORS preflight (`OPTIONS`) requests are handled without being
  subject to the auth rate limiter or requiring a token.
- Base `appsettings.json` defaults `Cors:AllowedOrigins` to an **empty array** — CORS denies all
  cross-origin requests until a specific environment explicitly configures allowed origins. This
  matches the "secure by default, opt in per environment" pattern already used for the JWT signing
  key (via user-secrets, not appsettings).
- `appsettings.Development.json` allows `http://localhost:5173` (Vite dev) and
  `http://localhost:4173` (`vite preview`), covering both the proxied dev flow and the
  previously-uncovered preview/static-serving case.

The Vite dev proxy (D1) is **not removed** — it remains the default dev experience (same-origin,
no preflight overhead, simplest mental model). CORS is an additive safety net for every other
serving scenario, not a replacement.

## Consequences

- Any future production deployment must set `Cors:AllowedOrigins` for its real environment
  (e.g. via `appsettings.Production.json` or environment variables) — an empty array in production
  means the deployed frontend's origin must be explicitly added or the API is unreachable from a
  browser. This is intentional (fail closed, not open) but must not be forgotten at deploy time.
- Verified live (not just unit-tested): a direct request to the backend port (bypassing the Vite
  proxy entirely) from an allowed origin receives `Access-Control-Allow-Origin`; a disallowed
  origin does not; a preflight `OPTIONS` for `POST /api/auth/login` from an allowed origin returns
  `204` with the expected `Access-Control-Allow-{Origin,Methods,Headers}` headers.
- New integration tests: `backend/tests/IntegrationTests/CorsTests.cs` (3 tests) verify the policy
  against the real, checked-in `appsettings.Development.json` values rather than per-test config
  overrides — `AuthFunctionalWebApplicationFactory`'s existing doc comment already documents why
  `ConfigureAppConfiguration` overrides don't reliably reach services (like `AddCorsFoundation`)
  that read `IConfiguration` synchronously while building services, before `WebApplicationFactory`
  merges its own config additions in. Same known limitation, same established workaround.
- No change to how the frontend calls the API — `apiClient.ts` still calls only relative
  `/api${path}` paths, riding the proxy in dev. CORS is what makes that same code work correctly
  if it's ever served from a different origin than the API.
