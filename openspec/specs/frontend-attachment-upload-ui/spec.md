# frontend-attachment-upload-ui Specification

## Purpose
TBD - created by archiving change et017-expense-ui. Update Purpose after archive.
## Requirements
### Requirement: Shared Attachment Picker Component
The frontend SHALL provide a single reusable receipt-attachment picker component, used by both
the expense creation form (`frontend-expense-submission-ui`) and the expense edit form
(`frontend-expense-maintenance-ui`), that lets the user select a local file and holds it in form
state without uploading it immediately (`docs/FRS.md` §4.1.1, §4.1.3–4.1.4).

#### Scenario: Selecting a file holds it in form state without uploading
- **WHEN** a user selects a file in the attachment picker
- **THEN** the frontend SHALL store the file in the form's local state and SHALL NOT call
  `POST /api/attachments` at that moment

#### Scenario: Same component renders in both create and edit forms
- **WHEN** the creation form and the edit form each render the attachment picker
- **THEN** both SHALL use the same underlying component and validation logic

### Requirement: Client-Side File Type Validation
The attachment picker SHALL validate, immediately on file selection and before form submission,
that the selected file's extension is `.pdf`, `.jpg`, `.jpeg`, or `.png` (**BR-03**,
`docs/FRS.md` §4.1.3). This check is UX-only; the backend's `attachment-upload` capability
remains the authoritative validator.

#### Scenario: Disallowed file type is rejected client-side
- **WHEN** a user selects a file with an extension other than `.pdf`, `.jpg`, `.jpeg`, or `.png`
- **THEN** the frontend SHALL display a client-side error immediately and SHALL NOT hold that
  file as the pending selection

#### Scenario: Allowed file type is accepted client-side
- **WHEN** a user selects a `.pdf`, `.jpg`, `.jpeg`, or `.png` file
- **THEN** the frontend SHALL accept the file as the pending selection with no error shown

### Requirement: Client-Side File Size Validation
The attachment picker SHALL validate, immediately on file selection, that the selected file's
size does not exceed 10 MB (**BR-03**, `docs/FRS.md` §4.1.4). This check is UX-only; the
backend's `attachment-upload` capability remains the authoritative validator.

#### Scenario: Oversized file is rejected client-side
- **WHEN** a user selects a file larger than 10 MB
- **THEN** the frontend SHALL display a client-side error immediately and SHALL NOT hold that
  file as the pending selection

#### Scenario: File at or under the size limit is accepted client-side
- **WHEN** a user selects a file of 10 MB or less
- **THEN** the frontend SHALL accept the file as the pending selection with no size error shown

### Requirement: Upload Is Deferred Until the Expense Form Is Saved
The attachment picker SHALL only call `POST /api/attachments` when the containing expense form is
actually saved — as `Draft`, `Submit` (create), or an edit save — and SHALL use the returned
`attachmentId` as the `receiptAttachmentId` in that same save request (`docs/SDS.md` §5.3's note
that an uploaded `attachmentId` is supplied in the subsequent expense request).

#### Scenario: Upload fires on Draft save
- **WHEN** a user has selected a pending file and saves the creation form as "Draft"
- **THEN** the frontend SHALL call `POST /api/attachments` with the selected file before calling
  `POST /api/expenses`, and SHALL use the returned `attachmentId` as `receiptAttachmentId`

#### Scenario: Upload fires on Submit save
- **WHEN** a user has selected a pending file and saves the creation form as "Submit"
- **THEN** the frontend SHALL call `POST /api/attachments` with the selected file before calling
  `POST /api/expenses`, and SHALL use the returned `attachmentId` as `receiptAttachmentId`

#### Scenario: Upload fires on edit save when the attachment was replaced
- **WHEN** a user has selected a new pending file on the edit form and saves the edit
- **THEN** the frontend SHALL call `POST /api/attachments` with the new file before calling
  `PUT /api/expenses/{id}`, and SHALL use the returned `attachmentId` as `receiptAttachmentId`

#### Scenario: No upload call when the attachment is unchanged on edit
- **WHEN** a user saves the edit form without selecting a new file
- **THEN** the frontend SHALL NOT call `POST /api/attachments`, and SHALL reuse the expense's
  existing `receiptAttachmentId` in the `PUT /api/expenses/{id}` request

### Requirement: Upload Failure Blocks the Expense Save
If `POST /api/attachments` fails during a deferred upload-on-save, the frontend SHALL surface the
error and SHALL NOT proceed to call the expense create/edit endpoint with an invalid or missing
`receiptAttachmentId`.

#### Scenario: Attachment upload failure prevents expense creation
- **WHEN** a user saves the creation form and the deferred `POST /api/attachments` call fails
- **THEN** the frontend SHALL display the upload error and SHALL NOT call `POST /api/expenses`

#### Scenario: Attachment upload failure prevents expense edit
- **WHEN** a user saves the edit form with a new file and the deferred `POST /api/attachments`
  call fails
- **THEN** the frontend SHALL display the upload error and SHALL NOT call
  `PUT /api/expenses/{id}`

### Requirement: Existing Attachment Preview on Edit
When the attachment picker renders inside the edit form for an expense that already has a
receipt attachment, it SHALL display the existing attachment's original file name as the current
selection without requiring the user to pick a file again (`docs/SDS.md` §3.7).

#### Scenario: Edit form shows the current attachment's file name
- **WHEN** the edit form loads for an expense with an existing receipt attachment
- **THEN** the attachment picker SHALL display that attachment's original file name as the
  current selection

