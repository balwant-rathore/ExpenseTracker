## Why

The backend authentication APIs (registration, login, logout, refresh, forgot/reset password —
ET003–ET005) are done, but there is no way for a user to reach them: the frontend has no auth
routes, forms, session/token handling, or route protection. ET016 builds the React authentication
module so a user can register, log in, reset a forgotten password, and reach an authenticated
area, with every other frontend ticket (ET017–ET019) depending on the session/route-guard
mechanism this ticket establishes.

## What Changes

- Add a public auth route group (`/login`, `/register`, `/forgot-password`, `/reset-password`)
  built with React Router, React Hook Form, and Zod schemas mirroring — but not replacing —
  backend validation (`docs/FRS.md` §3.1–3.2, §3.4; `docs/SDS.md` §5.1).
- Add a login page/form calling `POST /api/auth/login`, displaying the backend's single generic
  `AUTHENTICATION_FAILED` error without indicating which field was wrong (`docs/FRS.md` §3.2.2).
- Add a registration page/form calling `POST /api/auth/register` with `employeeNumber`, `email`,
  `password`; adds a client-side-only `confirmPassword` field (Zod cross-field check) that is
  never sent to the API (`docs/FRS.md` §3.1). **Note**: `docs/SDS.md` §5.1 still names this field
  `employeeId`, but ET004 renamed it to `employeeNumber` in the shipped `RegisterRequest` DTO —
  a decision already recorded in `docs/decisions/ADR-0003-registration-employeenumber-field.md`,
  which explicitly requires ET016 to use `employeeNumber`. `docs/SDS.md` §5.1 remains stale and
  pending its own follow-up doc fix (ADR-0003's own "Consequences" section already flags this).
- Add a two-step forgot/reset password flow: step 1 calls `POST /api/auth/forgot-password` with
  `email` and always shows the same generic confirmation; step 2 collects `otp` + `newPassword`
  (+ client-only confirm) and calls `POST /api/auth/reset-password` (`docs/FRS.md` §3.4).
- Add client-side session management: access token held in memory only (Zustand), refresh token
  persisted in `localStorage`, a silent-refresh call on app load when a refresh token is present,
  and a proactive refresh timer that renews the access token shortly before its 15-minute expiry
  (`docs/SDS.md` §4.3–4.4). Logout calls `POST /api/auth/logout` and clears both tokens
  (`docs/FRS.md` §3.3).
- Add route guards: an authentication gate (redirect to `/login` if no valid session) and a
  role-based guard (`<RequireRole>`) that restricts a route to one or more `EmployeeRole` values,
  read from the authenticated user returned at login/register/refresh — never decoded from the JWT
  itself (`docs/SDS.md` §4.1, §4.6; AGENTS.md §11 "never trust roles from the JWT").
- Add a minimal authenticated placeholder route at `/dashboard` (welcome message + logout button)
  so login/registration/refresh and the route guards have a real destination to redirect to and
  to be tested against end-to-end; ET019 replaces its contents with the real dashboard.
- **Open architecture decision requiring an ADR**: `docs/SDS.md` does not specify a client-side
  token storage strategy. This change introduces one (in-memory access token, `localStorage`
  refresh token, proactive silent refresh) and it must be recorded as an ADR in
  `docs/decisions/` before/alongside implementation, per AGENTS.md §13.

## Capabilities

### New Capabilities
- `frontend-login-ui`: Login page/form, submission to `/api/auth/login`, generic error display,
  redirect to the authenticated landing route on success.
- `frontend-registration-ui`: Registration page/form, submission to `/api/auth/register`,
  client-only confirm-password check, field-level error display, immediate authenticated session
  on success.
- `frontend-password-reset-ui`: Two-step forgot-password/reset-password UI calling the two
  respective backend endpoints, generic messaging for the request step, OTP + new-password entry
  for the reset step.
- `frontend-session-management`: Access-token-in-memory / refresh-token-in-`localStorage` storage,
  silent refresh on app load, proactive pre-expiry refresh timer, logout/token-clearing behavior.
- `frontend-route-guards`: Authentication-gate wrapper redirecting unauthenticated users to
  `/login`, and a role-based route restriction wrapper driven by the authenticated user's role.

### Modified Capabilities
- None. All existing `openspec/specs/` capabilities are backend-only and unaffected; this change
  only adds frontend consumers of their already-specified contracts.

## Impact

- **Affected code**: `frontend/src/features/auth/**` (new), `frontend/src/routes/**` (new auth
  routes + guards), `frontend/src/store/` (new Zustand auth store), `frontend/src/api/` (auth API
  client + TanStack Query hooks), a new minimal `frontend/src/pages/DashboardPage.tsx` placeholder.
- **One backend addition, discovered mid-implementation**: `GET /api/auth/me` (authenticated,
  returns the current user's `UserDto`). `POST /api/auth/refresh` returns only tokens, and the
  JWT may never carry role/identity (AGENTS.md §11), so there was no way to recover `user` after
  a page reload without it. Implemented in an isolated worktree per explicit user direction rather
  than deferred; see `docs/decisions/ADR-0017-frontend-token-storage.md`. No other backend
  endpoints, DTOs, or migrations are touched.
- **New dependency surface**: `@playwright/test` as a new root-level devDependency (see below) —
  no other new pnpm packages expected beyond the stack already declared in `docs/SDS.md` §2
  (React Router, TanStack Query, Zustand, React Hook Form, Zod).
- **E2E scope pulled forward from ET020**: per explicit user decision, this ticket adds one
  Playwright smoke spec per new UI path (login, registration, forgot/reset password, protected
  route redirect) rather than deferring all E2E to ET020. This introduces the repo's first
  `playwright.config.ts` and `@playwright/test` dependency — infrastructure `docs/TICKETS.md`'s
  ET020 row currently claims as its own scope. ET020 still owns the full FRS traceability matrix,
  CI wiring, and E2E coverage of every other ticket's flows; ET016 only bootstraps the harness and
  covers its own four UI paths.
- **Documentation debt**: requires one new ADR in `docs/decisions/` for the token-storage
  decision noted above before this change can be considered fully compliant with AGENTS.md §13.
