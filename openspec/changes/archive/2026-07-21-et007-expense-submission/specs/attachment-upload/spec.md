## MODIFIED Requirements

### Requirement: Filesystem Storage and Metadata Persistence
The `Infrastructure` layer SHALL write the uploaded file to a configurable storage root
(`Storage:AttachmentsRootPath`) under a `{yyyy}/{MM}/{guid}{extension}` path (per ADR-0005),
never using the client-supplied original filename as the on-disk filename. The `Attachment`
row SHALL record `FileName` (generated), `OriginalFileName` (as uploaded), `ContentType`,
`FileExtension`, `FileSize`, `StoragePath` (relative to the storage root), `UploadedAt`
(`docs/SDS.md` §3.7), and `UploadedByEmployeeId` (the authenticated caller's employee ID, not a
client-supplied value — needed by expense creation (ET007) to validate attachment ownership).
The database SHALL store only the relative path — never an absolute filesystem path — and never
the file bytes themselves.

#### Scenario: Uploaded file is written under the configured root
- **WHEN** a valid file is uploaded successfully
- **THEN** a file exists on disk at `{Storage:AttachmentsRootPath}/{yyyy}/{MM}/{guid}{ext}`,
  and the persisted `Attachment.StoragePath` is the relative `{yyyy}/{MM}/{guid}{ext}` portion

#### Scenario: Original filename is preserved as metadata only
- **WHEN** a file named `My Receipt (final).PDF` is uploaded
- **THEN** `Attachment.OriginalFileName` stores `My Receipt (final).PDF` verbatim, while the
  on-disk filename is the generated GUID-based name, not the original

#### Scenario: Attachment records the uploading employee
- **WHEN** an authenticated Employee or Manager uploads a valid file
- **THEN** the persisted `Attachment.UploadedByEmployeeId` is set to that caller's employee ID,
  regardless of any value the client may have sent
