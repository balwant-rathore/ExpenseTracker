## 1. Foundation — DTOs, failure reasons, repository contract

All backend-only (this ticket has no `frontend/` scope, per `design.md` Goals/Non-Goals).

- [x] 1.1 Write ADR-0010 (`docs/decisions/ADR-0010-draft-cancellation-scope.md`) documenting the Draft-cancellation deviation from FRS §4.3.1/SDS §6.3 (completed during `/plan`)
- [x] 1.2 Add `NotEditable`/`NotCancellable` to `backend/src/Application/Expenses/ExpenseFailureReason.cs` (D4)
- [x] 1.3 Add `backend/src/Application/Expenses/UpdateExpenseRequest.cs` (D3)
- [x] 1.4 Add `backend/src/Application/Expenses/UpdateExpenseRequestValidator.cs` — same field-level rules as `CreateExpenseRequestValidator` (category enum, `currency == "INR"`, description ≤ 500 chars), reusing its public `MaxDescriptionLength`/`RequiredCurrency` constants (D3)
- [x] 1.5 Add `Task<bool> TryUpdateAsync(Expense expense, CancellationToken cancellationToken)` to `backend/src/Domain/Repositories/IExpenseRepository.cs`, and implement it in `backend/src/Infrastructure/Persistence/Repositories/ExpenseRepository.cs`, reusing the existing private `IsUniqueViolation` helper to catch `IX_Expenses_AttachmentId` conflicts (D2) — also updated `FakeExpenseRepository` (test double in `ExpenseNumberGeneratorTests.cs`) to implement the new interface member

**Checkpoint:** ✅ `dotnet build` (0 errors) · ✅ `dotnet format --verify-no-changes`

## 2. Core Implementation

- [x] 2.1 Add `backend/src/Application/Expenses/ExpenseResult.cs`; delete `ExpenseCreationResult.cs`/`ExpenseSubmitResult.cs` (D1)
- [x] 2.2 Update `IExpenseService.cs`: `CreateAsync`/`SubmitAsync` return `Task<ExpenseResult>` (was `ExpenseCreationResult`/`ExpenseSubmitResult`); add `GetByIdAsync`, `UpdateAsync`, `CancelAsync` signatures
- [x] 2.3 Update `ExpenseService.CreateAsync`/`SubmitAsync` to return `ExpenseResult` (behavior unchanged, type only)
- [x] 2.4 Implement `ExpenseService.GetByIdAsync` — `ExpenseNotFound` → `NotOwner` → success; no status gate (an owner may view their expense in any status) (D6)
- [x] 2.5 Implement `ExpenseService.UpdateAsync` — order: `ExpenseNotFound` → `NotOwner` → `NotEditable` (`Status` not `Draft`/`Submitted`, BR-04) → `AmountNotPositive` (BR-01) → `ExpenseDateInFuture` (BR-02) → `AttachmentNotFound`/`AttachmentNotOwned` on the new `receiptAttachmentId` (BR-03) → mutate fields → `TryUpdateAsync` (`AttachmentAlreadyLinked` on unique-constraint conflict, D2) → set `UpdatedAt` only; `Status` and every workflow audit field left untouched
- [x] 2.6 Implement `ExpenseService.CancelAsync` — order: `ExpenseNotFound` → `NotOwner` → `NotCancellable` (`Status` not `Draft`/`Submitted`, BR-05/ADR-0010) → set `Status = Cancelled`, `UpdatedAt`, persist via `IUnitOfWork.ExecuteInTransactionAsync` (mirrors `SubmitAsync`'s save pattern)
- [x] 2.7 Add `GetById` (`[HttpGet("{id:guid}")]`), `Update` (`[HttpPut("{id:guid}")]`), `Cancel` (`[HttpPost("{id:guid}/cancel")]`) actions to `ExpensesController.cs`; inject `IValidator<UpdateExpenseRequest>` alongside the existing `createValidator`, reusing `ValidationErrorResult` for `Update`'s field-level failures
- [x] 2.8 Extend `ExpensesController.FailureResult`'s switch with `NotEditable` → `422` ("Only expenses in Draft or Submitted status can be edited.") and `NotCancellable` → `422` ("Only expenses in Draft or Submitted status can be cancelled.")

**Checkpoint:** ✅ `dotnet build` (0 errors) · ✅ `dotnet format --verify-no-changes`

## 3. Integration

- [x] 3.1 Manually exercised all three new endpoints against the local dev DB via curl: `GET` (own Draft, non-owned expense → 403, nonexistent → 404, unauthenticated → 401), `PUT` (edit Draft with attachment swap, resubmit-same-attachment, edit Submitted leaves Status unchanged, invalid category → 400, attachment already linked to another expense → 422, edit a Cancelled expense → 422 NotEditable, non-owner → 403), `POST .../cancel` (Draft → Cancelled, Submitted → Cancelled, already-Cancelled → 422 NotCancellable, non-owner → 403) — all status codes and envelope shapes matched `specs/expense-maintenance/spec.md`
- [x] 3.2 Confirmed via `/openapi/v1.json`: `/api/expenses/{id}` (GET+PUT) and `/api/expenses/{id}/cancel` appear correctly
- [x] 3.3 Ran the full existing test suite (102 unit + 68 integration, all green) — confirmed `POST /api/expenses`/`POST /api/expenses/{id}/submit` behavior is unaffected by the `ExpenseResult` consolidation (D1)

**Checkpoint:** ✅ `dotnet build` (0 errors) · ✅ `dotnet format --verify-no-changes` · ✅ `dotnet test` (170/170 passed)

## 4. Tests — one per spec delta scenario

Unit tests (`backend/tests/...UnitTests`):

- [x] 4.1 `UpdateExpenseRequestValidator`: rejects a category outside the seven `ExpenseCategory` values (*Edit Field-Level and Business-Rule Validation* — invalid category)
- [x] 4.2 `UpdateExpenseRequestValidator`: rejects any `currency` other than `INR` (*Edit Field-Level and Business-Rule Validation* — non-INR currency)
- [x] 4.3 `UpdateExpenseRequestValidator`: rejects `description` over 500 characters (*Edit Field-Level and Business-Rule Validation* — description over 500 chars)
- [x] 4.4 `ExpenseService.UpdateAsync`: rejects `amount <= 0` (*Edit Field-Level and Business-Rule Validation* — zero/negative amount)
- [x] 4.5 `ExpenseService.UpdateAsync`: rejects a future `expenseDate` (*Edit Field-Level and Business-Rule Validation* — future expense date)
- [x] 4.6 `ExpenseService.GetByIdAsync`: returns the expense for its owner (*Single Expense Retrieval Endpoint* — owner retrieves own expense)
- [x] 4.7 `ExpenseService.GetByIdAsync`: returns `NotOwner` for a non-owning caller (*Single Expense Retrieval Endpoint* — non-owner rejected)
- [x] 4.8 `ExpenseService.GetByIdAsync`: returns `ExpenseNotFound` for a nonexistent id (*Single Expense Retrieval Endpoint* — nonexistent expense)
- [x] 4.9 `ExpenseService.UpdateAsync`: owner edits their own `Draft` — fields updated, `Status` remains `Draft` (*Expense Edit Endpoint* — owner edits Draft)
- [x] 4.10 `ExpenseService.UpdateAsync`: owner edits their own `Submitted` expense — fields updated, `Status` remains `Submitted` (*Expense Edit Endpoint* — owner edits Submitted)
- [x] 4.11 `ExpenseService.UpdateAsync`: returns `NotOwner` for a non-owning caller (*Expense Edit Endpoint* — non-owner rejected)
- [x] 4.12 `ExpenseService.UpdateAsync`: returns `ExpenseNotFound` for a nonexistent id (*Expense Edit Endpoint* — nonexistent expense)
- [x] 4.13 `ExpenseService.UpdateAsync`: editing a `Submitted` expense leaves `SubmittedAt` unchanged; only `UpdatedAt` advances (*Editing Never Alters Workflow Status or Audit Fields* — SubmittedAt unchanged)
- [x] 4.14 `ExpenseService.UpdateAsync`: rejects edit when `Status` is `Approved` (*Non-Editable Expense States Are Rejected on Edit*)
- [x] 4.15 `ExpenseService.UpdateAsync`: rejects edit when `Status` is `ComplianceApproved` (*Non-Editable Expense States Are Rejected on Edit*)
- [x] 4.16 `ExpenseService.UpdateAsync`: rejects edit when `Status` is `Rejected` (*Non-Editable Expense States Are Rejected on Edit*, BR-07)
- [x] 4.17 `ExpenseService.UpdateAsync`: rejects edit when `Status` is `Cancelled` (*Non-Editable Expense States Are Rejected on Edit*)
- [x] 4.18 `ExpenseService.UpdateAsync`: rejects edit when `Status` is `Reimbursed` (*Non-Editable Expense States Are Rejected on Edit*)
- [x] 4.19 `ExpenseService.UpdateAsync`: replacing `receiptAttachmentId` with a valid new attachment succeeds; the old attachment is left unlinked, not deleted (*Attachment Replacement on Edit* — replacing succeeds)
- [x] 4.20 `ExpenseService.UpdateAsync`: resubmitting the expense's current `receiptAttachmentId` succeeds (*Attachment Replacement on Edit* — resubmitting same attachment)
- [x] 4.21 `ExpenseService.UpdateAsync`: rejects a replacement attachment already linked to a different expense (*Attachment Replacement on Edit* — already linked)
- [x] 4.22 `ExpenseService.UpdateAsync`: rejects a replacement attachment not uploaded by the caller (*Attachment Replacement on Edit* — not owned)
- [x] 4.23 `ExpenseService.UpdateAsync`: rejects a nonexistent replacement attachment (*Attachment Replacement on Edit* — nonexistent)
- [x] 4.24 `ExpenseService.CancelAsync`: owner cancels their own `Draft` — `Status` becomes `Cancelled` (*Expense Cancellation Endpoint* — cancel Draft, ADR-0010)
- [x] 4.25 `ExpenseService.CancelAsync`: owner cancels their own `Submitted` expense — `Status` becomes `Cancelled` (*Expense Cancellation Endpoint* — cancel Submitted)
- [x] 4.26 `ExpenseService.CancelAsync`: returns `NotOwner` for a non-owning caller (*Expense Cancellation Endpoint* — non-owner rejected)
- [x] 4.27 `ExpenseService.CancelAsync`: returns `ExpenseNotFound` for a nonexistent id (*Expense Cancellation Endpoint* — nonexistent expense)
- [x] 4.28 `ExpenseService.CancelAsync`: rejects cancelling an `Approved` expense (*Non-Cancellable Expense States Are Rejected on Cancel*)
- [x] 4.29 `ExpenseService.CancelAsync`: rejects cancelling a `ComplianceApproved` expense (*Non-Cancellable Expense States Are Rejected on Cancel*)
- [x] 4.30 `ExpenseService.CancelAsync`: rejects cancelling a `Rejected` expense (*Non-Cancellable Expense States Are Rejected on Cancel*)
- [x] 4.31 `ExpenseService.CancelAsync`: rejects cancelling a `Reimbursed` expense (*Non-Cancellable Expense States Are Rejected on Cancel*)
- [x] 4.32 `ExpenseService.CancelAsync`: rejects cancelling an already-`Cancelled` expense (*Non-Cancellable Expense States Are Rejected on Cancel* — no re-cancellation, FRS §7.2.4)

Integration tests (`backend/tests/...IntegrationTests`, `WebApplicationFactory`):

- [x] 4.33 `GET /api/expenses/{id}` as owner → `200` with expense data (*Single Expense Retrieval Endpoint* — owner retrieves own expense, end-to-end)
- [x] 4.34 `GET /api/expenses/{id}` as non-owner → `403 AUTHORIZATION_FAILED` (*Single Expense Retrieval Endpoint* — non-owner rejected, end-to-end)
- [x] 4.35 `GET /api/expenses/{id}` for a nonexistent id → `404 RESOURCE_NOT_FOUND` (*Single Expense Retrieval Endpoint* — nonexistent expense, end-to-end)
- [x] 4.36 `GET /api/expenses/{id}` with no `Authorization` header → `401 AUTHENTICATION_FAILED` (*Single Expense Retrieval Endpoint* — unauthenticated, end-to-end)
- [x] 4.37 `PUT /api/expenses/{id}` as owner on own `Draft` with valid fields → `200`, updated values persisted (*Expense Edit Endpoint* — owner edits Draft, end-to-end)
- [x] 4.38 `PUT /api/expenses/{id}` as owner on own `Submitted` expense with valid fields → `200`, `Status` remains `Submitted` (*Expense Edit Endpoint* — owner edits Submitted, end-to-end)
- [x] 4.39 `PUT /api/expenses/{id}` as non-owner → `403 AUTHORIZATION_FAILED` (*Expense Edit Endpoint* — non-owner rejected, end-to-end)
- [x] 4.40 `PUT /api/expenses/{id}` for a nonexistent id → `404 RESOURCE_NOT_FOUND` (*Expense Edit Endpoint* — nonexistent expense, end-to-end)
- [x] 4.41 `PUT /api/expenses/{id}` with no `Authorization` header → `401 AUTHENTICATION_FAILED` (*Expense Edit Endpoint* — unauthenticated, end-to-end)
- [x] 4.42 `PUT /api/expenses/{id}` with `status`/`submittedAt`/`approvedAt`/etc. in the request body → those values are ignored, no effect on the stored expense (*Editing Never Alters Workflow Status or Audit Fields* — client cannot set audit fields via edit)
- [x] 4.43 `PUT /api/expenses/{id}` targeting an `Approved` expense → `422 BUSINESS_RULE_VIOLATION` (*Non-Editable Expense States Are Rejected on Edit*, end-to-end representative case — the remaining 4 statuses are covered at the unit level, tasks 4.14–4.18)
- [x] 4.44 `PUT /api/expenses/{id}` with an invalid `category` → `400 VALIDATION_ERROR` with a `fields` entry for `category` (*Edit Field-Level and Business-Rule Validation* — invalid category, end-to-end)
- [x] 4.45 `PUT /api/expenses/{id}` swapping `receiptAttachmentId` to a valid new attachment → `200`, expense now references the new attachment (*Attachment Replacement on Edit* — replacing succeeds, end-to-end)
- [x] 4.46 `PUT /api/expenses/{id}` with a `receiptAttachmentId` already linked to a different expense → `422 BUSINESS_RULE_VIOLATION` (*Attachment Replacement on Edit* — already linked, end-to-end)
- [x] 4.47 `POST /api/expenses/{id}/cancel` as owner on own `Draft` → `200`, `Status = Cancelled` (*Expense Cancellation Endpoint* — cancel Draft, end-to-end, ADR-0010)
- [x] 4.48 `POST /api/expenses/{id}/cancel` as owner on own `Submitted` expense → `200`, `Status = Cancelled` (*Expense Cancellation Endpoint* — cancel Submitted, end-to-end)
- [x] 4.49 `POST /api/expenses/{id}/cancel` as non-owner → `403 AUTHORIZATION_FAILED` (*Expense Cancellation Endpoint* — non-owner rejected, end-to-end)
- [x] 4.50 `POST /api/expenses/{id}/cancel` for a nonexistent id → `404 RESOURCE_NOT_FOUND` (*Expense Cancellation Endpoint* — nonexistent expense, end-to-end)
- [x] 4.51 `POST /api/expenses/{id}/cancel` with no `Authorization` header → `401 AUTHENTICATION_FAILED` (*Expense Cancellation Endpoint* — unauthenticated, end-to-end)
- [x] 4.52 `POST /api/expenses/{id}/cancel` targeting an already-`Cancelled` expense → `422 BUSINESS_RULE_VIOLATION`, `Status` unchanged (*Non-Cancellable Expense States Are Rejected on Cancel* — no re-cancellation, end-to-end representative case — the remaining 4 statuses are covered at the unit level, tasks 4.28–4.31)

**Checkpoint:** ✅ `dotnet build` (0 errors) · ✅ `dotnet format --verify-no-changes` · ✅ `dotnet test` (228/228 passed: 140 unit incl. 38 new, 88 integration incl. 20 new)

## 5. Archive

- [x] 5.1 Re-run the full quality gate one final time: `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test` — all green (228/228)
- [x] 5.2 Run `openspec archive et008-expense-management`
- [x] 5.3 Leave `docs/TICKETS.md` ET008 `Status` as `In progress` — `/pr` sets `PR open (#N)`; `Done` is reserved for after merge
