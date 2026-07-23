## Context

ET001–ET019 shipped the full system with substantial test coverage already in place:

| Layer | Location | Count |
|---|---|---|
| Backend unit | `backend/tests/UnitTests/**/*.cs` | 45 files |
| Backend integration | `backend/tests/IntegrationTests/*.cs` | 37 files (all `WebApplicationFactory<Program>`-based, real SQL Server, no in-memory provider) |
| Frontend component | `frontend/src/**/*.test.{ts,tsx}` | 34 files (Vitest + RTL) |
| E2E | `e2e/01`–`08-*.spec.ts` | 8 files after this change (7 pre-existing + `08-dashboard-reports.spec.ts`; Playwright, `chromium` only, `workers: 1`, serial) |

No `.github/` directory and no CI pipeline exist yet (ET001 explicitly deferred CI). This design
covers: (1) the audit method for the FRS traceability matrix, (2) the e2e gap for ET019's
dashboard/reports/receipt-viewer surfaces, and (3) the CI pipeline architecture needed to actually
run all four layers automatically, including the parts SDS is silent on (SDS has no §"CI
Architecture" — ET001 deferred it, so this is genuinely new ground, not a deviation from an
existing decision).

**Key existing facts that shape the CI design (found by inspection, not documented anywhere):**

- Integration tests connect to a **real SQL Server** via `ConnectionStrings:DefaultConnection`
  (`Program.cs:20-21`, `UseSqlServer`) — there is no test-specific factory override of the
  connection string anywhere in `backend/tests/IntegrationTests/`. Locally this resolves via
  `dotnet user-secrets` (`backend/CLAUDE.md`); in CI it must resolve via the environment-variable
  config source ASP.NET Core reads by default (`ConnectionStrings__DefaultConnection`).
- `Program.cs` does **not** call `Database.Migrate()` at startup, and only runs the CSV
  `ISeedRunner` when `app.Environment.IsDevelopment()` (`Program.cs:52-60`). Migrations and (for
  e2e) seeding are the *caller's* responsibility, not the app's.
- Individual integration test classes (e.g. `DashboardTests`) insert and clean up their own
  `Employee`/`User` rows directly via `ApplicationDbContext` in `IAsyncLifetime.DisposeAsync` —
  they do **not** depend on the CSV seed data. So integration tests only need a *migrated* (schema
  present) database, not a *seeded* one.
- E2E specs, by contrast, register against specific reserved rows from
  `docs/EmployeeSeedData.csv` (e.g. `EMP050`/`EMP010`/`EMP001`/`EMP004` in `e2e/expenseTestData.ts`)
  via `registerOrLogin`'s idempotent register-or-login pattern. E2E therefore needs both a migrated
  **and** seeded database, plus the real API and frontend dev server running and reachable.
- `playwright.config.ts` has no `webServer` entry — today a developer must already have the
  backend and frontend running before invoking `pnpm e2e`. CI must reproduce that manually (start
  both, wait for readiness, then run Playwright, then tear down) rather than relying on Playwright
  to manage the processes.

## Goals / Non-Goals

**Goals:**
- Full AC-level FRS traceability: every acceptance criterion in `docs/FRS.md` §3–§10 and every
  `BR-01`–`BR-10` mapped to a real, passing test.
- Close the one known coverage gap: ET019's dashboard, finance search UI, monthly report download,
  and receipt/attachment viewer have no e2e coverage today.
- A working `.github/workflows/ci.yml` that runs lint → build → unit → integration → e2e, pass/fail
  only, on every PR and on pushes to `main`.
- Zero changes to production business logic — this ticket verifies and gates, it does not modify
  `Api`/`Application`/`Domain`/`Infrastructure` or frontend feature code.

**Non-Goals:**
- No numeric code-coverage threshold (per user decision already recorded in proposal.md).
- No change to `playwright.config.ts`'s existing `baseURL`/single-worker/serial conventions — CI
  reproduces the same local contract, it doesn't redesign the e2e harness.
- No deployment/CD stage — ET020 is verification/quality gates only, not a release pipeline.
- No retrofitting of a fresh-DB-per-test-class strategy for integration tests — the existing
  self-cleanup pattern (`IAsyncLifetime`) is followed as-is for any new integration test.

## Decisions

### 1. Traceability matrix: `docs/TRACEABILITY.md`, one table per FRS section

Columns: `AC / BR ID | Requirement (short) | Test Layer | Test File | Test Method/Describe | Status`.
`Status` is either `Covered` (test exists and passes today) or `Added (ET020)` (new test written in
this ticket) — this makes the gap-fill audit's actual delta visible in the PR diff, not just the
end state.

**Numbering discrepancy found and how it's handled:** `docs/FRS.md` §5 has two scenarios both
numbered `5.1.2` — one under §5.1 (Manager Review: "Approve expenses...") and one under §5.2
(Compliance Review: "The system shall allow Compliance officer to view valid expenses..."). The
second is a documentation typo (should read `5.2.2`); the matrix will list it as `5.1.2 (sic —
documented as 5.2.2)` under the Compliance Review section rather than silently renumbering
`docs/FRS.md` itself, since correcting the FRS text is outside this ticket's scope. Flagged here
per AGENTS.md §13 rather than fixed ad hoc mid-audit.

**Alternative considered:** appending a traceability column directly into `docs/FRS.md` (offered
as an option to the user) — rejected in favor of a standalone file so the FRS document, which is
authoritative spec text, doesn't get mixed with a build artifact that changes shape as tests
change.

### 2. E2E gap-fill: one new spec file, following existing conventions exactly

New file: `e2e/08-dashboard-reports.spec.ts`, structured like `e2e/07-*.spec.ts`
(`test.describe.serial`, one shared `page`, `registerOrLogin` per role switch). Covers, per the
spec delta's "Dashboard and Reports End-to-End Coverage" requirement: Employee/Manager/Finance
dashboard metrics, Finance monthly-report download, and the receipt/attachment viewer.

**Revised during implementation (superseding the original plan below the line):** rather than
reserving new seed rows, `e2e/08-dashboard-reports.spec.ts` reuses the existing, already-vetted
`EXPENSE_E2E_*` / `EXPENSE_E2E_MANAGER_*` / `EXPENSE_E2E_FINANCE_*` constants from
`e2e/expenseTestData.ts` (Employee=Audrey/EMP050, Manager=Charlotte/EMP010, Finance=David/EMP004
— the same accounts `05`/`06`/`07` already use). This was originally planned as new reserved rows
(EMP021 Employee/EMP011 Manager/EMP003 Finance, verified fresh during implementation) on the
assumption that dashboard assertions would need exact tile counts requiring isolated accounts.
Investigating `frontend/src/pages/DashboardPage.test.tsx` during implementation showed dashboard
assertions only check tile **presence/absence** (e.g. "Pending Approvals" tile exists for
Manager, absent for Employee), never exact numeric values — so account isolation isn't needed,
and reusing the existing, already-vetted constants is simpler and avoids permanently consuming
three more real accounts in the shared dev DB for no added test value. Per `AGENTS.md` §13, this
supersedes the original plan rather than leaving it stale.

~~Original plan (superseded): adds new reserved seed rows to `e2e/expenseTestData.ts` (following
the existing convention of hand-picking known-fresh `EmployeeNumber`s — the next unused rows
after `EMP050`/`EMP010`/`EMP001`/`EMP004`, verified fresh the same way prior tickets did: a direct
DB check for an existing `Users` row before committing to a row in the test file).~~

**Alternative considered:** extending `e2e/07-expense-compliance-and-finance.spec.ts` in place —
rejected because that file is already scoped to one specific workflow (ET018) per its own comment
header, and AGENTS.md's file-naming convention (`NN-<scope>.spec.ts`) implies one file per feature
area, not a growing catch-all.

### 3. CI pipeline architecture (`.github/workflows/ci.yml`)

Single workflow, single job, `ubuntu-latest`, stages run sequentially as job steps (a failing step
stops the job — GitHub Actions' default behavior, satisfying "fail at the first failing stage"
without needing separate dependent jobs):

1. **Checkout + setup**: `actions/checkout`, `actions/setup-dotnet` reading `backend/global.json`
   (added in Phase 1, pinning the exact local `dotnet --version` so CI can't drift onto a
   different .NET 9 patch version), `pnpm/action-setup@v4` with an explicit `version: 11` (the
   repo has no `packageManager` field in `package.json` to infer a version from — discovered
   during CI validation, where the action fails outright without one), `actions/setup-node`
   matching `pnpm-workspace.yaml`. `dotnet tool install --global dotnet-ef --version 9.0.18` runs
   before the migrate step, since `dotnet-ef` is a locally-installed global tool, not something
   `ubuntu-latest` ships with.
2. **SQL Server service container**: `mcr.microsoft.com/mssql/server:2022-latest` as a GitHub
   Actions `services:` container, `SA_PASSWORD` set to a CI-only generated value (lives only in the
   ephemeral workflow file/run, never a real secret, never touches `appsettings.*.json` or `.env`
   per `CLAUDE.md`'s "always ask" list — that list is about *this repo's real* secrets, not a
   throwaway CI container password), port `1433` mapped, with `--health-cmd`/`--health-retries`
   options. **No separate host-side "wait for SQL Server" job step is needed or used**: GitHub
   Actions itself blocks the job's steps from starting until the service container's own health
   check passes. An earlier draft of this workflow added a redundant host-side wait step running
   `sqlcmd` directly on the `ubuntu-latest` runner — that command doesn't exist on the runner
   (`mssql-tools18` is only installed inside the service container, where the health check itself
   runs it), so the step always failed; removed during CI validation.
3. **Backend lint/build**: `dotnet build backend/ExpenseTracker.sln` (surfaces analyzer warnings,
   matches `AGENTS.md`'s "backend analyzers via `dotnet build`" convention — no
   `TreatWarningsAsErrors` exists today, so CI doesn't newly fail on warnings that aren't failing
   locally).
4. **Frontend lint**: `pnpm --filter frontend lint` (`oxlint`, matches `.oxlintrc.json`).
5. **Frontend build**: `pnpm --filter frontend build`.
6. **DB migrate**: `dotnet ef database update --project backend/src/Infrastructure --startup-project backend/src/Api`
   against the service container, connection string supplied via
   `ConnectionStrings__DefaultConnection` env var (CI-only, points at `localhost,1433` inside the
   job).
7. **Backend unit tests**: `dotnet test --filter FullyQualifiedName~UnitTests` (no DB dependency,
   but runs after migrate for simple linear ordering).
8. **Backend integration tests**: `dotnet test --filter FullyQualifiedName~IntegrationTests` against
   the migrated (unseeded) service-container DB — consistent with Decision area above: integration
   tests manage their own fixture data and don't need the CSV seed.
9. **Frontend unit tests**: `pnpm --filter frontend test` (Vitest, no backend dependency).
10. **E2E**: start the backend API in the background (`ASPNETCORE_ENVIRONMENT=Development` so the
    `ISeedRunner` import runs — this is *required* here, unlike step 8, because e2e's
    `registerOrLogin` depends on real seeded `EmployeeNumber`s — and
    `RateLimiting__AuthEndpoints__PermitLimit=50`, a test-only override for this stage's backend
    process only, see Risks below for why), start the frontend (`pnpm --filter frontend dev`,
    matching `playwright.config.ts`'s hardcoded `baseURL: 'http://localhost:5173'`), poll both
    `/api/health` and `http://localhost:5173` until ready (simple curl-retry loop — no new
    dependency added for this), then `pnpm e2e`. Both background processes are killed in an
    `if: always()` cleanup step regardless of test outcome; e2e logs/traces upload as an artifact
    on failure.

**Alternative considered for step 10**: adding a `webServer` block to `playwright.config.ts` so
Playwright manages server lifecycle itself. Rejected for this change — it would alter a file
outside this ticket's audit/CI scope and change the *local* dev experience for every contributor,
not just CI; flagged as a follow-up open question rather than silently changed here.

### 4. No numeric coverage gate (confirmed decision)

CI's only signal is pass/fail per stage, matching the user's explicit choice in the proposal
clarification round. `coverlet.collector` is already referenced in both backend test `.csproj`
files but no coverage report/threshold step is added to CI by this change.

## Risks / Trade-offs

- **[Risk, confirmed during implementation] A full sequential e2e run (all 8 spec files)
  deterministically exceeds the production auth rate limit (`RateLimiting:AuthEndpoints:
  PermitLimit=5` per 300s, FRS 3.5.1/3.5.2).** Every `registerOrLogin` call against an
  already-registered account (which all reserved e2e accounts now permanently are) makes both a
  register attempt (rejected) and a login attempt — roughly 2 rate-limited requests per identity
  switch. Across 8 files with ~15-20 total identity switches, this exceeds the 5-permit budget for
  both the Register and Login named policies well before the suite finishes, reproduced directly
  (not a one-off flake) during ET020 implementation. **Mitigation:** the CI workflow
  (`.github/workflows/ci.yml`, §5) sets `RateLimiting__AuthEndpoints__PermitLimit` to a higher
  test-only value (e.g. 50) via environment variable for the e2e stage's backend process only —
  this doesn't touch `appsettings.json` or any production default, mirrors the existing pattern
  `RateLimitedWebApplicationFactory` already uses to override this same setting for backend
  integration tests, and the *actual* rate-limiting behavior (that a lower limit correctly returns
  429) stays independently verified by `RateLimitingTests.cs` — unaffected by this override.
  Locally, a developer running the full e2e suite must set the same environment variable before
  `dotnet run --project backend/src/Api`, or expect the run to fail partway through on a real,
  correctly-functioning rate limiter — this is expected/correct behavior, not a bug to route
  around by disabling the limiter entirely.
- **[Defect found and fixed, pre-existing, unrelated to ET020's new coverage]
  `e2e/authHelpers.ts`'s `registerOrLogin`, and `e2e/02-auth-login.spec.ts` /
  `e2e/04-route-guards.spec.ts`'s own assertions, waited for a `"Welcome, {name}"` banner that
  does not exist anywhere in the current frontend (`RegistrationForm.tsx`/`LoginForm.tsx` both
  navigate straight to `/dashboard` on success) — confirmed via a repo-wide grep returning zero
  matches. This silently broke 3 of the 4 accounts' fresh-registration detection and 2 standalone
  tests; fixed by switching to URL-based/real-UI-text detection (see the files' updated code and
  comments). Not a production defect — the app behaves correctly; the test assertions were stale.
- **[Self-inflicted, documented, not a defect]** `06-expense-manager-team-view.spec.ts`'s "manager
  rejects" test intermittently fails locally after many repeated full-suite reruns in one
  implementation session — confirmed via direct query that EMP050 accumulated 38 Submitted-status
  expenses (14 dated after `2026-01-06`), pushing the test's target row to page 2 of the default
  list view. This is purely a data-volume artifact of this implementation session's extensive
  local iteration against the same persistent dev DB (each full-suite rerun creates a fresh
  `2026-01-06` row without cleaning up the previous run's), not a defect in the app or the test —
  a single clean run (any one CI run, or a developer's first local run of the day) never
  accumulates enough same-day duplicate dates to hit this. Not fixed (no code/test change
  warranted for a self-inflicted local data-volume issue); noted here for transparency.
- **[Local dev-DB repair, done during implementation]** the shared local dev database's e2e
  accounts (`EMP050`/`EMP010`/`EMP004`/`EMP017`) had password hashes that no longer matched the
  `Password1` convention every e2e spec assumes (registered with a different password at some
  unknown prior point) — this blocked the *entire* pre-existing local e2e suite (05/06/07), not
  just ET020's new file. Fixed locally by resetting their `PasswordHash` to a real
  application-generated BCrypt hash of `Password1`. This has no effect on CI (which seeds a fresh
  database with no pre-existing `Users` rows) and no effect on production data.
- **[Risk] SQL Server container startup time inside CI (image pull + engine init) adds several
  minutes per run.** → Mitigation: accept it for now (matches "no CD, verification only" non-goal);
  revisit with a cached/pre-warmed image only if CI time becomes a real bottleneck — not solved
  preemptively here.
- **[Risk] E2E depends on hand-picked "known fresh" `EmployeeNumber` rows in a persistent dev DB
  convention; against a fresh CI DB every row is trivially fresh, so the "already registered" branch
  of `registerOrLogin` never actually executes in CI.** → Mitigation: none needed functionally
  (both branches are still covered by whichever developer runs e2e locally against the shared
  persistent dev DB); noted so nobody mistakes 100% CI e2e green for proof that the idempotent-login
  branch works — it's proven locally, not in CI.
- **[Risk] Backend `dotnet build` surfacing pre-existing analyzer warnings might look like new CI
  noise.** → Mitigation: capture current warning count as a baseline note in `docs/TRACEABILITY.md`
  or the PR description; do not add `TreatWarningsAsErrors` as part of this change (that would be a
  scope-creeping behavior change, not verification).
- **[Trade-off] Single sequential job (not fan-out across parallel jobs) is simpler to write and
  debug but slower wall-clock than parallel lint/unit/integration jobs.** Accepted for a first CI
  pipeline; parallelizing is a reasonable future improvement, not required by ET020's scope.

## Migration Plan

Not applicable in the schema-migration sense — no DB schema changes in this ticket. Rollout is:
merge the PR containing `docs/TRACEABILITY.md`, the new e2e spec, and `.github/workflows/ci.yml`;
CI starts running on the next push. No rollback concern beyond reverting the PR (CI running is
purely additive — it doesn't gate anything retroactively for already-merged tickets).

## Build + Test + Lint Checkpoint Commands

Run these locally before considering any task in `tasks.md` complete, in this order (mirrors the
CI stage order above and `CLAUDE.md`'s Quality Gates):

```bash
# Backend
dotnet build backend/ExpenseTracker.sln
dotnet test backend/ExpenseTracker.sln --filter FullyQualifiedName~UnitTests
dotnet ef database update --project backend/src/Infrastructure --startup-project backend/src/Api
dotnet test backend/ExpenseTracker.sln --filter FullyQualifiedName~IntegrationTests

# Frontend
pnpm --filter frontend lint
pnpm --filter frontend build
pnpm --filter frontend test

# E2E (requires backend `dotnet run --project backend/src/Api` and
# frontend `pnpm --filter frontend dev` already running in Development)
pnpm e2e
```

## Open Questions

1. **RESOLVED — `backend/global.json` SDK pin**: confirmed by user. Add it, pinning the exact
   `dotnet --version` in local use, so CI and local machines can't silently drift onto different
   .NET 9 patch versions.
2. **`playwright.config.ts` `webServer` automation** — confirmed out of scope for this change (see
   Decision 3 alternative); should it be filed as a follow-up ticket/ADR now, or left implicit?
   Left implicit for now — not blocking ET020.
3. **RESOLVED — New reserved e2e seed rows**: superseded during implementation. EMP021
   Employee/EMP011 Manager/EMP003 Finance were identified as genuinely fresh via direct DB
   inspection, but ultimately not used — `frontend/src/pages/DashboardPage.test.tsx` showed
   dashboard assertions only check tile presence/absence, not exact counts, so account isolation
   wasn't actually needed. `e2e/08-dashboard-reports.spec.ts` reuses the existing, already-vetted
   `EXPENSE_E2E_*` constants instead (see Decision 2).
4. **RESOLVED — CI-only SA password**: confirmed by user. Inline in the workflow YAML — it's
   ephemeral, per-run, protects nothing persistent, and is not a real credential per `CLAUDE.md`'s
   secret-handling rules.
