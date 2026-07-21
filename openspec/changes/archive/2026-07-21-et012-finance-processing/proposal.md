## Why

Approved expenses currently have no path to reimbursement and Finance has no way to
locate expenses across the organization beyond the generic `GET /api/expenses` list.
`docs/TICKETS.md` ET012 (FRS §7 "Finance Processing") closes this gap: a Finance-only
search endpoint (FRS §7.1.1–7.1.2), a `reimburse` workflow action that completes the
expense lifecycle (FRS §7.1.3, BR-08), and a data endpoint that the ET014 Excel exporter
will build on top of (FRS §7.1.4).

## What Changes

- Add `POST /api/expenses/search` (Finance only): filters by expense number, employee
  name, category, status, and created-date range, with the same server-side pagination/
  sorting conventions as `GET /api/expenses` (`docs/SDS.md` §5.4). Excludes `Draft`
  expenses from the searchable set, mirroring Finance's existing default visibility
  (`docs/FRS.md` §4.4.3) — confirmed with the ticket owner during `/spec`.
- Add `POST /api/expenses/{id}/reimburse` (Finance only): transitions an eligible
  expense to `Reimbursed` (FRS §7.1.3, BR-08), populating `ReimbursedAt` and
  `ReimbursedByEmployeeId` (`docs/SDS.md` §6.8). Eligibility is **category-conditioned**
  — confirmed with the ticket owner during `/spec` as an open decision reconciling FRS
  §7.1.3's literal wording ("Approved or Compliance Approved") against the SDS §6.1/§6.3
  workflow diagrams, which route only non-`ClientEntertainment` expenses through
  `Approved → Reimbursed` and only `ClientEntertainment` expenses through
  `Approved → ComplianceApproved → Reimbursed`:
  - `ClientEntertainment` expenses SHALL require `Status = ComplianceApproved`.
  - All other categories SHALL require `Status = Approved`.
  - Any other status (including a `ClientEntertainment` expense still at `Approved`)
    is rejected `422 BUSINESS_RULE_VIOLATION` with `Status` unchanged. This is logged as
    an ADR in `docs/decisions/` since it narrows FRS §7.1.3's literal text.
  - No self-reimbursement restriction is added: the SDS §6.4 authorization matrix
    already excludes `Finance` from `Create Expense`/`Save Draft`/`Submit Expense`, so a
    Finance-role employee can never own an expense — the scenario is structurally
    impossible, not merely unrestricted.
- Add `GET /api/reports/monthly-reimbursement` (Finance only) as a **data-only
  scaffold**: accepts `year`/`month`, queries reimbursed-expense data for that month
  (`docs/FRS.md` §10.1.1 fields: Employee, Expense Number, Category, Amount, Currency,
  Approval Date, Reimbursement Date), and returns it as JSON. Confirmed with the ticket
  owner during `/spec` as the ET012/ET014 boundary: ET014 ("Monthly reimbursement Excel
  generation using ClosedXML", FRS §10) swaps this endpoint's response to the `.xlsx`
  format described in `docs/SDS.md` §5.6/§8.4 — this ticket does not produce an Excel
  file.

## Capabilities

### New Capabilities
- `finance-expense-search`: `POST /api/expenses/search` — Finance-only multi-filter
  expense search with pagination/sorting, excluding `Draft` expenses (FRS §7.1.1–7.1.2).
- `expense-reimbursement`: `POST /api/expenses/{id}/reimburse` — Finance-only,
  category-conditioned transition to `Reimbursed` with audit field population
  (FRS §7.1.3, BR-08).
- `monthly-reimbursement-report`: `GET /api/reports/monthly-reimbursement` — Finance-only
  data aggregation endpoint for reimbursed expenses in a given month, JSON response in
  this ticket (FRS §7.1.4, §10.1.1; Excel generation deferred to ET014).

### Modified Capabilities
(none — `domain-model` already defines `ReimbursedAt`/`ReimbursedByEmployeeId` on
`Expense` per `docs/SDS.md` §3.6, and `expense-visibility` is unchanged since this
ticket adds a separate Finance-only search endpoint rather than altering
`GET /api/expenses`)

## Impact

- **Api**: three new controller actions (`ExpensesController.Search`,
  `ExpensesController.Reimburse`, new `ReportsController.MonthlyReimbursement`), new
  `[Authorize(Policy = "Finance")]`-gated routes.
- **Application**: new `IExpenseSearchService`/`IExpenseWorkflowService.ReimburseAsync`
  methods (or equivalent, per existing service seams from ET009–ET011), new
  `ExpenseSearchRequest`/`ExpenseSearchResponse` and
  `MonthlyReimbursementReportRequest`/`-Response` DTOs, new FluentValidation validators.
- **Domain/Infrastructure**: no schema changes — reuses existing `Expense` fields and
  `IExpenseRepository`; adds `IQueryable`-composed filtering for the search and report
  queries (no in-memory materialization, per `backend/CLAUDE.md`).
- **No frontend changes** (frontend Finance UI is ET018/ET019).
