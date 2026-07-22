# ADR-0021: Disable `UseHttpsRedirection` in Development to Fix Auth-Token Loss on Split Dev Runs

## Status

Accepted

## Context

`ADR-0019` added a real CORS policy so the API is reachable when the frontend and backend run as
genuinely separate origins, but it assumed the only failure mode was a missing
`Access-Control-Allow-Origin` header. It did not account for `Program.cs`'s
`app.UseHttpsRedirection()`, which is unconditional (runs in every environment).

`backend/src/Api/Properties/launchSettings.json` defines an `https` profile that binds both
`https://localhost:7126` and `http://localhost:5158`. When a developer runs that profile (the
common case — it's what Visual Studio/`dotnet run` typically launch), any request that arrives on
the `:5158` HTTP binding gets a `307` redirect to `:7126` from `UseHttpsRedirection`.
`frontend/vite.config.ts`'s dev proxy forwards `/api` to the hardcoded `http://localhost:5158`
target, so every proxied request hits that redirect.

Two failures follow from that single `307`:
- The browser's `fetch` (in `apiClient.ts`) follows the redirect itself, landing on
  `https://localhost:7126` — a different origin than the one the Vite proxy and the backend's
  `Cors:AllowedOrigins` (`http://localhost:5173`) were configured around, and often a self-signed
  dev cert the browser hasn't trusted, surfacing as a connection/redirect error.
- Per the Fetch spec, a redirect that crosses origin strips the `Authorization` request header
  before the redirected request is sent — so even when the redirect target were reachable, every
  authenticated API call would silently lose its bearer token and come back `401`.

This reproduces only when frontend and backend are started as two independent local processes
(the documented workflow in `frontend/CLAUDE.md`/`backend/CLAUDE.md`) — not in any test suite,
since `WebApplicationFactory`-based integration tests never traverse the Vite proxy or a real
browser's fetch redirect handling. Confirmed with the ticket owner during `/spec ET018` as
in-scope for this change rather than a separate ticket, since it blocks manually verifying
everything else ET018 adds.

## Decision

Skip `UseHttpsRedirection()` in the `Development` environment; keep it unconditional everywhere
else:

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
```

This is the smallest change that removes the redirect entirely for local dev (both the `http` and
`https` launch profiles now serve `:5158` without a redirect), while leaving HTTPS enforcement
intact for every non-Development environment — the actual security property
`UseHttpsRedirection` exists for (rejecting/redirecting plain-HTTP traffic) only matters once the
API is reachable from outside a developer's own machine.

Rejected alternatives:
- **Point the Vite proxy at the HTTPS port (`:7126`) instead.** Still requires the browser (or
  Node's proxy agent) to trust the dev-only self-signed certificate, trading one dev-environment
  papercut for another, and does nothing for anyone who launches the `https` profile but still
  expects the plain `http` binding to work standalone (e.g. calling the API directly with `curl`
  during manual testing).
- **Make `apiClient.ts` re-attach `Authorization` after a redirect.** Not possible — `fetch`
  performs the redirect and header-stripping internally; user code never observes the intermediate
  `307` to re-add the header, short of reimplementing HTTP requests without `fetch`'s built-in
  redirect following (`redirect: 'manual'`), which is a much larger change for a dev-only problem.

## Consequences

- No behavior change in any non-Development environment — `UseHttpsRedirection` still runs there.
- Local dev (either launch profile) no longer redirects `:5158` traffic, so the Vite proxy's
  existing hardcoded `http://localhost:5158` target (unchanged) reaches the API directly,
  same-origin from the browser's perspective, exactly as `ADR-0019`'s D1 already intended.
- `WebApplicationFactory<Program>` actually defaults to the `Development` environment already
  (confirmed by `OpenApiDocumentationTests`, which depends on the Development-only `/scalar` and
  `/openapi` routes) — the opposite of what an earlier draft of this ADR assumed. Existing
  integration tests are still unaffected by this change, but not because they run outside
  Development: `UseHttpsRedirection` never had a determinable HTTPS port in the `TestServer`
  harness (no real Kestrel binding), so it already no-opped there regardless of this guard.
  `HttpsRedirectionTests` pins the real behavior by explicitly configuring `ASPNETCORE_HTTPS_PORT`
  so the middleware has a port to redirect to, then asserts no `307` comes back under the
  (already-default) `Development` environment.
