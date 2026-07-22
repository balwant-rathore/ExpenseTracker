# ADR-0017: Frontend Token Storage — Access Token In Memory, Refresh Token in localStorage

## Status

Accepted

## Context

`docs/SDS.md` §4.3–4.4 defines the access token (JWT, HS256, 15-minute lifetime, `sub` claim
only) and refresh token (random, 7-day lifetime, rotated on every use) contracts, but does not
specify how a browser-based client should store either one between requests or page loads. ET016
is the first ticket to build a frontend client for these tokens, so this is a new decision, not a
deviation from an existing one — recorded here per `AGENTS.md` §13 ("a design/proposal doc that
goes stale during coding is itself a defect ... update in the same change").

Two ends of the trade-off space:
- Persisting both tokens (e.g. in `localStorage`) makes the session survive a page reload/reopened
  tab with no extra work, but exposes both tokens to any script-injection (XSS) vulnerability for
  their full lifetime.
- Keeping everything in memory only is most resistant to XSS token theft (nothing survives a
  reload for a script to read later) but forces re-login on every page reload or tab reopen,
  which is poor UX for a workday-long internal tool.

## Decision

- The **access token** is held only in memory (a Zustand store field), for the lifetime of the
  page. It is never written to `localStorage`, `sessionStorage`, or any cookie.
- The **refresh token** is persisted in `localStorage` (key `expensetracker.refreshToken`), so the
  session survives a reload or reopened tab.
- On application startup, if a refresh token is present in `localStorage`, the app immediately and
  silently calls `POST /api/auth/refresh` to obtain a fresh access/refresh token pair, then calls
  `GET /api/auth/me` with the new access token to obtain the `User` object, before rendering any
  protected route. A failure in either call clears the stored refresh token and the user is
  treated as unauthenticated. (`GET /api/auth/me` is a new backend endpoint introduced alongside
  this ADR — see "Why `GET /api/auth/me` exists" below. `POST /api/auth/refresh` itself returns
  only `{accessToken, refreshToken}`, never a `User`, so without this endpoint there would be no
  way to recover the user's identity/role after a reload without decoding it from the JWT — which
  AGENTS.md §11 explicitly forbids ("never embed anything beyond `sub`... never trust roles from
  the JWT").
- A proactive refresh timer, computed from the access token's own `exp` claim (decoded client-side,
  no new dependency — see `frontend/src/lib/jwt.ts`) minus a 60-second safety margin, renews the
  access token before it expires during an active session, rather than waiting for a request to
  fail with `401`.
- Logout clears both the in-memory access token and the stored refresh token unconditionally, even
  if the `POST /api/auth/logout` call itself fails.

## Why `GET /api/auth/me` exists

Discovered mid-implementation: none of the existing auth responses give the frontend a way to
recover `User` (name/role/employeeNumber) after a page reload. `POST /api/auth/refresh` returns
only tokens (`Application/Auth/RefreshResponse.cs`); embedding role/identity in the JWT itself is
explicitly forbidden (AGENTS.md §11). The only endpoints that ever return a `User` are `register`
and `login` — neither of which runs again on a reload.

Rather than work around this with a client-side cache of stale user data (rejected — see
conversation), a new authenticated `GET /api/auth/me` endpoint was added to the backend, following
the exact same "re-resolve from the `Employee` record" pattern `EmployeeRoleResolutionMiddleware`
and `AuthService.LoginAsync`/`RegisterAsync` already use, returning the same `UserDto` shape.
This was implemented alongside this frontend ticket (ET016) rather than deferred, per explicit
user direction, in an isolated worktree to keep frontend/backend changes from interleaving in one
working tree (per `CLAUDE.md` "Parallel Work").

## Consequences

- The access token's XSS exposure window is bounded to the current page's lifetime (never
  persisted); the refresh token's exposure window is bounded to its 7-day server-side lifetime,
  same as it would be under any storage choice, since the backend is the source of truth for
  revocation.
- A user who never returns within the refresh token's 7-day window must log in again — expected
  and matches the backend's own refresh-token lifetime design.
- If the backend's access-token claim shape ever changes such that `exp` is no longer present or
  decodable, `decodeJwtExpiry` must fail safe (treat the session as needing immediate refresh)
  rather than throw — enforced by a dedicated unit test.
- This ADR does not change any backend behavior; it constrains only the frontend
  (`frontend/src/store/authStore.ts`, `frontend/src/features/auth/session/`).
