## Context

ET007–ET009 already built `Expense` create/edit/cancel/view. The `Application.Expenses`
namespace follows an established, consistent shape:

- `IExpenseService`/`ExpenseService` — one method per action, returning a uniform
  `ExpenseResult` (never throwing for business-rule/ownership/not-found failures).
- `ExpenseFailureReason` enum — one member per distinguishable failure; the controller's
  `FailureResult` switch is the **single** place that maps a reason to an HTTP status +
  error envelope.
- `IExpenseRepository`/`ExpenseRepository` — `IQueryable` composition via
  `Expression<Func<Expense,bool>>` predicates (see `ExpenseVisibility.BuildPredicate`,
  used both for the list endpoint and, compiled, for single-item visibility checks).
- Request DTOs are plain `record`s with nullable/primitive members only (never `required`,
  never a real enum) — the `backend/CLAUDE.md` "Gotchas" rule, because `required` or an enum
  on a bound DTO bypasses the error envelope at deserialization time.
- FluentValidation validators are manually invoked in the controller (`ValidateAsync`),
  auto-discovered into DI via `AddValidatorsFromAssemblyContaining<...>`.
- Integration tests use `AttachmentFunctionalWebApplicationFactory`, a
  `SetExpenseStatusAsync` DB-direct helper to seed a starting `Status` without going
  through the API, and `CreateAuthorizedClientAsync(role, managerId)` for role-scoped
  callers.

This design implements ET010 (`docs/FRS.md` §5.1) entirely by extending these existing
patterns — no new architectural pattern is introduced. The one genuinely new piece is the
**skip-level manager check**: no code today walks the `Employee.Manager` chain two hops
(`ExpenseVisibility.BuildPredicate`'s `Manager` case only checks
`e.Employee.ManagerId == employeeId`, one hop).

## Goals / Non-Goals

**Goals:**
- `POST /api/expenses/{id}/approve` and `POST /api/expenses/{id}/reject`, matching the
  existing controller/service/repository layering exactly.
- BR-06 self-review restriction (blocks both Approve and Reject on a Manager's own
  expense) plus the skip-level escalation path for a Manager-owned expense.
- Mandatory, validated `rejectionComment`.
- `status` query filter added to the existing `GET /api/expenses` pipeline.
- A no-op `INotificationService` interface + implementation + a call site in
  `ExpenseService`, so ET015 only has to swap the DI registration.

**Non-Goals:**
- No real notification behavior (HTML log, templates, recipient resolution) — that is
  ET015's entire scope. This ticket only adds the interface and a call site that does
  nothing observable.
- No Compliance Officer workflow (`compliance-approve`/`compliance-reject`) — that is
  ET011.
- No frontend work — ET018 (Review UI) is a separate, later ticket.
- No schema/migration changes — every field ET010 touches
  (`ApprovedAt`/`ApprovedByEmployeeId`/`RejectedAt`/`RejectedByEmployeeId`/
  `RejectionComment`) already exists on `Expense` per `docs/SDS.md` §3.6 (ET002).

## Decisions

### D1 — Manager-relationship check lives in the service layer, not the ASP.NET Core policy
`[Authorize(Policy = AuthorizationPolicyNames.Manager)]` gates the *role* (any Manager can
reach the action). Whether *this* Manager is authorized for *this* expense (direct
manager, or skip-level for a Manager-owned expense, and never the expense's own owner) is
a business rule, checked inside `ExpenseService`, exactly mirroring how `NotOwner`
(Submit/Cancel/Update) and `NotVisible` (GetById) already separate "right role" from
"right relationship to this specific resource." **Alternative considered**: a custom
`IAuthorizationHandler` with a resource-based requirement. Rejected — it would introduce a
second authorization code path (policy handlers) alongside the existing service-layer
`ExpenseFailureReason` pattern for the exact same class of check the codebase already does
one way; consistency wins over marginally more idiomatic ASP.NET Core authorization.

### D2 — No second hop needed: `Expense.Employee.ManagerId` already covers the "skip-level" case
**Corrected during `/implement`** (the original draft of this decision was wrong — see
below). `Employee.ManagerId` always means "who this person reports to," regardless of
whether that person is an `Employee` or a `Manager`. When the expense owner is an ordinary
`Employee`, `expense.Employee.ManagerId` is that employee's direct manager — the normal
case. When the expense owner is itself a `Manager` who cannot approve their own expense
(BR-06), `expense.Employee.ManagerId` is *that Manager's own* reporting manager — which is
exactly the "skip-level" approver the proposal calls for. Both cases are the same single
field read off the already-loaded `expense.Employee` navigation; no second hop
(`Employee.Manager.ManagerId`) is needed, and `GetByIdWithEmployeeAsync`'s existing
`.Include(e => e.Employee)` (unchanged) is sufficient.

**Original draft's mistake, and why it was rejected**: an earlier version of this decision
proposed reading `expense.Employee.Manager?.ManagerId` (i.e., the owner's manager's
manager) as a *third* fallback branch, and added a `.ThenInclude(emp => emp.Manager)` to
support it. That is a real bug, not just unneeded complexity: it would authorize an
indirect (grandparent) manager to approve/reject an *ordinary* employee's expense too
(e.g. Alice→Bob→Carol: Carol could act on Alice's expense), directly contradicting the
"direct reports only, no indirect reports" rule the `expense-visibility` capability (ET009)
already establishes and that this ticket's own spec repeats. It was caught while writing
the integration tests for the skip-level scenario, before merge, and removed along with
the now-unnecessary `.ThenInclude`.

### D3 — Authorization check shape
```csharp
// BR-06: a Manager can never review their own expense. Otherwise a Manager reviews an
// expense iff they are the owner's ManagerId - see D2 for why no second hop is needed.
private static bool CanReview(Expense expense, Guid managerId)
{
    if (expense.EmployeeId == managerId)
    {
        return false;
    }

    return expense.Employee.ManagerId == managerId;
}
```
Both `ApproveAsync` and `RejectAsync` call this identically (via a shared private helper
on `ExpenseService`, not duplicated inline) — the "handled identically" guardrail in
`AGENTS.md` §13 applies directly here since Approve and Reject share the exact same
authorization rule.

### D4 — New `ExpenseFailureReason` members
Add `NotSubmitted` (expense `Status` is not `Submitted` at approve/reject time) and
`NotAuthorizedReviewer` (caller fails the `CanReview` check above — distinct from
`NotOwner`, which means "not *your own* resource"; here it means "not a reviewer *of*
this resource," the inverse relationship). Reuse the existing `RESOURCE_NOT_FOUND`/`404`
arm for `ExpenseNotFound` (already present). `rejectionComment` validation failures use
the existing `400 VALIDATION_ERROR` path via FluentValidation — no new failure reason
needed for that, following the `CreateExpenseRequestValidator` pattern exactly.

### D5 — `RejectExpenseRequest` and its validator
New file, mirroring `CreateExpenseRequestValidator`'s style:
```csharp
// backend/src/Application/Expenses/RejectExpenseRequest.cs
namespace Application.Expenses;

public record RejectExpenseRequest
{
    public string? RejectionComment { get; init; }
}
```
```csharp
// backend/src/Application/Expenses/RejectExpenseRequestValidator.cs
namespace Application.Expenses;

public class RejectExpenseRequestValidator : AbstractValidator<RejectExpenseRequest>
{
    public const int MaxRejectionCommentLength = 500;

    public RejectExpenseRequestValidator()
    {
        RuleFor(x => x.RejectionComment)
            .NotEmpty()
            .WithMessage("Rejection comment is required.")
            .MaximumLength(MaxRejectionCommentLength)
            .WithMessage("Rejection comment must not exceed 500 characters.");
    }
}
```
FluentValidation's `NotEmpty()` already rejects null, empty, and whitespace-only strings —
matching the `manager-expense-review` spec's "non-empty after trimming whitespace"
requirement with no extra code. **Note**: `ExpenseConfiguration` currently caps the
`RejectionComment` **column** at 1000 chars (per the codebase survey) while the spec caps
the **request** at 500 — 500 ≤ 1000 so no migration is needed; the validator is simply
stricter than the column, which is safe. This gap between column max-length (1000) and the
FRS-aligned request validation (500) is called out here rather than silently reconciled —
if the ticket owner intended the column to match 500, that would need its own migration and
is flagged as an open question below rather than silently changed.

### D6 — `status` filter added as a repository parameter, not a second predicate
`IExpenseRepository.GetPagedAsync` gains one new optional parameter,
`ExpenseStatus? statusFilter`, applied as an additional `.Where(e => e.Status ==
statusFilter.Value)` immediately after the visibility predicate — matching the existing
explicit-positional-parameter style of that method (no builder/spec pattern in this
codebase to extend instead). `ExpenseListRequest` gains `public string? Status { get;
init; }`; `ExpenseListRequestValidator` gains a `RuleFor(x => x.Status).Must(s => s is null
|| Enum.TryParse<ExpenseStatus>(s, out _))`, the same `Enum.TryParse<T>` idiom already used
for `Category`/`Action` in `CreateExpenseRequestValidator`.

### D7 — No-op notification service
```csharp
// backend/src/Application/Notifications/NotificationEvent.cs
namespace Application.Notifications;

public enum NotificationEvent { Submitted, Approved, Rejected, Reimbursed }
```
```csharp
// backend/src/Application/Notifications/INotificationService.cs
namespace Application.Notifications;

public interface INotificationService
{
    Task NotifyAsync(NotificationEvent notificationEvent, Expense expense, CancellationToken cancellationToken);
}
```
```csharp
// backend/src/Application/Notifications/NoOpNotificationService.cs
namespace Application.Notifications;

public class NoOpNotificationService : INotificationService
{
    public Task NotifyAsync(NotificationEvent notificationEvent, Expense expense, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```
Registered in `ExpenseServiceCollectionExtensions.AddExpenseFoundation`:
`services.AddScoped<INotificationService, NoOpNotificationService>();`. `ExpenseService`
constructor takes `INotificationService _notificationService` as a new dependency;
`ApproveAsync`/`RejectAsync` call `await _notificationService.NotifyAsync(NotificationEvent.Approved
/* or .Rejected */, expense, cancellationToken)` **after** `SaveChangesAsync` returns
successfully, never inside `ExecuteInTransactionAsync`, and its result is never awaited
into a failure path (SDS §7.6: notification failure must never roll back or block the
workflow response). Since the no-op body can never throw, no explicit try/catch is added
in ET010 — ET015 is responsible for adding failure isolation (e.g. try/catch + log) once a
real implementation can actually fail. This is called out explicitly so ET015 doesn't
assume try/catch already exists here.

**Placement rationale (corrected during `/implement`)**: `Infrastructure`'s `.csproj` only
references `Domain` and `Shared` — it has no `ProjectReference` to `Application`, so an
`Infrastructure`-hosted class cannot implement an `Application`-defined interface without
adding a new cross-project reference. The codebase's actual, observed precedent for a
dependency-free "service" (no DB/file access) is `ICompanyClock`/`CompanyClock` and
`IExpenseNumberGenerator`/`ExpenseNumberGenerator`, which both live together in
`Application/Expenses/` and are registered directly in
`ExpenseServiceCollectionExtensions` — no `Infrastructure` involvement. Since the no-op
notification stub does no I/O at all, it follows that same precedent: `NotificationEvent`,
`INotificationService`, and `NoOpNotificationService` all live in a new
`Application/Notifications/` folder, not split across `Application`/`Infrastructure` as
originally drafted here. ET015, when it builds the real HTML-log-backed implementation
(which *does* need file I/O and therefore *does* belong in `Infrastructure`), will need to
add the `Infrastructure` → `Application` project reference at that point — this is called
out so ET015 doesn't assume it already exists.

### D8 — `ExpenseResponse` gains rejection/approval fields
`ExpenseResponse` currently exposes none of the workflow audit fields beyond
`SubmittedAt`. Add `ApprovedAt`, `RejectedAt`, `RejectionComment` (string?) so a caller can
see the outcome of the approve/reject action in the `200` response body — without this,
`Reject`'s `200 OK` response would carry no visible trace of the very comment the caller
just submitted. `ApprovedByEmployeeId`/`RejectedByEmployeeId` are **not** added to the
response — no spec scenario or FRS line requires surfacing internal `EmployeeId` GUIDs to
the caller, and `ExpenseResponse` already resolves `EmployeeName` rather than raw IDs
elsewhere, so the same convention (name over ID) would apply if reviewer identity is later
requested — deferred as an open question below rather than guessed at.

## Risks / Trade-offs

- **[Risk]** `NotAuthorizedReviewer` and `NotOwner`/`NotVisible` are conceptually similar
  (both are 403s meaning "wrong relationship to this resource") — a future maintainer could
  conflate them or reuse the wrong one. → **Mitigation**: distinct enum member with an
  explicit doc-comment referencing this design's D3/D4, plus a dedicated integration test
  per scenario in the spec delta so behavior, not just naming, is pinned down.
- **[Risk]** Adding a constructor parameter (`INotificationService`) to `ExpenseService`
  changes its dependency graph; any existing unit test that manually `new ExpenseService(...)`
  rather than resolving through DI will fail to compile until updated.
  → **Mitigation**: flagged as a task-list item ("update ExpenseServiceTests constructor
  calls") so `/tasks` schedules it explicitly rather than it surfacing as a surprise build
  break.
- **[Trade-off]** The `status` filter (D6) is scoped as a single-value equality filter, not
  a multi-value list (e.g. `?status=Submitted,Approved`) — matches the spec's exact
  scenarios (all single-value) and FRS §5.1.1's literal ask ("view Submitted expenses").
  Multi-value filtering, if ever needed, is a future addition, not built speculatively here
  (per `AGENTS.md`'s no-speculative-abstraction guidance).

## Migration Plan

No EF Core migration is required — no entity/column changes. Deployment is a standard
API release: new endpoints are purely additive (no existing endpoint's contract changes
except `GET /api/expenses` gaining an *optional* query parameter, which is backward
compatible — omitting `status` reproduces current behavior exactly, per the
`expense-visibility` spec delta's "No status filter behaves as before" scenario).
Rollback is a standard revert-the-deploy; no data migration to undo.

## Open Questions

- Should `ApprovedByEmployeeId`/`RejectedByEmployeeId` (or their resolved employee names)
  be surfaced in `ExpenseResponse`? Deferred per D8 — no current spec scenario requires it.
  Flag for the ticket owner if the Review UI ticket (ET018) turns out to need "approved by"
  displayed.
- `ExpenseConfiguration`'s `RejectionComment` column is capped at 1000 chars while this
  design's validator caps the request at 500 (D5). If the intent was for the column to
  match the FRS-driven 500 exactly, that's a separate migration outside this ticket's
  scope — left as-is (validator stricter than column) unless the ticket owner says
  otherwise.

## File Changes

**New files:**
- `backend/src/Application/Expenses/RejectExpenseRequest.cs`
- `backend/src/Application/Expenses/RejectExpenseRequestValidator.cs`
- `backend/src/Application/Notifications/NotificationEvent.cs`
- `backend/src/Application/Notifications/INotificationService.cs`
- `backend/src/Application/Notifications/NoOpNotificationService.cs`
- `backend/tests/IntegrationTests/ExpenseApprovalTests.cs`
- `backend/tests/UnitTests/Application/Expenses/ExpenseApprovalServiceTests.cs` (or added to existing `ExpenseServiceTests.cs` — final placement decided at `/tasks`)

**Modified files:**
- `backend/src/Application/Expenses/IExpenseService.cs` — add `ApproveAsync`, `RejectAsync`
- `backend/src/Application/Expenses/ExpenseService.cs` — add `ApproveAsync`, `RejectAsync`,
  shared `CanReview` helper, `INotificationService` constructor dependency, notification
  calls post-commit
- `backend/src/Application/Expenses/ExpenseFailureReason.cs` — add `NotSubmitted`,
  `NotAuthorizedReviewer`
- `backend/src/Application/Expenses/ExpenseResponse.cs` — add `ApprovedAt`, `RejectedAt`,
  `RejectionComment`
- `backend/src/Application/Expenses/ExpenseListRequest.cs` — add `Status`
- `backend/src/Application/Expenses/ExpenseListRequestValidator.cs` — validate `Status`
- `backend/src/Api/Controllers/ExpensesController.cs` — add `Approve`, `Reject` actions;
  inject `IValidator<RejectExpenseRequest>`; extend `FailureResult` switch
- `backend/src/Api/Authorization/AuthorizationPolicyNames.cs` — no change expected
  (`Manager` policy already exists); confirm during implementation
- `backend/src/Infrastructure/Persistence/Repositories/ExpenseRepository.cs` — add
  `statusFilter` param to `GetPagedAsync`; `GetByIdWithEmployeeAsync`'s existing
  `.Include(e => e.Employee)` is unchanged (see D2 — no second hop is needed)
- `backend/src/Api/Extensions/ExpenseServiceCollectionExtensions.cs` — register
  `INotificationService`
- `backend/tests/UnitTests/Application/Expenses/ExpenseServiceTests.cs` — update
  constructor calls for the new `INotificationService` dependency
- `backend/tests/IntegrationTests/ExpenseVisibilityTests.cs` — add `status=` filter cases
- `docs/decisions/` — new ADR(s) for: BR-06 scope (Approve+Reject), skip-level escalation
  path, `status` filter addition (per proposal's Impact section)

## Reuse Summary

Everything below is reused as-is, with no interface changes: `IExpenseRepository.GetByIdAsync`
lookup pattern, `IUnitOfWork.ExecuteInTransactionAsync` transaction wrapping,
`ExpenseResult`/`Map(expense)` result shape, `AuthorizationPolicyNames.Manager` (already
registered), `EnvelopeAuthorizationMiddlewareResultHandler` (role-gate 403s),
`ClaimsPrincipalExtensions.GetEmployeeId()`, `ExpensesController.FailureResult`/`ValidationErrorResult`
helpers, `CreateAuthorizedClientAsync`/`SetExpenseStatusAsync` test helpers.

## Build + Test Checkpoints

Run from `backend/`, in this order, stopping at the first failure (per `AGENTS.md` §Quality
Gates — E2E is not applicable, this ticket has no user-facing frontend surface yet):

```bash
dotnet build                                              # 1. lint/analyzers surface here
dotnet test --filter FullyQualifiedName~UnitTests          # 2. unit
dotnet test --filter FullyQualifiedName~IntegrationTests   # 3. integration
```
