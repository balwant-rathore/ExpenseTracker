## Context

ET003–ET005 (backend auth) are merged and callable but have no frontend consumer. The frontend
workspace (`frontend/`) currently contains only ET001's bootstrap scaffold: an empty `App.tsx`,
`.gitkeep` placeholders in every `features/`, `pages/`, `routes/`, `store/`, `api/`, `types/`
folder, one generated shadcn `Button` component, and no `QueryClientProvider`, router, or Zustand
store wired up yet. This is a from-scratch build, not an extension of an existing pattern.

Two facts discovered while cross-checking the real backend code (not just `docs/SDS.md`'s prose)
change what this design must target, and are called out explicitly per AGENTS.md §13:

1. **`docs/SDS.md` §5.1 and `backend/CLAUDE.md` §Auth Approach both still call the registration
   field `employeeId`; the shipped `RegisterRequest` DTO (`backend/src/Application/Auth/RegisterRequest.cs`)
   is `(EmployeeNumber, Email, Password)`.** This is not a new discovery — it's an already-recorded
   decision, `docs/decisions/ADR-0003-registration-employeenumber-field.md`, which explicitly
   states "Frontend (ET016) must use `employeeNumber`." This design follows that ADR.
   `docs/SDS.md`/`backend/CLAUDE.md` remain stale pending their own follow-up doc fix (noted in
   ADR-0003's own Consequences); this ticket doesn't touch backend docs since it makes no backend
   code changes, but the mismatch is called out here so it isn't silently re-introduced.
2. **No CORS policy exists on the backend** (`backend/src/Api/Program.cs` has no `AddCors`/
   `UseCors`), and no frontend `.env`/Vite proxy exists yet. Without one of these, a browser
   running the Vite dev server (default port 5173) cannot call the API on `http://localhost:5158`
   (`backend/src/Api/Properties/launchSettings.json`) — the request is blocked before it reaches
   the backend. See Decision D1.

Every backend response shape below is transcribed from the actual current DTOs
(`backend/src/Application/Auth/*.cs`), not from `docs/SDS.md`'s prose, and confirmed camelCase via
ASP.NET Core's default `JsonSerializerDefaults.Web` (no custom naming policy is configured in
`Program.cs`).

## Goals / Non-Goals

**Goals:**
- Ship all five ET016 capabilities (`frontend-login-ui`, `frontend-registration-ui`,
  `frontend-password-reset-ui`, `frontend-session-management`, `frontend-route-guards`) exactly as
  specified in `openspec/changes/et016-authentication-ui/specs/`.
- Reach the six real backend auth endpoints from the browser during local development with zero
  backend code changes.
- Leave `/dashboard` as a placeholder authenticated route only — no real dashboard content (ET019).

**Non-Goals:**
- Any backend change beyond the single `GET /api/auth/me` addition (D2a) — CORS is resolved as a
  frontend-only dev-proxy instead (D1); no other DTOs/validators/endpoints are touched.
- Production deployment topology / production API base URL strategy — out of scope for this
  ticket; noted as an Open Question.
- Role-restricted *pages* — `RequireRole` is built and unit-tested as a reusable primitive, but no
  ET016 route actually applies a restrictive role list yet (there is no role-specific page to
  protect until ET017–ET019).
- ET020's full scope (FRS traceability matrix, CI quality-gate wiring, E2E coverage of every other
  ticket's flows) — ET016 only bootstraps the Playwright harness and covers its own four UI paths
  (D7); ET020 still owns everything beyond that.
- Automated verification of a *successful* password reset end-to-end — the OTP is logged only to
  the backend's server console (`docs/SDS.md` §4.5), which a Playwright test running against a
  live dev backend has no scripted access to. See D7's E2E scope note.

## Decisions

### D1: Vite dev-server proxy instead of backend CORS
**Decision**: Add a `server.proxy` entry to `frontend/vite.config.ts` forwarding `/api` to
`http://localhost:5158` (the `launchSettings.json` `http` profile). The frontend's `apiClient`
always calls relative `/api/...` paths.

**Why over adding CORS to the backend**: The proposal already commits to "no backend changes."
CORS middleware is a legitimate backend change (`Program.cs` edit) that AGENTS.md §11 doesn't
forbid outright, but it would (a) expand this ticket's blast radius into backend code the ticket
description doesn't list, and (b) still need frontend-side base-URL configuration for anything
that isn't same-origin. A dev proxy achieves the same "it works when you run both dev servers"
outcome with a one-line, frontend-only config change, and keeps the browser's calls same-origin
(simpler cookie/credential story if that's ever revisited). Production serving strategy (reverse
proxy, CORS, or same-origin static hosting) is explicitly deferred — see Open Questions.

### D2: Session storage and refresh mechanics
**Decision**: Zustand store (`frontend/src/store/authStore.ts`) holds `user: User | null`,
`accessToken: string | null` (memory only, never persisted), and
`status: 'bootstrapping' | 'authenticated' | 'unauthenticated'`. The refresh token is the *only*
thing written to `localStorage` (key `expensetracker.refreshToken`). On app start, a
`useSessionBootstrap()` hook (mounted once above the router) checks for a stored refresh token: if
absent, sets `status: 'unauthenticated'` immediately; if present, calls
`POST /api/auth/refresh` — success populates `user`/`accessToken`/rotates the stored refresh
token and sets `status: 'authenticated'`; a `401` clears the stored refresh token and sets
`status: 'unauthenticated'`. `ProtectedRoute` renders a loading state while `status ===
'bootstrapping'` (this is what satisfies the "in-flight silent refresh defers the redirect
decision" scenario in `frontend-route-guards`).

A separate `scheduleProactiveRefresh(accessToken)` helper decodes the access token's `exp` claim
(base64url-decode the JWT's middle segment — no `jwt-decode` dependency needed for one field) and
`setTimeout`s a refresh call for `exp - now - 60s` (60s safety margin for clock skew/latency).
Every successful login/register/refresh re-arms this timer with the new token; logout clears it.

**Alternatives considered**: Refresh-on-401 via a fetch/interceptor retry (the other option
presented to the user) was not chosen — proactive refresh was explicitly requested. Storing the
access token in `localStorage` alongside the refresh token was rejected for the same XSS-surface
reason the user chose memory-only for the access token.

**Note**: `docs/SDS.md` does not document a client token-storage strategy at all, so this is a new
architectural decision, not a deviation from an existing one — but per AGENTS.md §13 it still
needs an ADR. **`docs/decisions/ADR-0017-frontend-token-storage.md` was written as part of this
ticket's implementation**, before the ticket is considered ready for `/review`.

### D3: API client
**Decision**: `frontend/src/lib/apiClient.ts` — a thin wrapper around `fetch`, not axios (axios is
not an existing dependency; adding one needs the user's explicit sign-off per AGENTS.md, and
native `fetch` fully covers this ticket's needs). It:
- Prefixes every call with `/api` (relying on D1's proxy).
- Attaches `Authorization: Bearer <token>` when the caller passes an explicit `accessToken` option
  (only `logout` needs this among ET016's six calls). **Refined during implementation**: rather
  than `apiClient` importing the Zustand store directly to read the token itself, the caller
  (e.g. `useLogout`) reads `accessToken` from `authStore` and passes it in — keeps `apiClient` a
  store-agnostic utility and avoids an import-order dependency between Phase 1's `apiClient.ts`
  and Phase 2's `authStore.ts`.
- Parses a non-2xx JSON body into a typed `ApiError` (`{ code, message, fields, traceId }`,
  mirroring `Shared.ErrorHandling.ErrorResponse`) and throws it, so TanStack Query's `onError`/
  `error` always receives a typed shape instead of a raw `Response`.
- Never logs the request/response body for `login`, `register`, or `reset-password` calls
  (passwords/OTP must not hit the console — AGENTS.md §6, §11).

### D4: Routing structure
**Decision**: `react-router-dom` v7 data router (`createBrowserRouter`), built in
`frontend/src/routes/AppRouter.tsx`:
```
/                    → redirect to /dashboard (guard resolves further redirect to /login if unauth)
/login               → LoginPage                (public)
/register            → RegisterPage              (public)
/forgot-password      → ForgotPasswordPage        (public)
/reset-password       → ResetPasswordPage         (public)
/dashboard           → ProtectedRoute → DashboardPage (placeholder, ET019 replaces content)
```
`ProtectedRoute` and `RequireRole` are composable wrapper components (not route-object
`loader`s), matching the "wrapper" language already used in the approved spec deltas.

### D5: Zod schemas mirror the *real* DTOs, not the stale SDS text
Per D-context: registration schema uses `employeeNumber`. Password complexity Zod check
(≥8 chars, ≥1 letter, ≥1 digit) mirrors `docs/FRS.md` §3.1.2 for UX only — the backend's
`IPasswordPolicyValidator` remains authoritative and can still reject a password the frontend
accepted (frontend must handle the resulting `400 VALIDATION_ERROR`/`fields: ["password"]`
gracefully, per the already-approved spec). OTP schema enforces `^\d{6}$`, matching
`ResetPasswordRequestValidator`'s own regex exactly.

### D6: shadcn primitives
**Decision**: Generate `Input`, `Label`, `Card`, and `Field` shadcn components via
`pnpm exec shadcn add input label card field` (run from `frontend/`) before building the four
forms. **Correction made during implementation**: the design originally specified a `Form`
component, but this project's `base-nova` (base-ui-backed) shadcn style has no `form` registry
item with actual file content — `shadcn add form` runs without error but generates zero files
(confirmed via `shadcn view @shadcn/form`, whose `files` array is empty). This style instead
ships a `field` primitive (`Field`, `FieldLabel`, `FieldError`, `FieldDescription`, `FieldGroup`,
etc.) meant to pair directly with React Hook Form's `register`/`formState.errors`, without a
`Form`/`FormField` wrapper. Forms in this ticket (Section "Reuse of Existing Code" below) are
built with `Field`/`FieldLabel`/`FieldError` + plain `useForm()` instead. `field` also pulled in
`separator.tsx` as a registry dependency; `label.tsx` was already present and was skipped
(identical file). These are all codegen (file copies into `src/components/ui/`) built on
dependencies already installed (`@base-ui/react`, `class-variance-authority`, `clsx`,
`tailwind-merge`) — confirmed by hash-comparing `frontend/package.json` before/after the command:
unchanged, so no new dependency was added and Open Question 2 (below) is resolved.

### D2a: `GET /api/auth/me` — a backend addition, discovered mid-implementation
**Gap found while implementing `authStore`**: `POST /api/auth/refresh` returns only
`{accessToken, refreshToken}` (`Application/Auth/RefreshResponse.cs`) — never a `User`. Since the
JWT itself is forbidden from carrying anything beyond `sub` (AGENTS.md §11), there was no way for
the silent-refresh-on-load flow (D2) to recover the user's name/role/employeeNumber after a
reload. This contradicts D2/ADR-0017's original claim that silent refresh "restores the session"
— it can restore *tokens*, not *identity*.

**Resolution (explicit user decision, overriding this ticket's original "no backend changes"
non-goal)**: add a new authenticated `GET /api/auth/me` endpoint to the backend now, implemented
in a separate git worktree by a dedicated agent running in parallel with this ticket's frontend
work — not deferred to a follow-up ticket. Full rationale and consequences are recorded in
`docs/decisions/ADR-0017-frontend-token-storage.md`'s "Why `GET /api/auth/me` exists" section.

Revised session-bootstrap flow: `POST /api/auth/refresh` → on success, `GET /api/auth/me` with the
new access token → populate `user` + `accessToken`, `status: 'authenticated'`. A failure in either
call clears the stored refresh token and sets `status: 'unauthenticated'`. Login/register are
unaffected — their responses already include `User` inline, so no `/me` call is needed there. The
proactive refresh timer (D2) also doesn't call `/me` — only the token pair changes, not the user.

This is the one place this ticket's "no backend changes" Non-Goal no longer holds; the backend
work is scoped narrowly (one read-only, authenticated endpoint reusing existing patterns) and
tracked by its own ADR rather than a new ticket, per explicit user direction.

### D7: Playwright E2E harness (pulled forward from ET020)
**Decision** (per explicit user direction — see conversation): install `@playwright/test` as a
new **root** devDependency (`pnpm add -D -w @playwright/test`, requires the user's explicit
go-ahead per AGENTS.md's pnpm-add permission rule before running), add `playwright.config.ts` at
the repo root (`baseURL` pointing at the Vite dev server, e.g. `http://localhost:5173`, so
in-browser `/api` calls ride D1's proxy; `workers: 1` / `fullyParallel: false` since specs share
dev-DB state), and add `e2e/` at the repo root (`frontend/CLAUDE.md`: "run from repo/workspace
root, not frontend/") with one spec per UI path. **Filenames changed during implementation** to
numeric prefixes, since Playwright forbids importing one spec file from another (shared constants
had to move to a plain `e2e/testData.ts` module) and cross-file ordering (registration before
login) needed to be explicit:

- `e2e/testData.ts` (not a spec — shared constants)
- `e2e/01-auth-registration.spec.ts`
- `e2e/02-auth-login.spec.ts`
- `e2e/03-auth-password-reset.spec.ts`
- `e2e/04-route-guards.spec.ts`

**Test data strategy**: these specs run against a real local dev backend + SQL Server (not an
in-memory/`WebApplicationFactory` host), so seed data is finite and stateful across runs. Reserve
one `Role=Employee` row from `docs/EmployeeSeedData.csv` exclusively for E2E use (exact row chosen
at implementation time) and design each spec to be idempotent against a database that may or may
not already have that employee registered from a prior run:
- `auth-registration.spec.ts` asserts *either* outcome is handled correctly: a fresh reserved
  employee number registers successfully and redirects to `/dashboard`, OR (if already registered
  from an earlier run) submitting it again surfaces the spec'd `422 BUSINESS_RULE_VIOLATION`
  generic message — both are valid, already-specified behaviors, so the test doesn't need a
  database reset to be meaningful.
- `auth-login.spec.ts` runs after registration (Playwright `test.describe.serial` / project
  dependency) so it always has a known-good, already-registered account and password to log in
  with, regardless of whether registration in this run created it or found it pre-existing.
- `auth-password-reset.spec.ts` is scoped to what's actually verifiable without OTP access (per
  the Non-Goals note): submitting `forgot-password` shows the generic confirmation and navigates
  to the reset step; submitting the reset step with a syntactically-valid-but-wrong 6-digit OTP
  surfaces the generic `401` message. It does **not** assert a real password change succeeded.
- `route-guards.spec.ts` visits `/dashboard` unauthenticated (expects redirect to `/login`), then
  logs in and confirms `/dashboard` renders.

**Why pulled forward rather than left to ET020**: explicit user decision, overriding the default
recommendation to defer — see conversation. Flagged trade-off: this ticket now owns keeping
`playwright.config.ts` a clean, extensible base for ET020 to build on, not a throwaway.

## File Plan

New files:
```
frontend/src/types/auth.ts                        — User, EmployeeRole, AuthSession types
frontend/src/lib/apiClient.ts (+ .test.ts)         — fetch wrapper, ApiError
frontend/src/lib/jwt.ts (+ .test.ts)               — decodeJwtExpiry(token): number (epoch ms)
frontend/src/store/authStore.ts (+ .test.ts)       — Zustand auth store
frontend/src/features/auth/api/authApi.ts          — register/login/refresh/logout/forgotPassword/resetPassword
frontend/src/features/auth/api/useLogin.ts
frontend/src/features/auth/api/useRegister.ts
frontend/src/features/auth/api/useLogout.ts
frontend/src/features/auth/api/useForgotPassword.ts
frontend/src/features/auth/api/useResetPassword.ts
frontend/src/features/auth/schemas/loginSchema.ts
frontend/src/features/auth/schemas/registrationSchema.ts
frontend/src/features/auth/schemas/forgotPasswordSchema.ts
frontend/src/features/auth/schemas/resetPasswordSchema.ts
frontend/src/features/auth/components/LoginForm.tsx (+ .test.tsx)
frontend/src/features/auth/components/RegistrationForm.tsx (+ .test.tsx)
frontend/src/features/auth/components/ForgotPasswordForm.tsx (+ .test.tsx)
frontend/src/features/auth/components/ResetPasswordForm.tsx (+ .test.tsx)
frontend/src/features/auth/session/useSessionBootstrap.ts (+ .test.ts)
frontend/src/features/auth/session/scheduleProactiveRefresh.ts (+ .test.ts)
frontend/src/routes/AppRouter.tsx
frontend/src/routes/ProtectedRoute.tsx (+ .test.tsx)
frontend/src/routes/RequireRole.tsx (+ .test.tsx)
frontend/src/pages/LoginPage.tsx
frontend/src/pages/RegisterPage.tsx
frontend/src/pages/ForgotPasswordPage.tsx
frontend/src/pages/ResetPasswordPage.tsx
frontend/src/pages/DashboardPage.tsx
frontend/src/components/ui/input.tsx, label.tsx, field.tsx, card.tsx, separator.tsx  — shadcn-generated (D6)
docs/decisions/ADR-0017-frontend-token-storage.md  — ADR (D2)
playwright.config.ts (repo root)                   — Playwright harness (D7)
e2e/testData.ts                                     — (D7)
e2e/01-auth-registration.spec.ts                    — (D7)
e2e/02-auth-login.spec.ts                           — (D7)
e2e/03-auth-password-reset.spec.ts                  — (D7)
e2e/04-route-guards.spec.ts                         — (D7)
```

Modified files:
```
frontend/src/App.tsx        — render AppRouter instead of the static placeholder markup
frontend/src/main.tsx       — wrap App in QueryClientProvider + mount useSessionBootstrap
frontend/vite.config.ts     — add server.proxy for /api (D1)
frontend/package.json       — new shadcn-generated component files only if D6 needs no new deps
package.json (repo root)    — add @playwright/test devDependency + an `e2e` script (D7)
```

## Key Types (TypeScript)

```typescript
// src/types/auth.ts
export type EmployeeRole = 'Employee' | 'Manager' | 'Finance' | 'ComplianceOfficer'

export interface User {
  id: string
  email: string
  employeeNumber: string
  firstName: string
  lastName: string
  role: EmployeeRole
}

export interface AuthTokens {
  accessToken: string
  refreshToken: string
}
```

```typescript
// src/lib/apiClient.ts
export interface ApiError {
  code: string
  message: string
  fields: string[]
  traceId: string
}
```

## Zod Schemas (client-side UX validation only)

```typescript
// registrationSchema.ts — matches RegisterRequest(EmployeeNumber, Email, Password)
z.object({
  employeeNumber: z.string().min(1),
  email: z.string().email(),
  password: z.string().min(8).regex(/[A-Za-z]/).regex(/\d/),
  confirmPassword: z.string(),
}).refine(d => d.password === d.confirmPassword, { path: ['confirmPassword'] })
// confirmPassword is stripped before calling authApi.register()

// loginSchema.ts
z.object({ email: z.string().email(), password: z.string().min(1) })

// forgotPasswordSchema.ts
z.object({ email: z.string().email() })

// resetPasswordSchema.ts — matches ResetPasswordRequest(Email, Otp, NewPassword)
z.object({
  email: z.string().email(),
  otp: z.string().regex(/^\d{6}$/),
  newPassword: z.string().min(8).regex(/[A-Za-z]/).regex(/\d/),
  confirmNewPassword: z.string(),
}).refine(d => d.newPassword === d.confirmNewPassword, { path: ['confirmNewPassword'] })
```

## DB Changes

None. This ticket makes no backend or schema changes (see Non-Goals).

## Reuse of Existing Code

- `cn()` (`src/lib/utils.ts`), `Button` (`src/components/ui/button.tsx`), and the `@/*` path alias
  (`tsconfig.app.json`) are used as-is.
- `components.json`'s existing shadcn config (`style: base-nova`, `baseColor: neutral`) governs
  the new `Input`/`Label`/`Form`/`Card` generation in D6 — no config changes needed.
- Vitest + RTL setup (`src/test/setup.ts`, `vite.config.ts` `test` block) already supports
  component tests; no test-harness changes needed beyond the new spec files themselves.

### D8: Field-error casing mismatch and test tooling (discovered during implementation)
- `AuthController`'s `fields` array is not consistently cased: FluentValidation-driven `400`s
  return the C# property name verbatim (e.g. `"Email"`), while a few service-level checks hardcode
  lowerCamelCase (e.g. `"password"`, `"newPassword"`). `hasFieldError()`
  (`frontend/src/features/auth/utils/hasFieldError.ts`) matches case-insensitively so the frontend
  doesn't silently fail to highlight a field over a backend inconsistency outside this ticket's
  authority to fix.
- `@testing-library/user-event` is not an installed dependency (checked `frontend/package.json`
  and `node_modules` directly). All component tests use `fireEvent` instead, already available via
  `@testing-library/react`, rather than adding a new dependency without asking first.

## Risks / Trade-offs

- **[Risk] Proactive refresh timer drifts if the OS suspends the tab/laptop sleeps** →
  **Mitigation**: `useSessionBootstrap` also re-validates on `visibilitychange`/window focus,
  triggering an immediate refresh if the computed expiry has already passed.
- **[Risk] Vite proxy (D1) is dev-only; production has no equivalent configured** →
  **Mitigation**: explicitly called out as an Open Question, not silently deferred.
- **[Risk] `RequireRole` ships untested against a real restricted page** (no ET016 route uses it
  restrictively) → **Mitigation**: cover it with a unit test against a synthetic in-test route
  tree, per its own spec scenarios, so behavior is verified even though no production route
  exercises it yet.
- **[Risk] Manual JWT decode (D2) breaks if the backend ever changes token format** →
  **Mitigation**: `decodeJwtExpiry` is a single small, unit-tested function; a malformed/undecodable
  token falls back to treating the session as needing immediate refresh rather than throwing.
- **[Risk] E2E specs (D7) depend on a live local SQL Server + seeded Employees + both dev servers
  running** — they will not run in an environment without that (e.g. a sandboxed CI runner not yet
  wired up, since ET001 deferred CI/GitHub Actions) → **Mitigation**: documented as a local-only
  gate for now; `npx playwright test` is listed in Checkpoint Commands but ET020 is responsible for
  wiring it into any future CI pipeline.
- **[Risk] E2E specs mutate real dev-database state (register a reserved employee)** →
  **Mitigation**: idempotent-by-design specs (D7) that assert a valid outcome whether the reserved
  employee is fresh or already registered, rather than requiring a manual DB reset between runs.

## Migration Plan

1. Write the ADR (`docs/decisions/ADR-0017-frontend-token-storage.md`) documenting D2.
2. Add the Vite proxy (D1) and confirm `pnpm --filter frontend dev` can reach a running backend.
3. Generate shadcn primitives (D6), confirming no unexpected `package.json` changes.
4. Build bottom-up: types → apiClient/jwt → authStore → authApi/hooks → schemas → forms → pages →
   route guards → AppRouter → wire `App.tsx`/`main.tsx`.
5. **Ask the user to confirm before running** `pnpm add -D -w @playwright/test` (new root
   dependency, per AGENTS.md's pnpm-add permission rule), then add `playwright.config.ts` and the
   four `e2e/` specs (D7).
6. Run the quality gates (below) before opening the PR.

No rollback concerns beyond a normal git revert — no data migrations, no backend changes.

## Checkpoint Commands

```bash
pnpm --filter frontend lint
pnpm exec tsc --noEmit --project frontend/tsconfig.app.json
pnpm --filter frontend build
pnpm --filter frontend test
npx playwright test          # from repo root; requires local backend + SQL Server running (D7)
```
(No backend commands — no backend changes.)

## Open Questions

1. Production API base URL / same-origin strategy (D1 is dev-only) — out of scope for ET016, but
   should be captured as a follow-up ticket/ADR so it isn't forgotten.
2. ~~Confirm during implementation whether `shadcn add` modifies `package.json`~~ — **Resolved**:
   confirmed unchanged (hash-compared before/after); see D6.
3. ~~Which specific `Role=Employee` row to reserve for E2E use~~ — **Resolved**: `EMP015`/`EMP016`
   were already claimed by years of prior manual ticket testing against this persistent dev
   database; `EMP017` ("Liam Allen") was the first fresh row found by probing and is now the
   permanent reserved account, documented in `e2e/testData.ts`.
