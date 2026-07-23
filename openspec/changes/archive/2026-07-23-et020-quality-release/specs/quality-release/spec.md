## ADDED Requirements

### Requirement: FRS-to-Test Traceability Matrix
The repository SHALL contain `docs/TRACEABILITY.md`, a table mapping every numbered acceptance
criterion in `docs/FRS.md` §3–§10 and every business rule `BR-01`–`BR-10` (§11) to the specific
automated test file(s)/method(s) that verify it. Every row SHALL reference a test that actually
exists in the repository at the layer assigned by `AGENTS.md` §10/`docs/SDS.md` §10.5 (business
rules verified once on the backend; frontend tests verify UI behavior only) — **except** a row
where the audit discovers the acceptance criterion or business rule is not actually implemented
(a genuine functional gap, not merely an untested one). Per `AGENTS.md` §13 and this ticket's
"verify and gate, don't alter behavior" scope, such a row SHALL instead explicitly document the
gap and reference a tracked follow-up ticket, rather than silently patching production code or
asserting a test against incorrect behavior.

#### Scenario: Every acceptance criterion has a traceability row
- **WHEN** `docs/TRACEABILITY.md` is checked against the full list of numbered acceptance criteria
  in `docs/FRS.md` §3–§10
- **THEN** every acceptance criterion has at least one corresponding row naming a real test
  file/method, or — for a genuine implementation gap found during the audit — a row documenting
  the gap and a tracked follow-up ticket

#### Scenario: Every business rule has a traceability row
- **WHEN** `docs/TRACEABILITY.md` is checked against `BR-01` through `BR-10`
- **THEN** every business rule has at least one corresponding row naming a real backend test
  file/method (never a frontend-only test), or — for a genuine implementation gap found during
  the audit — a row documenting the gap and a tracked follow-up ticket

#### Scenario: Referenced test actually exists and passes
- **WHEN** a traceability row names a test file and method
- **THEN** that file and method exist in the repository and pass when the corresponding test suite
  is run

### Requirement: CI Quality Gate Pipeline
The repository SHALL define `.github/workflows/ci.yml` that runs, in order, on every pull request:
backend lint/analyzers (`dotnet build`), frontend lint (`pnpm --filter frontend lint`), backend
build (`dotnet build`), frontend build (`pnpm --filter frontend build`), backend unit tests,
backend integration tests, frontend unit tests (Vitest), and Playwright end-to-end tests. The
pipeline SHALL fail at the first failing stage rather than continuing, and SHALL NOT enforce a
numeric code-coverage threshold — pass/fail per stage is the only gate.

#### Scenario: Pipeline fails fast on a lint failure
- **WHEN** a pull request introduces a lint violation in either `backend/` or `frontend/`
- **THEN** the CI workflow fails at the lint stage and does not proceed to build or test stages

#### Scenario: Pipeline passes when all stages are green
- **WHEN** a pull request's lint, build, unit, integration, and e2e stages all succeed
- **THEN** the CI workflow reports an overall success status on the pull request

#### Scenario: A failing test blocks the pipeline regardless of coverage
- **WHEN** all tests pass except one backend integration test, even if overall statement coverage
  is high
- **THEN** the CI workflow fails, since no numeric coverage threshold can substitute for a passing
  test suite

### Requirement: Dashboard and Reports End-to-End Coverage
The Playwright end-to-end suite SHALL include scenarios covering the ET019 frontend surfaces that
existing `e2e/01`–`07` specs do not yet exercise: the Employee, Manager, and Finance dashboard
views (`docs/FRS.md` §8.1.1–§8.1.3), Finance's monthly reimbursement report download
(§7.1.4/§10.1.2), and the expense receipt/attachment viewer.

#### Scenario: Each dashboard role sees its own metrics end-to-end
- **WHEN** an Employee, a Manager, and a Finance user each log in and open the dashboard
- **THEN** each sees only the metrics `docs/FRS.md` §8.1.1–§8.1.3 assigns to their role, verified
  by an end-to-end test per role

#### Scenario: Finance downloads the monthly reimbursement report end-to-end
- **WHEN** a Finance user requests the monthly reimbursement report for a given month from the UI
- **THEN** an end-to-end test verifies the `.xlsx` file download is triggered successfully

#### Scenario: Receipt attachment viewer renders end-to-end
- **WHEN** a user with visibility into an expense opens its receipt attachment from the expense
  detail view
- **THEN** an end-to-end test verifies the attachment viewer renders the receipt content

### Requirement: Release Readiness Gate
A ticket SHALL NOT be marked `Done` in `docs/TICKETS.md` until its applicable quality gates
(lint, build, unit tests, integration tests, and end-to-end tests for user-facing flows, per
`CLAUDE.md`'s Quality Gates order) all pass in CI.

#### Scenario: Ticket status blocked by a failing gate
- **WHEN** any quality gate applicable to a ticket's scope is failing in CI
- **THEN** that ticket's `docs/TICKETS.md` status SHALL NOT be advanced to `Done`

#### Scenario: Ticket status advances once all applicable gates pass
- **WHEN** all quality gates applicable to a ticket's scope pass in CI and its PR is merged
- **THEN** that ticket's `docs/TICKETS.md` status SHALL be updated to `Done`
