## 1. Foundation — Contracts/DTOs

- [x] 1.1 Add `NotClientEntertainment` and `NotApprovedForCompliance` to
      `Application/Expenses/ExpenseFailureReason.cs`
- [x] 1.2 Append `ComplianceApproved` and `ComplianceRejected` to
      `Application/Notifications/NotificationEvent.cs` (append, not insert — design.md D5)
- [x] 1.3 Add `DateTime? ComplianceApprovedAt` to `Application/Expenses/ExpenseResponse.cs`,
      positioned after `ApprovedAt` and before `RejectedAt` (design.md D3)
- [x] 1.4 Add `ComplianceApproveAsync` and `ComplianceRejectAsync` to
      `Application/Expenses/IExpenseService.cs`

## 2. Core Implementation — Service Layer

- [x] 2.1 Implement private `CanComplianceReview(Expense) : ExpenseFailureReason?` in
      `ExpenseService.cs` — category check first, then status check, returns `null` when both
      pass (design.md D1; independent checks, not one combined boolean)
- [x] 2.2 Implement `ComplianceApproveAsync`: `GetByIdAsync` (no `Employee` include needed —
      design.md D2) → not-found → `CanComplianceReview` → set `Status = ComplianceApproved`,
      `ComplianceApprovedAt`, `ComplianceApprovedByEmployeeId`, `UpdatedAt` → transaction → notify
      `NotificationEvent.ComplianceApproved` post-commit
- [x] 2.3 Implement `ComplianceRejectAsync`: same not-found/`CanComplianceReview` guards → set
      `Status = Rejected`, `RejectedAt`, `RejectedByEmployeeId`,
      `RejectionComment = request.RejectionComment!`, `UpdatedAt` → transaction → notify
      `NotificationEvent.ComplianceRejected` post-commit
- [x] 2.4 Update `ExpenseService.Map(Expense)` to pass `expense.ComplianceApprovedAt` into the
      `ExpenseResponse` constructor (only existing call site — verified via repo-wide search)

## 3. Integration — API Layer

- [x] 3.1 Add `POST /api/expenses/{id}/compliance-approve` to `ExpensesController`,
      `[Authorize(Policy = AuthorizationPolicyNames.ComplianceOfficer)]`, delegating to
      `ComplianceApproveAsync`
- [x] 3.2 Add `POST /api/expenses/{id}/compliance-reject` to `ExpensesController`, same policy,
      validating with the existing injected `_rejectValidator` before calling
      `ComplianceRejectAsync`
- [x] 3.3 Extend the `FailureResult` switch: `NotClientEntertainment` → `422
      BUSINESS_RULE_VIOLATION`, `NotApprovedForCompliance` → `422 BUSINESS_RULE_VIOLATION`
- [x] 3.4 Checkpoint: `dotnet build` (0 errors/warnings), `dotnet format --verify-no-changes`

## 4. Tests — Unit (`ExpenseServiceTests.cs`)

- [x] 4.1 `ComplianceApproveAsync`: success sets `Status`/`ComplianceApprovedAt`/
      `ComplianceApprovedByEmployeeId`/`UpdatedAt`; returned response exposes
      `ComplianceApprovedAt`
- [x] 4.2 `ComplianceApproveAsync`: expense not found → `ExpenseNotFound`
- [x] 4.3 `ComplianceApproveAsync`: non-`ClientEntertainment` category (any status) →
      `NotClientEntertainment`
- [x] 4.4 `ComplianceApproveAsync`: `ClientEntertainment` but not `Approved` — parameterized
      over `Draft`, `Submitted`, `ComplianceApproved`, `Rejected`, `Cancelled`, `Reimbursed`
      individually → `NotApprovedForCompliance` (AGENTS.md §13 — every status enumerated, not
      one representative case)
- [x] 4.5 `ComplianceApproveAsync`: fires `NotificationEvent.ComplianceApproved` via
      `FakeNotificationService` after commit
- [x] 4.6 `ComplianceRejectAsync`: success sets `Status = Rejected`/`RejectedAt`/
      `RejectedByEmployeeId`/`RejectionComment`/`UpdatedAt`
- [x] 4.7 `ComplianceRejectAsync`: expense not found → `ExpenseNotFound`
- [x] 4.8 `ComplianceRejectAsync`: non-`ClientEntertainment` category → `NotClientEntertainment`
- [x] 4.9 `ComplianceRejectAsync`: `ClientEntertainment` but not `Approved` — parameterized over
      all six non-`Approved` statuses individually → `NotApprovedForCompliance`
- [x] 4.10 `ComplianceRejectAsync`: fires `NotificationEvent.ComplianceRejected` via
      `FakeNotificationService` after commit
- [x] 4.11 Checkpoint: `dotnet test --filter FullyQualifiedName~UnitTests` all green (222/222)

## 5. Tests — Integration (new `ExpenseComplianceReviewTests.cs`)

- [x] 5.1 Scaffold `ExpenseComplianceReviewTests.cs` with local helpers duplicated from
      `ExpenseApprovalTests.cs` (`CreateAuthorizedClientAsync`, `UploadAttachmentAsync`,
      `SetExpenseStatusAsync`, `AssertStatusUnchangedAsync`), with `CreateExpenseBody`
      parameterized on `category` instead of the hardcoded `"Travel"` (design.md, Tests section)
- [x] 5.2 Compliance-approve: `ComplianceOfficer` approves an `Approved` `ClientEntertainment`
      expense → `200`, `Status` becomes `ComplianceApproved`
- [x] 5.3 Compliance-approve: `Employee`/`Manager`/`Finance` roles → `403` (Theory, all three)
- [x] 5.4 Compliance-approve: nonexistent id → `404`
- [x] 5.5 Compliance-approve: no bearer token → `401`
- [x] 5.6 Compliance-approve: `Travel`-category `Approved` expense → `422`
      `NotClientEntertainment`, status unchanged
- [x] 5.7 Compliance-approve: `ClientEntertainment` expense in each of `Draft`, `Submitted`,
      `ComplianceApproved`, `Rejected`, `Cancelled`, `Reimbursed` → `422`
      `NotApprovedForCompliance`, status unchanged (Theory, all six)
- [x] 5.8 Compliance-approve: populates `ComplianceApprovedAt`/`ComplianceApprovedByEmployeeId`;
      response body exposes `complianceApprovedAt`
- [x] 5.9 Compliance-reject: `ComplianceOfficer` rejects an `Approved` `ClientEntertainment`
      expense with a valid comment → `200`, `Status` becomes `Rejected`
- [x] 5.10 Compliance-reject: `Employee`/`Manager`/`Finance` roles → `403` (Theory, all three)
- [x] 5.11 Compliance-reject: nonexistent id → `404`
- [x] 5.12 Compliance-reject: no bearer token → `401`
- [x] 5.13 Compliance-reject: missing / whitespace-only / oversized `rejectionComment` → `400`
      `VALIDATION_ERROR` with a `fields` entry for `rejectionComment` (Theory, all three cases)
- [x] 5.14 Compliance-reject: `Meals`-category `Approved` expense → `422`
      `NotClientEntertainment`, status unchanged
- [x] 5.15 Compliance-reject: `ClientEntertainment` expense in each non-`Approved` status →
      `422` `NotApprovedForCompliance`, status unchanged (Theory, all six)
- [x] 5.16 Compliance-reject: populates `RejectedAt`/`RejectedByEmployeeId`/`RejectionComment`
- [x] 5.17 Terminal: a `Rejected` `ClientEntertainment` expense cannot later be
      compliance-approved — `422`, `Status` remains `Rejected`
- [x] 5.18 Client cannot set audit fields directly: a request body including
      `complianceApprovedAt`, `complianceApprovedByEmployeeId`, `rejectedAt`, or
      `rejectedByEmployeeId` has those values ignored, for both endpoints
- [x] 5.19 Checkpoint: `dotnet test --filter FullyQualifiedName~IntegrationTests` all green
      (3 consecutive clean runs, 187/187 each — per ET010's `xunit.runner.json` non-parallel
      configuration)

## 6. Documentation

- [x] 6.1 Log an ADR in `docs/decisions/` for the "no BR-06-equivalent self-review restriction"
      decision — a deviation from the literal Manager-review precedent, confirmed with the
      ticket owner during `/spec` as structurally unattainable (`docs/SDS.md` §6.4 Authorization
      Matrix: only `Employee`/`Manager` can create expenses) — `ADR-0014`
- [x] 6.2 Update `openspec/changes/et011-compliance-review/design.md` in place if
      implementation deviates from any documented decision (AGENTS.md §13 — a stale design doc
      is itself a defect) — reviewed D1-D5 and File Changes against implementation; no deviation
      found, no update needed

## 7. Archive & Ticket Status

- [x] 7.1 Run the full checkpoint suite end-to-end one more time: `dotnet build`,
      `dotnet format --verify-no-changes`, `dotnet test` (unit + integration) — 0 errors/
      warnings, format clean, 222 unit + 187 integration tests all green
- [x] 7.2 Mark all checkboxes in this file `[x]` as they complete, then run
      `openspec archive et011-compliance-review`
- [x] 7.3 Keep `docs/TICKETS.md`'s ET011 `Status` at `In progress` (archiving happens before
      the PR exists — `/pr` sets `PR open (#N)`, `Done` is reserved for after merge)
