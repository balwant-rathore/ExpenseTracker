## MODIFIED Requirements

### Requirement: Attachment Entity Schema and Expense 1:1 Enforcement
The `Domain` layer SHALL define an `Attachment` entity per `docs/SDS.md` §3.7 (`FileName`,
`OriginalFileName`, `ContentType`, `FileExtension`, `FileSize`, `StoragePath`, `UploadedAt`), plus
a required `UploadedByEmployeeId` (FK to `Employee`) — an open decision beyond the literal
`docs/SDS.md` §3.7 field list, logged as an ADR in `docs/decisions/`, needed so expense creation
(ET007) can validate that the caller linking an attachment to an expense is the same employee
who uploaded it. `Expense.AttachmentId` SHALL carry a unique constraint so at most one Expense
can reference any given Attachment — an open decision beyond the literal `docs/SDS.md` §3.6 index
list, logged as an ADR, needed to enforce the strict one-to-one cardinality shown in
`docs/SDS.md` §3.1's relationship diagram.

#### Scenario: An Attachment cannot back two Expenses
- **WHEN** a second Expense row is inserted referencing an AttachmentId already referenced by an
  existing Expense
- **THEN** the insert SHALL fail a unique constraint violation

#### Scenario: Attachment records its uploader
- **WHEN** an Attachment row is inspected
- **THEN** its `UploadedByEmployeeId` SHALL reference the `Employee` who uploaded it, and SHALL
  NOT be null
