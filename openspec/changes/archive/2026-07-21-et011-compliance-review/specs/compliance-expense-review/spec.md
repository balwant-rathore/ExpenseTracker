## ADDED Requirements

### Requirement: Compliance Approval Endpoint
The `Api` layer SHALL expose `POST /api/expenses/{id}/compliance-approve`, restricted to
the `ComplianceOfficer` role (`docs/SDS.md` §5.2, §6.4), transitioning an `Approved`
`ClientEntertainment` expense to `ComplianceApproved` (`docs/FRS.md` §5.2.3). Unlike the
Manager review endpoints (`openspec/specs/manager-expense-review/spec.md`), no
ownership/hierarchy check is performed: any authenticated `ComplianceOfficer` may act on
any eligible expense, since a single Compliance Officer role exists in the system
(`docs/SDS.md` §2 "Roles").

#### Scenario: Compliance Officer approves an Approved Client Entertainment expense
- **WHEN** a `ComplianceOfficer` POSTs to `/api/expenses/{id}/compliance-approve` for an
  `Approved` `ClientEntertainment` expense
- **THEN** the response is `200` and the expense's `Status` becomes `ComplianceApproved`

#### Scenario: Non-ComplianceOfficer role is rejected
- **WHEN** an authenticated caller whose resolved role is `Employee`, `Manager`, or
  `Finance` POSTs to `/api/expenses/{id}/compliance-approve`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Nonexistent expense is rejected
- **WHEN** `POST /api/expenses/{id}/compliance-approve` targets an id with no matching
  `Expense` row
- **THEN** the response is `404` with code `RESOURCE_NOT_FOUND`

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `POST /api/expenses/{id}/compliance-approve` carries no valid
  `Authorization: Bearer` token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

### Requirement: Compliance Rejection Endpoint
The `Api` layer SHALL expose `POST /api/expenses/{id}/compliance-reject`, restricted to
the `ComplianceOfficer` role, accepting a `rejectionComment`, transitioning an `Approved`
`ClientEntertainment` expense to `Rejected` (`docs/FRS.md` §5.2.4–5.2.5). Authorization
mirrors the Compliance Approval Endpoint: no ownership/hierarchy check, any authenticated
`ComplianceOfficer` may act on any eligible expense.

#### Scenario: Compliance Officer rejects an Approved Client Entertainment expense with a comment
- **WHEN** a `ComplianceOfficer` POSTs to `/api/expenses/{id}/compliance-reject` with a
  valid `rejectionComment` for an `Approved` `ClientEntertainment` expense
- **THEN** the response is `200` and the expense's `Status` becomes `Rejected`

#### Scenario: Non-ComplianceOfficer role is rejected
- **WHEN** an authenticated caller whose resolved role is `Employee`, `Manager`, or
  `Finance` POSTs to `/api/expenses/{id}/compliance-reject`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Nonexistent expense is rejected
- **WHEN** `POST /api/expenses/{id}/compliance-reject` targets an id with no matching
  `Expense` row
- **THEN** the response is `404` with code `RESOURCE_NOT_FOUND`

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `POST /api/expenses/{id}/compliance-reject` carries no valid
  `Authorization: Bearer` token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

### Requirement: Mandatory Rejection Comment Validation on Compliance Rejection
`POST /api/expenses/{id}/compliance-reject` SHALL require `rejectionComment` to be
present, non-empty after trimming whitespace, and no more than 500 characters
(`docs/FRS.md` §5.2.5), reusing the identical validation rules already established for
`POST /api/expenses/{id}/reject` in `openspec/specs/manager-expense-review/spec.md`. A
request failing this check SHALL be rejected with `400 VALIDATION_ERROR` and a `fields`
entry for `rejectionComment`, with no change persisted.

#### Scenario: Missing rejectionComment is rejected
- **WHEN** a `POST /api/expenses/{id}/compliance-reject` request omits `rejectionComment`
  entirely
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for
  `rejectionComment`, and the expense's `Status` is unchanged

#### Scenario: Whitespace-only rejectionComment is rejected
- **WHEN** a `POST /api/expenses/{id}/compliance-reject` request's `rejectionComment`
  contains only whitespace characters
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for
  `rejectionComment`, and the expense's `Status` is unchanged

#### Scenario: Oversized rejectionComment is rejected
- **WHEN** a `POST /api/expenses/{id}/compliance-reject` request's `rejectionComment`
  exceeds 500 characters
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for
  `rejectionComment`, and the expense's `Status` is unchanged

### Requirement: Client Entertainment Category Restriction
`POST /api/expenses/{id}/compliance-approve` and `POST /api/expenses/{id}/compliance-reject`
SHALL reject, with `422 BUSINESS_RULE_VIOLATION` and the `Status` left unchanged, a
request targeting an expense whose `Category` is not `ClientEntertainment` — regardless of
that expense's `Status` (`docs/FRS.md` §5.2.1). This check is independent of, and
evaluated separately from, the Approved-Status Precondition below.

#### Scenario: Compliance-approving a non-Client-Entertainment expense is rejected
- **WHEN** `POST /api/expenses/{id}/compliance-approve` targets an `Approved` expense
  whose `Category` is `Travel`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

#### Scenario: Compliance-rejecting a non-Client-Entertainment expense is rejected
- **WHEN** `POST /api/expenses/{id}/compliance-reject` (with a valid `rejectionComment`)
  targets an `Approved` expense whose `Category` is `Meals`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

### Requirement: Approved-Status Precondition for Compliance Review
`POST /api/expenses/{id}/compliance-approve` and `POST /api/expenses/{id}/compliance-reject`
SHALL reject, with `422 BUSINESS_RULE_VIOLATION` and the `Status` left unchanged, a request
targeting a `ClientEntertainment` expense whose `Status` is anything other than `Approved`
(no backward transitions; no re-review of a `ComplianceApproved` or `Rejected` expense —
`docs/FRS.md` §5.2.6). This includes, individually, `Draft`, `Submitted`,
`ComplianceApproved`, `Rejected`, `Cancelled`, and `Reimbursed`.

#### Scenario: Compliance-approving a Submitted Client Entertainment expense is rejected
- **WHEN** `POST /api/expenses/{id}/compliance-approve` targets a `ClientEntertainment`
  expense whose `Status` is `Submitted`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

#### Scenario: Compliance-approving an already-ComplianceApproved expense is rejected
- **WHEN** `POST /api/expenses/{id}/compliance-approve` targets a `ClientEntertainment`
  expense whose `Status` is already `ComplianceApproved`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

#### Scenario: Compliance-approving a Rejected expense is rejected
- **WHEN** `POST /api/expenses/{id}/compliance-approve` targets a `ClientEntertainment`
  expense whose `Status` is `Rejected`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` remains
  `Rejected`

#### Scenario: Compliance-rejecting a Reimbursed expense is rejected
- **WHEN** `POST /api/expenses/{id}/compliance-reject` (with a valid `rejectionComment`)
  targets a `ClientEntertainment` expense whose `Status` is `Reimbursed`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and `Status` is
  unchanged

### Requirement: Audit Field Population on Compliance Approve and Reject
On a successful `POST /api/expenses/{id}/compliance-approve`, the system SHALL set
`ComplianceApprovedAt` to the current timestamp and `ComplianceApprovedByEmployeeId` to
the caller's `EmployeeId` (`docs/SDS.md` §6.8). On a successful
`POST /api/expenses/{id}/compliance-reject`, the system SHALL set the same
`RejectedAt`/`RejectedByEmployeeId`/`RejectionComment` fields that
`openspec/specs/manager-expense-review/spec.md`'s Manager Rejection Endpoint populates —
`Rejected` has one terminal meaning regardless of which role rejected the expense. Neither
endpoint SHALL accept or honor a client-supplied value for any audit field.

#### Scenario: Compliance approval populates ComplianceApprovedAt and ComplianceApprovedByEmployeeId
- **WHEN** an `Approved` `ClientEntertainment` expense is successfully compliance-approved
- **THEN** `ComplianceApprovedAt` is set to the current timestamp and
  `ComplianceApprovedByEmployeeId` is set to the approving Compliance Officer's
  `EmployeeId`

#### Scenario: Compliance rejection populates RejectedAt, RejectedByEmployeeId, and RejectionComment
- **WHEN** an `Approved` `ClientEntertainment` expense is successfully compliance-rejected
  with `rejectionComment: "Exceeds per-person entertainment limit"`
- **THEN** `RejectedAt` is set to the current timestamp, `RejectedByEmployeeId` is set to
  the rejecting Compliance Officer's `EmployeeId`, and `RejectionComment` stores
  `"Exceeds per-person entertainment limit"`

#### Scenario: Client cannot set audit fields directly
- **WHEN** a `POST /api/expenses/{id}/compliance-approve` or
  `POST /api/expenses/{id}/compliance-reject` request body includes
  `complianceApprovedAt`, `complianceApprovedByEmployeeId`, `rejectedAt`, or
  `rejectedByEmployeeId`
- **THEN** those values are ignored, and the system-computed values are used instead
