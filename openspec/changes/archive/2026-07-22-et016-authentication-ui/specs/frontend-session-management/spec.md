## ADDED Requirements

### Requirement: Access Token Held In Memory Only
The frontend SHALL hold the access token only in memory (e.g. a Zustand store) for the lifetime
of the page and SHALL NOT persist it to `localStorage`, `sessionStorage`, or any cookie
(`docs/SDS.md` §4.3; token storage architecture decision recorded in this change's proposal,
pending ADR).

#### Scenario: Access token is not found in persistent storage
- **WHEN** a user is logged in with an active session
- **THEN** the access token SHALL NOT be present in `localStorage`, `sessionStorage`, or any
  cookie readable by client script

### Requirement: Refresh Token Persistence and Silent Refresh on Load
The frontend SHALL persist the refresh token in `localStorage` so the session survives a page
reload or reopened tab. On application startup, if a refresh token is present, the frontend SHALL
silently call `POST /api/auth/refresh` to obtain a new access/refresh token pair before rendering
protected content (`docs/SDS.md` §4.4).

#### Scenario: Reloading the app with a stored refresh token restores the session
- **WHEN** the application loads and a refresh token exists in `localStorage`
- **THEN** the frontend SHALL call `POST /api/auth/refresh` before rendering any protected route,
  and on success SHALL restore the authenticated session without requiring the user to re-enter
  credentials

#### Scenario: No stored refresh token leaves the user unauthenticated
- **WHEN** the application loads and no refresh token exists in `localStorage`
- **THEN** the frontend SHALL treat the user as unauthenticated without calling
  `POST /api/auth/refresh`

#### Scenario: An invalid or expired stored refresh token clears the session
- **WHEN** the silent `POST /api/auth/refresh` call on startup responds `401`
- **THEN** the frontend SHALL clear the stored refresh token and treat the user as
  unauthenticated

### Requirement: Proactive Access Token Refresh Before Expiry
The frontend SHALL schedule a refresh of the access token shortly before its 15-minute expiry
(`docs/SDS.md` §4.3) using the current refresh token, rather than waiting for a request to fail
with `401`.

#### Scenario: Access token is renewed before it expires during an active session
- **WHEN** a user holds an active session for longer than the access token's lifetime
- **THEN** the frontend SHALL call `POST /api/auth/refresh` before the current access token
  expires and replace both the in-memory access token and the stored refresh token with the
  newly issued pair, without interrupting the user's current screen

### Requirement: Logout Clears Session
Logout SHALL call `POST /api/auth/logout` with the current refresh token, then clear both the
in-memory access token and the persisted refresh token regardless of the API response
(`docs/FRS.md` §3.3).

#### Scenario: Logout clears local session state
- **WHEN** a user triggers logout
- **THEN** the frontend SHALL call `POST /api/auth/logout`, clear the in-memory access token and
  the `localStorage` refresh token, and navigate to `/login`

#### Scenario: Logout clears local session even if the API call fails
- **WHEN** a user triggers logout and `POST /api/auth/logout` fails or times out
- **THEN** the frontend SHALL still clear the in-memory access token and the `localStorage`
  refresh token and navigate to `/login`
