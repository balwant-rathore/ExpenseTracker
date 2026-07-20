## Why

Ticket **ET006** (`docs/TICKETS.md`). Expense creation (ET007, next in build order) requires a
`receiptAttachmentId` per `docs/FRS.md` §4.1.1 (BR-03: receipt attachment is mandatory) and
`docs/SDS.md` §5.2's `POST /api/expenses` contract. The `Attachment` domain entity, EF
configuration, and `IAttachmentRepository` already exist (ET002), but no API exists yet to
actually upload a file, validate it, store it on the filesystem, and persist its metadata. ET006
delivers that upload capability so ET007 has something to call.

## What Changes

- Add `POST /api/attachments` (multipart/form-data, `Employee`/`Manager` roles) per
  `docs/SDS.md` §5.3 / `docs/FRS.md` §4.1.3–4.1.4: validates file extension + declared
  Content-Type against the PDF/JPG/PNG allow-list and the 10 MB size cap (**BR-03** support),
  returns `201` with `attachmentId`.
- Add an `Application`-layer `IAttachmentService`/`AttachmentService` (validation +
  orchestration) and an `Infrastructure`-layer `IFileStorageService` (filesystem read/write)
  per `docs/SDS.md` §1.4 ("Receipt files are stored on the server filesystem; database stores
  metadata + relative path only").
- Persist `Attachment` rows via the existing `IAttachmentRepository` — no entity/schema changes.
  Uploaded attachments remain unlinked (`Expense` is null) until ET007's expense-create flow
  associates one, per `docs/SDS.md` §3.7 ("Attachments may exist temporarily without an
  associated expense until expense creation completes successfully").
- Add a storage path/naming scheme (GUID filename, `yyyy/MM` subfolder under a configurable
  root) — not specified in `docs/SDS.md` §3.7, logged as **ADR-0005**.
- Add a background orphan-cleanup mechanism (`IHostedService`, hourly sweep, 24h threshold)
  that deletes `Attachment` rows/files never linked to an `Expense` — an addition beyond
  `docs/SDS.md` §3.7's literal text, logged as **ADR-0006**.
- Add `Storage:AttachmentsRootPath` configuration (non-secret default in `appsettings.json`,
  per-developer override via user-secrets, matching the existing connection-string pattern in
  `backend/CLAUDE.md`).

## Capabilities

### New Capabilities
- `attachment-upload`: `POST /api/attachments` upload endpoint, file validation (type/size),
  filesystem storage, `Attachment` metadata persistence, and the orphan-cleanup background job.

### Modified Capabilities
_(none — the `Attachment` entity schema and repository from `domain-model` are unchanged;
this ticket adds an API/service layer on top of them.)_

## Impact

- **New code**: `Api/Controllers/AttachmentsController.cs`; `Application/Services/
  IAttachmentService.cs` + `AttachmentService.cs`; `Application/DTOs/AttachmentDto.cs`;
  `Infrastructure/Storage/IFileStorageService.cs` + `FileStorageService.cs`;
  `Infrastructure/BackgroundServices/OrphanAttachmentCleanupService.cs`.
- **Config**: `appsettings.json` (`Storage:AttachmentsRootPath`, cleanup interval/threshold),
  DI registration in `Api/Program.cs`.
- **Decisions**: `docs/decisions/ADR-0005-attachment-storage-path-scheme.md`,
  `docs/decisions/ADR-0006-orphan-attachment-cleanup.md`.
- **Downstream**: unblocks ET007 (`POST /api/expenses` needs a real `attachmentId` to link).
- **No changes** to `Domain` entities, EF migrations, or existing auth/expense capabilities.
