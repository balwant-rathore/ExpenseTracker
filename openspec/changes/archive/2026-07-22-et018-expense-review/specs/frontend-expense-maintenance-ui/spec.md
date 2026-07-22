## MODIFIED Requirements

### Requirement: Expense Edit Form
The frontend SHALL provide an expense edit route, restricted to the expense's owner, that
pre-fills a form (the same field set and Zod schema as expense creation) with the expense's
current `expenseDate`, `category`, `amount`, `currency`, `description`, and receipt attachment,
and on submission calls `PUT /api/expenses/{id}` with the edited values (`docs/FRS.md` §4.2,
`docs/SDS.md` §5.2). The edit form SHALL NOT expose any control that sets `Status` directly —
status changes remain reachable only through the dedicated Submit/Cancel actions
(`docs/SDS.md` §1.3, `AGENTS.md` §11). Ownership SHALL be checked before the form renders — not
only via Edit-link visibility on the detail page — so a non-owner navigating directly to the edit
route (e.g. by URL) never sees the form rendered as editable; a non-owner SHALL instead be
redirected to the expense's detail route (`/expenses/{id}`).

#### Scenario: Owner opens the edit form pre-filled with current values
- **WHEN** the owner of an editable expense navigates to its edit route
- **THEN** the frontend SHALL render the form with all fields pre-filled from the expense's
  current data

#### Scenario: Valid edit submission calls the update endpoint
- **WHEN** the owner changes one or more fields validly and submits the edit form
- **THEN** the frontend SHALL call `PUT /api/expenses/{id}` with the edited field values, and on
  success SHALL refetch and display the updated expense

#### Scenario: Edit form has no status control
- **WHEN** the edit form renders
- **THEN** it SHALL NOT present any input, dropdown, or control that sets or implies a `Status`
  value directly

#### Scenario: Non-owner navigating directly to the edit route is redirected
- **WHEN** an authenticated user who is not the expense's owner (e.g. a Manager viewing a direct
  report's expense) navigates directly to `/expenses/{id}/edit`
- **THEN** the frontend SHALL redirect to `/expenses/{id}` without ever rendering the edit form
