# ADR-0006: Orphaned Attachment Cleanup

## Status

Accepted

## Context

`docs/SDS.md` §3.7 notes that "Attachments may exist temporarily without an associated expense
until expense creation completes successfully," but neither `docs/FRS.md` nor `docs/SDS.md`
specifies what happens to an `Attachment` uploaded via `POST /api/attachments` (ET006) that is
*never* linked to an `Expense` (e.g., the user uploads a receipt, then abandons the form). Left
alone, these accumulate indefinitely as both database rows and files on disk.

## Decision

Add a background `IHostedService` (`OrphanAttachmentCleanupService`,
`backend/src/Infrastructure/BackgroundServices/`) that runs on its own interval
(`AttachmentCleanup:IntervalMinutes`, default 60) and deletes any `Attachment` row — and its
corresponding file — where `Expense` is still null and `UploadedAt` is older than
`AttachmentCleanup:OrphanThresholdHours` (default 24). Each sweep runs in its own DI scope and is
wrapped in a try/catch that logs and continues on failure, so a cleanup error never affects an
in-flight upload or expense-creation request.

Rejected alternative: an on-demand sweep triggered by the next `POST /api/attachments` request
instead of a dedicated background service. This was rejected because it adds latency and
failure surface to the hot upload path for what is fundamentally a cold-path, time-based
housekeeping concern — a periodic background sweep keeps the two fully decoupled.

## Consequences

- An `Attachment` can briefly exist orphaned (up to ~`OrphanThresholdHours` + one sweep interval)
  before cleanup removes it — acceptable, since `docs/SDS.md` §3.7 already describes attachments
  as temporarily unlinked by design.
- `IAttachmentRepository` gained `Query()` and `Remove()` (beyond the `GetByIdAsync`/`AddAsync`
  minimal base `IRepository<TEntity>` contract) specifically to support this sweep.
- Deleting a file that's already gone (e.g., a double-run edge case) is treated as a no-op by
  `FileStorageService.DeleteAsync`, keeping the sweep idempotent.
