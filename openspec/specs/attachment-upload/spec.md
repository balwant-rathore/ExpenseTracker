# attachment-upload Specification

## Purpose
TBD - created by archiving change et006-file-attachments. Update Purpose after archive.
## Requirements
### Requirement: Attachment Upload Endpoint
The `Api` layer SHALL expose `POST /api/attachments` accepting `multipart/form-data` with a
single `file` field, restricted to the `Employee` and `Manager` roles (`docs/SDS.md` §5.3,
`docs/FRS.md` §4.1.5). On success it SHALL persist an `Attachment` row via
`IAttachmentRepository` and return `201 Created` with the new `attachmentId`. The returned
`attachmentId` is not yet linked to any `Expense` — linkage happens later via
`POST /api/expenses` (ET007).

#### Scenario: Valid upload returns an attachmentId
- **WHEN** an authenticated Employee or Manager POSTs a valid PDF file under 10 MB to
  `/api/attachments`
- **THEN** the response is `201 Created` with a JSON body containing a new `attachmentId`, and
  an `Attachment` row exists in the database with `Expense` unset (null)

#### Scenario: Unauthenticated request is rejected
- **WHEN** a request to `POST /api/attachments` carries no valid `Authorization: Bearer` token
- **THEN** the response is `401` with code `AUTHENTICATION_FAILED`

#### Scenario: Role outside Employee/Manager is rejected
- **WHEN** an authenticated user whose derived role is `Finance` or `ComplianceOfficer` POSTs to
  `/api/attachments`
- **THEN** the response is `403` with code `AUTHORIZATION_FAILED`

### Requirement: File Type and Size Validation
The `Application` layer SHALL reject any upload whose file extension or declared
`Content-Type` is not one of PDF, JPG, PNG, or whose size exceeds 10 MB (**BR-03**,
`docs/FRS.md` §4.1.3–4.1.4, `docs/SDS.md` §3.7). Validation checks the file extension and the
multipart-declared `Content-Type` header only — no binary signature/magic-byte inspection is
performed. Rejected uploads SHALL NOT create an `Attachment` row or write a file to disk.

#### Scenario: Disallowed file type is rejected
- **WHEN** a file with extension `.docx` (or any extension/Content-Type outside PDF/JPG/PNG) is
  uploaded
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry identifying
  the file field, and no `Attachment` row or file is persisted

#### Scenario: Oversized file is rejected
- **WHEN** a file larger than 10 MB is uploaded
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry identifying
  the file field, and no `Attachment` row or file is persisted

#### Scenario: File at exactly the 10 MB boundary is accepted
- **WHEN** a valid PDF/JPG/PNG file of exactly 10 MB (10,485,760 bytes) is uploaded
- **THEN** the upload succeeds with `201 Created`

### Requirement: Filesystem Storage and Metadata Persistence
The `Infrastructure` layer SHALL write the uploaded file to a configurable storage root
(`Storage:AttachmentsRootPath`) under a `{yyyy}/{MM}/{guid}{extension}` path (per ADR-0005),
never using the client-supplied original filename as the on-disk filename. The `Attachment`
row SHALL record `FileName` (generated), `OriginalFileName` (as uploaded), `ContentType`,
`FileExtension`, `FileSize`, `StoragePath` (relative to the storage root), and `UploadedAt`
(`docs/SDS.md` §3.7). The database SHALL store only the relative path — never an absolute
filesystem path — and never the file bytes themselves.

#### Scenario: Uploaded file is written under the configured root
- **WHEN** a valid file is uploaded successfully
- **THEN** a file exists on disk at `{Storage:AttachmentsRootPath}/{yyyy}/{MM}/{guid}{ext}`,
  and the persisted `Attachment.StoragePath` is the relative `{yyyy}/{MM}/{guid}{ext}` portion

#### Scenario: Original filename is preserved as metadata only
- **WHEN** a file named `My Receipt (final).PDF` is uploaded
- **THEN** `Attachment.OriginalFileName` stores `My Receipt (final).PDF` verbatim, while the
  on-disk filename is the generated GUID-based name, not the original

### Requirement: Orphaned Attachment Cleanup
A background hosted service SHALL periodically (at least hourly) delete `Attachment` rows —
and their corresponding files on disk — that have no associated `Expense` and whose
`UploadedAt` is older than 24 hours (ADR-0006). Cleanup SHALL run independently of any request
and its failure SHALL NOT affect any in-flight upload or expense-creation request.

#### Scenario: Orphaned attachment older than 24 hours is deleted
- **WHEN** the cleanup sweep runs and finds an `Attachment` row with no linked `Expense` and
  `UploadedAt` more than 24 hours in the past
- **THEN** the `Attachment` row is deleted from the database and its file is deleted from disk

#### Scenario: Recently uploaded orphan is retained
- **WHEN** the cleanup sweep runs and finds an `Attachment` row with no linked `Expense` but
  `UploadedAt` less than 24 hours in the past
- **THEN** the row and its file are left untouched

#### Scenario: Linked attachment is never deleted
- **WHEN** the cleanup sweep runs and finds an `Attachment` row that has an associated
  `Expense`, regardless of age
- **THEN** the row and its file are left untouched

