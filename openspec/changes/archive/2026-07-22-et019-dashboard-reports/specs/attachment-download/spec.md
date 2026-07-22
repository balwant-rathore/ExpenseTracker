## ADDED Requirements

### Requirement: Attachment Download Endpoint
The `Api` layer SHALL expose `GET /api/attachments/{id}`, requiring authentication, that resolves
the attachment's owning `Expense` and, when the caller is authorized to view that expense, returns
the stored file content as the response body (`docs/SDS.md` §1.4 file storage design; new endpoint
not previously documented — see the ADR called out in this change's proposal).

#### Scenario: Authorized caller downloads an existing attachment
- **WHEN** an authenticated caller who is authorized to view the owning expense GETs
  `/api/attachments/{id}` for an existing attachment
- **THEN** the response is `200` with the stored file content as the body

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `GET /api/attachments/{id}` carries no valid `Authorization: Bearer` token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

#### Scenario: Nonexistent attachment returns not found
- **WHEN** an authenticated caller GETs `/api/attachments/{id}` for an id that does not exist
- **THEN** the response is `404` with code `RESOURCE_NOT_FOUND`

### Requirement: Authorization Reuses Expense Visibility Rule
The endpoint SHALL authorize the caller using the exact same `ExpenseVisibility.BuildPredicate`
rule already enforced by `GET /api/expenses/{id}` (`expense-visibility` capability), evaluated
against the attachment's owning `Expense` — not a separate or narrower authorization rule. A
caller who cannot view the owning expense SHALL be rejected with `403`, never receiving the file
content, regardless of whether the attachment itself exists.

#### Scenario: Owner can download their own expense's attachment regardless of status
- **WHEN** the owner of an expense in any status GETs `/api/attachments/{id}` for that expense's
  attachment
- **THEN** the response is `200` with the file content

#### Scenario: Manager can download a direct report's non-Draft expense's attachment
- **WHEN** a `Manager` GETs `/api/attachments/{id}` for a `Submitted` expense owned by one of
  their direct reports
- **THEN** the response is `200` with the file content

#### Scenario: Manager cannot download an indirect report's attachment
- **WHEN** a `Manager` GETs `/api/attachments/{id}` for an expense owned by an employee two levels
  down the reporting chain (not a direct report)
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Finance can download any non-Draft expense's attachment
- **WHEN** a `Finance` caller GETs `/api/attachments/{id}` for any employee's `Approved` expense
- **THEN** the response is `200` with the file content

#### Scenario: Compliance Officer can download an Approved Client Entertainment attachment
- **WHEN** a `ComplianceOfficer` GETs `/api/attachments/{id}` for an `Approved`
  `ClientEntertainment` expense
- **THEN** the response is `200` with the file content

#### Scenario: Compliance Officer can download a ComplianceApproved Client Entertainment attachment
- **WHEN** a `ComplianceOfficer` GETs `/api/attachments/{id}` for a `ComplianceApproved`
  `ClientEntertainment` expense
- **THEN** the response is `200` with the file content, per the same `ComplianceApproved`
  visibility carve-out already granted by `expense-visibility`'s "Compliance Officer Default
  Visibility" requirement — Compliance retains attachment access after acting on the expense

#### Scenario: Compliance Officer is rejected for a non-eligible expense's attachment
- **WHEN** a `ComplianceOfficer` GETs `/api/attachments/{id}` for a `Travel` expense, or for a
  `ClientEntertainment` expense whose status is `Draft`, `Submitted`, `Rejected`, `Cancelled`, or
  `Reimbursed`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

#### Scenario: Draft expense's attachment is not visible to a non-owner
- **WHEN** a `Manager` GETs `/api/attachments/{id}` for a direct report's `Draft` expense
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

### Requirement: Attachment Response Content Type and Disposition
The response SHALL set `Content-Type` to the attachment's stored `ContentType` and
`Content-Disposition: inline` naming the attachment's `OriginalFileName`, so that a browser
renders the file directly (PDF/JPG/PNG) rather than forcing a download prompt.

#### Scenario: PDF attachment downloads with its stored content type
- **WHEN** an authorized caller downloads a `.pdf` attachment
- **THEN** the response `Content-Type` is `application/pdf` and `Content-Disposition` is `inline`
  with the original file name

#### Scenario: Image attachment preserves its original file name
- **WHEN** an authorized caller downloads a `.jpg` or `.png` attachment
- **THEN** the response `Content-Disposition` is `inline` and names the attachment's
  `OriginalFileName`, not its internally generated storage file name
