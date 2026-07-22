## ADDED Requirements

### Requirement: Expense List Screen
The frontend SHALL provide an expense list route, available to any authenticated user, that calls
`GET /api/expenses` and renders the returned `items` in a paginated table/list reflecting the
response's `page`, `pageSize`, and `totalRecords` (`docs/FRS.md` §4.4, `docs/SDS.md` §5.2). The
frontend SHALL render exactly what the backend returns for the caller's role and SHALL NOT apply
any additional client-side ownership or role-based filtering — per-role visibility (Employee: own
only; Manager: own + direct reports' non-Draft) is already enforced server-side by the
`expense-visibility` capability.

#### Scenario: Authenticated user sees a paginated list
- **WHEN** an authenticated user navigates to the expense list route
- **THEN** the frontend SHALL call `GET /api/expenses` and render its `items`, with pagination
  controls reflecting `page`, `pageSize`, and `totalRecords`

#### Scenario: List reflects exactly what the backend returns
- **WHEN** `GET /api/expenses` returns a set of expenses for the caller's role
- **THEN** the frontend SHALL render exactly that set, without hiding or adding any expense based
  on client-side role logic

#### Scenario: Manager's list includes their direct reports' non-Draft expenses
- **WHEN** a `Manager` navigates to the expense list route and the backend response includes
  both the manager's own expenses and their direct reports' non-`Draft` expenses
- **THEN** the frontend SHALL render both groups in the same list, with no approve/reject action
  controls shown (those actions are out of scope for this capability — see ET018)

### Requirement: List Pagination and Sorting Controls
The list screen SHALL provide UI controls for `page`, `pageSize` (10, 20, 50, 100), `sortBy`
(`expenseDate`, `expenseNumber`, `createdAt`, `amount`, `submittedAt`, `approvedAt`,
`reimbursedAt`, `rejectedAt`), and `sortDirection` (`asc`, `desc`), passing the user's selection
as query parameters to `GET /api/expenses` (`docs/SDS.md` §5.2).

#### Scenario: Changing the page size refetches with the new value
- **WHEN** a user selects a different page size from the control
- **THEN** the frontend SHALL call `GET /api/expenses` with the new `pageSize` and display the
  refreshed results

#### Scenario: Changing the sort field refetches with the new value
- **WHEN** a user selects a different `sortBy` field or toggles `sortDirection`
- **THEN** the frontend SHALL call `GET /api/expenses` with the updated `sortBy`/`sortDirection`
  and display the refreshed, re-sorted results

### Requirement: Status Filter
The list screen SHALL provide a status filter control offering the seven `ExpenseStatus` values,
passing the selection as the `status` query parameter to `GET /api/expenses`
(`docs/SDS.md` §5.2, `expense-visibility` capability's optional status filter).

#### Scenario: Selecting a status filters the list via the backend
- **WHEN** a user selects a status (e.g. `Submitted`) from the status filter
- **THEN** the frontend SHALL call `GET /api/expenses?status=Submitted` and display only the
  returned matching expenses

#### Scenario: Clearing the status filter restores the unfiltered list
- **WHEN** a user clears the status filter after having selected one
- **THEN** the frontend SHALL call `GET /api/expenses` without a `status` parameter

### Requirement: Category and Date-Range Filters
The list screen SHALL provide category and expense-date-range filter controls that narrow the
currently loaded page of results client-side (the backend list endpoint does not accept category
or date-range query parameters). These filters SHALL be clearly presented as narrowing only the
current page, not the full server-side result set.

#### Scenario: Category filter narrows the current page
- **WHEN** a user selects a category (e.g. `Travel`) from the category filter
- **THEN** the frontend SHALL display only the currently loaded page's items matching that
  category, without issuing a new request with a category parameter

#### Scenario: Date-range filter narrows the current page
- **WHEN** a user sets a from/to expense-date range
- **THEN** the frontend SHALL display only the currently loaded page's items whose `expenseDate`
  falls within that range

#### Scenario: Combined filters narrow within the current page
- **WHEN** a user has both a category filter and a date range set
- **THEN** the frontend SHALL display only items in the current page matching both conditions

### Requirement: Expense Detail Screen
The frontend SHALL provide an expense detail route that calls `GET /api/expenses/{id}` and
renders the full expense (`docs/FRS.md` §4.4, `docs/SDS.md` §5.2), including a `404` state when
the backend returns `RESOURCE_NOT_FOUND` and a `403` state when the backend returns
`AUTHORIZATION_FAILED`.

#### Scenario: Authorized viewer sees full expense detail
- **WHEN** an authenticated user whom the backend permits to view a given expense navigates to
  its detail route
- **THEN** the frontend SHALL render the expense's full data as returned by
  `GET /api/expenses/{id}`

#### Scenario: Nonexistent expense shows a not-found state
- **WHEN** `GET /api/expenses/{id}` responds `404 RESOURCE_NOT_FOUND`
- **THEN** the frontend SHALL render a not-found state instead of an empty or broken detail view

#### Scenario: Unauthorized viewer shows an access-denied state
- **WHEN** `GET /api/expenses/{id}` responds `403 AUTHORIZATION_FAILED`
- **THEN** the frontend SHALL render an access-denied state instead of attempting to display
  partial expense data

### Requirement: Action Visibility on Detail Reflects Ownership and Status
The detail screen SHALL show Edit/Submit/Cancel actions only when the currently authenticated
user is the expense's owner and its `Status` makes that action valid (per
`frontend-expense-submission-ui` and `frontend-expense-maintenance-ui`); a non-owner viewing an
expense they are permitted to see (e.g. a Manager viewing a direct report's expense) SHALL see a
read-only detail view with no workflow action controls, since approve/reject actions belong to a
separate capability (ET018).

#### Scenario: Owner sees applicable actions
- **WHEN** the owner of a `Draft` expense views its detail
- **THEN** the frontend SHALL show the Edit, Submit, and Cancel actions

#### Scenario: Non-owner sees a read-only view
- **WHEN** a Manager views a direct report's `Submitted` expense detail
- **THEN** the frontend SHALL NOT show any Edit, Submit, or Cancel action, and SHALL NOT show any
  approve/reject action
