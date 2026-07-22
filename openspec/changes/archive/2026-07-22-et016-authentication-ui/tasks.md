Note: this ticket has no backend component (see `design.md` Non-Goals) — every task below is
frontend-only or repo-root tooling. `[PARALLEL]`/separate-worktree splitting per `CLAUDE.md`
"Parallel Work" applies to frontend-vs-backend work within one ticket; since there is no backend
work here, nothing in this file is marked `[PARALLEL]`.

## 1. Foundation & Scaffolding

- [x] 1.1 Write ADR `docs/decisions/ADR-0017-frontend-token-storage.md` documenting the
      token-storage decision (access token in memory, refresh token in `localStorage`, silent +
      proactive refresh) per `design.md` D2, per AGENTS.md §13.
- [x] 1.2 Ran `pnpm add -D -w @playwright/test` (new root devDependency, per `design.md` D7 and
      AGENTS.md's pnpm-add permission rule) — user confirmed.
- [x] 1.3 Added `playwright.config.ts` at the repo root (`baseURL` → Vite dev server) per D7.
- [x] 1.4 Added `server.proxy` for `/api` → `http://localhost:5158` in `frontend/vite.config.ts`
      per D1. (Live reachability check deferred to Section 5's manual smoke pass, once a full
      login/register flow exists to test against.)
- [x] 1.5 Generate shadcn primitives: `pnpm exec shadcn add input label card field` (run from
      `frontend/`; `form` was in the original plan but has no file content in this project's
      `base-nova` style — corrected to `field`, see design.md D6). Confirmed `package.json`
      gained no new dependency entries (hash-compared before/after).
- [x] 1.6 Added `frontend/src/types/auth.ts` (`User`, `EmployeeRole`, `AuthTokens`) matching the
      real `UserDto`/`AuthResponse`/`RefreshResponse` shapes (not the stale `docs/SDS.md` wording)
      per `design.md` Key Types.
- [x] 1.7 Added `frontend/src/lib/apiClient.ts` + `apiClient.test.ts` — fetch wrapper, `ApiError`
      type, error-envelope parsing, `Authorization` header attached via an explicit `accessToken`
      param (D3, refined: caller-supplied token instead of a direct store import, to avoid an
      import-order dependency on Phase 2's `authStore`).
- [x] 1.8 Added `frontend/src/lib/jwt.ts` + `jwt.test.ts` — `decodeJwtExpiry(token)`, with tests
      confirming a malformed/undecodable token falls back to `0` rather than throwing (D2, Risks).

**Checkpoint 1** (frontend only — no backend changes in this ticket):
- [x] 1.9 `pnpm --filter frontend lint` → 0 errors/warnings
- [x] 1.10 `pnpm exec tsc --noEmit --project frontend/tsconfig.app.json` → 0 errors
- [x] 1.11 `pnpm --filter frontend test` → all green (11 tests: apiClient 7, jwt 4)

## 2. Core Implementation — Session & API Layer

- [x] 2.1 Added `frontend/src/store/authStore.ts` + `authStore.test.ts` — `user`, `accessToken`,
      `status` (`bootstrapping`/`authenticated`/`unauthenticated`), session set/clear/setAccessToken
      actions (D2).
- [x] 2.2 Added `frontend/src/features/auth/session/scheduleProactiveRefresh.ts` + test — computes
      refresh delay from `decodeJwtExpiry` minus 60s margin (D2).
- [x] 2.3 Added `frontend/src/features/auth/session/useSessionBootstrap.ts` + test — reads stored
      refresh token on mount, calls `/api/auth/refresh` then `/api/auth/me` if present (D2a), sets
      `status` accordingly, re-checks on `visibilitychange` (D2, Risks). Also owns the proactive
      refresh timer (re-armed on every `accessToken` change) and the `localStorage` refresh-token
      helpers (`getStoredRefreshToken`/`storeRefreshToken`/`clearStoredRefreshToken`), reused by
      the mutation hooks in 2.5.
- [x] 2.4 Added `frontend/src/features/auth/api/authApi.ts` — `register`, `login`, `refresh`,
      `me`, `logout`, `forgotPassword`, `resetPassword` calls via `apiClient`, using the real
      request field names (`employeeNumber`, not `employeeId`; `me` added per D2a).
- [x] 2.5 Added TanStack Query hooks: `useLogin.ts`, `useRegister.ts`, `useLogout.ts`,
      `useForgotPassword.ts`, `useResetPassword.ts` — login/register store the refresh token and
      call `setSession`; logout clears local session `onSettled` (unconditionally, even on API
      failure) per the spec.
- [x] 2.6 **(New, per D2a)** Backend agent completed `GET /api/auth/me` in an isolated worktree
      (`D:\ClaudeCode\ExpenseTracker\.claude\worktrees\agent-a5ae531c888c427db`, branch
      `worktree-agent-a5ae531c888c427db`, commit `78bcee6`). Reuses `UserDto` flat shape and the
      existing `[Authorize]` + Employee-record-resolution pattern; 3 new integration tests; full
      backend suite green (533/533). Updated `docs/SDS.md` §5.1, `AGENTS.md` §8, and
      `docs/decisions/ADR-0017-auth-me-endpoint.md`. **Not merged/pushed** — sits in its own
      worktree/branch pending a separate decision on how to land it (see Follow-up Tasks in the
      final `/implement` summary).

## 3. Core Implementation — Forms & Schemas

- [x] 3.1 Added `frontend/src/features/auth/schemas/loginSchema.ts` (email + non-empty password).
- [x] 3.2 Added `frontend/src/features/auth/schemas/registrationSchema.ts` — `employeeNumber`,
      `email`, `password` (≥8 chars, ≥1 letter, ≥1 digit), `confirmPassword` cross-field refine
      (D5); `confirmPassword` is stripped before calling `authApi.register`.
- [x] 3.3 Added `frontend/src/features/auth/schemas/forgotPasswordSchema.ts` (email only).
- [x] 3.4 Added `frontend/src/features/auth/schemas/resetPasswordSchema.ts` — `email`,
      `otp` (`^\d{6}$`, matching `ResetPasswordRequestValidator` exactly), `newPassword`,
      `confirmNewPassword` cross-field refine (D5).
- [x] 3.4a **(unplanned addition)** Added `frontend/src/features/auth/utils/hasFieldError.ts` —
      case-insensitive match against the backend's `fields` array, needed because
      `AuthController` returns inconsistent casing (FluentValidation failures use the PascalCase
      C# property name; a couple of service-level checks hardcode lowerCamelCase) — discovered
      while implementing field-level error display.
- [x] 3.5 Added `frontend/src/features/auth/components/LoginForm.tsx` + test (shadcn
      `Field`/`FieldLabel`/`FieldError`, not `Form` — see D6 correction) — React Hook Form + Zod,
      calls `useLogin`, renders the generic `401`/`429` messages (never field-specific for auth
      failure), stores the session on success (navigation to `/dashboard` verified via store
      state, not DOM route change).
- [x] 3.6 Added `frontend/src/features/auth/components/RegistrationForm.tsx` + test — calls
      `useRegister`, renders field-level `400` errors, the generic `409` duplicate-email message,
      and the generic `422` invalid-employee-number message against `employeeNumber`.
- [x] 3.7 Added `frontend/src/features/auth/components/ForgotPasswordForm.tsx` + test — calls
      `useForgotPassword`, always shows the same generic confirmation with a manual "Enter code"
      link to `/reset-password` (carrying the email via router state), handles `429`.
- [x] 3.8 Added `frontend/src/features/auth/components/ResetPasswordForm.tsx` + test — calls
      `useResetPassword`, renders `410` (expired, with a link back to forgot-password), `401`
      (generic invalid/used-OTP message), `400` (field-level `newPassword` message), navigates to
      `/login` on success (verified with a real `Routes` stub in the test).
- [x] 3.9 **(unplanned addition)** Added `frontend/src/test/renderWithProviders.tsx` — shared
      `QueryClientProvider` + `MemoryRouter` test wrapper, reused across all component/page tests
      to avoid duplicating provider boilerplate in every test file.

**Note**: `@testing-library/user-event` is not an installed dependency; all component tests use
`fireEvent` instead (already available), per AGENTS.md's "ask before adding a pnpm dependency."

Checkpoint: `pnpm --filter frontend lint` (0 warnings), `tsc --noEmit` (0 errors),
`pnpm --filter frontend test` (46/46 green) — all passed after this phase.

## 4. Core Implementation — Route Guards & Pages

- [x] 4.1 Added `frontend/src/routes/ProtectedRoute.tsx` + test — renders children when
      `status === 'authenticated'`, redirects to `/login` when `'unauthenticated'`, shows a
      loading state while `'bootstrapping'`.
- [x] 4.2 Added `frontend/src/routes/RequireRole.tsx` + test — restricts to a passed
      `EmployeeRole[]`, reading only `authStore.user.role` (never the JWT payload).
- [x] 4.3 Added `frontend/src/pages/LoginPage.tsx`, `RegisterPage.tsx`, `ForgotPasswordPage.tsx`,
      `ResetPasswordPage.tsx` (composing the forms from Section 3, shadcn `Card`).
- [x] 4.4 Added `frontend/src/pages/DashboardPage.tsx` — minimal authenticated placeholder
      ("Welcome, {firstName}" + logout button wired to `useLogout`).
- [x] 4.5 Added `frontend/src/routes/AppRouter.tsx` — `createBrowserRouter` wiring `/login`,
      `/register`, `/forgot-password`, `/reset-password` (public) and `/dashboard` (behind
      `ProtectedRoute`), `/` redirecting to `/dashboard`.

Checkpoint: lint (0 warnings), `tsc --noEmit` (0 errors), `pnpm --filter frontend test` (52/52
green) — all passed after this phase.

## 5. Integration

- [x] 5.1 Modified `frontend/src/main.tsx` — wrap `App` in `QueryClientProvider`, mount
      `useSessionBootstrap` above the router.
- [x] 5.2 Modified `frontend/src/App.tsx` — render `AppRouter` in place of the static placeholder.
- [x] 5.3 Smoke pass, run against a real local backend + SQL Server (both dev servers started,
      then stopped after): registered a throwaway account (`EMP024`/`harper.smoketest@company.com`
      — a `Role=Employee` seed row), logged in, logged out, called forgot-password, verified a
      wrong-OTP reset attempt returns the expected `401`, verified `422` (bad employee number) and
      `409` (duplicate email) registration failures, verified `401` wrong-password login — **every
      response matched the frontend's expected shape and error codes exactly**. Performed via
      `curl` through the Vite dev proxy (validates D1 end-to-end) rather than a browser
      click-through — no browser-automation tool was invoked for this ticket. **Known gap**: the
      silent-refresh-restores-`user` flow (D2a, depends on `GET /api/auth/me`) could not be
      smoke-tested here because that endpoint only exists on the separate backend
      worktree/branch (`worktree-agent-a5ae531c888c427db`), not merged into this branch — re-test
      once that lands.

**Checkpoint 2**:
- [x] 5.4 `pnpm --filter frontend lint` → 0 errors/warnings
- [x] 5.5 `pnpm --filter frontend build` → 0 errors
- [x] 5.6 `pnpm --filter frontend test` → all green (52/52)

## 6. Tests — `frontend-login-ui` (one per spec scenario)

All in `frontend/src/features/auth/components/LoginForm.test.tsx`.

- [x] 6.1 Valid credentials call `POST /api/auth/login` with exactly `email`/`password`
- [x] 6.2 Empty `email`/`password` blocked client-side, no API call made
- [x] 6.3 `401 AUTHENTICATION_FAILED` renders one generic message, not attached to either field
- [x] 6.4 `429 RATE_LIMIT_EXCEEDED` renders a generic throttling message
- [x] 6.5 Successful login stores the session and navigates to `/dashboard` regardless of role

## 7. Tests — `frontend-registration-ui`

All in `frontend/src/features/auth/components/RegistrationForm.test.tsx`.

- [x] 7.1 Valid form calls `POST /api/auth/register` with `employeeNumber`/`email`/`password` only
- [x] 7.2 Mismatched `confirmPassword` blocks submission, no API call made
- [x] 7.3 `400 VALIDATION_ERROR` renders each field's message next to that field
- [x] 7.4 `409 RESOURCE_CONFLICT` renders the generic duplicate-email message
- [x] 7.5 `422 BUSINESS_RULE_VIOLATION` renders the generic message against `employeeNumber`
- [x] 7.6 Successful registration stores the session and navigates to `/dashboard` directly

## 8. Tests — `frontend-password-reset-ui`

8.1–8.2 in `ForgotPasswordForm.test.tsx`; 8.3–8.7 in `ResetPasswordForm.test.tsx`.

- [x] 8.1 Any submitted email on `forgot-password` shows the identical generic confirmation
- [x] 8.2 `429` on `forgot-password` shows a generic throttling message
- [x] 8.3 Valid OTP + compliant new password calls `reset-password` with `email`/`otp`/
      `newPassword` only, then navigates to `/login` with a success message
- [x] 8.4 Mismatched `confirmNewPassword` blocks submission, no API call made
- [x] 8.5 `410 RESOURCE_EXPIRED` shows an expiry message with a link back to forgot-password
- [x] 8.6 `401 AUTHENTICATION_FAILED` shows the generic invalid/used-OTP message
- [x] 8.7 `400 VALIDATION_ERROR` shows field-level detail on `newPassword`

## 9. Tests — `frontend-session-management`

9.1–9.5 in `useSessionBootstrap.test.ts`; 9.6–9.7 in `useLogout.test.tsx` (both added during this
traceability pass — 9.1/9.5/9.6/9.7 had no test until now, found by cross-checking this list
against actual test files rather than assuming Phase 2's tests already covered them).

- [x] 9.1 Access token is never present in `localStorage`/`sessionStorage`/cookies
- [x] 9.2 App load with a stored refresh token calls `/api/auth/refresh` before rendering
      protected routes and restores the session on success
- [x] 9.3 App load with no stored refresh token treats the user as unauthenticated, no API call
- [x] 9.4 A `401` from the startup silent-refresh clears the stored refresh token
- [x] 9.5 Access token is proactively renewed before its 15-minute expiry during an active session
- [x] 9.6 Logout calls `/api/auth/logout`, clears in-memory + stored tokens (navigation to
      `/login` is `ProtectedRoute` reacting to `status` flipping to `unauthenticated`, not a
      direct `navigate()` call in `useLogout` — covered by `ProtectedRoute.test.tsx` 10.1)
- [x] 9.7 Logout clears local session state even when the API call fails/times out

## 10. Tests — `frontend-route-guards`

10.1–10.3 in `ProtectedRoute.test.tsx`; 10.4–10.6 in `RequireRole.test.tsx`.

- [x] 10.1 Unauthenticated visit to a protected route redirects to `/login`, no content rendered
- [x] 10.2 Authenticated visit to a protected route renders its content
- [x] 10.3 Navigation during an in-flight startup silent-refresh defers the redirect decision
- [x] 10.4 User with an allowed role reaches a `RequireRole`-restricted route
- [x] 10.5 User with a disallowed role is blocked and redirected away
- [x] 10.6 Role check reads only `authStore.user.role`, never decodes the JWT

Checkpoint: lint (0 warnings), `tsc --noEmit` (0 errors), `pnpm --filter frontend test` (56/56
green, up from 52 after adding the 9.1/9.5/9.6/9.7 gap-fill tests).

## 11. Tests — E2E (Playwright, per D7)

Filenames use numeric prefixes (`01`–`04`, not the bare names originally planned) because
Playwright forbids importing one spec file from another for shared constants — moved to
`e2e/testData.ts` instead — and cross-file execution order (registration must run before login)
needed to be explicit given `playwright.config.ts`'s `workers: 1` / `fullyParallel: false`.

- [x] 11.1 `e2e/01-auth-registration.spec.ts` — reserved seed employee registers successfully
      (fresh run) or hits the generic "already registered" `422` message (repeat run); both
      asserted as valid outcomes. **Verified twice in a row against a live backend**: run 1 hit
      the fresh-success branch, run 2 (same process) hit the already-registered branch — confirms
      the idempotent design actually works, not just compiles.
- [x] 11.2 `e2e/02-auth-login.spec.ts` — logs in with the reserved account (registered by 01 in
      the same run, file-ordering guaranteed by the numeric prefix) and lands on `/dashboard`.
- [x] 11.3 `e2e/03-auth-password-reset.spec.ts` — forgot-password shows the generic confirmation
      and navigates to the reset step; submitting a wrong-but-well-formed OTP surfaces the generic
      `401` message (no assertion of an actual successful password change — OTP is console-only,
      see `design.md` Non-Goals).
- [x] 11.4 `e2e/04-route-guards.spec.ts` — unauthenticated visit to `/dashboard` redirects to
      `/login`; after login, `/dashboard` renders.

**Reserved E2E account note**: `EMP015`/`EMP016` (the originally planned reserved rows) turned
out to already be registered from years of prior manual ticket testing against this persistent
dev database. `EMP017` ("Liam Allen") was the first genuinely fresh row found by probing —
adopted as the permanent reserved account (`e2e/testData.ts`), now registered with
`e2e-check-017@company.com` / `Password1`. The probing itself also incidentally confirmed the
register endpoint's rate limiter (5 requests / 5 min, IP-partitioned) works correctly — it
rejected the burst with `429`, which is why the first full suite run below needed a retry after
the window cleared.

**Checkpoint 3 (final, before archive)** — all run against a live local backend + SQL Server:
- [x] 11.5 `pnpm --filter frontend lint` → 0 errors/warnings
- [x] 11.6 `pnpm exec tsc --noEmit --project frontend/tsconfig.app.json` → 0 errors
- [x] 11.7 `pnpm --filter frontend build` → 0 errors
- [x] 11.8 `pnpm --filter frontend test` → 56/56 green
- [x] 11.9 `npx playwright test` (repo root) → 6/6 green, twice in a row (see 11.1's idempotency
      note)

## 12. Archive

- [x] 12.1 `openspec archive et016-authentication-ui`
- [ ] 12.2 Update `docs/TICKETS.md` ET016 row: `Status` → `PR open (#N)` once the PR is opened
      (per the ticket-status convention: archive landing does not itself mean `Done`).
      **Intentionally left unchecked here** — per `/implement`'s explicit instruction, this
      ticket's `docs/TICKETS.md` status stays `In progress` through archiving; `/pr` is what sets
      `PR open (#N)`, and `Done` is reserved for after the PR actually merges.
