# frontend-expense-submission-ui Specification

## Purpose
TBD - created by archiving change et017-expense-ui. Update Purpose after archive.
## Requirements
### Requirement: Expense Creation Form
The frontend SHALL provide an expense creation route rendering a form (React Hook Form + Zod)
that collects `expenseDate`, `category`, `amount`, `currency` (fixed to `INR`, not user-editable —
`docs/FRS.md` §4.1.1), `description`, and a receipt attachment, restricted to authenticated users
whose role is `Employee` or `Manager` (`docs/SDS.md` §5.2, §6.4). On submission the frontend
SHALL call `POST /api/expenses` with the collected fields and an `action` of `Draft` or `Submit`
per the user's chosen action (`docs/FRS.md` §4.1.2).

#### Scenario: Employee or Manager can reach the creation form
- **WHEN** an authenticated user whose role is `Employee` or `Manager` navigates to the expense
  creation route
- **THEN** the frontend SHALL render the creation form

#### Scenario: Currency is fixed and not editable
- **WHEN** the creation form renders
- **THEN** the `currency` field SHALL display `INR` and SHALL NOT be editable by the user

#### Scenario: Valid submission calls the create endpoint with the chosen action
- **WHEN** a user fills all fields validly, attaches a receipt, and submits with either the
  "Save as Draft" or "Submit" action
- **THEN** the frontend SHALL call `POST /api/expenses` with `action` set to `Draft` or `Submit`
  respectively, and the exact field values entered

### Requirement: Draft and Submit Action Selection
The creation form SHALL offer two distinct submission actions — "Save as Draft" and "Submit" —
mapping to the backend's `action: "Draft"` and `action: "Submit"` values (`docs/FRS.md` §4.1.2).
Both actions SHALL run the same client-side field validation before the request is sent; neither
action bypasses the other's checks.

#### Scenario: Save as Draft succeeds with valid data
- **WHEN** a user completes all mandatory fields validly and selects "Save as Draft"
- **THEN** the frontend calls `POST /api/expenses` with `action: "Draft"`, and on success
  navigates to the new expense's detail view showing `Status: Draft`

#### Scenario: Submit succeeds with valid data
- **WHEN** a user completes all mandatory fields validly and selects "Submit"
- **THEN** the frontend calls `POST /api/expenses` with `action: "Submit"`, and on success
  navigates to the new expense's detail view showing `Status: Submitted`

#### Scenario: Draft and Submit share the same client-side validation
- **WHEN** a mandatory field is invalid or missing
- **THEN** the frontend SHALL block submission and display a field-level error regardless of
  whether the user selected "Save as Draft" or "Submit"

### Requirement: Client-Side Field Validation Mirrors Backend Rules for UX Only
The creation form's Zod schema SHALL validate, before submission, that `category` is one of the
seven `ExpenseCategory` values, `amount` is greater than zero (**BR-01**), `expenseDate` is not
after the current date (**BR-02**), and `description` does not exceed 500 characters
(`docs/FRS.md` §4.1.1, §11 Business Rules Glossary). These checks are UX-only; the frontend SHALL
treat the backend's response as authoritative and SHALL NOT skip calling the API when client
validation passes, nor suppress a backend rejection that client validation missed
(`docs/SDS.md` §1.3, `AGENTS.md` §11).

#### Scenario: Non-positive amount is blocked client-side
- **WHEN** a user enters an `amount` of zero or less and attempts to submit
- **THEN** the frontend SHALL display a field-level error and SHALL NOT call
  `POST /api/expenses`

#### Scenario: Future expense date is blocked client-side
- **WHEN** a user selects an `expenseDate` after the current date and attempts to submit
- **THEN** the frontend SHALL display a field-level error and SHALL NOT call
  `POST /api/expenses`

#### Scenario: Description over 500 characters is blocked client-side
- **WHEN** a user enters a `description` longer than 500 characters and attempts to submit
- **THEN** the frontend SHALL display a field-level error and SHALL NOT call
  `POST /api/expenses`

#### Scenario: Backend rejection is still surfaced even if client validation passed
- **WHEN** the form's client-side validation passes but `POST /api/expenses` responds with
  `400 VALIDATION_ERROR` or `422 BUSINESS_RULE_VIOLATION`
- **THEN** the frontend SHALL display the backend's field-level error(s) on the form and SHALL
  NOT treat the submission as successful

### Requirement: Receipt Attachment Is Mandatory on Create
The creation form SHALL require a receipt attachment before allowing either "Save as Draft" or
"Submit" (**BR-03**, `docs/FRS.md` §4.1.1) — using the shared attachment picker component from
the `frontend-attachment-upload-ui` capability.

#### Scenario: Submission is blocked with no attachment selected
- **WHEN** a user completes all other fields validly but selects no receipt file and attempts to
  submit (either action)
- **THEN** the frontend SHALL display a field-level error for the attachment and SHALL NOT call
  `POST /api/expenses`

### Requirement: Submit Existing Draft Action
The frontend SHALL provide a "Submit" action on a `Draft` expense's detail view, visible only to
the expense's owner, that calls `POST /api/expenses/{id}/submit` (`docs/FRS.md` §4.1.2,
`docs/SDS.md` §5.2). On success the view SHALL reflect the expense's new `Submitted` status by
refetching the expense rather than optimistically updating it.

#### Scenario: Owner submits their own Draft from the detail view
- **WHEN** the owner of a `Draft` expense clicks "Submit" on its detail view
- **THEN** the frontend SHALL call `POST /api/expenses/{id}/submit`, and on success SHALL
  refetch and display the expense with `Status: Submitted`

#### Scenario: Submit action is hidden for non-Draft expenses
- **WHEN** the detail view renders an expense whose `Status` is not `Draft`
- **THEN** the "Submit" action SHALL NOT be shown

#### Scenario: Backend rejection on submit is surfaced
- **WHEN** `POST /api/expenses/{id}/submit` responds with `422 BUSINESS_RULE_VIOLATION` (e.g. the
  Draft's stored data no longer passes validation)
- **THEN** the frontend SHALL display the error and SHALL NOT change the displayed status

