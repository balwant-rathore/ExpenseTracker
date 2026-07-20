## Why

The backend currently has no authentication or authorization primitives at all — every later
auth/expense ticket (ET004 register/login/refresh/logout, ET005 password reset, and every
role-gated expense endpoint from ET007 onward) depends on JWT issuance, refresh-token lifecycle
management, password hashing, per-request role resolution, and rate-limiting policies already
existing. ET003 builds that shared foundation now, with no public endpoints of its own, so ET004+
can wire dedicated action endpoints against it per `docs/TICKETS.md`'s fixed build order.

## What Changes

- Add `IJwtTokenService` (`Application`) to issue and validate HS256 JWT access tokens: 15-minute
  lifetime, `sub` (UserId) as the only claim — no role or permission claims embedded
  (`docs/FRS.md` §3.2.1, `docs/SDS.md` §4.3).
- Add `IRefreshTokenService` (`Application`) + `RefreshToken` persistence (already modeled in
  `domain-model`) to generate a cryptographically random 7-day refresh token, persist only its
  SHA-256 hash, rotate it on every use, and revoke all of a user's refresh tokens when a
  already-revoked token is reused or a password reset completes (`docs/FRS.md` §3.3.1–3.3.2,
  §3.4.5, `docs/SDS.md` §4.4).
- Add `IPasswordHasher` (`Application`) wrapping BCrypt for hash + verify, enforcing the password
  policy (min 8 chars, ≥1 letter, ≥1 digit) as a reusable validator (`docs/FRS.md` §3.1.2,
  `docs/SDS.md` §4.2).
- Add JWT bearer authentication wiring plus custom role-resolution middleware: after JWT
  validation, the middleware loads the caller's `Employee` record via `UserId` (`sub`) and adds
  the resolved `EmployeeRole` as a claim on the current request's `ClaimsPrincipal` — the token
  itself never carries a role, and the claim is re-derived every request (`docs/AGENTS.md` §7,
  §11; `docs/SDS.md` §4.1, §4.6).
- Add named ASP.NET Core authorization policies for each `EmployeeRole` (Employee, Manager,
  Finance, ComplianceOfficer), consumed later via `[Authorize(Policy = ...)]` per
  `backend/CLAUDE.md`'s policy-based-authorization guidance — no inline `if (role == ...)` checks.
- Add named ASP.NET Core `RateLimiter` middleware policies (sliding-window, placeholder limits) for
  the four auth endpoints (`register`, `login`, `forgot-password`, `reset-password`) so ET004/ET005
  only need to annotate their new endpoints once built (`docs/FRS.md` §3.5, `docs/SDS.md` §4.7).
  Numeric limits are a placeholder pending real endpoint load-testing in ET004/ET005 — logged as an
  open decision below.
- Wire the JWT signing key through `dotnet user-secrets` for local development, matching the
  existing DB-connection-string pattern in `backend/CLAUDE.md` — never committed to
  `appsettings.*.json`.
- **No new controller endpoints.** This ticket is infrastructure only; `register`/`login`/
  `refresh`/`logout` land in ET004, `forgot-password`/`reset-password` in ET005. Everything above
  is verified via unit tests and integration tests that exercise the services, middleware, and
  policies directly (via `WebApplicationFactory` with a minimal in-test-project endpoint), not
  through any shipped `Api` route.
  - **Clarification**: the test-only endpoint is mapped via a shared `CustomWebApplicationFactory`
    (`ConfigureTestServices` / endpoint routing hook) in `backend/tests/IntegrationTests`, layered
    on top of the real `Program.cs` pipeline — it is never a controller file in `Api`. This fixture
    is built once in ET003 and reused by ET004/ET005 integration tests.

## Capabilities

### New Capabilities
- `jwt-token-issuance`: JWT access-token generation and validation (HS256, 15 min, `sub`-only claim).
- `refresh-token-lifecycle`: Refresh token generation, SHA-256 hashing, rotation-on-use, and
  revoke-all-on-reuse/on-password-reset.
- `password-hashing`: BCrypt password hash/verify service and password complexity validation.
- `role-based-authorization`: Per-request Employee role resolution middleware plus named
  authorization policies per `EmployeeRole`.
- `auth-rate-limiting`: Named `RateLimiter` policies and middleware registration for the four
  rate-limited auth endpoints.

### Modified Capabilities
_None._ This ticket adds new `Application`/`Api` infrastructure; it does not change the
`domain-model` or `project-bootstrap` requirements already specified (it consumes the `User` and
`RefreshToken` entities `domain-model` already defines, unchanged).

## Impact

- **Affected projects**: `backend/src/Application` (new services + interfaces), `backend/src/Api`
  (DI wiring, JWT bearer auth, authorization policies, rate-limiter middleware registration,
  `appsettings.json` schema for `Jwt:*` and rate-limit settings), `backend/src/Infrastructure` (no
  new tables — reuses `RefreshToken`/`User` from `domain-model`).
- **New dependencies**: `Microsoft.AspNetCore.Authentication.JwtBearer`,
  `System.IdentityModel.Tokens.Jwt`, `BCrypt.Net-Next` (NuGet) — flagged per `CLAUDE.md`'s
  "always ask first" rule for adding a NuGet dependency.
- **Open architecture decision requiring an ADR** (`docs/decisions/`): the exact numeric
  rate-limit thresholds (requests per window) are not specified anywhere in `docs/FRS.md` or
  `docs/SDS.md`; ET003 ships placeholder sliding-window limits that ET004/ET005 may need to
  revisit once the real endpoints exist.
- **Tests**: `backend/tests/UnitTests` for token/hashing/rate-limit-policy logic in isolation;
  `backend/tests/IntegrationTests` for the JWT bearer + role-resolution middleware pipeline against
  a temporary test-only endpoint mapped by the shared `CustomWebApplicationFactory` (see
  Clarification above) — not a route defined in `Api`. ET004/ET005 reuse this same fixture.
