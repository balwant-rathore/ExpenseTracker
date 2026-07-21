**Scope note**: ET010 is backend-only (`docs/TICKETS.md` Domain = Workflow). The Review UI
that consumes these endpoints is ET018 (Planned, separate ticket) — no frontend tasks are
scheduled here.

## 1. Foundation: Contracts, DTOs, and Failure Reasons

- [x] 1.1 Add `RejectExpenseRequest` record (`backend/src/Application/Expenses/RejectExpenseRequest.cs`) — `RejectionComment` as nullable `string?`, no `required` modifier (design.md D5)
- [x] 1.2 Add `RejectExpenseRequestValidator` (`backend/src/Application/Expenses/RejectExpenseRequestValidator.cs`) — `NotEmpty()` + `MaximumLength(500)` on `RejectionComment`, mirroring `CreateExpenseRequestValidator`
- [x] 1.3 Add `NotSubmitted` and `NotAuthorizedReviewer` members to `ExpenseFailureReason` (`backend/src/Application/Expenses/ExpenseFailureReason.cs`)
- [x] 1.4 Extend `ExpenseResponse` (`backend/src/Application/Expenses/ExpenseResponse.cs`) with `ApprovedAt`, `RejectedAt`, `RejectionComment`; update the `Map(expense)` mapping in `ExpenseService`
- [x] 1.5 Add `Status` property (`string?`) to `ExpenseListRequest` (`backend/src/Application/Expenses/ExpenseListRequest.cs`)
- [x] 1.6 Add a `Status` validation rule to `ExpenseListRequestValidator` (`Enum.TryParse<ExpenseStatus>`, null-permitted) — mirrors the `Category`/`Action` `Enum.TryParse` pattern in `CreateExpenseRequestValidator`
- [x] 1.7 Add `INotificationService` interface + `NotificationEvent` enum (`backend/src/Application/Notifications/INotificationService.cs`, design.md D7)
- [x] 1.8 Add `NoOpNotificationService` (`backend/src/Infrastructure/Notifications/NoOpNotificationService.cs`)
- [x] 1.9 Confirm no EF Core migration is required — `ApprovedAt`/`ApprovedByEmployeeId`/`RejectedAt`/`RejectedByEmployeeId`/`RejectionComment` already exist on `Expense` per `docs/SDS.md` §3.6 (ET002); run `dotnet ef migrations list --project src/Infrastructure --startup-project src/Api` and verify no pending model changes are detected

**Checkpoint 1**: `dotnet build` → 0 errors (new types compile in isolation before wiring).

## 2. Core Implementation: Backend Workflow Logic

- [x] 2.1 Add a shared `CanReview(Expense expense, Guid managerId)` private helper to `ExpenseService` implementing BR-06 self-block + direct-manager + skip-level checks (design.md D3) — used identically by both `ApproveAsync` and `RejectAsync`
- [x] 2.2 ~~Extend the expense-with-employee repository query to `.ThenInclude(emp => emp.Manager)`~~ — corrected during `/implement`: no second hop is needed. `expense.Employee.ManagerId` alone covers both the direct-manager and skip-level cases (design.md D2); the existing `.Include(e => e.Employee)` is unchanged. An earlier draft's two-hop check was a real bug (it would have authorized an indirect/grandparent manager) and was removed before merge.
- [x] 2.3 Add `ApproveAsync(Guid managerId, Guid expenseId, CancellationToken ct)` to `IExpenseService`/`ExpenseService`: not-found → `ExpenseNotFound`; not `Submitted` → `NotSubmitted`; `CanReview` fails → `NotAuthorizedReviewer`; else set `Status = Approved`, `ApprovedAt`, `ApprovedByEmployeeId`, `UpdatedAt`, commit via `ExecuteInTransactionAsync`
- [x] 2.4 Add `RejectAsync(Guid managerId, Guid expenseId, RejectExpenseRequest request, CancellationToken ct)` to `IExpenseService`/`ExpenseService`: same authorization/status checks as Approve; set `Status = Rejected`, `RejectedAt`, `RejectedByEmployeeId`, `RejectionComment`, `UpdatedAt`, commit via `ExecuteInTransactionAsync`
- [x] 2.5 Add optional `ExpenseStatus? statusFilter` parameter to `IExpenseRepository.GetPagedAsync`/`ExpenseRepository.GetPagedAsync`, applied as an additional `.Where(...)` after the visibility predicate (design.md D6); thread it through `ExpenseService.GetVisibleAsync`
- [x] 2.6 Add `INotificationService` as a constructor dependency of `ExpenseService`; call `NotifyAsync(NotificationEvent.Approved/.Rejected, expense, ct)` after `SaveChangesAsync` succeeds in both `ApproveAsync` and `RejectAsync` (never inside the transaction, never able to roll it back — design.md D7)

**Checkpoint 2**: `dotnet build` → 0 errors.

## 3. Integration: Api Layer, DI, and Error Envelope

- [x] 3.1 Add `Approve` action to `ExpensesController`: `POST api/expenses/{id:guid}/approve`, `[Authorize(Policy = AuthorizationPolicyNames.Manager)]`, calls `_expenseService.ApproveAsync(User.GetEmployeeId(), id, ct)`, maps result via existing `FailureResult`/`Ok(new ExpenseEnvelopeResponse(...))` pattern
- [x] 3.2 Add `Reject` action to `ExpensesController`: `POST api/expenses/{id:guid}/reject`, `[Authorize(Policy = AuthorizationPolicyNames.Manager)]`, constructor-inject `IValidator<RejectExpenseRequest>`, validate → `ValidationErrorResult` on failure, else call `_expenseService.RejectAsync(...)`
- [x] 3.3 Extend `ExpensesController.FailureResult` switch: `NotSubmitted` → `422 BUSINESS_RULE_VIOLATION`; `NotAuthorizedReviewer` → `403 AUTHORIZATION_FAILED`
- [x] 3.4 Register `INotificationService` → `NoOpNotificationService` in `ExpenseServiceCollectionExtensions.AddExpenseFoundation` (`services.AddScoped<INotificationService, NoOpNotificationService>()`)
- [x] 3.5 Verify the two new endpoints and the `status` query parameter appear correctly in the existing Scalar/OpenAPI UI (no manual annotation expected, per ET005's existing Swagger/Scalar wiring — this is a verification task, not new code)
- [x] 3.6 Update `ExpenseServiceTests` (`backend/tests/UnitTests/Application/Expenses/ExpenseServiceTests.cs`) constructor calls to supply a fake/mock `INotificationService` so existing tests keep compiling

**Checkpoint 3**: `dotnet build` → 0 errors; `dotnet format --verify-no-changes`.

## 4. Tests: Manager Approval Endpoint

- [x] 4.1 Integration test: direct manager approves a direct report's `Submitted` expense → `200`, `Status` becomes `Approved`
- [x] 4.2 Integration test: manager unrelated to the expense owner attempts approve → `403 AUTHORIZATION_FAILED`, status unchanged
- [x] 4.3 Integration test: caller with role `Employee`, `Finance`, or `ComplianceOfficer` attempts approve → `403 AUTHORIZATION_FAILED`
- [x] 4.4 Integration test: approve targeting a nonexistent expense id → `404 RESOURCE_NOT_FOUND`
- [x] 4.5 Integration test: approve with no bearer token → `401 AUTHENTICATION_FAILED`

## 5. Tests: Manager Rejection Endpoint

- [x] 5.1 Integration test: direct manager rejects a direct report's `Submitted` expense with a valid comment → `200`, `Status` becomes `Rejected`
- [x] 5.2 Integration test: manager unrelated to the expense owner attempts reject → `403 AUTHORIZATION_FAILED`, status unchanged
- [x] 5.3 Integration test: caller with role `Employee`, `Finance`, or `ComplianceOfficer` attempts reject → `403 AUTHORIZATION_FAILED`
- [x] 5.4 Integration test: reject targeting a nonexistent expense id → `404 RESOURCE_NOT_FOUND`
- [x] 5.5 Integration test: reject with no bearer token → `401 AUTHENTICATION_FAILED`

## 6. Tests: Mandatory Rejection Comment Validation

- [x] 6.1 Integration test: reject request omitting `rejectionComment` → `400 VALIDATION_ERROR`, `fields` includes `rejectionComment`
- [x] 6.2 Integration test: reject request with whitespace-only `rejectionComment` → `400 VALIDATION_ERROR`
- [x] 6.3 Integration test: reject request with `rejectionComment` over 500 characters → `400 VALIDATION_ERROR`
- [x] 6.4 Unit test: `RejectExpenseRequestValidator` rejects null/empty/whitespace/oversized values and accepts a valid comment

## 7. Tests: BR-06 Self-Review Restriction

- [x] 7.1 Integration test: manager attempts to approve their own `Submitted` expense → `403 AUTHORIZATION_FAILED`, status unchanged
- [x] 7.2 Integration test: manager attempts to reject their own `Submitted` expense (valid comment) → `403 AUTHORIZATION_FAILED`, status unchanged
- [x] 7.3 Unit test: `CanReview` helper returns `false` when `expense.EmployeeId == managerId`

## 8. Tests: Skip-Level Escalation

- [x] 8.1 Integration test: a Manager's own `ManagerId` approves that Manager's own `Submitted` expense → `200`, `Status` becomes `Approved`
- [x] 8.2 Integration test: a Manager's own `ManagerId` rejects that Manager's own `Submitted` expense with a valid comment → `200`, `Status` becomes `Rejected`
- [x] 8.3 Unit test (corrected during `/implement` - see design.md D2/ADR-0012): verifies `ApproveAsync`/`RejectAsync` succeed when `expense.Employee.ManagerId == managerId` for a Manager-owned expense (the same single-hop field as the direct-manager case) - not a separate `expense.Employee.Manager?.ManagerId` traversal, which would have been a bug
- [x] 8.4 Integration test: a Manager-owned expense whose owner has a null `ManagerId` has no eligible approver — any other Manager's approve attempt is `403`, and the expense remains `Submitted` (documents the accepted gap per design.md Open Questions / spec's "known, accepted gap" scenario, not a defect)

## 9. Tests: Non-Submitted Expenses Rejected on Approve/Reject

- [x] 9.1 Integration test: approve a `Draft` expense → `422 BUSINESS_RULE_VIOLATION`, status unchanged
- [x] 9.2 Integration test: approve an already-`Approved` expense → `422 BUSINESS_RULE_VIOLATION`, status unchanged
- [x] 9.3 Integration test: approve a `Cancelled` expense → `422 BUSINESS_RULE_VIOLATION`, status unchanged
- [x] 9.4 Integration test: reject a `Draft` expense (valid comment) → `422 BUSINESS_RULE_VIOLATION`, status unchanged
- [x] 9.5 Integration test: reject a `Reimbursed` expense (valid comment) → `422 BUSINESS_RULE_VIOLATION`, status unchanged

## 10. Tests: Rejected Expenses Are Terminal

- [x] 10.1 Integration test: approve targeting an expense whose `Status` is `Rejected` → `422 BUSINESS_RULE_VIOLATION`, `Status` remains `Rejected`

## 11. Tests: Audit Field Population

- [x] 11.1 Integration test: successful approval sets `ApprovedAt` to the current timestamp and `ApprovedByEmployeeId` to the approving manager's `EmployeeId`
- [x] 11.2 Integration test: successful rejection sets `RejectedAt`, `RejectedByEmployeeId`, and `RejectionComment` to the submitted values
- [x] 11.3 Integration test: client-supplied `approvedAt`/`approvedByEmployeeId`/`rejectedAt`/`rejectedByEmployeeId` fields in the request body are ignored on both endpoints

## 12. Tests: Status Filter on Expense List Endpoint

- [x] 12.1 Integration test: manager GETs `/api/expenses?status=Submitted` → `items` contains only `Submitted` expenses from that manager's existing visible set
- [x] 12.2 Integration test: status filter never expands visibility — an `Employee` GETs `/api/expenses?status=Approved` and sees only their own `Approved` expenses
- [x] 12.3 Integration test (regression): `GET /api/expenses` with no `status` query parameter behaves exactly as it did before this change (re-run/extend existing ET009 `ExpenseVisibilityTests` scenarios)
- [x] 12.4 Integration test: `GET /api/expenses?status=NotARealStatus` → `400 VALIDATION_ERROR`

**Checkpoint 4**: `dotnet build` → 0 errors; `dotnet format --verify-no-changes`; `dotnet test --filter FullyQualifiedName~UnitTests` → all green; `dotnet test --filter FullyQualifiedName~IntegrationTests` → all green.

## 13. Archive & Ticket Tracking

- [x] 13.1 Re-run the full quality gate once more end-to-end (`dotnet build` → unit → integration) immediately before archiving
- [x] 13.2 Log ADRs in `docs/decisions/` for the three open decisions confirmed during `/spec`/`/plan`: BR-06 broadened to block Reject as well as Approve; the skip-level escalation authorization path; the `status` query filter addition to `GET /api/expenses`
- [x] 13.3 Run `openspec archive et010-manager-review`
- [ ] 13.4 Update `docs/TICKETS.md`: set ET010's `Status` to `PR open (#N)` once the PR is opened (per this repo's convention, archive happens before the PR is raised) - out of `/implement`'s scope; `/implement` keeps status `In progress`, `/pr` sets `PR open (#N)`
- [ ] 13.5 Open the PR referencing ET010 and FRS §5.1 - out of `/implement`'s scope, handled by `/pr`
