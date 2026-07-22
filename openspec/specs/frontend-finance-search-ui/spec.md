# frontend-finance-search-ui Specification

## Purpose
TBD - created by archiving change et018-expense-review. Update Purpose after archive.
## Requirements
### Requirement: Finance Search Screen
The frontend SHALL provide a dedicated search route (`/finance/search`), restricted to the
`Finance` role, that calls `POST /api/expenses/search` and renders the returned `items` in a
paginated table reflecting `page`, `pageSize`, and `totalRecords` (`docs/FRS.md` §7.1.1,
`finance-expense-search` capability) — distinct from the general `/expenses` list, since this
endpoint's filters and Draft-exclusion behavior differ from `GET /api/expenses`.

#### Scenario: Finance sees a paged result with no filters applied
- **WHEN** a `Finance` caller navigates to `/finance/search`
- **THEN** the frontend SHALL call `POST /api/expenses/search` with an empty filter body and
  render the returned `items`, `page`, `pageSize`, and `totalRecords`

#### Scenario: Non-Finance role cannot reach the route
- **WHEN** an authenticated user whose role is not `Finance` navigates to `/finance/search`
- **THEN** the frontend SHALL NOT render the search screen and SHALL redirect the user away from
  it

#### Scenario: Each result row links to the expense detail
- **WHEN** the search screen renders a result row
- **THEN** that row SHALL link to the corresponding `/expenses/{id}` detail route

### Requirement: Search Filter Controls
The search screen SHALL provide filter controls for `expenseNumber`, `employeeName`, `category`,
`status`, and a `fromDate`/`toDate` range, passing each as the corresponding field in the
`POST /api/expenses/search` request body (`finance-expense-search` capability's Search Filters
requirement). Omitted filters SHALL NOT be included in the request body.

#### Scenario: Filling expenseNumber and submitting filters by exact match
- **WHEN** a `Finance` caller enters an expense number and submits the filter form
- **THEN** the frontend SHALL call `POST /api/expenses/search` with `expenseNumber` set to the
  entered value

#### Scenario: Filling employeeName and submitting filters by substring
- **WHEN** a `Finance` caller enters an employee name and submits the filter form
- **THEN** the frontend SHALL call `POST /api/expenses/search` with `employeeName` set to the
  entered value

#### Scenario: Category and status filters combine in one request
- **WHEN** a `Finance` caller selects both a category and a status and submits the filter form
- **THEN** the frontend SHALL call `POST /api/expenses/search` with both `category` and `status`
  set in the same request

#### Scenario: Setting a date range passes fromDate and toDate
- **WHEN** a `Finance` caller sets a from/to date range and submits the filter form
- **THEN** the frontend SHALL call `POST /api/expenses/search` with `fromDate` and `toDate` set to
  the selected values

#### Scenario: Clearing all filters re-issues an unfiltered search
- **WHEN** a `Finance` caller clears every filter after having set one or more
- **THEN** the frontend SHALL call `POST /api/expenses/search` with no filter fields set

### Requirement: Search Pagination and Sorting Controls
The search screen SHALL provide UI controls for `page`, `pageSize` (20, 50, 100, 500 — the
`finance-expense-search` capability's allowed set, distinct from the general expense list's 10,
20, 50, 100), `sortBy` (`expenseDate`, `expenseNumber`, `createdAt`, `amount`, `submittedAt`,
`approvedAt`, `reimbursedAt`, `rejectedAt`), and `sortDirection` (`asc`, `desc`), passing the
user's selection as fields in the `POST /api/expenses/search` request body. The controls SHALL
only offer these backend-allowed values.

#### Scenario: Changing the page size re-issues the search with the new value
- **WHEN** a `Finance` caller selects a different page size from the control
- **THEN** the frontend SHALL call `POST /api/expenses/search` with the new `pageSize` and display
  the refreshed results

#### Scenario: Changing sortBy or sortDirection re-issues the search with updated values
- **WHEN** a `Finance` caller selects a different `sortBy` field or toggles `sortDirection`
- **THEN** the frontend SHALL call `POST /api/expenses/search` with the updated `sortBy`/
  `sortDirection` and display the refreshed, re-sorted results

#### Scenario: Only backend-allowed page sizes are offered
- **WHEN** the page size control renders
- **THEN** it SHALL offer only 20, 50, 100, and 500 — not 10, which `GET /api/expenses` allows but
  `POST /api/expenses/search` does not

### Requirement: Reimbursement Happens on the Detail Screen, Not Inline in Search Results
The search screen SHALL NOT render its own inline "Reimburse" control in the results table;
reimbursement is performed on the expense detail screen via the Finance Reimburse Action defined
in `frontend-expense-review-actions-ui`, reached by navigating from a search result row. This
avoids two independently-maintained implementations of the same action.

#### Scenario: Search results table has no inline reimburse control
- **WHEN** the search screen renders its results table
- **THEN** no row SHALL present a "Reimburse" button or control directly within the table

#### Scenario: Reimbursing a search result happens after navigating to its detail
- **WHEN** a `Finance` caller clicks a search result row for an eligible expense and then clicks
  "Reimburse" on the detail screen that opens
- **THEN** the frontend SHALL call `POST /api/expenses/{id}/reimburse` exactly as specified by
  `frontend-expense-review-actions-ui`'s Finance Reimburse Action requirement

