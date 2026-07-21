## Context

ET010 established the full pattern this ticket reuses: `ExpenseResult`/`ExpenseFailureReason`
for service-layer outcomes never thrown as exceptions, a controller `FailureResult` switch as
the single reason→HTTP mapping point, `IUnitOfWork.ExecuteInTransactionAsync` wrapping the
mutation, and a post-commit `INotificationService.NotifyAsync` call (placeholder, no-op,
ET015 wires the real implementation). ET009 already fully specified and implemented Compliance
Officer read visibility (`ExpenseVisibility.BuildPredicate` — `ClientEntertainment` category
`Approved`/`ComplianceApproved` only). ET011 adds only the two write endpoints; no new
architectural pattern, no schema migration, no new external dependency.

Reused as-is, unmodified:
- `Application/Expenses/RejectExpenseRequest.cs` + `RejectExpenseRequestValidator.cs` — the
  compliance-reject endpoint binds the exact same request type. No new DTO.
- `Api/Authorization/AuthorizationPolicyNames.ComplianceOfficer` — already registered in ET003.
- `IUnitOfWork`, `ICompanyClock` (not needed here — no date-in-future check applies to a
  review action), `IExpenseRepository.GetByIdWithEmployeeAsync` — not actually needed either,
  since (unlike Manager review) no `Employee.ManagerId` hop is required; `GetByIdAsync` (no
  `Employee` include) is sufficient and cheaper.

## Goals / Non-Goals

**Goals:**
- `POST /api/expenses/{id}/compliance-approve` and `POST /api/expenses/{id}/compliance-reject`,
  gated to `ComplianceOfficer`, enforcing category + status preconditions independently.
- Populate `ComplianceApprovedAt`/`ComplianceApprovedByEmployeeId` (approve) and
  `RejectedAt`/`RejectedByEmployeeId`/`RejectionComment` (reject, shared fields with Manager
  rejection — one terminal `Rejected` meaning regardless of reviewer role).
- Surface `ComplianceApprovedAt` on `ExpenseResponse` (currently absent) so a caller can
  observe the transition, matching the existing pattern of exposing `*At` timestamps but not
  `*ByEmployeeId` fields (see `ApprovedAt` without `ApprovedByEmployeeId` today).
- Extend `NotificationEvent` with `ComplianceApproved`/`ComplianceRejected`.

**Non-Goals:**
- No ownership/hierarchy authorization check (unlike `ExpenseService.CanReview` for Managers)
  — confirmed during `/spec`: a single Compliance Officer role exists, so role-policy gating
  at the controller is the entire authorization check.
- No self-review guard — confirmed during `/spec` as unattainable (only `Employee`/`Manager`
  can create expenses per `docs/SDS.md` §6.4), so no dead branch is added.
- No changes to `expense-visibility`/`ExpenseVisibility.BuildPredicate` — ET009 already covers
  Compliance Officer read visibility completely.
- No EF Core migration — all touched columns already exist on `Expense`.
- No frontend work (out of scope per `docs/TICKETS.md` — ET018 covers review screens).

## Decisions

### D1: Two independent business-rule checks, not one combined check
`CanComplianceReview`-equivalent logic is deliberately two separate checks — category first,
then status — each returning its own `ExpenseFailureReason` (`NotClientEntertainment`,
`NotApprovedForCompliance`). Rationale: the spec's "Client Entertainment Category Restriction"
and "Approved-Status Precondition" requirements are written as independently-scenario-tested
rules (e.g. a `Travel`/`Approved` expense must fail on category even though its status is
otherwise fine) — collapsing them into one boolean would lose that distinction and make it
impossible to write a test that isolates one axis from the other, contradicting AGENTS.md §13's
"checklist, not a phrase" guardrail.

### D2: No `Employee` navigation load needed
`ApproveAsync`/`RejectAsync` (Manager) call `GetByIdWithEmployeeAsync` because `CanReview` needs
`expense.Employee.ManagerId`. The compliance methods have no such dependency — they call the
existing `GetByIdAsync` instead (no `Include(e => e.Employee)`), avoiding an unnecessary join.
`ExpenseResponse.Map`'s `employeeName` derivation already null-guards on `expense.Employee`
being unset, so this is safe (verified against current `Map` implementation, which does
`expense.Employee is not null ? ... : null`).

### D3: `ExpenseResponse` gains `ComplianceApprovedAt` only, not `ComplianceApprovedByEmployeeId`
Matches the existing, established pattern: `ApprovedAt` is exposed, `ApprovedByEmployeeId` is
not; `RejectedAt` is exposed, `RejectedByEmployeeId` is not. `ComplianceApprovedAt` is added in
the same position audit timestamps already occupy (after `ApprovedAt`, before `RejectedAt`, per
the entity's own field order in `docs/SDS.md` §3.6). This is a positional record — every
existing call site constructing `ExpenseResponse` (only `ExpenseService.Map`) is updated in the
same change; there is exactly one such call site.

### D4: Reuse `RejectExpenseRequest`/`RejectExpenseRequestValidator` verbatim
Confirmed during `/spec`: identical validation rules (required, non-empty after trim, ≤500
chars). Introducing a second, structurally-identical DTO (`ComplianceRejectExpenseRequest`)
would be pure duplication with no behavioral difference — the existing type is reused directly
in the new controller action and the new service method's signature.

### D5: `NotificationEvent` gains two new values, existing values untouched
`ComplianceApproved`/`ComplianceRejected` appended to the enum (order: `Submitted, Approved,
Rejected, Reimbursed, ComplianceApproved, ComplianceRejected` — appending rather than inserting
avoids renumbering existing values, relevant if this enum is ever persisted/serialized by
ordinal elsewhere; confirmed it currently is not, but appending is strictly safer regardless).

## File Changes

**New:**
- None — no new files. (`RejectExpenseRequest`/`RejectExpenseRequestValidator` are reused, not
  duplicated; see D4.)

**Modified:**
- `backend/src/Application/Expenses/ExpenseFailureReason.cs` — add `NotClientEntertainment`,
  `NotApprovedForCompliance`.
- `backend/src/Application/Expenses/IExpenseService.cs` — add:
  ```csharp
  Task<ExpenseResult> ComplianceApproveAsync(Guid complianceOfficerId, Guid expenseId, CancellationToken cancellationToken);
  Task<ExpenseResult> ComplianceRejectAsync(Guid complianceOfficerId, Guid expenseId, RejectExpenseRequest request, CancellationToken cancellationToken);
  ```
- `backend/src/Application/Expenses/ExpenseService.cs` — add `ComplianceApproveAsync`,
  `ComplianceRejectAsync`, and a private `CanComplianceReview` helper (returns
  `ExpenseFailureReason?` so it can distinguish the two independent failure reasons, unlike
  Manager's boolean `CanReview`):
  ```csharp
  public async Task<ExpenseResult> ComplianceApproveAsync(Guid complianceOfficerId, Guid expenseId, CancellationToken cancellationToken)
  {
      var expense = await _expenseRepository.GetByIdAsync(expenseId, cancellationToken);
      if (expense is null) return ExpenseResult.Failure(ExpenseFailureReason.ExpenseNotFound);

      var reviewFailure = CanComplianceReview(expense);
      if (reviewFailure is not null) return ExpenseResult.Failure(reviewFailure.Value);

      var now = DateTime.UtcNow;
      expense.Status = ExpenseStatus.ComplianceApproved;
      expense.ComplianceApprovedAt = now;
      expense.ComplianceApprovedByEmployeeId = complianceOfficerId;
      expense.UpdatedAt = now;

      await _unitOfWork.ExecuteInTransactionAsync(
          () => _unitOfWork.SaveChangesAsync(cancellationToken), cancellationToken);
      await _notificationService.NotifyAsync(NotificationEvent.ComplianceApproved, expense, cancellationToken);

      return ExpenseResult.Success(Map(expense));
  }
  // ComplianceRejectAsync mirrors this: same not-found/category/status guards, then sets
  // Status = Rejected, RejectedAt, RejectedByEmployeeId, RejectionComment = request.RejectionComment!,
  // notifies ComplianceRejected.

  // Category check first, then status — see Decision D1. Returns null when both pass.
  private static ExpenseFailureReason? CanComplianceReview(Expense expense)
  {
      if (expense.Category != ExpenseCategory.ClientEntertainment)
      {
          return ExpenseFailureReason.NotClientEntertainment;
      }

      if (expense.Status != ExpenseStatus.Approved)
      {
          return ExpenseFailureReason.NotApprovedForCompliance;
      }

      return null;
  }
  ```
  Also update `Map(Expense)` to pass `expense.ComplianceApprovedAt` into the `ExpenseResponse`
  constructor (see D3).
- `backend/src/Application/Expenses/ExpenseResponse.cs` — insert `DateTime?
  ComplianceApprovedAt` after `ApprovedAt`, before `RejectedAt` (positional record — see D3).
- `backend/src/Application/Notifications/NotificationEvent.cs` — append `ComplianceApproved`,
  `ComplianceRejected` (see D5).
- `backend/src/Api/Controllers/ExpensesController.cs`:
  ```csharp
  [HttpPost("{id:guid}/compliance-approve")]
  [Authorize(Policy = AuthorizationPolicyNames.ComplianceOfficer)]
  public async Task<IActionResult> ComplianceApprove(Guid id, CancellationToken cancellationToken)
  { /* same shape as Approve() — calls _expenseService.ComplianceApproveAsync */ }

  [HttpPost("{id:guid}/compliance-reject")]
  [Authorize(Policy = AuthorizationPolicyNames.ComplianceOfficer)]
  public async Task<IActionResult> ComplianceReject(Guid id, RejectExpenseRequest request, CancellationToken cancellationToken)
  { /* same shape as Reject() — validates with the existing _rejectValidator, calls ComplianceRejectAsync */ }
  ```
  Extend the `FailureResult` switch with `NotClientEntertainment` → `422
  BUSINESS_RULE_VIOLATION` ("Only Client Entertainment expenses can be reviewed by
  Compliance.") and `NotApprovedForCompliance` → `422 BUSINESS_RULE_VIOLATION` ("Only expenses
  in Approved status can be compliance-reviewed.").

**Tests (new):**
- `backend/tests/UnitTests/Application/Expenses/ExpenseServiceTests.cs` — add unit tests for
  `ComplianceApproveAsync`/`ComplianceRejectAsync`: success path (both), not-found, wrong
  category (non-ClientEntertainment, any status), wrong status (ClientEntertainment but not
  Approved — Draft/Submitted/ComplianceApproved/Rejected/Cancelled/Reimbursed individually per
  AGENTS.md §13's "enumerate every item" rule), audit field population, notification fired
  with the correct new event, `FakeNotificationService` assertions.
- `backend/tests/IntegrationTests/ExpenseComplianceReviewTests.cs` (new file, following the
  same self-contained-helpers convention as `ExpenseApprovalTests.cs`/
  `ExpenseVisibilityTests.cs`) — covers every spec scenario: role gating (200 for
  ComplianceOfficer, 403 for Employee/Manager/Finance), 404, 401, category guard, status guard
  (each of the 6 non-Approved statuses), rejection comment validation (missing/whitespace/
  oversized), terminal-Rejected (a Rejected expense can't later be compliance-approved), audit
  fields. Local helpers duplicate `CreateAuthorizedClientAsync`/`UploadAttachmentAsync`/
  `SetExpenseStatusAsync` from `ExpenseApprovalTests.cs`, with `CreateExpenseBody` parameterized
  on `category` (existing helper hardcodes `"Travel"`; this file needs both `"Travel"` for the
  wrong-category scenarios and `"ClientEntertainment"` for everything else) instead of copying
  the fixed-`"Travel"` version verbatim.

## Risks / Trade-offs

- **[Risk]** Two independent 422 checks (category, then status) means a `Travel`/`Submitted`
  expense reports `NotClientEntertainment`, never surfacing the also-true status problem. →
  **Mitigation**: acceptable and intentional — category is checked first because it's the more
  fundamental eligibility gate (an expense that's never reviewable by Compliance shouldn't
  report a status-shaped error), and every spec scenario pins down category-only and
  status-only cases separately, so behavior is fully test-defined either way.
- **[Risk]** `ComplianceApprovedAt` is a new positional field inserted into `ExpenseResponse`,
  a positional record — a future contributor adding another field in the wrong position could
  silently misalign constructor args at any of the (currently one) call sites. →
  **Mitigation**: this ticket updates the one existing call site (`ExpenseService.Map`) in the
  same change (AGENTS.md §13); no other call sites exist today per a repo-wide search.

## Backend Checkpoints

```bash
dotnet build                          # 0 errors, 0 warnings
dotnet format --verify-no-changes
dotnet test --filter FullyQualifiedName~UnitTests
dotnet test --filter FullyQualifiedName~IntegrationTests
```

No frontend changes in this ticket — frontend checkpoint commands don't apply.
