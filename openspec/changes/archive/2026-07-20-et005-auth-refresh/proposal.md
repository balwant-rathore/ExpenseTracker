## Why

ET004 delivered registration, login, refresh, and logout, but users who forget their password
have no self-service recovery path, and the placeholder rate-limiter policies registered in ET003
still aren't attached to any real endpoint. Ticket ET005 (`docs/TICKETS.md`) closes out the
Authentication domain by adding forgot-password/OTP/reset-password (`docs/FRS.md` §3.4–3.5,
`docs/SDS.md` §4.5) and by replacing Swagger UI with Scalar for OpenAPI browsing (ADR-0004).

## What Changes

- Add `POST /api/auth/forgot-password` — accepts an `email`, always returns the same `200 OK`
  regardless of whether the account exists (`docs/FRS.md` 3.4.1). If the account exists, generates
  a 6-digit numeric OTP, hashes it (SHA-256), persists it via `PasswordResetOtp` with a 10-minute
  expiry, invalidates any prior unused OTP for that user (3.4.6), and logs the plaintext OTP to the
  server console only (`docs/SDS.md` §4.5).
- Add `POST /api/auth/reset-password` — accepts `email`, `otp`, `newPassword`; validates the OTP
  (exists, unexpired, unused, matches hash) and the new password's complexity, then updates the
  user's `PasswordHash`, marks the OTP used, and revokes **all** of that user's active refresh
  tokens (3.4.5) via the existing bulk-revocation method (`refresh-token-lifecycle` spec, already
  implemented in ET003 — consumed, not modified, by this ticket).
  - Expired OTP → `410 RESOURCE_EXPIRED`.
  - Wrong or already-used OTP → `401 AUTHENTICATION_FAILED` (generic, no distinction from expired
    vs. wrong vs. used beyond the expired case, per anti-enumeration principle).
  - Weak new password → `400 VALIDATION_ERROR` with field-level detail (reusing the existing
    password-complexity validator from `password-hashing`).
- Attach the ET003 placeholder `forgot-password` and `reset-password` `RateLimiter` policies
  (`auth-rate-limiting` spec) to these two new endpoints with concrete sliding-window limits,
  matching the login/registration policy shape (3.5.3, 3.5.4). This finalizes the "pending real
  endpoint availability" placeholder note in that spec — no new policy design, just attachment +
  concrete limits.
- **BREAKING (dev-facing only, not a public API contract)**: Replace Swagger UI with Scalar as the
  interactive OpenAPI documentation UI, served against the same `/swagger/v1/swagger.json`
  (or equivalent) OpenAPI document ASP.NET Core already generates — no change to the OpenAPI
  document/contract itself, only to the browsing UI. This is a deviation from `docs/SDS.md` §2/§11,
  which only names "OpenAPI (Swagger)" as the documentation tool without specifying a UI product;
  captured as **ADR-0004** in `docs/decisions/`.

## Capabilities

### New Capabilities
- `password-reset-otp`: forgot-password request + OTP generation/hashing/expiry/invalidation, and
  reset-password OTP verification + password update + full refresh-token revocation.
- `openapi-documentation-ui`: Scalar as the served interactive OpenAPI UI, replacing Swagger UI,
  per ADR-0004.

### Modified Capabilities
- `auth-rate-limiting`: the existing placeholder policies for `forgot-password` and
  `reset-password` (registered but unattached in ET003) are attached to real endpoints with
  concrete limits — the spec's "this ticket adds no endpoints that use them" caveat no longer
  holds after ET005.

## Impact

- **Backend**: new `Application` service method(s) on (or alongside) the existing auth service for
  forgot-password/reset-password; `IPasswordResetOtpService` (or similar) using the existing
  `PasswordResetOtp` entity (already modeled in `docs/SDS.md` §3.5, no schema migration expected);
  reuse of `IPasswordHasher`, the password-complexity validator, and the refresh-token
  bulk-revocation method. New `AuthController` actions with `[EnableRateLimiting]` attached. Scalar
  NuGet package addition and Program.cs/Startup wiring change (removes or replaces Swagger UI
  middleware registration).
- **No frontend changes** — ET016 (React auth module) consumes these endpoints later.
- **No DB schema changes anticipated** — `PasswordResetOtp` table already exists per ET002.
- **Docs**: new `docs/decisions/ADR-0004-scalar-openapi-ui.md`.
