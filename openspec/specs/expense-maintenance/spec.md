# expense-maintenance Specification

## Purpose
TBD - created by archiving change et008-expense-management. Update Purpose after archive.
## Requirements
### Requirement: Single Expense Retrieval Endpoint
The `Api` layer SHALL expose `GET /api/expenses/{id}`, returning the expense mapped to
`ExpenseResponse` (`docs/FRS.md` §4.4, `docs/SDS.md` §5.2) when the authenticated caller
is permitted to view it under the same per-role default visibility rules as
`GET /api/expenses` (the `expense-visibility` capability):
- **Employee** — only their own expense, any `Status`.
- **Manager** — their own expense (any `Status`), or a direct report's expense whose
  `Status` is not `Draft`.
- **Finance** — any expense whose `Status` is not `Draft`.
- **ComplianceOfficer** — a `ClientEntertainment`-category expense whose `Status` is
  `Approved` or `ComplianceApproved`.

A caller requesting an expense that exists but falls outside their visible set
receives `403 AUTHORIZATION_FAILED`, matching ET008's established non-owner-caller
precedent (confirmed with the ticket owner during `/spec` as an intentional
anti-enumeration tradeoff, not a defect).

#### Scenario: Owner retrieves their own expense
- **WHEN** the owning employee GETs `/api/expenses/{id}` for their own expense
- **THEN** the response is `200` with the expense's current data

#### Scenario: Non-owner is rejected
- **WHEN** an `Employee`-role caller who does not own the expense, and is not its
  owner's manager/Finance/an eligible Compliance reviewer, GETs `/api/expenses/{id}`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED` (unchanged from
  ET008 — this is the Employee-role instance of the broader per-role visibility rule
  below; the Manager/Finance/Compliance instances of the same rule are covered by the
  scenarios that follow)

#### Scenario: Manager retrieves their own expense
- **WHEN** a `Manager` GETs `/api/expenses/{id}` for an expense they own, regardless of
  its `Status`
- **THEN** the response is `200` with the expense's current data

#### Scenario: Manager retrieves a direct report's non-Draft expense
- **WHEN** a `Manager` GETs `/api/expenses/{id}` for a direct report's expense whose
  `Status` is not `Draft`
- **THEN** the response is `200` with the expense's current data

#### Scenario: Manager is rejected for a direct report's Draft expense
- **WHEN** a `Manager` GETs `/api/expenses/{id}` for a direct report's `Draft` expense
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Manager is rejected for an unrelated employee's expense
- **WHEN** a `Manager` GETs `/api/expenses/{id}` for an expense belonging to an
  employee who is neither them nor their direct report
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Finance retrieves any non-Draft expense
- **WHEN** a `Finance` caller GETs `/api/expenses/{id}` for any employee's expense
  whose `Status` is not `Draft`
- **THEN** the response is `200` with the expense's current data

#### Scenario: Finance is rejected for a Draft expense
- **WHEN** a `Finance` caller GETs `/api/expenses/{id}` for an expense whose `Status`
  is `Draft`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Compliance retrieves an Approved or Compliance Approved Client Entertainment expense
- **WHEN** a `ComplianceOfficer` GETs `/api/expenses/{id}` for a `ClientEntertainment`
  expense whose `Status` is `Approved` or `ComplianceApproved`
- **THEN** the response is `200` with the expense's current data

#### Scenario: Compliance is rejected for a Reimbursed Client Entertainment expense
- **WHEN** a `ComplianceOfficer` GETs `/api/expenses/{id}` for a `ClientEntertainment`
  expense whose `Status` is `Reimbursed`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Compliance is rejected for a non-Client-Entertainment expense
- **WHEN** a `ComplianceOfficer` GETs `/api/expenses/{id}` for an expense in any other
  category
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Nonexistent expense is rejected
- **WHEN** `GET /api/expenses/{id}` targets an id with no matching `Expense` row
- **THEN** the response is `404` with code `RESOURCE_NOT_FOUND`

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `GET /api/expenses/{id}` carries no valid `Authorization: Bearer`
  token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

### Requirement: Expense Edit Endpoint
The `Api` layer SHALL expose `PUT /api/expenses/{id}`, restricted to the expense's owning
employee, allowing `expenseDate`, `category`, `amount`, `currency`, `description`, and
`receiptAttachmentId` to be updated while the expense's `Status` is `Draft` or `Submitted`
(**BR-04**, `docs/FRS.md` §4.2.1–4.2.2). The endpoint SHALL NOT accept or change `Status`
directly — status transitions remain exclusively reachable via their own dedicated action
endpoints (`docs/SDS.md` §1.3).

#### Scenario: Owner edits their own Draft
- **WHEN** the owning employee PUTs valid field values to `/api/expenses/{id}` for their own
  `Draft` expense
- **THEN** the response is `200`, the expense's fields reflect the new values, and `Status`
  remains `Draft`

#### Scenario: Owner edits their own Submitted expense
- **WHEN** the owning employee PUTs valid field values to `/api/expenses/{id}` for their own
  `Submitted` expense
- **THEN** the response is `200`, the expense's fields reflect the new values, and `Status`
  remains `Submitted`

#### Scenario: Non-owner is rejected
- **WHEN** an authenticated employee who does not own the expense PUTs to
  `/api/expenses/{id}`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`, and the expense is
  unchanged

#### Scenario: Nonexistent expense is rejected
- **WHEN** `PUT /api/expenses/{id}` targets an id with no matching `Expense` row
- **THEN** the response is `404` with code `RESOURCE_NOT_FOUND`

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `PUT /api/expenses/{id}` carries no valid `Authorization: Bearer`
  token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

### Requirement: Editing Never Alters Workflow Status or Audit Fields
A successful `PUT /api/expenses/{id}` SHALL update only the editable fields and
`UpdatedAt`; it SHALL NOT change `Status` or any workflow audit field (`SubmittedAt`,
`ApprovedAt`, `ComplianceApprovedAt`, `RejectedAt`, `ReimbursedAt`, or their
`...ByEmployeeId` counterparts), matching `docs/SDS.md` §6.3's state transition matrix,
which defines no status transition triggered by editing.

#### Scenario: Editing a Submitted expense leaves SubmittedAt unchanged
- **WHEN** a `Submitted` expense with an existing `SubmittedAt` timestamp is successfully
  edited
- **THEN** `Status` remains `Submitted`, `SubmittedAt` is unchanged, and only `UpdatedAt`
  advances

#### Scenario: Client cannot set Status or audit fields via edit
- **WHEN** a `PUT /api/expenses/{id}` request body includes `status`, `submittedAt`,
  `approvedAt`, `rejectedAt`, `reimbursedAt`, or their `...ByEmployeeId` counterparts
- **THEN** those values are ignored and have no effect on the stored expense

### Requirement: Non-Editable Expense States Are Rejected on Edit
`PUT /api/expenses/{id}` SHALL reject, with `422 BUSINESS_RULE_VIOLATION` and no changes
persisted, an edit targeting an expense whose `Status` is anything other than `Draft` or
`Submitted` (**BR-04**). This includes, individually, `Approved`, `ComplianceApproved`,
`Rejected` (**BR-07**, `docs/FRS.md` §4.4.5 — read-only is one instance of this broader
rule), `Cancelled`, and `Reimbursed`.

#### Scenario: Editing an Approved expense is rejected
- **WHEN** `PUT /api/expenses/{id}` targets an expense whose `Status` is `Approved`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and the expense is
  unchanged

#### Scenario: Editing a Compliance Approved expense is rejected
- **WHEN** `PUT /api/expenses/{id}` targets an expense whose `Status` is
  `ComplianceApproved`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and the expense is
  unchanged

#### Scenario: Editing a Rejected expense is rejected
- **WHEN** `PUT /api/expenses/{id}` targets an expense whose `Status` is `Rejected`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and the expense is
  unchanged

#### Scenario: Editing a Cancelled expense is rejected
- **WHEN** `PUT /api/expenses/{id}` targets an expense whose `Status` is `Cancelled`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and the expense is
  unchanged

#### Scenario: Editing a Reimbursed expense is rejected
- **WHEN** `PUT /api/expenses/{id}` targets an expense whose `Status` is `Reimbursed`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and the expense is
  unchanged

### Requirement: Edit Field-Level and Business-Rule Validation
`PUT /api/expenses/{id}` SHALL re-run, identically to expense creation (`docs/FRS.md`
§4.1.1), every field-level check (`category` must be one of the seven `ExpenseCategory`
values, `currency` must be exactly `INR`, `description` must not exceed 500 characters) and
every business-rule check (`amount` greater than zero — **BR-01**; `expenseDate` not after
the current company-local date — **BR-02**, **BR-10**) against the edited values, rejecting
any violation and persisting no change.

#### Scenario: Invalid category is rejected
- **WHEN** an edit request's `category` is not one of the seven defined `ExpenseCategory`
  values
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for
  `category`

#### Scenario: Non-INR currency is rejected
- **WHEN** an edit request's `currency` is anything other than `INR`
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for
  `currency`

#### Scenario: Description over 500 characters is rejected
- **WHEN** an edit request's `description` exceeds 500 characters
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for
  `description`

#### Scenario: Zero or negative amount is rejected
- **WHEN** an edit request has `amount <= 0`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and no change is
  persisted

#### Scenario: Future expense date is rejected
- **WHEN** an edit request has `expenseDate` after the current company-local date
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and no change is
  persisted

### Requirement: Attachment Replacement on Edit
`PUT /api/expenses/{id}` SHALL allow `receiptAttachmentId` to be replaced with a different
`Attachment`, re-running the same ownership and linkage checks expense creation uses
(**BR-03**, `docs/FRS.md` §4.1.1): the referenced `Attachment` SHALL exist, SHALL NOT
already be linked to a *different* `Expense`, and SHALL have been uploaded by the caller. On
a successful replacement, the previously linked `Attachment` becomes unlinked (retained in
storage, per `docs/SDS.md` §3.7's allowance for attachments existing without an associated
expense) rather than deleted.

#### Scenario: Replacing the attachment with a valid new one succeeds
- **WHEN** the owner edits an expense with a `receiptAttachmentId` for a different
  attachment they uploaded, not linked to any other expense
- **THEN** the response is `200`, the expense now references the new attachment, and the
  previously linked attachment remains in storage but is no longer referenced by this
  expense

#### Scenario: Resubmitting the same attachment succeeds
- **WHEN** the owner edits an expense supplying the same `receiptAttachmentId` the expense
  already references
- **THEN** the response is `200` and the edit succeeds without being rejected as
  "already linked"

#### Scenario: Replacement attachment already linked to a different expense is rejected
- **WHEN** an edit request's `receiptAttachmentId` references an `Attachment` already linked
  to a different `Expense`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and no change is
  persisted

#### Scenario: Replacement attachment not owned by the caller is rejected
- **WHEN** an edit request's `receiptAttachmentId` references an `Attachment` whose
  `UploadedByEmployeeId` does not match the authenticated caller's employee ID
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and no change is
  persisted

#### Scenario: Nonexistent replacement attachment is rejected
- **WHEN** an edit request's `receiptAttachmentId` does not reference any existing
  `Attachment`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and no change is
  persisted

### Requirement: Expense Cancellation Endpoint
The `Api` layer SHALL expose `POST /api/expenses/{id}/cancel`, restricted to the expense's
owning employee, transitioning a `Draft` or `Submitted` expense to `Cancelled`
(**BR-05**). Extending cancellation to `Draft` expenses (not only `Submitted`, as the literal
text of `docs/FRS.md` §4.3.1 and `docs/SDS.md` §6.3 states) is a deliberate deviation
confirmed with the ticket owner during `/spec` and logged as an ADR in
`docs/decisions/` per this repository's OpenSpec proposal rules.

#### Scenario: Owner cancels their own Draft
- **WHEN** the owning employee POSTs to `/api/expenses/{id}/cancel` for their own `Draft`
  expense
- **THEN** the response is `200` and the expense's `Status` becomes `Cancelled`

#### Scenario: Owner cancels their own Submitted expense
- **WHEN** the owning employee POSTs to `/api/expenses/{id}/cancel` for their own
  `Submitted` expense
- **THEN** the response is `200` and the expense's `Status` becomes `Cancelled`

#### Scenario: Non-owner is rejected
- **WHEN** an authenticated employee who does not own the expense POSTs to
  `/api/expenses/{id}/cancel`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`, and the expense's
  `Status` is unchanged

#### Scenario: Nonexistent expense is rejected
- **WHEN** `POST /api/expenses/{id}/cancel` targets an id with no matching `Expense` row
- **THEN** the response is `404` with code `RESOURCE_NOT_FOUND`

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `POST /api/expenses/{id}/cancel` carries no valid
  `Authorization: Bearer` token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

### Requirement: Non-Cancellable Expense States Are Rejected on Cancel
`POST /api/expenses/{id}/cancel` SHALL reject, with `422 BUSINESS_RULE_VIOLATION` and the
`Status` left unchanged, a cancellation targeting an expense whose `Status` is anything
other than `Draft` or `Submitted`. This includes, individually, `Approved`,
`ComplianceApproved`, `Rejected`, `Reimbursed`, and an already-`Cancelled` expense (no
backward transition, no re-cancellation, `docs/FRS.md` §7.2.4).

#### Scenario: Cancelling an Approved expense is rejected
- **WHEN** `POST /api/expenses/{id}/cancel` targets an expense whose `Status` is `Approved`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

#### Scenario: Cancelling a Compliance Approved expense is rejected
- **WHEN** `POST /api/expenses/{id}/cancel` targets an expense whose `Status` is
  `ComplianceApproved`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

#### Scenario: Cancelling a Rejected expense is rejected
- **WHEN** `POST /api/expenses/{id}/cancel` targets an expense whose `Status` is `Rejected`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

#### Scenario: Cancelling a Reimbursed expense is rejected
- **WHEN** `POST /api/expenses/{id}/cancel` targets an expense whose `Status` is
  `Reimbursed`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

#### Scenario: Cancelling an already-Cancelled expense is rejected
- **WHEN** `POST /api/expenses/{id}/cancel` targets an expense whose `Status` is already
  `Cancelled`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

