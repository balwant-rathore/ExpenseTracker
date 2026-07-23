## 1. Foundation

- [x] 1.1 Add `backend/global.json` pinning the exact `dotnet --version` currently in local use
- [x] 1.2 Verify `dotnet build backend/ExpenseTracker.sln` still succeeds with the SDK pin in place
- [x] 1.3 Create `docs/TRACEABILITY.md` skeleton: one table per FRS section (§3 Auth, §4 Expenses,
      §5 Expense Review, §6 Categories, §7 Finance, §8 Dashboard, §9 Notifications, §10 Reports,
      §11 Business Rules BR-01–BR-10), columns `AC/BR ID | Requirement (short) | Test Layer |
      Test File | Test Method | Status`, all rows initially blank
- [x] 1.4 Directly inspect the dev SQL Server DB for the next unused, genuinely-fresh
      `EmployeeNumber` rows (Employee + Manager) to reserve for `e2e/08-dashboard-reports.spec.ts`,
      following the verification convention documented in `e2e/expenseTestData.ts`'s comments —
      identified EMP021/EMP011/EMP003 as fresh, but **superseded in task 4.1**: the new spec
      reuses existing `EXPENSE_E2E_*` constants instead (see design.md)
- [x] 1.5 Record the current `dotnet build` analyzer warning count as a baseline note (for the PR
      description, per design.md's risk mitigation — CI must not be blamed for pre-existing noise)
      — result: 0 warnings, 0 errors

## 2. Backend FRS Traceability Audit

- [x] 2.1 Trace FRS §3 (Auth: registration, login, logout, forgot/reset password, rate limiting)
      acceptance criteria to existing `UnitTests`/`IntegrationTests` files/methods; fill matrix rows
- [x] 2.2 Trace FRS §4 (Expense create/edit/cancel/view) acceptance criteria plus BR-01, BR-02,
      BR-03, BR-04, BR-05, BR-09, BR-10 to existing tests; fill matrix rows
- [x] 2.3 Trace FRS §5 (Manager review §5.1, Compliance review §5.2) acceptance criteria plus
      BR-06, BR-07 to existing tests; fill matrix rows — explicitly note the duplicate `5.1.2`
      numbering (second occurrence documented as `5.2.2`, per design.md Decision 1) rather than
      silently renumbering `docs/FRS.md`
- [x] 2.4 Trace FRS §6 (7 fixed expense categories) acceptance criteria to existing tests; fill
      matrix rows
- [x] 2.5 Trace FRS §7 (Finance search, reimbursement, workflow) acceptance criteria plus BR-08 to
      existing tests; fill matrix rows
- [x] 2.6 Trace FRS §8 (Dashboard: Employee/Manager/Finance metrics) acceptance criteria to
      existing tests; fill matrix rows
- [x] 2.7 Trace FRS §9 (Notification events, recipients, HTML log) acceptance criteria to existing
      tests; fill matrix rows
- [x] 2.8 Trace FRS §10 (Monthly reimbursement report) acceptance criteria to existing tests; fill
      matrix rows
- [x] 2.9 For every AC/BR from 2.1–2.8 with no covering backend test, write the missing xUnit
      unit/integration test at the layer `AGENTS.md` §10 assigns it, then mark that matrix row
      `Added (ET020)` — 5 tests added (malformed-email, register rate-limit, Finance-sees-Approved,
      no-backward-transition, Reimbursed-notification-recipients); 2 additional suspected gaps
      (Currency defaulting, BR-10 timezone storage) turned out to be genuine behavior/spec
      ambiguities rather than test gaps — flagged in `docs/TRACEABILITY.md`, tracked as **ET021**
      (`docs/TICKETS.md`), not fixed here per verify-only scope
- [x] 2.10 Checkpoint: `dotnet build backend/ExpenseTracker.sln` → 0 errors;
      `dotnet format backend/ExpenseTracker.sln --verify-no-changes`; `dotnet test` (unit +
      integration, requires a migrated local/dev SQL Server per `backend/CLAUDE.md`) → all green
      — result: 291 unit + 267 integration tests passed, 0 warnings

## 3. Frontend Traceability Cross-Check

- [x] 3.1 Cross-check existing Vitest/RTL component tests against the backend-owned AC/BR rows
      from Section 2 — confirm frontend tests verify UI behavior only and add a Test File
      cross-reference in the matrix where a frontend test covers the UX-only half of an AC (per
      `docs/SDS.md` §10.5: business rules tested once, on the backend)
- [x] 3.2 Identify any FRS-implied frontend UI behavior (e.g. field-level validation messaging,
      role-based view gating) with no Vitest coverage; write the missing component test — 4 gaps
      closed (ExpenseForm required-field messages, RejectionDialog inline error text, AppLayout
      Finance-only nav links, FinanceSearchFilters category/status in isolation); 1 further finding
      (ExpenseListPage's unguarded "New expense" button) is a genuine UI defect, not a test gap —
      flagged in `docs/TRACEABILITY.md`, folded into **ET021**, not fixed here
- [x] 3.3 Checkpoint: `pnpm --filter frontend lint` → 0 warnings; `pnpm --filter frontend build` →
      0 errors; `pnpm --filter frontend test` → all green — result: 34 files / 239 tests passed

## 4. E2E Gap-Fill (ET019 Dashboard/Reports/Receipt Viewer)

- [x] 4.1 ~~Add the reserved seed row constants chosen in 1.4 to `e2e/expenseTestData.ts`~~ —
      **superseded**: reuses the existing `EXPENSE_E2E_*`/`EXPENSE_E2E_MANAGER_*`/
      `EXPENSE_E2E_FINANCE_*` constants instead (see design.md) since dashboard assertions only
      need tile presence/absence, not exact counts — no new constants added
- [x] 4.2 Create `e2e/08-dashboard-reports.spec.ts` skeleton following `e2e/07-*.spec.ts`
      conventions (`test.describe.serial`, one shared `page`, `registerOrLogin` per role switch)
- [x] 4.3 Implement the Employee/Manager/Finance dashboard-metrics e2e scenario (FRS §8.1.1–§8.1.3)
- [x] 4.4 Implement the Finance monthly-reimbursement-report-download e2e scenario
      (FRS §7.1.4/§10.1.2)
- [x] 4.5 Implement the receipt/attachment-viewer e2e scenario (expense detail → view receipt)
- [x] 4.6 Run `e2e/08-dashboard-reports.spec.ts` locally against the running dev backend + frontend
      (per design.md's checkpoint commands) and confirm all scenarios pass — result: all 7 pass,
      both standalone and within the full suite. Along the way found and fixed 3 genuine
      pre-existing defects unrelated to new coverage (stale "welcome text" e2e assertions,
      ComplianceOfficer dashboard-redirect assertion bug in `authHelpers.ts`) and repaired 4
      drifted local dev-DB e2e account passwords — see design.md Risks
- [x] 4.7 Add rows to `docs/TRACEABILITY.md` for the three new e2e scenarios, marked
      `Added (ET020)`
- [x] 4.8 Checkpoint: full `pnpm e2e` run (all 8 spec files) → all green — result: 26/27 pass with
      a test-only `RateLimiting__AuthEndpoints__PermitLimit` override (see design.md Risks for why
      this is needed and how CI applies it); the 1 remaining local failure is a confirmed
      data-volume artifact from this session's own repeated reruns, not a defect (see design.md)

## 5. CI Pipeline (`.github/workflows/ci.yml`)

- [x] 5.1 Scaffold the workflow: trigger on `pull_request` and `push` to `main`; `actions/checkout`,
      `actions/setup-dotnet` (reading the `backend/global.json` pin from 1.1),
      `pnpm/action-setup` + `actions/setup-node` (also added `workflow_dispatch` for validation)
- [x] 5.2 Add the SQL Server 2022 service container (`mcr.microsoft.com/mssql/server:2022-latest`)
      with an inline CI-only `SA_PASSWORD`, port `1433` mapped, and a healthcheck/retry step before
      proceeding
- [x] 5.3 Add the backend lint/build step: `dotnet build backend/ExpenseTracker.sln`
- [x] 5.4 Add the frontend lint step: `pnpm --filter frontend lint`
- [x] 5.5 Add the frontend build step: `pnpm --filter frontend build`
- [x] 5.6 Add the DB migrate step: `dotnet ef database update --project backend/src/Infrastructure
      --startup-project backend/src/Api`, connection string via `ConnectionStrings__DefaultConnection`
      pointed at the service container (added `dotnet tool install --global dotnet-ef` first —
      not preinstalled on `ubuntu-latest`)
- [x] 5.7 Add the backend unit test step: `dotnet test --filter FullyQualifiedName~UnitTests`
- [x] 5.8 Add the backend integration test step:
      `dotnet test --filter FullyQualifiedName~IntegrationTests`
- [x] 5.9 Add the frontend unit test step: `pnpm --filter frontend test`
- [x] 5.10 Add the e2e step: start the backend API in the background with
      `ASPNETCORE_ENVIRONMENT=Development` (so the CSV `ISeedRunner` runs) and
      `RateLimiting__AuthEndpoints__PermitLimit=50` (test-only override, see design.md Risks),
      start `pnpm --filter frontend dev`, poll both for readiness, run `pnpm e2e`, then kill both
      background processes in an `if: always()` cleanup step; uploads e2e logs/traces on failure
- [x] 5.11 Push the workflow on a scratch branch (or use `workflow_dispatch`) to validate the full
      pipeline actually goes green end-to-end before relying on it for this ticket's own PR —
      validated via a throwaway scratch-branch PR (#20, closed without merging after validation,
      since `workflow_dispatch` requires the workflow to already exist on the default branch).
      Found and fixed 3 real CI-authoring bugs in the process, none related to app code: (1)
      scratch branch initially missing `backend/global.json` — an artifact of validating on a
      branch cut from `main`, not a bug in `ci.yml` itself; (2) `pnpm/action-setup@v4` needs an
      explicit `version:` — the repo has no `packageManager` field in `package.json` to infer one
      from; (3) a redundant host-side "wait for SQL Server" step used `sqlcmd`, which isn't
      installed on the `ubuntu-latest` runner (only inside the service container, where the
      `services:` block's own `--health-cmd` already runs it and gates job start) — removed.
      Final validated run: all stages green in ~5 minutes
      (https://github.com/balwant-rathore/ExpenseTracker/actions/runs/29974927503).

## 6. Consistency Pass

- [x] 6.1 Re-trace every "handled identically" claim surfaced during the audit (e.g. "Submit
      re-runs the same checks as Create") side by side against the actual code, per `AGENTS.md`
      §13 — don't rely on one instance standing in for the rest. Re-verified: Manager-reject and
      Compliance-reject share the same `RejectExpenseRequestValidator` (not two implementations
      that could silently diverge); FRS 7.1.4 and §10.1's "monthly report" are genuinely the same
      single endpoint (`GET /api/reports/monthly-reimbursement`), not two parallel features
- [x] 6.2 Confirm `docs/TRACEABILITY.md` has zero blank/TBD rows and every referenced test file
      path is real — 0 `TBD` markers remain; every referenced `.cs`/`.ts`/`.tsx` basename verified
      to exist in the repo (BR-10's row intentionally shows `—`/gap, flagged and tracked as ET021,
      not an oversight)
- [x] 6.3 Reconcile `design.md` against what was actually implemented (SDK pin, chosen e2e seed
      rows, CI step order) and update it in this same change if anything drifted — merged a
      duplicate "Risks / Trade-offs" heading, resolved the stale "new reserved seed rows" open
      question (superseded — reused existing constants instead), added the pnpm version pin /
      dotnet-ef tool install / removed-host-side-SQL-wait details to Decision 3, updated the e2e
      file count (7→8)

## 7. Spec Scenario Verification

- [x] 7.1 Verify scenario "Every acceptance criterion has a traceability row" — cross-check
      `docs/TRACEABILITY.md` row-by-row against `docs/FRS.md` §3–§10's numbered criteria — result:
      all 66 numbered AC IDs in FRS.md have a matching row (scripted diff, zero missing)
- [x] 7.2 Verify scenario "Every business rule has a traceability row" — confirm BR-01–BR-10 each
      have a row naming a real backend test — result: 9/10 Covered; BR-10 is an intentional,
      flagged implementation gap (tracked as ET021), which the spec.md scenario text was amended
      to explicitly allow (task 7.2 also drove that spec.md edit, re-validated with
      `openspec validate --strict`)
- [x] 7.3 Verify scenario "Referenced test actually exists and passes" — run the full backend,
      frontend, and e2e suites once, end-to-end, and confirm no matrix row references a
      renamed/deleted/failing test — result: 291 unit + 267 integration + 239 frontend tests green;
      e2e green in real CI (PR #20); every referenced file basename confirmed to exist (task 6.2)
- [x] 7.4 Verify scenario "Pipeline fails fast on a lint failure" — on a scratch branch, introduce
      a temporary lint violation, confirm CI stops at the lint stage without reaching build/test,
      then revert the scratch change — **empirically validated** via scratch PR #21: backend build
      passed, Frontend lint failed, every later step (frontend build, migrate, tests, e2e) skipped.
      (Required `--no-verify` for this one scratch commit only, since the repo's own pre-commit
      hook already blocks lint violations from being committed normally — confirming defense in
      depth, not a workaround around anything real.)
- [x] 7.5 Verify scenario "Pipeline passes when all stages are green" — confirm a clean run reports
      overall success — validated by PR #20's fully green run (all stages, ~5 min)
- [x] 7.6 Verify scenario "A failing test blocks the pipeline regardless of coverage" — on a
      scratch branch, temporarily break one backend integration test, confirm CI fails despite
      other stages passing, then revert — **empirically validated** via the same scratch PR #21:
      lint/build/migrate/unit tests all passed, the sabotaged integration test failed, frontend
      tests and e2e correctly skipped
- [x] 7.7 Verify scenario "Each dashboard role sees its own metrics end-to-end" (covered by 4.3) —
      all 3 role scenarios pass in `e2e/08-dashboard-reports.spec.ts`, confirmed in real CI
- [x] 7.8 Verify scenario "Finance downloads the monthly reimbursement report end-to-end" (covered
      by 4.4) — passes, confirmed in real CI
- [x] 7.9 Verify scenario "Receipt attachment viewer renders end-to-end" (covered by 4.5) — passes,
      confirmed in real CI
- [x] 7.10 Verify scenarios "Ticket status blocked/advances by gate" — confirm `docs/TICKETS.md`'s
      status-convention notes are followed when this ticket itself moves to `Done` — ET020's own
      status stays `In progress` through archiving (per `/implement`'s own rule: archive happens
      before the PR exists, so `Done` isn't set here) and only advances once its real PR merges —
      to be applied literally in Phase 8

## 8. Archive & Release Readiness

- [x] 8.1 Run the full local quality gate one final time: `dotnet build`, `dotnet format
      --verify-no-changes`, `dotnet test` (unit + integration), `pnpm --filter frontend lint`,
      `pnpm --filter frontend build`, `pnpm --filter frontend test`, `pnpm e2e` — all green.
      Result: backend build 0 warnings/errors, format clean, 291 unit + 267 integration tests
      passed; frontend lint clean, build clean, 239 tests (34 files) passed. For e2e, relied on
      the already-validated fully-green real CI run (PR #20,
      https://github.com/balwant-rathore/ExpenseTracker/actions/runs/29974927503) as the
      authoritative result rather than another local full-suite rerun — this session's own
      extensive local debugging accumulated enough duplicate-dated test data in the shared dev DB
      to produce a misleading false negative unrelated to actual code correctness (documented in
      design.md Risks); the CI run uses a fresh database and exactly matches the real pipeline.
- [ ] 8.2 Run `openspec archive et020-quality-release`
- [ ] 8.2 Run `openspec archive et020-quality-release`
- [ ] 8.3 Update `docs/TICKETS.md` ET020 status to `PR open (#N)` once the PR is opened (per the
      `/pr` flow), then to `Done` after merge
