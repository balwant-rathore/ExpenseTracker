## 1. Foundation — API contracts, DTOs, EF Core migration

All backend-only (this ticket has no `frontend/` scope, per `design.md` Goals/Non-Goals).

- [x] 1.1 Add `UploadedByEmployeeId` (`Guid`) to `backend/src/Domain/Entities/Attachment.cs` (ADR-0008)
- [x] 1.2 Configure the new FK in `backend/src/Infrastructure/Persistence/Configurations/AttachmentConfiguration.cs` (`HasOne(...).WithMany().HasForeignKey(a => a.UploadedByEmployeeId).OnDelete(DeleteBehavior.Restrict)`, no inverse `Employee` collection)
- [x] 1.3 Clear any existing `Attachment` rows in the local dev database (manual, one-time — required before 1.4 succeeds; see `design.md` Migration Plan)
- [x] 1.4 Generate migration: `dotnet ef migrations add AddAttachmentUploader --project src/Infrastructure --startup-project src/Api`
- [x] 1.5 Apply migration: `dotnet ef database update --project src/Infrastructure --startup-project src/Api`; verify the `Attachments` table has the new non-null column
- [x] 1.6 Add the `employee_id` claim in `backend/src/Api/Authentication/EmployeeRoleResolutionMiddleware.cs` (D3)
- [x] 1.7 Add `GetEmployeeId()` to `backend/src/Api/Authentication/ClaimsPrincipalExtensions.cs` (D3)
- [x] 1.8 Add `backend/src/Application/Expenses/` DTOs: `CreateExpenseRequest`, `ExpenseAction` enum, `ExpenseResponse`, `ExpenseFailureReason`, `ExpenseCreationResult`, `ExpenseSubmitResult` (per `design.md` DTOs/Records)

**Checkpoint:** ✅ `dotnet build` (0 errors) · ✅ `dotnet format --verify-no-changes`

## 2. Core Implementation

- [x] 2.1 Implement `IExpenseNumberGenerator`/`ExpenseNumberGenerator` — per-day prefix count + zero-pad + retry-on-unique-violation (ADR-0007)
- [x] 2.2 Implement `CreateExpenseRequestValidator` (FluentValidation) — category is a valid `ExpenseCategory`, `currency == "INR"`, `description` ≤ 500 chars
- [x] 2.3 Implement `IExpenseService`/`ExpenseService.CreateAsync` — BR-01 (amount > 0) and BR-02 (date not in the future) identically for Draft/Submit; attachment exists, unlinked, and owned by caller; expense-number generation; transaction via `IUnitOfWork.ExecuteInTransactionAsync`; sets `SubmittedAt` only when `action == Submit`
- [x] 2.4 Implement `ExpenseService.SubmitAsync` (redesigned per layering fix: `IExpenseRepository` gained `CountByExpenseNumberPrefixAsync`/`ExistsByAttachmentIdAsync`/`TryAddAsync`→`ExpenseInsertOutcome` instead of a leaked `Detach()`/EF Core types in Application) — loads expense, `NotOwner` if caller ≠ `Expense.EmployeeId`, `NotDraft` if `Status != Draft`, re-runs the same BR-01/BR-02/attachment checks, transitions to `Submitted` and sets `SubmittedAt`
- [x] 2.5 Update `UploadAttachmentRequest` (add `required Guid UploadedByEmployeeId`) and `AttachmentService.UploadAsync` (set it on the created `Attachment`)
- [x] 2.6 Update `AttachmentsController.Upload` to pass `User.GetEmployeeId()` into the constructed `UploadAttachmentRequest`
- [x] 2.7 Implement `ExpensesController` (plus new `ExpenseEnvelopeResponse` DTO for the `{ "expense": {...} }` wrapper) — `POST /api/expenses`, `POST /api/expenses/{id}/submit`, `[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]`, `ValidationErrorResult` for validator failures, a `FailureResult` switch (mirroring `AuthController`) mapping `ExpenseFailureReason` → `400`/`403`/`404`/`422`
- [x] 2.8 Implement `ExpenseServiceCollectionExtensions.AddExpenseFoundation` (registers `IExpenseService`, `IExpenseNumberGenerator`)
- [x] 2.9 Wire `builder.Services.AddExpenseFoundation();` into `backend/src/Api/Program.cs`

**Checkpoint:** ✅ `dotnet build` (0 errors) · ✅ `dotnet format --verify-no-changes` (fixed a regression in 2 pre-existing ET006 test files caused by the new required `UploadedByEmployeeId` field)

## 3. Integration

- [x] 3.1 Manually exercised both endpoints via curl against the local dev DB (Scalar UI substituted with direct HTTP calls): Draft creation, immediate-Submit creation, submit-existing-draft, re-submit rejection, and 5 negative-path checks (unauthenticated, already-linked attachment, amount<=0, future date, invalid category/currency) — all returned correct status codes and envelope shapes. **Caught and fixed a real bug**: `ExpenseResponse.Category`/`.Status` were typed as raw C# enums, serializing as integers (`0`) instead of strings — fixed to match `UserDto`'s established string-enum convention.
- [x] 3.2 Confirmed via `/openapi/v1.json`: both `/api/expenses` and `/api/expenses/{id}/submit` appear correctly
- [x] 3.3 Confirmed `POST /api/attachments` end-to-end with the new `UploadedByEmployeeId` — verified via direct DB query that the uploader's EmployeeId is persisted correctly; fixed 2 pre-existing ET006 unit tests that broke on the new required field

**Checkpoint:** ✅ `dotnet build` (0 errors) · ✅ `dotnet format --verify-no-changes` · ✅ `dotnet test` (126/126 passed: 70 unit + 56 integration, pre-existing suite unaffected)

## 4. Tests — one per spec delta scenario

Unit tests (`backend/tests/...UnitTests`):

- [x] 4.1 `ExpenseNumberGenerator`: generated number's `yyyyMMdd` reflects the creation date, not `ExpenseDate` (*Expense Number Generation* — reflects creation date)
- [x] 4.2 `ExpenseService.CreateAsync`: retries and succeeds when the repository simulates an `ExpenseNumberConflict` outcome on the first attempt (relocated from `ExpenseNumberGenerator` to `ExpenseService` — the retry loop lives there per the Phase 2 layering fix) (*Expense Number Generation* — concurrent creation never collides)
- [x] 4.3 `CreateExpenseRequestValidator`: rejects a category outside the seven `ExpenseCategory` values (*Field-Level Validation* — invalid category)
- [x] 4.4 `CreateExpenseRequestValidator`: rejects any `currency` other than `INR` (*Field-Level Validation* — non-INR currency)
- [x] 4.5 `CreateExpenseRequestValidator`: rejects `description` over 500 characters (*Field-Level Validation* — description over 500 chars)
- [x] 4.6 `ExpenseService.CreateAsync`: rejects `amount <= 0` with `action: Draft` (*Business Rule Validation* — zero/negative amount, Draft)
- [x] 4.7 `ExpenseService.CreateAsync`: rejects `amount <= 0` with `action: Submit` (*Business Rule Validation* — zero/negative amount, Submit)
- [x] 4.8 `ExpenseService.CreateAsync`: rejects a future `expenseDate` with `action: Draft` (*Business Rule Validation* — future date, Draft)
- [x] 4.9 `ExpenseService.CreateAsync`: rejects a future `expenseDate` with `action: Submit` (*Business Rule Validation* — future date, Submit)
- [x] 4.10 `ExpenseService.CreateAsync`: rejects a `receiptAttachmentId` that doesn't exist (*Attachment Linkage and Ownership Validation* — missing attachment)
- [x] 4.11 `ExpenseService.CreateAsync`: rejects a `receiptAttachmentId` already linked to another `Expense` (*Attachment Linkage and Ownership Validation* — already-linked attachment)
- [x] 4.12 `ExpenseService.CreateAsync`: rejects an attachment uploaded by a different employee (*Attachment Linkage and Ownership Validation* — uploaded by different employee)
- [x] 4.13 `ExpenseService.CreateAsync`: created expense has `Status = Draft` and `SubmittedAt = null` when `action: Draft` (*Workflow Initialization* — Draft has no SubmittedAt)
- [x] 4.14 `ExpenseService.CreateAsync`: created expense has `Status = Submitted` and `SubmittedAt` populated when `action: Submit` (*Workflow Initialization* — Submitted has SubmittedAt)
- [x] 4.15 `ExpenseService.SubmitAsync`: owner's `Draft` transitions to `Submitted` with `SubmittedAt` populated (*Submit Existing Draft Endpoint* — owner submits own Draft)
- [x] 4.16 `ExpenseService.SubmitAsync`: returns `NotOwner` when caller ≠ `Expense.EmployeeId` (*Submit Existing Draft Endpoint* — non-owner rejected)
- [x] 4.17 `ExpenseService.SubmitAsync`: returns `NotDraft` when the expense's `Status` isn't `Draft` (*Submit Existing Draft Endpoint* — submitting a non-Draft expense)
- [x] 4.18 `ExpenseService.SubmitAsync`: returns a business-rule failure when the stored Draft's linked attachment is no longer valid, and `Status` remains `Draft` (*Submit Existing Draft Endpoint* — re-validation blocks an invalid Draft)

Integration tests (`backend/tests/...IntegrationTests`, `WebApplicationFactory`):

- [x] 4.19 `POST /api/expenses` with `action: Draft` → `201`, `Status = Draft` (*Expense Creation Endpoint* — Draft creation succeeds)
- [x] 4.20 `POST /api/expenses` with `action: Submit` → `201`, `Status = Submitted` (*Expense Creation Endpoint* — immediate-submit creation succeeds)
- [x] 4.21 `POST /api/expenses` with no `Authorization` header → `401 AUTHENTICATION_FAILED` (*Expense Creation Endpoint* — unauthenticated request)
- [x] 4.22 `POST /api/expenses` as a `Finance`/`ComplianceOfficer`-role user → `403 AUTHORIZATION_FAILED` (*Expense Creation Endpoint* — role outside Employee/Manager)
- [x] 4.23 `POST /api/expenses` with an `expenseNumber` field in the request body → response's generated `expenseNumber` is unaffected by the client-supplied value (*Expense Number Generation* — client-supplied expense number is ignored)
- [x] 4.24 `POST /api/expenses` or `POST /api/expenses/{id}/submit` with `submittedAt`/`approvedAt`/etc. in the request body → those values have no effect; system-computed values are used (*Workflow Initialization* — client cannot set audit fields directly)
- [x] 4.25 `POST /api/expenses/{id}/submit` as the owning employee on a valid Draft → `200`, `Status = Submitted` (*Submit Existing Draft Endpoint* — owner submits own Draft, end-to-end)
- [x] 4.26 `POST /api/expenses/{id}/submit` as a non-owning employee → `403 AUTHORIZATION_FAILED` (*Submit Existing Draft Endpoint* — non-owner rejected, end-to-end)
- [x] 4.27 `POST /api/expenses/{id}/submit` targeting an already-`Submitted` expense → `422 BUSINESS_RULE_VIOLATION`, status unchanged (*Submit Existing Draft Endpoint* — submitting a non-Draft expense, end-to-end)
- [x] 4.28 `POST /api/attachments` as an authenticated Employee/Manager → persisted `Attachment.UploadedByEmployeeId` matches the caller (*attachment-upload capability* — Attachment records the uploading employee)
- [x] 4.29 EF Core/DB-level: inserting an `Attachment` row and inspecting it confirms `UploadedByEmployeeId` is set and non-null (*domain-model capability* — Attachment records its uploader) — model-metadata test (FK config), no live DB needed
- [x] 4.30 Regression: a second `Expense` referencing an already-referenced `AttachmentId` still fails a unique-constraint violation after the schema change (*domain-model capability* — an Attachment cannot back two Expenses, still holds) — no new test needed; covered by the pre-existing `Expense_AttachmentId_IsUniqueIndex` test, confirmed still green in Phase 3

**Checkpoint:** ✅ `dotnet build` (0 errors) · ✅ `dotnet format --verify-no-changes` · ✅ 159/159 passed (92 unit incl. 22 new, 67 integration incl. 11 new)

## 5. Archive

- [x] 5.1 Re-run the full quality gate one final time: `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test` — all green, stop and fix at first failure
- [x] 5.2 Run `openspec archive et007-expense-submission` — archived as `2026-07-21-et007-expense-submission`; `expense-submission` capability created (7 requirements), `domain-model`/`attachment-upload` deltas applied
- [x] 5.3 `docs/TICKETS.md` ET007 `Status` left as `In progress` (not `Done`) — `/pr` sets `PR open (#N)`, `Done` is reserved for after merge
