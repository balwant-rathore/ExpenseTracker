## Context

ET007 already built `Application/Expenses/` (`ExpenseService`, `IExpenseService`,
`ExpenseFailureReason`, `ICompanyClock`, `IExpenseNumberGenerator`), `Api/Controllers/
ExpensesController.cs` (`POST ""`, `POST "{id}/submit"`), and the supporting
`IExpenseRepository`/`IAttachmentRepository` (`Infrastructure/Persistence/Repositories/`).
This ticket adds three actions to the *same* controller/service rather than a new module:
`GET {id}`, `PUT {id}`, `POST {id}/cancel`.

Constraints from `docs/SDS.md` that shape this design: status transitions only via dedicated
action endpoints, never a generic status field (§1.3, `AGENTS.md` §11); role is re-resolved
from `Employee` every request (§4.1, already available via `User.GetEmployeeId()` from
ET007's D3); state-changing operations run in a DB transaction (§11.1); audit fields are
system-managed only (§6.8).

## Goals / Non-Goals

**Goals:**
- `GET /api/expenses/{id}`, `PUT /api/expenses/{id}`, `POST /api/expenses/{id}/cancel` per
  the approved `specs/expense-maintenance/spec.md` delta.
- Edit re-validates identically to create (BR-01/02/03, category/currency/description) and
  allows swapping `receiptAttachmentId`.
- Cancel covers `Draft` and `Submitted` (ADR-0010 — see Decisions).
- Read-only enforcement (BR-04/BR-07) on edit; equivalent non-`Draft`/`Submitted` gate on
  cancel.

**Non-Goals** (per `docs/TICKETS.md` build order):
- List/search/pagination, Manager/Finance/Compliance visibility (ET009).
- Manager approve/reject, compliance actions, reimbursement (ET010–ET012).
- Notifications (ET015) — not wired to edit/cancel in this ticket.

## Decisions

### D1 — Consolidate `ExpenseCreationResult`/`ExpenseSubmitResult` into one `ExpenseResult`
Both existing result records are structurally identical: `(bool Succeeded, ExpenseResponse?
Expense, ExpenseFailureReason FailureReason)` with the same two static factories. Adding three
more identically-shaped records for `GetByIdAsync`/`UpdateAsync`/`CancelAsync` (5 copies of the
same triplet) is duplication, not the established pattern worth repeating. This design
consolidates both into a single `ExpenseResult` used by all five `IExpenseService` methods, and
deletes `ExpenseCreationResult.cs`/`ExpenseSubmitResult.cs`. This touches two files from the
already-merged ET007, but is a pure internal-DTO rename — no behavior, route, or response-body
change; `ExpensesController` never references the concrete result type by name (it only reads
`.Succeeded`/`.Expense`/`.FailureReason`), so the call sites are unaffected beyond the type name
in `IExpenseService`'s signatures. Confirmed via search: no test file references
`ExpenseCreationResult`/`ExpenseSubmitResult` by name, so this is a safe, contained rename.

Alternative rejected: leave `ExpenseCreationResult`/`ExpenseSubmitResult` as-is and add
`ExpenseDetailResult`/`ExpenseUpdateResult`/`ExpenseCancelResult`. Rejected — five files
carrying the exact same three fields and two factory methods is the premature-duplication
case the root `CLAUDE.md` tells us to avoid, once the pattern has repeated past a second copy.

### D2 — Attachment-swap conflict detection: DB unique constraint only, no pre-check query
`CreateAsync` (ET007) never pre-queries whether an attachment is already linked before insert —
it relies solely on `TryAddAsync` catching the `IX_Expenses_AttachmentId` unique-constraint
violation via `SqlException` (2601/2627), returning `ExpenseInsertOutcome.AttachmentAlreadyLinked`.
This design mirrors that exact pattern for edit: add `Task<bool> TryUpdateAsync(Expense
expense, CancellationToken)` to `IExpenseRepository`, calling `SaveChangesAsync` on the
already-tracked entity and catching the same unique-constraint violation, returning `false` on
conflict. `UpdateAsync` maps `false` → `ExpenseFailureReason.AttachmentAlreadyLinked` (same
enum value `CreateAsync` already uses), which the controller already maps to `422`.

No separate `ExistsByAttachmentIdAsync(id, excludingExpenseId)` query is needed: the update
statement only touches the row being edited, so if the new `receiptAttachmentId` already
belongs to a *different* expense, the update itself violates the unique index; if the caller
resubmits the expense's own current attachment, no other row holds that value, so the update
succeeds. This keeps the DB constraint as the single source of truth for this check, exactly
as `CreateAsync` already established, instead of introducing a second, redundant check that
could drift from the constraint's actual behavior.

Alternative rejected: pre-check with a `Query().AnyAsync(e => e.AttachmentId == id && e.Id !=
expenseId)` before saving. Rejected — duplicates a check the DB already performs atomically and
introduces a TOCTOU race window `CreateAsync`'s design deliberately avoids.

### D3 — Edit validation split: FluentValidation for field-level, service for business rules
`PUT /api/expenses/{id}` has a request body (unlike `POST .../submit`), so it follows
`CreateAsync`'s split, not `SubmitAsync`'s: a new `UpdateExpenseRequestValidator`
(FluentValidation) runs at the controller before the service is called, covering the same
field-level checks as `CreateExpenseRequestValidator` (category enum, `currency == "INR"`,
description length) — reusing its public `MaxDescriptionLength`/`RequiredCurrency` constants
rather than redefining them. `ExpenseService.UpdateAsync` then runs the business-rule checks
(BR-01 amount, BR-02 date, BR-03 attachment ownership/linkage) itself, exactly like
`CreateAsync`. `SubmitAsync`'s in-service currency/description re-checks are unaffected by this
change — `PUT` and `POST .../submit` remain two independently-validated code paths, as they
were designed in ET007.

### D4 — New failure reasons: `NotEditable` and `NotCancellable`, distinct from `NotDraft`
`SubmitAsync` already uses `ExpenseFailureReason.NotDraft` to mean "status must be exactly
`Draft`." Edit and cancel both accept a *different* set (`Draft` **or** `Submitted`), so reusing
`NotDraft` would misdescribe the actual rule and produce a wrong-sounding error message on a
`Submitted` expense. Two new enum values are added instead: `NotEditable` (edit's gate, BR-04)
and `NotCancellable` (cancel's gate, BR-05), each mapped to its own `422` message in
`ExpensesController.FailureResult`.

### D5 — Cancellation from `Draft` is an explicit, ADR-logged deviation
`docs/FRS.md` §4.3.1 and `docs/SDS.md` §6.3's state transition matrix both restrict `Cancel` to
`Submitted` only. Per `/spec` clarification with the ticket owner, this implementation extends
cancellation to `Draft` as well, following root `AGENTS.md` §9's workflow summary ("Cancellation
flow: `Draft` or `Submitted` → `Cancelled`"), on the reasoning that an employee should be able to
abandon a saved-but-never-submitted expense outright rather than have it linger forever with no
path to `Cancelled`. This is logged as **ADR-0010** (`docs/decisions/ADR-0010-draft-
cancellation-scope.md`) per this repo's OpenSpec proposal rule ("call out any deviation from
`docs/SDS.md` as an explicit open architecture decision requiring an ADR, not a silent
implementation choice") — not applied silently. `docs/FRS.md`/`docs/SDS.md` themselves are left
as historical record (per this project's established convention from ADR-0008, which similarly
noted `docs/SDS.md` was "now out of date" rather than editing it); the ADR is the authoritative
note that this implementation's actual cancel-eligibility is `Draft` or `Submitted`.

### D6 — `GetById`/`Update`/`Cancel` ownership: manual check, reusing ET007's D6 pattern
Same reasoning as `SubmitAsync`'s ownership check (ET007 D6): the `EmployeeOrManager`
`[Authorize]` policy only gates by role, not by resource ownership. All three new methods load
the expense via `IExpenseRepository.GetByIdAsync`, compare `Expense.EmployeeId` to
`employeeId` (from `User.GetEmployeeId()`), and return `ExpenseFailureReason.NotOwner` (already
mapped to `403`) on mismatch — no new authorization mechanism introduced.

## File Plan

**New — `backend/src/Application/Expenses/`:**
- `UpdateExpenseRequest.cs` — request record: `ExpenseDate`, `Category` (`string?`), `Amount`,
  `Currency` (`string?`), `Description` (`string?`), `ReceiptAttachmentId`. No `Action`/`Status`
  field — edit never transitions status (D3, `docs/SDS.md` §1.3).
- `UpdateExpenseRequestValidator.cs` — FluentValidation, same field-level rules as
  `CreateExpenseRequestValidator` (category enum, currency == INR, description ≤ 500), reusing
  its public constants (D3).
- `ExpenseResult.cs` — replaces `ExpenseCreationResult.cs`/`ExpenseSubmitResult.cs` (D1):
  `record ExpenseResult(bool Succeeded, ExpenseResponse? Expense, ExpenseFailureReason
  FailureReason)` with `Success(ExpenseResponse)`/`Failure(ExpenseFailureReason)` factories.

**Modified:**
- `backend/src/Application/Expenses/IExpenseService.cs` — `CreateAsync`/`SubmitAsync` return
  `Task<ExpenseResult>` (was `Task<ExpenseCreationResult>`/`Task<ExpenseSubmitResult>`); add
  `Task<ExpenseResult> GetByIdAsync(Guid employeeId, Guid expenseId, CancellationToken ct)`,
  `Task<ExpenseResult> UpdateAsync(Guid employeeId, Guid expenseId, UpdateExpenseRequest
  request, CancellationToken ct)`, `Task<ExpenseResult> CancelAsync(Guid employeeId, Guid
  expenseId, CancellationToken ct)`.
- `backend/src/Application/Expenses/ExpenseService.cs` — update `CreateAsync`/`SubmitAsync`
  return type; implement the three new methods per Decisions D2–D6.
- `backend/src/Application/Expenses/ExpenseFailureReason.cs` — add `NotEditable`,
  `NotCancellable` (D4).
- `backend/src/Domain/Repositories/IExpenseRepository.cs` — add `Task<bool>
  TryUpdateAsync(Expense expense, CancellationToken cancellationToken)` (D2).
- `backend/src/Infrastructure/Persistence/Repositories/ExpenseRepository.cs` — implement
  `TryUpdateAsync`, reusing the existing private `IsUniqueViolation` helper (D2).
- `backend/src/Api/Controllers/ExpensesController.cs` — inject `IValidator<UpdateExpenseRequest>`
  alongside the existing `createValidator`; add `GetById` (`[HttpGet("{id:guid}")]`), `Update`
  (`[HttpPut("{id:guid}")]`), `Cancel` (`[HttpPost("{id:guid}/cancel")]`) actions; extend
  `FailureResult`'s switch with `NotEditable`/`NotCancellable` → `422`.

**Removed:**
- `backend/src/Application/Expenses/ExpenseCreationResult.cs`,
  `backend/src/Application/Expenses/ExpenseSubmitResult.cs` (D1, replaced by `ExpenseResult.cs`).

**New — `docs/decisions/`:**
- `ADR-0010-draft-cancellation-scope.md` (D5).

## DTOs / Records

```csharp
// UpdateExpenseRequest.cs — mirrors CreateExpenseRequest minus Action; nullable string
// fields per backend/CLAUDE.md's ET007 gotcha (no `required`, no enum-typed properties,
// so a genuinely-missing JSON key binds to null/default and reaches FluentValidation
// instead of failing model binding before the error envelope applies).
public record UpdateExpenseRequest
{
    public DateOnly ExpenseDate { get; init; }
    public string? Category { get; init; }
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
    public string? Description { get; init; }
    public Guid ReceiptAttachmentId { get; init; }
}

// ExpenseResult.cs — replaces ExpenseCreationResult/ExpenseSubmitResult (D1); used by
// CreateAsync, SubmitAsync, GetByIdAsync, UpdateAsync, CancelAsync
public record ExpenseResult(bool Succeeded, ExpenseResponse? Expense, ExpenseFailureReason FailureReason)
{
    public static ExpenseResult Success(ExpenseResponse expense) => new(true, expense, ExpenseFailureReason.None);
    public static ExpenseResult Failure(ExpenseFailureReason reason) => new(false, null, reason);
}

// ExpenseFailureReason.cs — NotEditable/NotCancellable added (D4)
public enum ExpenseFailureReason
{
    None, AttachmentNotFound, AttachmentAlreadyLinked, AttachmentNotOwned,
    AmountNotPositive, ExpenseDateInFuture, ExpenseNotFound, NotOwner, NotDraft,
    CurrencyInvalid, DescriptionTooLong, NotEditable, NotCancellable,
}
```

`IExpenseService` additions:

```csharp
Task<ExpenseResult> GetByIdAsync(Guid employeeId, Guid expenseId, CancellationToken cancellationToken);
Task<ExpenseResult> UpdateAsync(Guid employeeId, Guid expenseId, UpdateExpenseRequest request, CancellationToken cancellationToken);
Task<ExpenseResult> CancelAsync(Guid employeeId, Guid expenseId, CancellationToken cancellationToken);
```

`ExpenseService` method logic (failure-reason order matters — first failing check wins):

- **`GetByIdAsync`**: `ExpenseNotFound` (not found) → `NotOwner` (`EmployeeId` mismatch) →
  `Success(Map(expense))`. No status gate — an owner may view their expense in any status.
- **`UpdateAsync`**: `ExpenseNotFound` → `NotOwner` → `NotEditable` (`Status` not in
  `{Draft, Submitted}`) → `AmountNotPositive` (BR-01) → `ExpenseDateInFuture` (BR-02) →
  `AttachmentNotFound`/`AttachmentNotOwned` (new `receiptAttachmentId`, BR-03) → mutate fields
  → `TryUpdateAsync`; `false` → `AttachmentAlreadyLinked` (D2). On success, set
  `expense.UpdatedAt = DateTime.UtcNow` only — `Status` and every workflow audit field are
  untouched.
- **`CancelAsync`**: `ExpenseNotFound` → `NotOwner` → `NotCancellable` (`Status` not in
  `{Draft, Submitted}`) → set `Status = Cancelled`, `UpdatedAt = DateTime.UtcNow`, persist via
  `IUnitOfWork.ExecuteInTransactionAsync(() => _unitOfWork.SaveChangesAsync(ct), ct)` (same
  pattern `SubmitAsync` already uses). No `CancelledAt`/`CancelledByEmployeeId` field exists in
  `docs/SDS.md` §3.6's `Expense` schema, so none is added — cancellation is recorded only via
  `Status` and `UpdatedAt`, consistent with "no schema changes" (see Impact in `proposal.md`).

Controller failure-reason → HTTP mapping additions (mirrors the existing
`ExpensesController.FailureResult` switch): `NotEditable` → `422 BUSINESS_RULE_VIOLATION`
("Only expenses in Draft or Submitted status can be edited."); `NotCancellable` → `422
BUSINESS_RULE_VIOLATION` ("Only expenses in Draft or Submitted status can be cancelled.").
`ExpenseNotFound`/`NotOwner`/`AttachmentNotFound`/`AttachmentAlreadyLinked`/
`AttachmentNotOwned`/`AmountNotPositive`/`ExpenseDateInFuture` reuse their existing mappings
unchanged. Field-level failures on edit (`category`/`currency`/`description`) go through
`UpdateExpenseRequestValidator` → the existing `ValidationErrorResult` helper → `400
VALIDATION_ERROR`, never reaching the service — identical to create.

## DB Changes

None. `ExpenseStatus.Cancelled` already exists (ET002); no new column, index, or migration.
Attachment swap only updates the existing `Expense.AttachmentId` foreign key on an already-
tracked row — no schema change.

## Reuse

Confirmed already in place, reused as-is: `IExpenseRepository`/`IAttachmentRepository`,
`EmployeeOrManager` policy, `User.GetEmployeeId()`, `IUnitOfWork.ExecuteInTransactionAsync`,
`ValidationErrorResult`/error-envelope pattern, FluentValidation assembly-scan registration
(`UpdateExpenseRequestValidator` auto-registers, no DI change needed), the `Result` +
`FailureReason` + controller-`switch` idiom, and `ExpenseRepository`'s private
`IsUniqueViolation` helper (reused by the new `TryUpdateAsync`). No new NuGet package, no new
authorization policy, no new error-handling mechanism, no `Program.cs` change (all new types
live in the already-registered `Application`/`Infrastructure` assemblies).

## Risks / Trade-offs

- **[Extending cancellation to `Draft` diverges from the literal FRS/SDS text]** → Mitigation:
  logged as ADR-0010 (D5) with explicit rationale and ticket-owner confirmation, not a silent
  choice; the spec delta (`specs/expense-maintenance/spec.md`) documents the actual implemented
  behavior so future tickets read the accurate contract, not the stale FRS/SDS wording.
- **[Consolidating `ExpenseResult` touches two files from an already-merged ticket (ET007)]** →
  Mitigation: confirmed via repo-wide search that no test or non-`Application.Expenses` file
  references the old type names directly; the change is type-name-only, no behavior differs,
  and existing `ExpenseSubmissionTests.cs`/`ExpenseServiceTests.cs` assert on response bodies
  and result properties (`.Succeeded`, `.Expense`, `.FailureReason`), not on the concrete result
  type, so they require no edits.
- **[Relying solely on the DB unique constraint for attachment-swap conflicts (D2) means the
  failure is only detected at `SaveChangesAsync` time, not earlier]** → Mitigation: this is the
  same trade-off `CreateAsync`/`TryAddAsync` already accepted for the identical check; no new
  risk is introduced, and it avoids a second, potentially-inconsistent check path.

## Migration Plan

1. Add `NotEditable`/`NotCancellable` to `ExpenseFailureReason.cs` (D4).
2. Add `ExpenseResult.cs`; delete `ExpenseCreationResult.cs`/`ExpenseSubmitResult.cs`; update
   `IExpenseService.cs`/`ExpenseService.cs` signatures for `CreateAsync`/`SubmitAsync` (D1).
3. Add `UpdateExpenseRequest.cs`/`UpdateExpenseRequestValidator.cs` (D3).
4. Add `IExpenseRepository.TryUpdateAsync`/`ExpenseRepository.TryUpdateAsync` (D2).
5. Implement `ExpenseService.GetByIdAsync`/`UpdateAsync`/`CancelAsync` (D2–D6).
6. Add the three new `ExpensesController` actions and extend `FailureResult` (D4).
7. Write ADR-0010 (D5).

Rollback: revert the commit(s); no data migration exists to unwind (no schema change).

## Build/Test/Lint Checkpoints

Run after each numbered step above, in this order, stopping at the first failure
(`backend/CLAUDE.md`, root `CLAUDE.md` Quality Gates):

1. `dotnet build` (from `backend/`) — after every file change.
2. `dotnet test --filter FullyQualifiedName~UnitTests` — unit tests for
   `UpdateExpenseRequestValidator` (field-level rules), `ExpenseService` (`GetByIdAsync`/
   `UpdateAsync`/`CancelAsync` — ownership, status-gate, BR-01/02/03 branches, attachment-swap
   success/conflict), and the renamed `ExpenseResult` usages in existing `ExpenseServiceTests`.
3. `dotnet test --filter FullyQualifiedName~IntegrationTests` — `WebApplicationFactory` tests
   for `GET/PUT /api/expenses/{id}` and `POST /api/expenses/{id}/cancel` covering every scenario
   in `specs/expense-maintenance/spec.md`.
4. `dotnet test` (full suite) — confirms no regression in the existing
   `ExpenseSubmissionTests`/`ExpenseServiceTests` from the `ExpenseResult` consolidation (D1).

No frontend or E2E gate applies — this ticket is backend-only (no `frontend/` changes, per
`docs/TICKETS.md` ET008 scope).

## Open Questions

None outstanding — the four ambiguities raised during `/spec` (single-expense GET ownership,
Draft-cancellation scope, attachment mutability on edit, edit validation strictness) were
resolved with the ticket owner and are recorded in `proposal.md` and ADR-0010 above.
