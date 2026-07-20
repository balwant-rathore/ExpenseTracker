## Why

ET003 delivered the auth *infrastructure* (JWT issuance, refresh-token lifecycle, BCrypt hashing,
role policies, rate-limiter policies) but no HTTP endpoints exist yet — `backend/src/Api/Controllers/`
contains only `HealthController`. Employees cannot register, log in, refresh a session, or log out.
ET004 (`docs/TICKETS.md`) closes that gap by exposing the four auth endpoints defined in
`docs/SDS.md` §5.1, implementing `docs/FRS.md` §3.1–3.3, on top of the services ET003 already built.

## What Changes

- Add `POST /api/auth/register` (`docs/FRS.md` §3.1, `docs/SDS.md` §5.1): validates the supplied
  `employeeNumber` against a pre-seeded, active `Employee.EmployeeNumber`, enforces unique
  case-insensitive email and password complexity, creates the `User` account, and immediately logs
  the user in (issues access + refresh token). **Deviates from `docs/SDS.md` §5.1's literal
  `employeeId` field name — see `docs/decisions/ADR-0003-registration-employeenumber-field.md`.**
- Add `POST /api/auth/login` (`docs/FRS.md` §3.2): verifies email + password, returns a generic
  `AUTHENTICATION_FAILED` error for any mismatch (wrong email or wrong password, indistinguishable),
  issues access + refresh token on success.
- Add `POST /api/auth/refresh` (`docs/SDS.md` §5.1): redeems a refresh token via the existing
  `IRefreshTokenService.RedeemAsync`, returns a new access + refresh token pair, or `401` on
  not-found/expired/reuse-detected.
- Add `POST /api/auth/logout` (`docs/FRS.md` §3.3, requires `Authorization: Bearer`): revokes only
  the single refresh token supplied in the request body (not a bulk revoke — bulk revoke stays
  reserved for password-reset in ET005 and reuse-detection).
- Add request/response DTOs (`RegisterRequest`, `LoginRequest`, `RefreshRequest`, `LogoutRequest`,
  `AuthResponse`, `UserDto`, `TokenPairDto`) — none exist yet.
- Introduce FluentValidation as the project's validation library for these DTOs (no validation
  library exists yet; this choice sets precedent for later tickets per `backend/CLAUDE.md`).
  **Requires an explicit NuGet package add (`dotnet add package FluentValidation`), which needs
  user approval per `CLAUDE.md`'s permission model before `/implement` runs it.**
- `UserDto` in register/login responses includes `id`, `email`, `employeeNumber`, `firstName`,
  `lastName`, `role` — never `passwordHash` (`docs/FRS.md` §3.1.4).
- Registration rejects with a generic error (no field-level distinction) when the employee number
  doesn't match any seeded `Employee`, or matches one with `IsActive = false` — both cases return
  the same message to avoid revealing which condition applied (anti-enumeration).
- `/api/auth/refresh` and `/api/auth/logout` are **not** rate-limited — only register/login stay
  behind the ET003 `AuthRegister`/`AuthLogin` policies, matching `docs/SDS.md` §5.1's documented
  error responses (no `429` listed for refresh/logout).

## Capabilities

### New Capabilities
- `user-registration`: `POST /api/auth/register` — employee-number validation, duplicate-email and
  inactive-employee rejection, password complexity, account creation, immediate token issuance.
- `user-login`: `POST /api/auth/login` — credential verification with a single generic failure
  error for any invalid combination.
- `session-refresh`: `POST /api/auth/refresh` — HTTP contract wrapping the existing
  `IRefreshTokenService.RedeemAsync` redemption/rotation behavior.
- `user-logout`: `POST /api/auth/logout` — authenticated endpoint revoking exactly the one
  refresh token supplied by the caller.

### Modified Capabilities
- `refresh-token-lifecycle`: adds a new "Single Refresh Token Revocation" requirement (revoke one
  named token, distinct from the existing "Bulk Refresh Token Revocation" requirement used by
  reuse-detection and reserved for ET005's password-reset flow) — needed by the `user-logout`
  capability.

## Impact

- **New files**: `backend/src/Api/Controllers/AuthController.cs`; DTOs and FluentValidation
  validators under `backend/src/Application/Auth/`; wiring in
  `backend/src/Api/Extensions/AuthServiceCollectionExtensions.cs`.
- **New dependency**: FluentValidation NuGet package (approval required before add).
- **Modified**: `refresh-token-lifecycle` spec gains a single-token-revoke requirement;
  `IRefreshTokenService` gains a `RevokeAsync(rawToken, ...)`-shaped method (exact signature is a
  design.md concern, not this proposal's).
- **Docs**: `docs/SDS.md` §5.1 should be updated to say `employeeNumber` instead of `employeeId`
  in a follow-up docs change (tracked by ADR-0003, not part of this ticket's code scope).
- **No** new rate-limiter policies, no changes to `jwt-token-issuance`, `password-hashing`,
  `auth-rate-limiting`, or `role-based-authorization` specs.
