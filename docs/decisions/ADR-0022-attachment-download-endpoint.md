# ADR-0022: `GET /api/attachments/{id}` Attachment Download Endpoint

## Status

Accepted

## Context

ET019 (`docs/TICKETS.md`) adds a "View Receipt" link to the expense detail screen
(`frontend-attachment-viewer-ui` capability) so a user can actually open an expense's uploaded
receipt — today `AttachmentsController` (`backend/src/Api/Controllers/AttachmentsController.cs`)
exposes only `POST /api/attachments` (upload); there is no way to read a stored file back. This
endpoint does not exist in `docs/SDS.md` §5.3 today, so adding it is an explicit deviation from
the current SDS API surface, logged here per `AGENTS.md` §13 and `openspec/config.yaml`
`rules.proposal`.

Two design questions needed resolving before implementation:

1. **Authorization** — who can download a given attachment? Confirmed with the ticket owner
   during `/spec`: identical to who can already view the attachment's owning expense (the
   `attachment-download` capability's "Authorization Reuses Expense Visibility Rule" requirement).
2. **A subtle ASP.NET Core attribute-combination bug**, caught during `/plan`: `AttachmentsController`
   declares `[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]` at the **class**
   level. ASP.NET Core combines (ANDs) class-level and action-level `[Authorize]` attributes — a
   method-level attribute does not replace a class-level one. Adding a `Download` action with a
   bare `[Authorize]` would still be gated by the class's `EmployeeOrManager` policy, silently
   blocking Finance and Compliance Officer callers, which directly contradicts requirement 1.

## Decision

- Add `GET /api/attachments/{id}` to `AttachmentsController`. Authorization reuses
  `Application.Expenses.ExpenseVisibility.BuildPredicate(role, employeeId)` — the same rule
  already enforced on `GET /api/expenses/{id}` — applied to the attachment's owning expense
  (looked up via a new `IExpenseRepository.GetByAttachmentIdWithEmployeeAsync`). No new
  authorization policy or duplicated visibility logic is introduced.
- Move `[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]` from the controller's
  class level down onto the `Upload` action only, leaving the class with a bare `[Authorize]`
  (any authenticated user) — mirroring `ExpensesController`'s existing pattern (bare class-level
  `[Authorize]`, per-action policies where needed). `Upload`'s authorization behavior is
  unchanged; this is an attribute relocation, not a requirement change to the existing
  `attachment-upload` capability.
- The response sets `Content-Disposition: inline` (not `attachment`) with the attachment's stored
  `ContentType`, so the browser renders the file directly rather than forcing a download —
  required by the `frontend-attachment-viewer-ui` capability's "open inline in a new tab" UX.
  `File(result.Content!, result.ContentType!)` is called without a `fileDownloadName` argument,
  since that overload forces `Content-Disposition: attachment`; the header is set manually instead,
  via `Microsoft.Net.Http.Headers.ContentDispositionHeaderValue.SetHttpFileName` (not raw string
  interpolation) — `OriginalFileName` is caller-controlled upload input, and `SetHttpFileName`
  correctly quotes/escapes it (and encodes non-ASCII names per RFC 5987), whereas interpolating it
  directly into the header string would let a filename containing `"` corrupt the header.
- A missing attachment and an attachment not (yet) linked to any expense are both reported as
  `404 RESOURCE_NOT_FOUND` — an unlinked attachment has no owning expense to authorize against,
  so it is indistinguishable from "not found" to every caller, including its own uploader. The same
  `404 RESOURCE_NOT_FOUND` is also returned if the attachment's DB row resolves and passes
  authorization but the file is absent from disk (storage drift) — `AttachmentService.DownloadAsync`
  catches `FileNotFoundException` from `IFileStorageService.OpenReadAsync` rather than letting it
  surface as an unhandled `500`.

## Consequences

- `backend/src/Application/Attachments/AttachmentDownloadResult.cs`,
  `AttachmentDownloadFailureReason.cs` — new types mirroring `ExpenseResult`'s
  `Success`/`Failure` shape.
- `backend/src/Application/Attachments/IAttachmentService.cs`, `AttachmentService.cs` — new
  `DownloadAsync(Guid employeeId, EmployeeRole role, Guid attachmentId, CancellationToken)`.
- `backend/src/Domain/Repositories/IExpenseRepository.cs`,
  `Infrastructure/Persistence/Repositories/ExpenseRepository.cs` — new
  `GetByAttachmentIdWithEmployeeAsync`, mirroring `GetByIdWithEmployeeAsync`'s
  `Include(e => e.Employee).Include(e => e.Attachment)` pattern.
- `backend/src/Domain/Storage/IFileStorageService.cs`, `Infrastructure/Storage/FileStorageService.cs`
  — new `OpenReadAsync(string relativePath, CancellationToken)` returning a read-only `FileStream`.
- `backend/src/Api/Controllers/AttachmentsController.cs` — new `Download` action; class-level
  policy relocated onto `Upload`.
- `docs/SDS.md` §5.3 — new endpoint row (this same change).
- No EF Core migration — no schema change, only reads of existing `Attachment` columns.
- No change to any existing workflow, business-rule, or authorization behavior — `Upload`'s
  actual access remains Employee/Manager only; the new endpoint's visibility rule is the exact
  rule already enforced on `GET /api/expenses/{id}`.
