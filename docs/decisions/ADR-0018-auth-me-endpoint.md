# ADR-0018: Authenticated GET /api/auth/me for Session Restoration

## Status

Accepted

## Context

ET016 (frontend React auth module) needs to restore the current user's identity (name, role,
employee number) after a page reload, using only a stored refresh token. `POST /api/auth/refresh`
returns `{ accessToken, refreshToken }` only (`Application.Auth.RefreshResponse`) — it never
returns a `User`, because by design (`AGENTS.md` §11 / §7, `backend/CLAUDE.md` §Auth Approach) the
JWT access token carries only the `sub` claim and no role or profile data is ever trusted from the
token itself; role and identity are always re-resolved from the `Employee` record per request. No
endpoint previously existed for the frontend to fetch "who am I" after a silent refresh, leaving
it with a valid access token but no way to populate its in-memory user state.

## Decision

Add a new authenticated `GET /api/auth/me` endpoint to `AuthController`, guarded by `[Authorize]`
(the same attribute `Logout` already uses) and **not** rate-limited, since it requires an
already-valid JWT rather than being one of the four brute-forceable credential/OTP endpoints.

- `IAuthService.GetCurrentUserAsync(Guid userId, CancellationToken)` resolves the user id (from
  `User.GetUserId()`, the existing `ClaimsPrincipalExtensions` helper) via
  `IUserRepository.GetByIdWithEmployeeAsync`, exactly the same repository call and `UserDto`
  construction `LoginAsync`/`RegisterAsync` already use — no new resolution path.
- Reuses the existing `Application.Auth.UserDto` record as-is; the response body is `UserDto`'s
  fields flat at the root (no additional envelope), matching how `RefreshResponse` and other DTOs
  are already returned directly by this controller.
- If the user/employee can't be resolved (should not happen in practice, since
  `EmployeeRoleResolutionMiddleware` already rejects unauthenticated/inactive requests with `401
  AUTHENTICATION_FAILED` before the controller runs), the controller returns the same generic
  `401 AUTHENTICATION_FAILED` envelope via the existing `FailureResult(AuthFailureReason
  .RefreshTokenInvalid)` path — reused rather than adding a new `AuthFailureReason` member, since
  the resulting envelope is identical either way and this is a defensive branch, not a distinct
  business failure.

Rejected alternative: embed role/name claims directly in the JWT so the frontend could decode them
client-side without a round trip. Rejected because it directly contradicts the standing rule
(`AGENTS.md` §11) that roles are never trusted from the token and are always re-resolved from the
`Employee` record per request — an inactive employee or a role change must take effect
immediately, not only at the next login.

## Consequences

- Frontend's silent-refresh flow (ET016) calls `GET /api/auth/me` immediately after a successful
  `POST /api/auth/refresh` on app load, to repopulate its in-memory `user` state from a stored
  refresh token alone.
- No other endpoint's behavior, request/response shape, or auth requirement changes.
- `docs/SDS.md` §5.1 and root `AGENTS.md` §8 list `GET /api/auth/me` alongside the other
  `/api/auth/*` endpoints.
- No new DB schema, migration, or `AuthFailureReason` value was introduced.
