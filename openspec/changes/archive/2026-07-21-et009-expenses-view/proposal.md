## Why

ET008 deliberately scoped `GET /api/expenses/{id}` to the owning employee only, deferring
"broader role-based visibility (Manager team view, Finance view, Compliance Client
Entertainment view, list/search/pagination)" to this ticket (`docs/FRS.md` §4.4,
`docs/SDS.md` §5.2). Right now there is no way for a Manager, Finance, or Compliance
Officer to see any expense at all — the workflow-review tickets (ET010–ET012) cannot be
built without this. ET009 (`docs/TICKETS.md`) closes that gap: it adds the paginated,
sorted `GET /api/expenses` list endpoint with per-role default visibility, and widens
`GET /api/expenses/{id}` to match the same visibility rules.

## What Changes

- Add `GET /api/expenses` — server-side paginated (`page`/`pageSize`), sorted
  (`sortBy`/`sortDirection`) list of expenses visible to the caller, with default
  visibility determined entirely by the caller's role (`docs/FRS.md` §4.4.1–4.4.4,
  `docs/SDS.md` §5.2):
  - **Employee** — only expenses they own, any status (4.4.1).
  - **Manager** — their own expenses (any status) merged with their **direct reports'**
    non-`Draft` expenses (4.4.2). Direct reports only (`Employee.ManagerId`/
    `DirectReports` is a direct, non-recursive relationship) — confirmed with the ticket
    owner during `/spec`, no recursive hierarchy traversal.
  - **Finance** — all expenses except `Draft` (4.4.3).
  - **Compliance Officer** — `ClientEntertainment`-category expenses whose `Status` is
    `Approved` or `ComplianceApproved` (4.4.4). This extends past the literal FRS text
    ("can only view `Approved` expenses"), confirmed with the ticket owner during
    `/spec`: Compliance retains visibility into an expense after they act on it
    (`ComplianceApproved`), but not once Finance reimburses it (`Reimbursed` is
    excluded). No ADR is raised for this — the ticket owner explicitly waived that
    requirement for this specific, narrow scope decision; it is recorded here and will
    be restated in `design.md`.
- **MODIFIED**: `GET /api/expenses/{id}` (`expense-maintenance` capability, currently
  owner-only per ET008) — extend visibility to the same per-role rules as the new list
  endpoint above, confirmed with the ticket owner during `/spec`. An authenticated
  caller requesting an expense outside their visible set (exists, but not theirs to see)
  continues to receive `403 AUTHORIZATION_FAILED`, matching ET008's existing
  non-owner-caller precedent (an intentional anti-enumeration tradeoff, not a defect).
- Query parameters: `page` (default 1), `pageSize` (default **20**, allowed
  10/20/50/100), `sortBy` (`expenseDate` default; `expenseNumber`, `createdAt`,
  `amount`, `submittedAt`, `approvedAt`, `reimbursedAt`, `rejectedAt`), `sortDirection`
  (`desc` default, `asc`). The `pageSize` default is confirmed as **20** per the ticket
  owner during `/spec` — `docs/SDS.md` §5.2's query-parameter table states a default of
  10, conflicting with its own worked JSON example showing `pageSize: 20`; this is a
  documentation inconsistency in the SDS to flag, not a design deviation, since the SDS
  never designates one of the two as authoritative.
- `EmployeeName` (First + Last) is added to `ExpenseResponse` for list/detail display,
  since Manager/Finance/Compliance views span multiple employees.

## Capabilities

### New Capabilities
- `expense-visibility`: Role-based default visibility rules (which expenses a caller
  may see) shared by both the list endpoint and the widened single-expense endpoint,
  plus the `GET /api/expenses` list endpoint itself (pagination, sorting).

### Modified Capabilities
- `expense-maintenance`: The "Single Expense Retrieval Endpoint" requirement changes
  from owner-only to the shared per-role visibility rules defined by the new
  `expense-visibility` capability.

## Impact

- **Api**: `ExpensesController` gains `GET /api/expenses`; `GetById` action's
  authorization check changes from a straight ownership comparison to a shared
  visibility check.
- **Application**: `IExpenseService`/`ExpenseService` gain a `GetVisibleAsync` (list)
  method and a visibility predicate reused by `GetByIdAsync`; `ExpenseResponse` gains
  `EmployeeName`. New `ExpenseQueryRequest`/paged response DTOs.
- **Domain**: `IExpenseRepository` gains a query method for role-scoped, paged, sorted
  retrieval (`IQueryable` composition — no in-memory filtering per `backend/CLAUDE.md`).
  No schema changes; `Employee.ManagerId`/`DirectReports` (already modeled) is read, not
  altered.
- **Infrastructure**: `ExpenseRepository` implements the new query method against the
  existing `Expenses`/`Employees` tables.
- No frontend, auth, or database schema impact. No dependency changes.
