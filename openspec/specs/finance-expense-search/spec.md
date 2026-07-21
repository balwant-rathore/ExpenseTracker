# finance-expense-search Specification

## Purpose
TBD - created by archiving change et012-finance-processing. Update Purpose after archive.
## Requirements
### Requirement: Finance Search Endpoint
The `Api` layer SHALL expose `POST /api/expenses/search`, restricted to the `Finance`
role (`docs/SDS.md` §5.4, §6.4), accepting optional filters `expenseNumber`,
`employeeName`, `category`, `status`, `fromDate`, `toDate`, plus pagination/sorting
parameters, and returning a server-side paged and sorted result
(`docs/FRS.md` §7.1.1).

#### Scenario: Finance caller receives a paged result with no filters
- **WHEN** a `Finance` caller POSTs to `/api/expenses/search` with an empty filter body
- **THEN** the response is `200` with `items`, `page`, `pageSize`, and `totalRecords`

#### Scenario: Non-Finance role is rejected
- **WHEN** an authenticated caller whose resolved role is `Employee`, `Manager`, or
  `ComplianceOfficer` POSTs to `/api/expenses/search`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `POST /api/expenses/search` carries no valid
  `Authorization: Bearer` token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

### Requirement: Draft Expenses Are Never Searchable
`POST /api/expenses/search` SHALL exclude every `Draft`-status expense from both the
searchable set and the result set, even when the caller explicitly filters
`status: "Draft"` — mirroring Finance's existing default visibility rule on
`GET /api/expenses` (`docs/FRS.md` §4.4.3, `openspec/specs/expense-visibility/spec.md`).
Confirmed with the ticket owner during `/spec` as this endpoint's default visibility.

#### Scenario: Draft expenses are excluded with no status filter
- **WHEN** a `Finance` caller POSTs to `/api/expenses/search` with no `status` filter
  and a `Draft` expense exists
- **THEN** that expense is excluded from `items`

#### Scenario: Explicitly filtering status=Draft returns no results
- **WHEN** a `Finance` caller POSTs to `/api/expenses/search` with
  `{ "status": "Draft" }`
- **THEN** the response is `200` with `items` empty and `totalRecords` equal to `0`,
  regardless of how many `Draft` expenses exist

### Requirement: Search Filters
`POST /api/expenses/search` SHALL apply each supplied filter as follows
(`docs/FRS.md` §7.1.1):
- `expenseNumber`: exact match (case-insensitive), since `ExpenseNumber` is a unique,
  system-generated identifier.
- `employeeName`: case-insensitive substring ("contains") match against the
  concatenation of the expense owner's `Employee.FirstName`, a single space, and
  `Employee.LastName` — an open decision beyond the literal FRS/SDS text, adopted here
  since neither document specifies exact-vs-partial matching semantics.
- `category`: exact match against one of the seven `ExpenseCategory` values.
- `status`: exact match against one of the seven `ExpenseStatus` values, subject to the
  Draft-exclusion rule above.
- `fromDate`/`toDate`: an inclusive range filter on `Expense.CreatedAt`, per FRS §7.1.1's
  "Date Range (created date range)" — not `ExpenseDate`.

All filters are combined with logical AND. Omitted filters SHALL NOT narrow the result
set. An invalid `category` or `status` value SHALL be rejected with
`400 VALIDATION_ERROR`.

#### Scenario: expenseNumber filter matches exactly
- **WHEN** a `Finance` caller POSTs `{ "expenseNumber": "EXP-20260710-0007" }` and an
  expense with that exact `ExpenseNumber` exists
- **THEN** the response's `items` contains only that expense

#### Scenario: employeeName filter matches a substring case-insensitively
- **WHEN** a `Finance` caller POSTs `{ "employeeName": "raj" }` and an expense is owned
  by an employee named "Raj Malhotra"
- **THEN** that expense is included in `items`

#### Scenario: category and date range filters combine with AND
- **WHEN** a `Finance` caller POSTs
  `{ "category": "Travel", "fromDate": "2026-07-01", "toDate": "2026-07-31" }`
- **THEN** the response's `items` contains only `Travel` expenses whose `CreatedAt`
  falls within July 2026

#### Scenario: Invalid category is rejected
- **WHEN** a `Finance` caller POSTs `{ "category": "NotARealCategory" }`
- **THEN** the response is `400` with code `VALIDATION_ERROR`

#### Scenario: Invalid status is rejected
- **WHEN** a `Finance` caller POSTs `{ "status": "NotARealStatus" }`
- **THEN** the response is `400` with code `VALIDATION_ERROR`

### Requirement: Search Pagination and Sorting
`POST /api/expenses/search` SHALL apply server-side pagination and sorting per
`docs/SDS.md` §5.4: `page` (default 1), `pageSize` (default 20; allowed 20, 50, 100,
500), `sortBy` (default `expenseDate`; allowed `expenseDate`, `expenseNumber`,
`createdAt`, `amount`, `submittedAt`, `approvedAt`, `reimbursedAt`, `rejectedAt`),
`sortDirection` (default `desc`; allowed `asc`, `desc`). Filtering, sorting, and paging
SHALL be applied in the database query itself (`IQueryable` composition), never by
materializing the full result set into memory first (`backend/CLAUDE.md`).

#### Scenario: Defaults apply when no paging/sorting parameters are given
- **WHEN** a `Finance` caller POSTs `{}` to `/api/expenses/search`
- **THEN** the response reflects `page=1`, `pageSize=20`, sorted by `expenseDate` `desc`

#### Scenario: Valid custom paging and sorting is honored
- **WHEN** a `Finance` caller POSTs
  `{ "page": 2, "pageSize": 100, "sortBy": "amount", "sortDirection": "asc" }`
- **THEN** the response's `page` and `pageSize` reflect the request, and `items` is
  sorted by `amount` ascending

#### Scenario: Invalid pageSize is rejected
- **WHEN** a `Finance` caller POSTs `{ "pageSize": 15 }` (not one of 20, 50, 100, 500)
- **THEN** the response is `400` with code `VALIDATION_ERROR`

#### Scenario: Invalid sortBy is rejected
- **WHEN** a `Finance` caller POSTs `{ "sortBy": "notAField" }`
- **THEN** the response is `400` with code `VALIDATION_ERROR`

