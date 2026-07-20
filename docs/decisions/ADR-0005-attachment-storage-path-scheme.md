# ADR-0005: Attachment Storage Path Scheme

## Status

Accepted

## Context

`docs/SDS.md` §3.7 specifies that `Attachment` files are stored on the server filesystem with a
`StoragePath` recorded in the database, but does not specify a directory layout or on-disk
filename convention. `docs/FRS.md` §4.1.3–4.1.4 (BR-03) only constrain file type and size, not
storage layout. Something has to be decided before `POST /api/attachments` (ET006) can write a
file anywhere.

Two risks shaped the options considered:
- Using the client-supplied original filename on disk risks path-traversal characters and
  filename collisions between unrelated uploads.
- A single flat directory of all attachments grows unbounded over the life of the application,
  which becomes slow to enumerate/back up.

## Decision

Store each uploaded file under a configurable root (`Storage:AttachmentsRootPath`) at
`{yyyy}/{MM}/{guid}{extension}`, where the GUID is freshly generated per upload — never the
client-supplied filename. The original filename is preserved only as `Attachment.OriginalFileName`
metadata in the database, never used to construct a filesystem path.

Rejected alternatives:
- **Flat directory, GUID filename**: avoids the collision/traversal risk but not the unbounded
  directory growth.
- **GUID-prefixed original filename** (`{guid}_{originalFileName}`): keeps the original name
  visible on disk for support/debugging, but reintroduces path-traversal/invalid-character risk
  from untrusted input and provides no material benefit over the metadata column.

## Consequences

- `FileStorageService.SaveAsync` (`backend/src/Infrastructure/Storage/FileStorageService.cs`)
  resolves an absolute root from `IHostEnvironment.ContentRootPath` +
  `StorageOptions.AttachmentsRootPath`, then writes under the `{yyyy}/{MM}/{guid}{ext}` relative
  path, which is what `Attachment.StoragePath` stores.
- The on-disk filename is never derived from user input, closing off path traversal via a crafted
  `FileName`.
- Directory size per month is bounded to that month's upload volume rather than growing
  unbounded in a single directory.
