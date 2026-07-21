# dashboard-summary Specification

## Purpose
TBD - created by archiving change et013-dashboard. Update Purpose after archive.
## Requirements
### Requirement: Dashboard Endpoint
The `Api` layer SHALL expose `GET /api/dashboard`, requiring authentication, returning a
role-specific summary of the caller's visible expenses computed from live data with no
separately persisted reporting state (`docs/FRS.md` §8.1, `docs/SDS.md` §8.1–8.2). The
caller's role SHALL be re-resolved from the `Employee` record on every request, never
trusted from the JWT.

#### Scenario: Authenticated Employee/Manager/Finance caller receives a dashboard
- **WHEN** an authenticated caller whose resolved role is `Employee`, `Manager`, or
  `Finance` GETs `/api/dashboard`
- **THEN** the response is `200` with the metrics applicable to that role

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `GET /api/dashboard` carries no valid `Authorization: Bearer`
  token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

### Requirement: Compliance Officer Has No Dashboard
A `ComplianceOfficer`-role caller SHALL be rejected when calling `GET /api/dashboard`,
per `docs/SDS.md` §8.1/§8.5 ("No dashboard" / dashboard access matrix excludes
Compliance). Confirmed with the ticket owner during `/spec`.

#### Scenario: Compliance Officer is rejected
- **WHEN** a `ComplianceOfficer` GETs `/api/dashboard`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

### Requirement: Employee Dashboard Metrics
An `Employee`-role caller's dashboard response SHALL contain exactly three metrics,
each scoped to expenses the caller owns (`Expense.EmployeeId == caller`), per
`docs/FRS.md` §8.1.1:
- `totalSubmitted`: count of the caller's expenses with `Status == Submitted`.
- `approved`: count of the caller's expenses with `Status == Approved` or
  `Status == ComplianceApproved` (`docs/SDS.md` §8.1 footnote).
- `reimbursed`: count of the caller's expenses with `Status == Reimbursed`.

No other metric fields (e.g. `pendingApprovals`, `pendingReimbursements`) SHALL be
present in the Employee response.

#### Scenario: Employee sees only their own counts
- **WHEN** an `Employee` GETs `/api/dashboard` and owns two `Submitted` expenses and
  another employee owns three `Submitted` expenses
- **THEN** the response's `totalSubmitted` is `2`

#### Scenario: Employee approved count includes Compliance Approved
- **WHEN** an `Employee` GETs `/api/dashboard` and owns one `Approved` expense and one
  `ComplianceApproved` expense
- **THEN** the response's `approved` is `2`

#### Scenario: Employee Draft and Cancelled expenses are never counted
- **WHEN** an `Employee` GETs `/api/dashboard` and owns one `Draft` expense and one
  `Cancelled` expense, with no other expenses
- **THEN** the response's `totalSubmitted`, `approved`, and `reimbursed` are all `0`

#### Scenario: Employee response omits Manager/Finance-only fields
- **WHEN** an `Employee` GETs `/api/dashboard`
- **THEN** the response body contains no `pendingApprovals` or
  `pendingReimbursements` field

### Requirement: Manager Dashboard Metrics
A `Manager`-role caller's dashboard response SHALL contain four metrics, scoped to the
manager's **direct reports only** (`Employee.ManagerId == caller`, non-recursive —
matching `openspec/specs/expense-visibility/spec.md`'s existing precedent), and SHALL
**exclude the manager's own expenses entirely** from every metric, per `docs/SDS.md`
§8.1 ("Manager dashboard excludes the manager's own expenses"). This is a deliberate
divergence from `GET /api/expenses` visibility (which does include the manager's own
expenses) and was confirmed with the ticket owner during `/spec`.
- `totalSubmitted`: count of direct reports' expenses with `Status == Submitted`.
- `approved`: count of direct reports' expenses with `Status == Approved` or
  `Status == ComplianceApproved`.
- `reimbursed`: count of direct reports' expenses with `Status == Reimbursed`.
- `pendingApprovals`: count of direct reports' expenses with `Status == Submitted`
  (i.e., identical to `totalSubmitted` for the Manager scope — both represent expenses
  currently awaiting the manager's decision).

#### Scenario: Manager's own expenses are excluded from every metric
- **WHEN** a `Manager` GETs `/api/dashboard` and owns a `Submitted` expense, with no
  expenses from any direct report
- **THEN** the response's `totalSubmitted` and `pendingApprovals` are both `0`

#### Scenario: Manager sees only direct reports' counts
- **WHEN** a `Manager` GETs `/api/dashboard` and a direct report owns one `Submitted`
  expense
- **THEN** the response's `totalSubmitted` and `pendingApprovals` are both `1`

#### Scenario: Manager does not see an indirect report's expense
- **WHEN** a `Manager` GETs `/api/dashboard` and an employee two levels down the
  reporting chain (a report of one of the caller's direct reports) has a `Submitted`
  expense
- **THEN** that expense is not counted in any Manager metric

#### Scenario: Manager approved count includes Compliance Approved
- **WHEN** a `Manager` GETs `/api/dashboard` and a direct report owns one `Approved`
  expense and one `ComplianceApproved` expense
- **THEN** the response's `approved` is `2`

#### Scenario: Manager Draft and Cancelled expenses are never counted
- **WHEN** a `Manager` GETs `/api/dashboard` and a direct report owns one `Draft`
  expense and one `Cancelled` expense, with no other expenses
- **THEN** all four Manager metrics are `0`

#### Scenario: Manager response omits the Finance-only field
- **WHEN** a `Manager` GETs `/api/dashboard`
- **THEN** the response body contains no `pendingReimbursements` field

### Requirement: Finance Dashboard Metrics
A `Finance`-role caller's dashboard response SHALL contain five metrics, scoped
organization-wide across every employee (no manager/team restriction), per
`docs/FRS.md` §8.1.3:
- `totalSubmitted`: count of all expenses with `Status == Submitted`.
- `approved`: count of all expenses with `Status == Approved` or
  `Status == ComplianceApproved`.
- `reimbursed`: count of all expenses with `Status == Reimbursed`.
- `pendingApprovals`: count of all expenses with `Status == Submitted` (awaiting a
  Manager decision — does not include `Approved` Client Entertainment expenses still
  awaiting Compliance sign-off; confirmed with the ticket owner during `/spec`).
- `pendingReimbursements`: count of all expenses that are actually reimbursable right
  now — `(Status == Approved AND Category != ClientEntertainment)` **or**
  `Status == ComplianceApproved`. Excludes `Approved` Client Entertainment expenses
  still awaiting Compliance sign-off, since those are not yet eligible for the
  `/reimburse` action (confirmed with the ticket owner during `/spec`).

#### Scenario: Finance sees organization-wide counts
- **WHEN** a `Finance` caller GETs `/api/dashboard` and two different employees (with
  different managers) each own a `Submitted` expense
- **THEN** the response's `totalSubmitted` is `2`

#### Scenario: Pending reimbursements excludes Approved Client Entertainment awaiting compliance
- **WHEN** a `Finance` caller GETs `/api/dashboard` and one `ClientEntertainment`
  expense has `Status == Approved` (not yet Compliance-approved), with no other
  expenses
- **THEN** the response's `pendingReimbursements` is `0`

#### Scenario: Pending reimbursements includes Compliance Approved Client Entertainment
- **WHEN** a `Finance` caller GETs `/api/dashboard` and one `ClientEntertainment`
  expense has `Status == ComplianceApproved`
- **THEN** the response's `pendingReimbursements` is `1`

#### Scenario: Pending reimbursements includes Approved non-Client-Entertainment expenses
- **WHEN** a `Finance` caller GETs `/api/dashboard` and one `Travel` expense has
  `Status == Approved`
- **THEN** the response's `pendingReimbursements` is `1`

#### Scenario: Pending approvals excludes Approved Client Entertainment awaiting compliance
- **WHEN** a `Finance` caller GETs `/api/dashboard` and one `ClientEntertainment`
  expense has `Status == Approved` (not yet Compliance-approved), with no
  `Submitted`-status expenses
- **THEN** the response's `pendingApprovals` is `0`

#### Scenario: Finance approved count includes Compliance Approved
- **WHEN** a `Finance` caller GETs `/api/dashboard` and one expense has
  `Status == Approved` and another has `Status == ComplianceApproved`
- **THEN** the response's `approved` is `2`

#### Scenario: Finance Draft and Cancelled expenses are never counted
- **WHEN** a `Finance` caller GETs `/api/dashboard` and one expense has `Status ==
  Draft` and another has `Status == Cancelled`, with no other expenses
- **THEN** all five Finance metrics are `0`

