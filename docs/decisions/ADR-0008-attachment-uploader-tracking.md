# ADR-0008: Attachment Uploader Tracking

## Status

Accepted

## Context

The `Attachment` entity (ET006, `docs/SDS.md` §3.7) records only file metadata — it has no
column identifying which employee uploaded it. Since `attachmentId` is a GUID returned to
whoever called `POST /api/attachments`, and expense creation (ET007) accepts any not-yet-linked
`attachmentId` from the authenticated caller, any authenticated Employee/Manager could
technically link *any* not-yet-linked attachment — including one uploaded by a different
employee — to their own expense. This was raised as an explicit open question during
`/spec ET007` and the ticket owner chose to close the gap now rather than accept it, even though
it means modifying an entity from an already-merged ticket (ET006).

## Decision

Add a required `UploadedByEmployeeId` (`Guid`, FK to `Employee.EmployeeId`, `OnDelete:
Restrict` — consistent with `AGENTS.md` §5 "cascade delete disabled on business entities") to
`Attachment`, populated from the authenticated caller (never client-supplied) inside
`AttachmentService.UploadAsync`. Expense creation (ET007) validates
`attachment.UploadedByEmployeeId == <authenticated employee>` as part of its business-rule
validation (`422 BUSINESS_RULE_VIOLATION` on mismatch, alongside the existing "must exist" and
"must not already be linked" checks).

No inverse navigation collection (e.g. `Employee.UploadedAttachments`) is added — nothing in
ET007's scope queries attachments by employee, so a bidirectional relationship isn't needed yet.

**Migration note:** the new column is non-nullable with no default. Any `Attachment` rows
already present in a local/dev database from ET006 testing must be cleared before this
migration is applied (see `design.md` Migration Plan) — acceptable since those rows are
ephemeral test uploads (either already linked to a test `Expense` or due for the orphan-cleanup
sweep within 24 hours per ADR-0006), not production data.

Rejected alternative: leave the gap and rely on GUID unguessability (the option presented
alongside this one). Rejected by the ticket owner as insufficient given attachment IDs may
appear in client-side network logs, browser history, or be otherwise observable by the
uploading employee's own tooling.

## Consequences

- `docs/SDS.md` §3.7's `Attachment` field list is now out of date; `AGENTS.md` §9 and the
  `domain-model`/`attachment-upload` OpenSpec capability specs have been updated to include
  `UploadedByEmployeeId` as part of this change (see `specs/domain-model/spec.md` and
  `specs/attachment-upload/spec.md` deltas).
- `AttachmentService.UploadAsync` and `UploadAttachmentRequest` gain a required
  `UploadedByEmployeeId` parameter/field, sourced from the controller via the authenticated
  principal (see `design.md` Decision on `ClaimsPrincipalExtensions.GetEmployeeId()`) — this is
  an internal signature change, not an API request/response shape change (the field is never
  client-supplied).
