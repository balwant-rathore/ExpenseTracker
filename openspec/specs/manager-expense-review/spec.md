# manager-expense-review Specification

## Purpose
TBD - created by archiving change et010-manager-review. Update Purpose after archive.
## Requirements
### Requirement: Manager Approval Endpoint
The `Api` layer SHALL expose `POST /api/expenses/{id}/approve`, restricted to the
`Manager` role (`docs/SDS.md` §5.2, §6.4), transitioning a `Submitted` expense to
`Approved` (`docs/FRS.md` §5.1.2). The caller SHALL be authorized either as the expense
owner's direct manager (`Employee.ManagerId` matches the caller) or, when the owner is
itself a Manager, as that owner's own `ManagerId` (skip-level escalation — see the
Skip-Level Escalation requirement below).

#### Scenario: Direct manager approves a report's Submitted expense
- **WHEN** a `Manager` POSTs to `/api/expenses/{id}/approve` for a `Submitted` expense
  owned by one of their direct reports
- **THEN** the response is `200` and the expense's `Status` becomes `Approved`

#### Scenario: Unrelated manager is rejected
- **WHEN** a `Manager` POSTs to `/api/expenses/{id}/approve` for a `Submitted` expense
  owned by an employee who is neither their direct report nor themselves
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`, and the expense's
  `Status` is unchanged

#### Scenario: Non-Manager role is rejected
- **WHEN** an authenticated caller whose resolved role is `Employee`, `Finance`, or
  `ComplianceOfficer` POSTs to `/api/expenses/{id}/approve`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Nonexistent expense is rejected
- **WHEN** `POST /api/expenses/{id}/approve` targets an id with no matching `Expense`
  row
- **THEN** the response is `404` with code `RESOURCE_NOT_FOUND`

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `POST /api/expenses/{id}/approve` carries no valid
  `Authorization: Bearer` token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

### Requirement: Manager Rejection Endpoint
The `Api` layer SHALL expose `POST /api/expenses/{id}/reject`, restricted to the
`Manager` role, accepting a `rejectionComment`, transitioning a `Submitted` expense to
`Rejected` (`docs/FRS.md` §5.1.3–5.1.4). Authorization mirrors the Approval Endpoint:
the expense owner's direct manager, or (for a Manager-owned expense) that owner's own
`ManagerId`.

#### Scenario: Direct manager rejects a report's Submitted expense with a comment
- **WHEN** a `Manager` POSTs to `/api/expenses/{id}/reject` with a valid
  `rejectionComment` for a `Submitted` expense owned by one of their direct reports
- **THEN** the response is `200` and the expense's `Status` becomes `Rejected`

#### Scenario: Unrelated manager is rejected
- **WHEN** a `Manager` POSTs to `/api/expenses/{id}/reject` for a `Submitted` expense
  owned by an employee who is neither their direct report nor themselves
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`, and the expense's
  `Status` is unchanged

#### Scenario: Non-Manager role is rejected
- **WHEN** an authenticated caller whose resolved role is `Employee`, `Finance`, or
  `ComplianceOfficer` POSTs to `/api/expenses/{id}/reject`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Nonexistent expense is rejected
- **WHEN** `POST /api/expenses/{id}/reject` targets an id with no matching `Expense` row
- **THEN** the response is `404` with code `RESOURCE_NOT_FOUND`

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `POST /api/expenses/{id}/reject` carries no valid
  `Authorization: Bearer` token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

### Requirement: Mandatory Rejection Comment Validation
`POST /api/expenses/{id}/reject` SHALL require `rejectionComment` to be present,
non-empty after trimming whitespace, and no more than 500 characters (`docs/FRS.md`
§5.1.4, mirroring the `Description` field's 500-character cap for consistency). A
request failing this check SHALL be rejected with `400 VALIDATION_ERROR` and a `fields`
entry for `rejectionComment`, with no change persisted.

#### Scenario: Missing rejectionComment is rejected
- **WHEN** a `POST /api/expenses/{id}/reject` request omits `rejectionComment` entirely
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for
  `rejectionComment`, and the expense's `Status` is unchanged

#### Scenario: Whitespace-only rejectionComment is rejected
- **WHEN** a `POST /api/expenses/{id}/reject` request's `rejectionComment` contains only
  whitespace characters
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for
  `rejectionComment`, and the expense's `Status` is unchanged

#### Scenario: Oversized rejectionComment is rejected
- **WHEN** a `POST /api/expenses/{id}/reject` request's `rejectionComment` exceeds 500
  characters
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for
  `rejectionComment`, and the expense's `Status` is unchanged

### Requirement: Manager Self-Review Restriction (BR-06)
A `Manager` SHALL NOT approve or reject their own expense (**BR-06**). This extends past
the literal `docs/FRS.md` §5.1.6 text, which names only Approve, to also block Reject —
confirmed with the ticket owner during `/spec` and matching `AGENTS.md` §11's "Managers
cannot approve/reject their own expenses (BR-06)."

#### Scenario: Manager cannot approve their own expense
- **WHEN** a `Manager` POSTs to `/api/expenses/{id}/approve` for their own `Submitted`
  expense
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`, and the expense's
  `Status` is unchanged

#### Scenario: Manager cannot reject their own expense
- **WHEN** a `Manager` POSTs to `/api/expenses/{id}/reject` (with an otherwise valid
  `rejectionComment`) for their own `Submitted` expense
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`, and the expense's
  `Status` is unchanged

### Requirement: Skip-Level Escalation for a Manager's Own Expense
When a `Submitted` expense's owner is itself a `Manager` (so BR-06 blocks that owner from
approving/rejecting their own expense), the owner's own `ManagerId` (their reporting
manager, i.e. skip-level relative to the expense owner's own direct reports) SHALL be
authorized to approve or reject that expense. This is an open decision beyond the literal
text of `docs/FRS.md`/`docs/SDS.md`, confirmed with the ticket owner during `/spec` and
logged as an ADR in `docs/decisions/`.

#### Scenario: Skip-level manager approves a manager-owned expense
- **WHEN** a `Manager` whose `EmployeeId` matches another Manager's `ManagerId` POSTs to
  `/api/expenses/{id}/approve` for that other Manager's own `Submitted` expense
- **THEN** the response is `200` and the expense's `Status` becomes `Approved`

#### Scenario: Skip-level manager rejects a manager-owned expense
- **WHEN** a `Manager` whose `EmployeeId` matches another Manager's `ManagerId` POSTs to
  `/api/expenses/{id}/reject` with a valid `rejectionComment` for that other Manager's own
  `Submitted` expense
- **THEN** the response is `200` and the expense's `Status` becomes `Rejected`

#### Scenario: A Manager with no ManagerId has no eligible approver
- **WHEN** a `Manager` whose `Employee.ManagerId` is null submits their own expense and
  no other caller attempts to approve/reject it
- **THEN** the expense remains `Submitted` indefinitely — this is a known, accepted gap
  for this ticket, not a defect

### Requirement: Non-Submitted Expenses Are Rejected on Approve or Reject
`POST /api/expenses/{id}/approve` and `POST /api/expenses/{id}/reject` SHALL reject, with
`422 BUSINESS_RULE_VIOLATION` and the `Status` left unchanged, a request targeting an
expense whose `Status` is anything other than `Submitted` (no backward transitions,
`docs/FRS.md` §7.2.4). This includes, individually, `Draft`, `Approved`,
`ComplianceApproved`, `Rejected`, `Cancelled`, and `Reimbursed`.

#### Scenario: Approving a Draft expense is rejected
- **WHEN** `POST /api/expenses/{id}/approve` targets an expense whose `Status` is `Draft`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

#### Scenario: Approving an already-Approved expense is rejected
- **WHEN** `POST /api/expenses/{id}/approve` targets an expense whose `Status` is
  already `Approved`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

#### Scenario: Approving a Cancelled expense is rejected
- **WHEN** `POST /api/expenses/{id}/approve` targets an expense whose `Status` is
  `Cancelled`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

#### Scenario: Rejecting a Draft expense is rejected
- **WHEN** `POST /api/expenses/{id}/reject` (with a valid `rejectionComment`) targets an
  expense whose `Status` is `Draft`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

#### Scenario: Rejecting a Reimbursed expense is rejected
- **WHEN** `POST /api/expenses/{id}/reject` (with a valid `rejectionComment`) targets an
  expense whose `Status` is `Reimbursed`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

### Requirement: Rejected Expenses Are Terminal
A `Rejected` expense SHALL NOT be transitioned to `Approved` by any later call to
`POST /api/expenses/{id}/approve` (**BR-07**, `docs/FRS.md` §5.1.5, §7.2.4). The employee
must submit a new expense instead (`docs/FRS.md` §7.2.5).

#### Scenario: A Rejected expense cannot later be approved
- **WHEN** `POST /api/expenses/{id}/approve` targets an expense whose `Status` is
  `Rejected`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status`
  remains `Rejected`

### Requirement: Audit Field Population on Approve and Reject
On a successful `POST /api/expenses/{id}/approve`, the system SHALL set `ApprovedAt` to
the current timestamp and `ApprovedByEmployeeId` to the caller's `EmployeeId`
(`docs/SDS.md` §6.8). On a successful `POST /api/expenses/{id}/reject`, the system SHALL
set `RejectedAt`, `RejectedByEmployeeId`, and `RejectionComment`. Neither endpoint SHALL
accept or honor a client-supplied value for any audit field.

#### Scenario: Approval populates ApprovedAt and ApprovedByEmployeeId
- **WHEN** a `Submitted` expense is successfully approved
- **THEN** `ApprovedAt` is set to the current timestamp and `ApprovedByEmployeeId` is set
  to the approving manager's `EmployeeId`

#### Scenario: Rejection populates RejectedAt, RejectedByEmployeeId, and RejectionComment
- **WHEN** a `Submitted` expense is successfully rejected with `rejectionComment: "Missing itemized receipt"`
- **THEN** `RejectedAt` is set to the current timestamp, `RejectedByEmployeeId` is set to
  the rejecting manager's `EmployeeId`, and `RejectionComment` stores `"Missing itemized
  receipt"`

#### Scenario: Client cannot set audit fields directly
- **WHEN** a `POST /api/expenses/{id}/approve` or `POST /api/expenses/{id}/reject`
  request body includes `approvedAt`, `approvedByEmployeeId`, `rejectedAt`, or
  `rejectedByEmployeeId`
- **THEN** those values are ignored, and the system-computed values are used instead

