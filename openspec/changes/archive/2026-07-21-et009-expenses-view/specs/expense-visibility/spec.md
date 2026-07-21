## ADDED Requirements

### Requirement: Expense List Endpoint
The `Api` layer SHALL expose `GET /api/expenses`, requiring authentication, returning a
server-side paged and sorted list of expenses visible to the caller under their role's
default visibility rules (`docs/FRS.md` §4.4, `docs/SDS.md` §5.2), each mapped to
`ExpenseResponse`.

#### Scenario: Authenticated caller receives a paged result
- **WHEN** an authenticated caller GETs `/api/expenses`
- **THEN** the response is `200` with `items`, `page`, `pageSize`, and `totalRecords`

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `GET /api/expenses` carries no valid `Authorization: Bearer`
  token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

### Requirement: Employee Default Visibility
An `Employee`-role caller SHALL see, via `GET /api/expenses`, only expenses they own,
at any `Status` including `Draft` (`docs/FRS.md` §4.4.1).

#### Scenario: Employee sees only their own expenses
- **WHEN** an `Employee` GETs `/api/expenses`
- **THEN** the response's `items` contains only expenses whose `EmployeeId` matches the
  caller, and excludes every other employee's expenses

#### Scenario: Employee's own Draft expense is included
- **WHEN** an `Employee` GETs `/api/expenses` and owns a `Draft` expense
- **THEN** that `Draft` expense is included in `items`

### Requirement: Manager Default Visibility
A `Manager`-role caller SHALL see, via `GET /api/expenses`, their own expenses (any
`Status`) merged with the non-`Draft` expenses of their **direct reports only**
(`Employee.ManagerId` — not a recursive reporting chain), per `docs/FRS.md` §4.4.2.

#### Scenario: Manager sees their own expenses at any status
- **WHEN** a `Manager` GETs `/api/expenses` and owns a `Draft` expense
- **THEN** that `Draft` expense is included in `items`

#### Scenario: Manager sees a direct report's non-Draft expense
- **WHEN** a `Manager` GETs `/api/expenses` and a direct report (an `Employee` whose
  `ManagerId` is the caller) has a `Submitted` expense
- **THEN** that expense is included in `items`

#### Scenario: Manager does not see a direct report's Draft expense
- **WHEN** a `Manager` GETs `/api/expenses` and a direct report has a `Draft` expense
- **THEN** that expense is excluded from `items`

#### Scenario: Manager does not see an indirect report's expense
- **WHEN** a `Manager` GETs `/api/expenses` and an employee two levels down the
  reporting chain (a report of one of the caller's direct reports) has a `Submitted`
  expense
- **THEN** that expense is excluded from `items` — visibility is direct reports only,
  not recursive

#### Scenario: Manager does not see an unrelated employee's expense
- **WHEN** a `Manager` GETs `/api/expenses` and an employee who neither reports to them
  nor is them has a `Submitted` expense
- **THEN** that expense is excluded from `items`

### Requirement: Finance Default Visibility
A `Finance`-role caller SHALL see, via `GET /api/expenses`, all expenses across every
employee except those with `Status` `Draft` (`docs/FRS.md` §4.4.3).

#### Scenario: Finance sees non-Draft expenses of any employee
- **WHEN** a `Finance` caller GETs `/api/expenses`
- **THEN** the response's `items` includes every employee's expense whose `Status` is
  not `Draft`

#### Scenario: Finance does not see Draft expenses
- **WHEN** a `Finance` caller GETs `/api/expenses` and any employee has a `Draft`
  expense
- **THEN** that expense is excluded from `items`

### Requirement: Compliance Officer Default Visibility
A `ComplianceOfficer`-role caller SHALL see, via `GET /api/expenses`, only
`ClientEntertainment`-category expenses whose `Status` is `Approved` or
`ComplianceApproved` (`docs/FRS.md` §4.4.4). This extends past the literal FRS text
("can only view `Approved` expenses") to also include `ComplianceApproved`, confirmed
with the ticket owner during `/spec` — Compliance retains visibility into an expense
after acting on it, but loses visibility once Finance reimburses it (`Reimbursed` is
excluded).

#### Scenario: Compliance sees an Approved Client Entertainment expense
- **WHEN** a `ComplianceOfficer` GETs `/api/expenses` and a `ClientEntertainment`
  expense has `Status` `Approved`
- **THEN** that expense is included in `items`

#### Scenario: Compliance sees a Compliance Approved Client Entertainment expense
- **WHEN** a `ComplianceOfficer` GETs `/api/expenses` and a `ClientEntertainment`
  expense has `Status` `ComplianceApproved`
- **THEN** that expense is included in `items`

#### Scenario: Compliance does not see a Reimbursed Client Entertainment expense
- **WHEN** a `ComplianceOfficer` GETs `/api/expenses` and a `ClientEntertainment`
  expense has `Status` `Reimbursed`
- **THEN** that expense is excluded from `items`

#### Scenario: Compliance does not see non-Client-Entertainment expenses
- **WHEN** a `ComplianceOfficer` GETs `/api/expenses` and an expense in any other
  category has `Status` `Approved`
- **THEN** that expense is excluded from `items`

#### Scenario: Compliance does not see a Draft or Submitted Client Entertainment expense
- **WHEN** a `ComplianceOfficer` GETs `/api/expenses` and a `ClientEntertainment`
  expense has `Status` `Draft` or `Submitted`
- **THEN** that expense is excluded from `items`

### Requirement: List Pagination and Sorting
`GET /api/expenses` SHALL apply server-side pagination and sorting per `docs/SDS.md`
§5.2: `page` (default 1), `pageSize` (default 20; allowed 10, 20, 50, 100), `sortBy`
(default `expenseDate`; allowed `expenseDate`, `expenseNumber`, `createdAt`, `amount`,
`submittedAt`, `approvedAt`, `reimbursedAt`, `rejectedAt`), `sortDirection` (default
`desc`; allowed `asc`, `desc`). Filtering, sorting, and paging SHALL be applied in the
database query itself, never by materializing the full visible set into memory first.

#### Scenario: Defaults apply when no query parameters are given
- **WHEN** an authenticated caller GETs `/api/expenses` with no query parameters
- **THEN** the response reflects `page=1`, `pageSize=20`, sorted by `expenseDate`
  `desc`

#### Scenario: Valid custom paging and sorting is honored
- **WHEN** a caller GETs `/api/expenses?page=2&pageSize=50&sortBy=amount&sortDirection=asc`
- **THEN** the response's `page` and `pageSize` reflect the request, and `items` is
  sorted by `amount` ascending

#### Scenario: Invalid pageSize is rejected
- **WHEN** a caller GETs `/api/expenses?pageSize=15` (not one of 10, 20, 50, 100)
- **THEN** the response is `400` with code `VALIDATION_ERROR`

#### Scenario: Invalid sortBy is rejected
- **WHEN** a caller GETs `/api/expenses?sortBy=notAField`
- **THEN** the response is `400` with code `VALIDATION_ERROR`
