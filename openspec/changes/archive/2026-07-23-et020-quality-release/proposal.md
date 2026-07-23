## Why

ET001–ET019 delivered the full Expense Management System (auth, expenses, review workflows,
finance, dashboard, reports, notifications) but no ticket has yet verified, end-to-end, that every
`docs/FRS.md` acceptance criterion and business rule (BR-01–BR-10) is actually covered by an
automated test, nor has the repo had CI enforcement to keep that true going forward. ET020
(`docs/TICKETS.md`, Domain: Quality, FRS Sections: All) closes that gap before release: audit
existing coverage, fill genuine gaps, produce a traceability matrix, and wire CI quality gates so
regressions are caught automatically rather than by manual review.

## What Changes

- Audit all existing backend tests (45 unit / 37 integration files under `backend/tests/`),
  frontend tests (34 Vitest files under `frontend/src/`), and Playwright e2e specs (`e2e/01`–`07`)
  against every numbered acceptance criterion in `docs/FRS.md` §3–§10 and BR-01–BR-10 (§11).
- Fill identified gaps only — most notably, no e2e coverage exists yet for the ET019 dashboard,
  finance search, monthly-report-download, and receipt-viewer flows (FRS §7.1.4, §8, §10); any
  other AC/BR found unmapped during the audit gets a targeted unit/integration/e2e test added at
  the layer AGENTS.md §10/SDS §10.5 assigns it (business rules on the backend only; frontend tests
  verify UI behavior, not business rules).
- Produce `docs/TRACEABILITY.md`: one row per FRS acceptance criterion/BR mapping it to the
  specific test file(s)/method(s) that verify it, per the full AC-level trace agreed with the
  user (not a lighter gap-only pass).
- Add `.github/workflows/ci.yml` running, in order, the quality gates from `CLAUDE.md`: backend
  lint/build (`dotnet build`, analyzers) → frontend lint (`pnpm --filter frontend lint`) → build
  (`dotnet build`, `pnpm --filter frontend build`) → backend unit tests → backend integration
  tests → frontend unit tests (Vitest) → Playwright e2e. The gate is pass/fail on all suites —
  no numeric code-coverage threshold is enforced (per user decision).
- Mark release readiness: update `docs/TICKETS.md` ET020 status once the above lands and gates
  are green.

No production business logic changes. If the audit surfaces a genuine functional gap (an FRS
requirement with no implementing code at all, not just no test), that is a separate defect to be
raised against the ticket that should have implemented it, not silently patched inside ET020.

## Capabilities

### New Capabilities
- `quality-release`: FRS-to-test traceability matrix and CI quality-gate pipeline behavior
  (what must pass, in what order, before a change is release-ready).

### Modified Capabilities
(none — this change verifies and gates existing behavior; it does not alter any requirement in
an existing capability spec)

## Impact

- **New files**: `docs/TRACEABILITY.md`, `.github/workflows/ci.yml`, plus any net-new test files
  discovered as gaps (expected: `e2e/08-dashboard-reports.spec.ts` or similar for ET019 flows).
- **No changes** to `Api`/`Application`/`Domain`/`Infrastructure` production code or frontend
  `features/`/`pages/` components, unless the audit uncovers a genuine defect (tracked separately,
  not folded into this change).
- **CI**: first GitHub Actions workflow in the repo — no prior `.github/` directory exists.
- **Docs**: `docs/TICKETS.md` ET020 row status updated per `docs/TICKETS.md`'s status-convention
  notes once implementation lands.
