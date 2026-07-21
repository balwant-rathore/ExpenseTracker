## 1. Foundation — DTOs, Validators, Contracts

No EF Core migration in this ticket (design.md Migration Plan — reuses existing
`Expense.ReimbursedAt`/`ReimbursedByEmployeeId` columns and `ExpenseSortField.ReimbursedAt`).

- [x] 1.1 Add `ExpenseSearchRequest` record to `src/Application/Expenses/ExpenseSearchRequest.cs`
      (`ExpenseNumber`, `EmployeeName`, `Category`, `Status` as `string?`; `FromDate`/`ToDate`
      as `DateOnly?`; `Page`/`PageSize`/`SortBy`/`SortDirection` defaulted, matching
      `ExpenseListRequest`) — design.md D9
- [x] 1.2 Add `ExpenseSearchRequestValidator` to
      `src/Application/Expenses/ExpenseSearchRequestValidator.cs` — `PageSize` allowed
      `[20, 50, 100, 500]` (SDS §5.4, distinct from the list endpoint's `[10,20,50,100]`);
      `SortBy`/`SortDirection` reuse `ExpenseListRequestValidator.AllowedSortFields`/
      `AllowedSortDirections`; `Category`/`Status` valid-or-null against their enums;
      `Page >= 1`
- [x] 1.3 Add `ExpenseFailureReason.NotEligibleForReimbursement` to
      `src/Application/Expenses/ExpenseFailureReason.cs`
- [x] 1.4 Add `ReimbursedAt` (`DateTime?`) to `ExpenseResponse` in
      `src/Application/Expenses/ExpenseResponse.cs` — design.md D3
- [x] 1.5 Add `SearchAsync`/`ReimburseAsync` method signatures to `IExpenseService` in
      `src/Application/Expenses/IExpenseService.cs`
- [x] 1.6 Add `SearchPagedAsync`/`GetReimbursedForReportAsync` method signatures to
      `IExpenseRepository` in `src/Domain/Repositories/IExpenseRepository.cs` — flat
      parameters, no `Application`-layer type leakage (design.md D5)
- [x] 1.7 Add `MonthlyReimbursementQuery` record (`int? Year`, `int? Month`) to
      `src/Application/Reports/MonthlyReimbursementQuery.cs`
- [x] 1.8 Add `MonthlyReimbursementQueryValidator` to
      `src/Application/Reports/MonthlyReimbursementQueryValidator.cs` — `NotNull()` on
      both, `InclusiveBetween(1, 12)` on `Month`, `InclusiveBetween(1, 9999)` on `Year`
      (design.md D9)
- [x] 1.9 Add `MonthlyReimbursementRecord` record to
      `src/Application/Reports/MonthlyReimbursementRecord.cs` (`EmployeeName`,
      `ExpenseNumber`, `Category`, `Amount`, `Currency`, `ApprovalDate`,
      `ReimbursementDate`)
- [x] 1.10 Add `MonthlyReimbursementReportResponse` record (`Items`) to
      `src/Application/Reports/MonthlyReimbursementReportResponse.cs`
- [x] 1.11 Add `IReportService` interface to `src/Application/Reports/IReportService.cs`

**Checkpoint 1** (backend-only ticket — no frontend gate applies):
- [x] 1.12 `dotnet build` → 0 errors
- [x] 1.13 `dotnet format --verify-no-changes` → clean

## 2. Core Implementation — Repositories & Services

- [x] 2.1 Implement `SearchPagedAsync` in
      `src/Infrastructure/Persistence/Repositories/ExpenseRepository.cs` — base predicate
      `e.Status != ExpenseStatus.Draft`, layered optional filters
      (`expenseNumber` exact, `employeeName` case-insensitive contains on
      `"{FirstName} {LastName}"`, `category`/`status` exact, `createdFromUtc`/
      `createdToUtc` range on `CreatedAt`), `Include(Employee)`, paged/sorted via the
      same `OrderBy` switch pattern as `GetPagedAsync` — design.md D5, D6
- [x] 2.2 Implement `GetReimbursedForReportAsync` in
      `src/Infrastructure/Persistence/Repositories/ExpenseRepository.cs` —
      `Status == Reimbursed && ReimbursedAt` in `[rangeStartUtcInclusive,
      rangeEndUtcExclusive)`, `Include(Employee)`, ordered by `ReimbursedAt`
- [x] 2.3 Implement `ExpenseService.SearchAsync` in
      `src/Application/Expenses/ExpenseService.cs` — parse `Category`/`Status`/
      `SortBy`/`SortDirection`, convert `FromDate`/`ToDate` to UTC bounds via
      `ToDateTime(TimeOnly.MinValue/MaxValue, DateTimeKind.Utc)` (design.md D4), call
      `SearchPagedAsync`, map via existing `Map()` into `PagedExpenseResponse`
- [x] 2.4 Implement `ExpenseService.ReimburseAsync` in
      `src/Application/Expenses/ExpenseService.cs` — load via
      `GetByIdWithEmployeeAsync`, `ExpenseNotFound` if missing, category-conditioned
      `IsEligibleForReimbursement` check (design.md D2) →
      `NotEligibleForReimbursement` on failure, else set `Status = Reimbursed`,
      `ReimbursedAt`, `ReimbursedByEmployeeId`, `UpdatedAt` inside
      `_unitOfWork.ExecuteInTransactionAsync`, then
      `_notificationService.NotifyAsync(NotificationEvent.Reimbursed, ...)` after commit
- [x] 2.5 Update `ExpenseService.Map()` to populate the new `ExpenseResponse.ReimbursedAt`
      field
- [x] 2.6 Implement `ReportService` in `src/Application/Reports/ReportService.cs` —
      compute `[rangeStart, rangeStart.AddMonths(1))` in UTC from `year`/`month`, call
      `GetReimbursedForReportAsync`, map each `Expense` to `MonthlyReimbursementRecord`
      with `ApprovalDate = expense.ComplianceApprovedAt ?? expense.ApprovedAt`
      (design.md D8)

**Checkpoint 2**:
- [x] 2.7 `dotnet build` → 0 errors
- [x] 2.8 `dotnet test --filter FullyQualifiedName~UnitTests` → all green (existing
      suite unaffected by additive changes)

## 3. Integration — Controllers & DI Wiring

- [x] 3.1 Add `[HttpPost("search")]` `Search` action to `ExpensesController` in
      `src/Api/Controllers/ExpensesController.cs` — `[Authorize(Policy =
      AuthorizationPolicyNames.Finance)]`, validates via `ExpenseSearchRequestValidator`,
      delegates to `IExpenseService.SearchAsync`
- [x] 3.2 Add `[HttpPost("{id:guid}/reimburse")]` `Reimburse` action to
      `ExpensesController` — `[Authorize(Policy = AuthorizationPolicyNames.Finance)]`,
      delegates to `IExpenseService.ReimburseAsync`
- [x] 3.3 Add `NotEligibleForReimbursement` arm to `ExpensesController.FailureResult` —
      `422 BUSINESS_RULE_VIOLATION`
- [x] 3.4 Create `ReportsController` in `src/Api/Controllers/ReportsController.cs` —
      `[Route("api/reports")]`, class-level `[Authorize(Policy =
      AuthorizationPolicyNames.Finance)]`, `[HttpGet("monthly-reimbursement")]` action
      bound from `[FromQuery] MonthlyReimbursementQuery`, validates via
      `MonthlyReimbursementQueryValidator`, returns
      `MonthlyReimbursementReportResponse`
- [x] 3.5 Create `ReportServiceCollectionExtensions.AddReportFoundation` in
      `src/Api/Extensions/ReportServiceCollectionExtensions.cs` — registers
      `IReportService` → `ReportService`
- [x] 3.6 Wire `builder.Services.AddReportFoundation();` into `src/Api/Program.cs`
      alongside the existing `AddExpenseFoundation`/`AddAttachmentFoundation` calls

**Checkpoint 3**:
- [x] 3.7 `dotnet build` → 0 errors
- [x] 3.8 `dotnet format --verify-no-changes` → clean
- [x] 3.9 Manual smoke check via Scalar UI (`dotnet run --project src/Api`, dev
      environment) — confirm `POST /api/expenses/search`,
      `POST /api/expenses/{id}/reimburse`, `GET /api/reports/monthly-reimbursement`
      all appear in the OpenAPI document with the `Finance` policy applied

## 4. Tests — One Per Spec Scenario

### 4.1 `finance-expense-search` (14 scenarios)

- [x] 4.1.1 Unit: `ExpenseSearchRequestValidatorTests` — invalid `PageSize` rejected
- [x] 4.1.2 Unit: `ExpenseSearchRequestValidatorTests` — invalid `SortBy` rejected
- [x] 4.1.3 Unit: `ExpenseSearchRequestValidatorTests` — invalid `Category` rejected
- [x] 4.1.4 Unit: `ExpenseSearchRequestValidatorTests` — invalid `Status` rejected
- [x] 4.1.5 Unit: `ExpenseServiceTests.SearchAsync` — defaults applied when no
      paging/sorting supplied
- [x] 4.1.6 Integration `ExpenseSearchTests`: Finance caller receives a paged result
      with no filters (200, `items`/`page`/`pageSize`/`totalRecords`)
- [x] 4.1.7 Integration `ExpenseSearchTests`: non-Finance role (`Employee`, `Manager`,
      `ComplianceOfficer`) rejected 403
- [x] 4.1.8 Integration `ExpenseSearchTests`: unauthenticated request rejected 401
- [x] 4.1.9 Integration `ExpenseSearchTests`: Draft expenses excluded with no status
      filter
- [x] 4.1.10 Integration `ExpenseSearchTests`: explicit `status: "Draft"` returns empty
      result, not an error
- [x] 4.1.11 Integration `ExpenseSearchTests`: `expenseNumber` filter matches exactly
- [x] 4.1.12 Integration `ExpenseSearchTests`: `employeeName` filter matches a substring
      case-insensitively
- [x] 4.1.13 Integration `ExpenseSearchTests`: `category` + date-range filters combine
      with AND
- [x] 4.1.14 Integration `ExpenseSearchTests`: valid custom paging/sorting honored
      (`page`, `pageSize=100`, `sortBy=amount`, `sortDirection=asc`)

### 4.2 `expense-reimbursement` (14 scenarios)

- [x] 4.2.1 Unit: `ExpenseServiceTests.ReimburseAsync` — `IsEligibleForReimbursement`
      true for `Approved` non-Client-Entertainment
- [x] 4.2.2 Unit: `ExpenseServiceTests.ReimburseAsync` — `IsEligibleForReimbursement`
      true for `ComplianceApproved` Client Entertainment
- [x] 4.2.3 Unit: `ExpenseServiceTests.ReimburseAsync` — audit fields
      (`ReimbursedAt`/`ReimbursedByEmployeeId`/`UpdatedAt`) set on success
- [x] 4.2.4 Integration `ExpenseReimbursementTests`: Finance reimburses an
      `Approved` non-Client-Entertainment expense → 200, `Status = Reimbursed`
- [x] 4.2.5 Integration `ExpenseReimbursementTests`: Finance reimburses a
      `ComplianceApproved` Client Entertainment expense → 200, `Status = Reimbursed`
- [x] 4.2.6 Integration `ExpenseReimbursementTests`: reimbursing an `Approved`
      (not yet Compliance Approved) Client Entertainment expense → 422, status
      unchanged
- [x] 4.2.7 Integration `ExpenseReimbursementTests`: reimbursing a `ComplianceApproved`
      non-Client-Entertainment expense → 422 (inconsistent-state guard)
- [x] 4.2.8 Integration `ExpenseReimbursementTests`: reimbursing `Submitted` → 422
- [x] 4.2.9 Integration `ExpenseReimbursementTests`: reimbursing `Rejected` → 422
- [x] 4.2.10 Integration `ExpenseReimbursementTests`: reimbursing already-`Reimbursed`
      → 422
- [x] 4.2.11 Integration `ExpenseReimbursementTests`: reimbursing `Cancelled` → 422
- [x] 4.2.12 Integration `ExpenseReimbursementTests`: non-Finance role
      (`Employee`, `Manager`, `ComplianceOfficer`) rejected 403
- [x] 4.2.13 Integration `ExpenseReimbursementTests`: nonexistent expense id → 404
- [x] 4.2.14 Integration `ExpenseReimbursementTests`: unauthenticated request → 401;
      client-supplied `reimbursedAt`/`reimbursedByEmployeeId` in the request body are
      ignored

### 4.3 `monthly-reimbursement-report` (10 scenarios)

- [x] 4.3.1 Unit: `MonthlyReimbursementQueryValidatorTests` — missing `Year` rejected
- [x] 4.3.2 Unit: `MonthlyReimbursementQueryValidatorTests` — missing `Month` rejected
- [x] 4.3.3 Unit: `MonthlyReimbursementQueryValidatorTests` — out-of-range `Month`
      (e.g. 13) rejected
- [x] 4.3.4 Unit: `ReportServiceTests` — `ApprovalDate` uses `ComplianceApprovedAt` for
      a reimbursed Client Entertainment expense, `ApprovedAt` otherwise
- [x] 4.3.5 Unit: `ReportServiceTests` — month with no reimbursements returns an empty
      list (not an exception)
- [x] 4.3.6 Integration `MonthlyReimbursementReportTests`: Finance retrieves monthly
      reimbursement data → 200 with matching records
- [x] 4.3.7 Integration `MonthlyReimbursementReportTests`: non-Finance role rejected 403
- [x] 4.3.8 Integration `MonthlyReimbursementReportTests`: unauthenticated request
      rejected 401
- [x] 4.3.9 Integration `MonthlyReimbursementReportTests`: only `Reimbursed` expenses
      within the requested month are included (an expense reimbursed in an adjacent
      month is excluded)
- [x] 4.3.10 Integration `MonthlyReimbursementReportTests`: non-Reimbursed (e.g.
      `Approved`) expenses excluded even if `ApprovedAt` falls within the month

**Checkpoint 4**:
- [x] 4.4 `dotnet test --filter FullyQualifiedName~UnitTests` → all green
- [x] 4.5 `dotnet test --filter FullyQualifiedName~IntegrationTests` → all green
- [x] 4.6 `dotnet format --verify-no-changes` → clean
- [x] 4.7 Confirm every scenario in the three approved spec deltas
      (`specs/finance-expense-search/spec.md`, `specs/expense-reimbursement/spec.md`,
      `specs/monthly-reimbursement-report/spec.md`) has a corresponding test task above
      — no scenario left untested

## 5. Archive & Ticket Status

- [x] 5.1 Run `openspec archive et012-finance-processing`
- [x] 5.2 Update `docs/TICKETS.md` ET012 row: `Status` → `PR open (#N)` once the PR is
      opened (per the ticket's own status convention — archive precedes PR, PR precedes
      `Done`)
- [x] 5.3 Open the PR (`gh pr create`, per `/pr`) referencing FRS §7 and this change name
