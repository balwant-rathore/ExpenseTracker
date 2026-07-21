## ADDED Requirements

### Requirement: Optional Status Filter on the Expense List Endpoint
`GET /api/expenses` SHALL accept an optional `status` query parameter (one of the seven
`ExpenseStatus` values) that, when present, narrows the caller's already-visible set
(per their role's default visibility rules) to only expenses matching that `Status`.
This supports `docs/FRS.md` §5.1.1 — a Manager viewing only `Submitted` expenses awaiting
their review — without changing any role's underlying visibility rules. The filter is an
open decision beyond ET009's literal scope, confirmed with the ticket owner during
`/spec`.

#### Scenario: Manager filters to only Submitted expenses
- **WHEN** a `Manager` GETs `/api/expenses?status=Submitted`
- **THEN** the response's `items` contains only expenses with `Status = Submitted` from
  among the expenses that manager can already see (their own plus direct reports')

#### Scenario: Status filter never expands visibility
- **WHEN** an `Employee` GETs `/api/expenses?status=Approved`
- **THEN** the response's `items` contains only that employee's own `Approved` expenses
  — the filter narrows, but never expands, the caller's default visibility set

#### Scenario: No status filter behaves as before
- **WHEN** an authenticated caller GETs `/api/expenses` with no `status` query parameter
- **THEN** the response includes expenses at every `Status` value the caller's role is
  permitted to see, unchanged from existing behavior

#### Scenario: Invalid status value is rejected
- **WHEN** a caller GETs `/api/expenses?status=NotARealStatus`
- **THEN** the response is `400` with code `VALIDATION_ERROR`
