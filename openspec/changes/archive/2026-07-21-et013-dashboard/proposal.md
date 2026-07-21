## Why

Employees, Managers, and Finance currently have no way to see an at-a-glance summary of expense
volumes and pending work — they must page through `GET /api/expenses` / `POST /api/expenses/search`
and count manually. `docs/FRS.md` §8 (Status Dashboard) requires a single role-specific summary
endpoint; this is ticket **ET013** in `docs/TICKETS.md`, next in the fixed build order after
ET012 (Finance Processing, merged).

## What Changes

- Add `GET /api/dashboard`, returning a role-specific aggregation of the caller's visible
  expenses, re-resolving the caller's role from the `Employee` record per request
  (`docs/FRS.md` §8.1, `docs/SDS.md` §8.1–8.2).
- **Employee** response: `totalSubmitted`, `approved`, `reimbursed` — counts scoped to the
  caller's own expenses only (`docs/FRS.md` §8.1.1).
- **Manager** response: `totalSubmitted`, `approved`, `reimbursed`, `pendingApprovals` — counts
  scoped to the manager's **direct reports only**, explicitly **excluding the manager's own
  expenses** (`docs/FRS.md` §8.1.2; `docs/SDS.md` §8.1 "Manager dashboard excludes the manager's
  own expenses" — confirmed with the ticket owner during `/spec` as taking precedence over the
  less explicit FRS wording, and as a deliberate divergence from `GET /api/expenses`' visibility
  rule, which does include the manager's own expenses).
- **Finance** response: `totalSubmitted`, `approved`, `reimbursed`, `pendingApprovals`,
  `pendingReimbursements` — counts scoped organization-wide (`docs/FRS.md` §8.1.3).
- **Compliance Officer** calling `GET /api/dashboard` receives `403 AUTHORIZATION_FAILED` — per
  `docs/SDS.md` §8.1/§8.5, Compliance Officer has no dashboard (confirmed with the ticket owner
  during `/spec`).
- Each response includes only the fields applicable to the caller's role (`docs/SDS.md` §8.2) —
  no shared flat DTO with unused zero fields across roles.
- Metric definitions (all confirmed with the ticket owner during `/spec`, since neither FRS nor
  SDS defines them at this level of precision):
  - `totalSubmitted`: count of expenses **currently** in `Status == Submitted` (not a cumulative
    lifetime count).
  - `approved`: count of expenses in `Status == Approved` **or** `Status == ComplianceApproved`,
    per `docs/SDS.md` §8.1 footnote ("Approved includes Approved and ComplianceApproved").
  - `reimbursed`: count of expenses in `Status == Reimbursed`.
  - `pendingApprovals` (Manager/Finance only): count of expenses in `Status == Submitted` —
    awaiting a Manager decision. Does not include `Approved` Client Entertainment expenses still
    awaiting Compliance sign-off.
  - `pendingReimbursements` (Finance only): count of expenses that are **actually reimbursable
    right now** — `(Status == Approved AND Category != ClientEntertainment)` **or**
    `Status == ComplianceApproved`. Excludes `Approved` Client Entertainment expenses still
    awaiting Compliance sign-off, since those are not yet eligible for the `/reimburse` action.
  - Manager/Finance team scope uses **direct reports only** (`Employee.ManagerId == caller`),
    non-recursive — matching the existing precedent in
    `openspec/specs/expense-visibility/spec.md`.
  - `Draft` and `Cancelled` expenses are never counted in any metric.

No deviation from `docs/SDS.md` is being introduced beyond the confirmed clarifications above,
which fill gaps the SDS leaves open rather than contradicting it; no ADR is required.

## Capabilities

### New Capabilities
- `dashboard-summary`: Role-specific `GET /api/dashboard` aggregation endpoint for Employee,
  Manager, and Finance, with Compliance Officer explicitly rejected.

### Modified Capabilities

(none — this ticket only adds a new read-only aggregation endpoint; no existing expense
workflow, visibility, or search requirement changes)

## Impact

- **Api**: new `DashboardController` (`GET /api/dashboard`), role-based authorization consistent
  with existing `AuthorizationPolicyNames`.
- **Application**: new `IDashboardService`/`DashboardService`, per-role response DTOs
  (`EmployeeDashboardResponse`, `ManagerDashboardResponse`, `FinanceDashboardResponse`), built on
  `IQueryable` aggregation (COUNT queries) against the existing `Expense`/`Employee` EF Core
  model — no schema changes, no new migration.
- **Domain/Infrastructure**: no entity or schema changes; reuses `Expense`, `Employee`,
  `ExpenseStatus`, `ExpenseCategory`, `EmployeeRole` as-is.
- **Tests**: new unit tests (per-role metric calculation, direct-reports-only scoping, Draft/
  Cancelled exclusion) and integration tests (role authorization incl. Compliance Officer 403,
  response shape per role) per `docs/SDS.md` §10.
- No frontend, notification, or reporting impact in this ticket (frontend dashboard UI is
  ET019).
