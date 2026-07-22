# frontend-expense-maintenance-ui Specification

## Purpose
TBD - created by archiving change et017-expense-ui. Update Purpose after archive.
## Requirements
### Requirement: Expense Edit Form
The frontend SHALL provide an expense edit route, restricted to the expense's owner, that
pre-fills a form (the same field set and Zod schema as expense creation) with the expense's
current `expenseDate`, `category`, `amount`, `currency`, `description`, and receipt attachment,
and on submission calls `PUT /api/expenses/{id}` with the edited values (`docs/FRS.md` §4.2,
`docs/SDS.md` §5.2). The edit form SHALL NOT expose any control that sets `Status` directly —
status changes remain reachable only through the dedicated Submit/Cancel actions
(`docs/SDS.md` §1.3, `AGENTS.md` §11).

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

### Requirement: Edit Access Is Limited to Draft and Submitted Expenses
The frontend SHALL only offer the edit action (**BR-04**, `docs/FRS.md` §4.2.2) when the expense's
`Status` is `Draft` or `Submitted`, and SHALL NOT show it for `Approved`, `ComplianceApproved`,
`Rejected` (**BR-07** — read-only), `Cancelled`, or `Reimbursed` expenses (`docs/SDS.md` §6.3).

#### Scenario: Edit action shown for a Draft expense
- **WHEN** an owner views their own `Draft` expense's detail
- **THEN** the "Edit" action SHALL be shown

#### Scenario: Edit action shown for a Submitted expense
- **WHEN** an owner views their own `Submitted` expense's detail
- **THEN** the "Edit" action SHALL be shown

#### Scenario: Edit action hidden for non-editable statuses
- **WHEN** an owner views their own expense whose `Status` is `Approved`, `ComplianceApproved`,
  `Rejected`, `Cancelled`, or `Reimbursed`
- **THEN** the "Edit" action SHALL NOT be shown, individually, for each of those statuses

#### Scenario: Backend rejection of a stale edit attempt is surfaced
- **WHEN** `PUT /api/expenses/{id}` responds with `422 BUSINESS_RULE_VIOLATION` because the
  expense transitioned to a non-editable status after the edit form was opened
- **THEN** the frontend SHALL display the error and SHALL NOT show the edit as successful

### Requirement: Attachment Replacement on Edit
The edit form SHALL allow the receipt attachment to be replaced using the same attachment picker
component used at creation (`frontend-attachment-upload-ui`), pre-populated with the expense's
current attachment, and SHALL pass the resulting `receiptAttachmentId` (new or unchanged) to
`PUT /api/expenses/{id}` (`docs/FRS.md` §4.1.1, `docs/SDS.md` §5.2).

#### Scenario: Existing attachment is shown by default
- **WHEN** the edit form loads for an expense with an existing receipt attachment
- **THEN** the picker SHALL display the current attachment's file name without requiring the
  user to re-select it

#### Scenario: Replacing the attachment updates the request payload
- **WHEN** the user selects a new file in the edit form's attachment picker and submits
- **THEN** the frontend SHALL upload the new file and include the newly returned
  `attachmentId` as `receiptAttachmentId` in the `PUT /api/expenses/{id}` request

#### Scenario: Leaving the attachment unchanged resubmits the same id
- **WHEN** the user submits the edit form without selecting a new file
- **THEN** the frontend SHALL include the expense's existing `receiptAttachmentId` unchanged in
  the `PUT /api/expenses/{id}` request

### Requirement: Cancel Action
The frontend SHALL provide a "Cancel" action, visible only to the expense's owner and only when
`Status` is `Draft` or `Submitted` (**BR-05**, `docs/FRS.md` §4.3), that calls
`POST /api/expenses/{id}/cancel` after an explicit user confirmation step (`docs/SDS.md` §5.2).
On success the view SHALL refetch and display the expense with `Status: Cancelled`.

#### Scenario: Owner cancels their own Draft expense
- **WHEN** the owner of a `Draft` expense confirms the "Cancel" action
- **THEN** the frontend SHALL call `POST /api/expenses/{id}/cancel`, and on success SHALL
  refetch and display the expense with `Status: Cancelled`

#### Scenario: Owner cancels their own Submitted expense
- **WHEN** the owner of a `Submitted` expense confirms the "Cancel" action
- **THEN** the frontend SHALL call `POST /api/expenses/{id}/cancel`, and on success SHALL
  refetch and display the expense with `Status: Cancelled`

#### Scenario: Cancel requires explicit confirmation
- **WHEN** the owner clicks "Cancel" on an eligible expense
- **THEN** the frontend SHALL require an explicit confirmation step before calling
  `POST /api/expenses/{id}/cancel`

#### Scenario: Cancel action hidden for non-cancellable statuses
- **WHEN** the detail view renders an expense whose `Status` is `Approved`, `ComplianceApproved`,
  `Rejected`, `Reimbursed`, or already `Cancelled`
- **THEN** the "Cancel" action SHALL NOT be shown, individually, for each of those statuses

