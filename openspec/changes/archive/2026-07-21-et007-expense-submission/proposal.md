## Why

Employees currently have no way to create an expense at all — `Expense`, `ExpenseCategory`,
and `ExpenseStatus` exist in the domain model (ET002) and a receipt can be uploaded and stored
(ET006), but nothing links them together or moves an expense into the workflow. ET007
(`docs/TICKETS.md`) closes that gap: it builds the create endpoint (Draft or immediate Submit),
automatic expense-number generation, the BR-01/BR-02/BR-03 validation those actions require, and
initial workflow-state transitions, per `docs/FRS.md` §4.1 and §11 and `docs/SDS.md` §3.6, §3.9,
§5.2, and §6.

## What Changes

- Add `POST /api/expenses` (Employee/Manager, per `docs/SDS.md` §5.2): creates an `Expense` row
  in `Draft` or `Submitted` status per the request's `action` field, generating an immutable,
  unique `ExpenseNumber` (format `EXP-yyyyMMdd-XXXX`, `yyyyMMdd` = the generation date, i.e.
  `CreatedAt`; `XXXX` a globally increasing per-day counter — concurrency-safe generation
  mechanism logged as an ADR since `docs/SDS.md` §3.9 specifies the format but not the
  mechanism).
- Add `POST /api/expenses/{id}/submit` (Owner, per `docs/SDS.md` §5.2): transitions an existing
  `Draft` expense the caller owns to `Submitted`, setting `SubmittedAt`. Bundled into this ticket
  (not ET008) because it shares all the same validation, expense-number-already-assigned, and
  workflow-initialization logic as the create endpoint's `action: Submit` path.
- Enforce BR-01 (Amount > 0), BR-02 (Expense Date not in the future, company-local timezone per
  BR-10), and the 500-character Description limit identically for `Draft` and `Submitted` —
  Draft is not a relaxed-validation state.
- Enforce BR-03 (receipt attachment mandatory) by requiring a valid, existing, currently-
  unlinked `receiptAttachmentId` from a prior `POST /api/attachments` (ET006) call — already
  structurally enforced by `Expense.AttachmentId` being non-nullable with a unique index
  (`docs/SDS.md` §3.6-§3.7), but the service layer must translate a missing/already-linked
  attachment into a clean `422 BUSINESS_RULE_VIOLATION` rather than a raw DB constraint failure.
- **New:** validate that the authenticated caller is the same employee who uploaded the
  referenced attachment. This requires adding an `UploadedByEmployeeId` column to `Attachment`
  (not in the original `docs/SDS.md` §3.7 schema) — logged as an ADR, since the attachment's
  owner is otherwise untracked and any authenticated Employee/Manager could currently link any
  not-yet-linked attachment ID to their own expense.
- Currency is fixed to `INR` (`docs/FRS.md` §4.1.1 "Default to Rupees - INR"); the request
  accepts the field (per the `docs/SDS.md` §5.2 example body) but rejects any value other than
  `INR` as a validation error — no multi-currency support is in scope.
- Authorization reuses the existing `EmployeeOrManager` policy (already registered for
  `AttachmentsController` in ET006) — no new policy needed.

## Capabilities

### New Capabilities
- `expense-submission`: Expense creation (Draft or immediate Submit), automatic expense-number
  generation, BR-01/BR-02/BR-03 validation, attachment-ownership validation, and the standalone
  submit-existing-draft endpoint — i.e. all of `docs/FRS.md` §4.1 and §11 and `docs/SDS.md` §3.6,
  §3.9, §5.2 (the `POST /api/expenses` and `POST /api/expenses/{id}/submit` rows) and §6 (the
  `Draft` → `Submitted` edge of the workflow only — later transitions are out of scope for this
  ticket).

### Modified Capabilities
- `domain-model`: the `Attachment` entity gains a required `UploadedByEmployeeId` (FK to
  `Employee`), beyond the field list in the existing "Attachment Entity Schema and Expense 1:1
  Enforcement" requirement — needs a new EF Core migration.
- `attachment-upload`: the "Filesystem Storage and Metadata Persistence" requirement's
  recorded-fields list gains `UploadedByEmployeeId`, populated from the authenticated caller at
  upload time (no client input, no request/response shape change).

## Impact

- **New:** `backend/src/Api/Controllers/ExpensesController.cs`; `backend/src/Application/Expenses/`
  (service, request/response DTOs, FluentValidation validators, expense-number generator);
  DI registration extension (`ExpenseServiceCollectionExtensions`, following the
  `AttachmentServiceCollectionExtensions` pattern).
- **Modified:** `backend/src/Domain/Entities/Attachment.cs` (+`UploadedByEmployeeId`);
  `backend/src/Infrastructure/Persistence/Configurations/AttachmentConfiguration.cs`;
  `backend/src/Application/Attachments/AttachmentService.cs` (set the new column at upload); a
  new EF Core migration.
- **Dependencies:** none added — reuses `IExpenseRepository`, `IAttachmentRepository`,
  `EmployeeOrManager` policy, and the FluentValidation/error-envelope conventions already in
  place from ET002/ET003/ET006.
- **Out of scope for ET007** (per `docs/TICKETS.md`): expense edit/cancel (ET008), list/search/
  view endpoints (ET009), manager/compliance/finance review actions (ET010-ET012), and any
  workflow transition beyond `Draft → Submitted`.
