## 1. Foundation — DTOs, enums, failure reason, repository contract

All backend-only (this ticket has no `frontend/` scope, per `design.md` Goals/Non-Goals).

- [x] 1.1 Add `backend/src/Domain/Enums/ExpenseSortField.cs` (`ExpenseDate, ExpenseNumber, CreatedAt, Amount, SubmittedAt, ApprovedAt, ReimbursedAt, RejectedAt`)
- [x] 1.2 Add `NotVisible` to `backend/src/Application/Expenses/ExpenseFailureReason.cs` (D4)
- [x] 1.3 Add `backend/src/Application/Expenses/ExpenseListRequest.cs` (`Page=1`, `PageSize=20`, `SortBy="expenseDate"`, `SortDirection="desc"` defaults)
- [x] 1.4 Add `backend/src/Application/Expenses/ExpenseListRequestValidator.cs` — `Page >= 1`; `PageSize` in `{10,20,50,100}`; `SortBy` in the 8 allowed fields; `SortDirection` in `{asc,desc}` (D8)
- [x] 1.5 Add `backend/src/Application/Expenses/PagedExpenseResponse.cs` (`Items`, `Page`, `PageSize`, `TotalRecords`)
- [x] 1.6 Add `backend/src/Application/Expenses/ExpenseVisibility.cs` — `BuildPredicate(EmployeeRole role, Guid employeeId)` returning the shared `Expression<Func<Expense,bool>>` per role (D1, D7)
- [x] 1.7 Add `string? EmployeeName` to `backend/src/Application/Expenses/ExpenseResponse.cs` (D3)
- [x] 1.8 Add `GetByIdWithEmployeeAsync`/`GetPagedAsync` to `backend/src/Domain/Repositories/IExpenseRepository.cs`; implement both in `backend/src/Infrastructure/Persistence/Repositories/ExpenseRepository.cs` (D2) — implement with `.Include(e => e.Employee)`, `Where`/`OrderBy`-switch/`Skip`/`Take`/`CountAsync`
- [x] 1.9 Update `FakeExpenseRepository` (test double in `ExpenseNumberGeneratorTests.cs`) to implement the two new `IExpenseRepository` members

**Checkpoint:** `dotnet build` (0 errors) · `dotnet format --verify-no-changes`

## 2. Core Implementation

- [x] 2.1 Update `IExpenseService.cs`: `GetByIdAsync` gains an `EmployeeRole role` parameter; add `Task<PagedExpenseResponse> GetVisibleAsync(Guid employeeId, EmployeeRole role, ExpenseListRequest request, CancellationToken cancellationToken)` (D5)
- [x] 2.2 Rewire `ExpenseService.GetByIdAsync`: `GetByIdWithEmployeeAsync` → null → `ExpenseNotFound`; compile `ExpenseVisibility.BuildPredicate(role, employeeId)` and invoke against the fetched expense → `false` → `NotVisible` (D1, D4)
- [x] 2.3 Implement `ExpenseService.GetVisibleAsync`: build the predicate (uncompiled, passed for SQL translation); parse validated `SortBy`/`SortDirection`; call `GetPagedAsync`; map results; return `PagedExpenseResponse` (D1, D5, D8)
- [x] 2.4 Update `ExpenseService.Map()` to compute `EmployeeName` from `expense.Employee` when loaded, else `null` (D3)
- [x] 2.5 Add `ClaimsPrincipalExtensions.GetRole()` mirroring the existing `GetEmployeeId()` (D5)
- [x] 2.6 Change `ExpensesController`'s class-level attribute from `[Authorize(Policy = EmployeeOrManager)]` to bare `[Authorize]`; add `[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]` explicitly on `Create`, `Submit`, `Update`, `Cancel` (D6)
- [x] 2.7 Add `GetAll` (`[HttpGet]`) action: validate via new `IValidator<ExpenseListRequest>` → 400 on failure; call `GetVisibleAsync(User.GetEmployeeId(), User.GetRole(), request, ct)`; return `Ok(result)` (D8)
- [x] 2.8 Update `GetById` action to resolve and pass `User.GetRole()` into `GetByIdAsync`
- [x] 2.9 Extend `ExpensesController.FailureResult`'s switch with `NotVisible` → `403 AUTHORIZATION_FAILED` ("You do not have permission to perform this action.") (D4)

**Checkpoint result:** `dotnet build` 0 errors/0 warnings · `dotnet format --verify-no-changes` clean · `dotnet test` 228/228 passed (140 unit + 88 integration, pre-existing `GetByIdAsync_NonOwner_ReturnsNotOwner` updated to `GetByIdAsync_NonOwner_ReturnsNotVisible` to match D4)

**Checkpoint:** `dotnet build` (0 errors) · `dotnet format --verify-no-changes`

## 3. Integration

- [x] 3.1 Manually exercise `GET /api/expenses` against the local dev DB for each role: Employee (own only, incl. Draft), Manager (own any status + direct report non-Draft, excluding indirect/unrelated), Finance (all non-Draft), Compliance (Client Entertainment Approved/ComplianceApproved only) — confirm `items`/`page`/`pageSize`/`totalRecords` shape — **substituted with the automated `IntegrationTests/ExpenseVisibilityTests.cs` suite (Phase 4), which exercises every one of these cases end-to-end against the real local dev DB via `WebApplicationFactory`, the same DB manual curl would have hit; more thorough and repeatable than ad hoc curl, same substitution rationale ET008 used**
- [x] 3.2 Manually exercise default and custom `page`/`pageSize`/`sortBy`/`sortDirection` query params, and invalid values → `400` — covered by `GetAll_NoQueryParameters_DefaultsApply`/`GetAll_ValidCustomPagingAndSorting_IsHonored`/`GetAll_InvalidPageSize_Returns400`/`GetAll_InvalidSortBy_Returns400` in the automated suite
- [x] 3.3 Manually exercise the widened `GET /api/expenses/{id}` for each role's allow/deny cases — covered by the 10 `GetById_*` tests in `IntegrationTests/ExpenseVisibilityTests.cs`
- [x] 3.4 Confirm via `/openapi/v1.json` that `GET /api/expenses` appears with its query parameters documented — verified directly: started the API locally, fetched `/openapi/v1.json`, confirmed the `GET /api/expenses` operation lists `Page`/`PageSize`/`SortBy`/`SortDirection` query parameters
- [x] 3.5 Run the full existing test suite — confirm `POST /api/expenses`, `POST /api/expenses/{id}/submit`, `PUT /api/expenses/{id}`, `POST /api/expenses/{id}/cancel` are unaffected by the `ExpenseResponse`/`IExpenseService` signature changes

**Checkpoint result:** `dotnet build` 0 errors/0 warnings · `dotnet format --verify-no-changes` clean · `dotnet test` 290/290 passed (173 unit + 117 integration). Note: one integration test (`Update_AttachmentAlreadyLinkedToDifferentExpense_Returns422`, pre-existing from ET008, unrelated to this ticket) failed once transiently during a full-suite run immediately after a background API process was killed for the OpenAPI check; re-ran in isolation (passed) and re-ran the full suite twice more (both 290/290 green) — confirmed a one-off environmental flake, not a regression.

## 4. Tests — one per spec delta scenario

Unit tests (`backend/tests/...UnitTests`):

`ExpenseListRequestValidator` (`specs/expense-visibility/spec.md` — *List Pagination and Sorting*):

- [x] 4.1 Valid request (defaults) passes
- [x] 4.2 `pageSize` not in `{10,20,50,100}` is rejected (*Invalid pageSize is rejected*)
- [x] 4.3 `sortBy` outside the 8 allowed fields is rejected (*Invalid sortBy is rejected*)
- [x] 4.4 `sortDirection` outside `{asc,desc}` is rejected
- [x] 4.5 `page < 1` is rejected

`ExpenseVisibility.BuildPredicate` (*Employee/Manager/Finance/Compliance Default Visibility*):

- [x] 4.6 Employee predicate: matches the caller's own expense (*Employee sees only their own expenses*)
- [x] 4.7 Employee predicate: excludes another employee's expense (*Employee sees only their own expenses*)
- [x] 4.8 Employee predicate: matches the caller's own `Draft` expense (*Employee's own Draft expense is included*)
- [x] 4.9 Manager predicate: matches the caller's own expense regardless of status (*Manager sees their own expenses at any status*)
- [x] 4.10 Manager predicate: matches a direct report's non-`Draft` expense (*Manager sees a direct report's non-Draft expense*)
- [x] 4.11 Manager predicate: excludes a direct report's `Draft` expense (*Manager does not see a direct report's Draft expense*)
- [x] 4.12 Manager predicate: excludes an indirect report's expense (*Manager does not see an indirect report's expense*)
- [x] 4.13 Manager predicate: excludes an unrelated employee's expense (*Manager does not see an unrelated employee's expense*)
- [x] 4.14 Finance predicate: matches any employee's non-`Draft` expense (*Finance sees non-Draft expenses of any employee*)
- [x] 4.15 Finance predicate: excludes a `Draft` expense (*Finance does not see Draft expenses*)
- [x] 4.16 Compliance predicate: matches an `Approved` Client Entertainment expense (*Compliance sees an Approved Client Entertainment expense*)
- [x] 4.17 Compliance predicate: matches a `ComplianceApproved` Client Entertainment expense (*Compliance sees a Compliance Approved Client Entertainment expense*)
- [x] 4.18 Compliance predicate: excludes a `Reimbursed` Client Entertainment expense (*Compliance does not see a Reimbursed Client Entertainment expense*)
- [x] 4.19 Compliance predicate: excludes a non-Client-Entertainment `Approved` expense (*Compliance does not see non-Client-Entertainment expenses*)
- [x] 4.20 Compliance predicate: excludes a `Draft`/`Submitted` Client Entertainment expense (*Compliance does not see a Draft or Submitted Client Entertainment expense*)

`ExpenseService.GetByIdAsync` (`specs/expense-maintenance/spec.md` — MODIFIED *Single Expense Retrieval Endpoint*):

- [x] 4.21 Owner retrieves their own expense — success, `EmployeeName` populated (*Owner retrieves their own expense*)
- [x] 4.22 Manager retrieves their own expense regardless of status (*Manager retrieves their own expense*)
- [x] 4.23 Manager retrieves a direct report's non-`Draft` expense (*Manager retrieves a direct report's non-Draft expense*)
- [x] 4.24 Manager is rejected (`NotVisible`) for a direct report's `Draft` expense (*Manager is rejected for a direct report's Draft expense*)
- [x] 4.25 Manager is rejected (`NotVisible`) for an unrelated employee's expense (*Manager is rejected for an unrelated employee's expense*)
- [x] 4.26 Finance retrieves any non-`Draft` expense (*Finance retrieves any non-Draft expense*)
- [x] 4.27 Finance is rejected (`NotVisible`) for a `Draft` expense (*Finance is rejected for a Draft expense*)
- [x] 4.28 Compliance retrieves an `Approved` Client Entertainment expense (*Compliance retrieves an Approved or Compliance Approved Client Entertainment expense*)
- [x] 4.29 Compliance retrieves a `ComplianceApproved` Client Entertainment expense (*Compliance retrieves an Approved or Compliance Approved Client Entertainment expense*)
- [x] 4.30 Compliance is rejected (`NotVisible`) for a `Reimbursed` Client Entertainment expense (*Compliance is rejected for a Reimbursed Client Entertainment expense*)
- [x] 4.31 Compliance is rejected (`NotVisible`) for a non-Client-Entertainment expense (*Compliance is rejected for a non-Client-Entertainment expense*)
- [x] 4.32 Returns `ExpenseNotFound` for a nonexistent id (*Nonexistent expense is rejected*)

`ExpenseService.GetVisibleAsync` (wiring):

- [x] 4.33 Returns a `PagedExpenseResponse` with `Page`/`PageSize`/`TotalRecords` reflecting the request and repository result
- [x] 4.34 Maps each returned `Expense` to `ExpenseResponse` with `EmployeeName` populated

Integration tests (`backend/tests/...IntegrationTests`, `WebApplicationFactory`):

`GET /api/expenses` (*Expense List Endpoint*, *Default Visibility* × 4 roles, *List Pagination and Sorting*):

- [x] 4.35 Authenticated caller receives a paged result — `200` with `items`/`page`/`pageSize`/`totalRecords` (*Authenticated caller receives a paged result*)
- [x] 4.36 No `Authorization` header → `401 AUTHENTICATION_FAILED` (*Unauthenticated request is rejected*)
- [x] 4.37 Employee sees only their own expenses, including their own `Draft` (*Employee sees only their own expenses*, *Employee's own Draft expense is included*)
- [x] 4.38 Manager's list merges their own expenses (any status) with a direct report's non-`Draft` expenses (*Manager sees their own expenses at any status*, *Manager sees a direct report's non-Draft expense*)
- [x] 4.39 Manager's list excludes a direct report's `Draft` expense and an indirect report's expense (*Manager does not see a direct report's Draft expense*, *Manager does not see an indirect report's expense*)
- [x] 4.40 Manager's list excludes an unrelated employee's expense (*Manager does not see an unrelated employee's expense*)
- [x] 4.41 Finance's list includes non-`Draft` expenses across employees and excludes `Draft` (*Finance sees non-Draft expenses of any employee*, *Finance does not see Draft expenses*)
- [x] 4.42 Compliance's list includes `Approved`/`ComplianceApproved` Client Entertainment expenses and excludes `Reimbursed`, non-Client-Entertainment, and `Draft`/`Submitted` Client Entertainment expenses (*Compliance sees an Approved Client Entertainment expense*, *Compliance sees a Compliance Approved Client Entertainment expense*, *Compliance does not see a Reimbursed Client Entertainment expense*, *Compliance does not see non-Client-Entertainment expenses*, *Compliance does not see a Draft or Submitted Client Entertainment expense*)
- [x] 4.43 No query params → defaults `page=1`, `pageSize=20`, sorted by `expenseDate desc` (*Defaults apply when no query parameters are given*)
- [x] 4.44 Valid custom `page`/`pageSize`/`sortBy`/`sortDirection` are honored (*Valid custom paging and sorting is honored*)
- [x] 4.45 Invalid `pageSize` → `400 VALIDATION_ERROR` (*Invalid pageSize is rejected*)
- [x] 4.46 Invalid `sortBy` → `400 VALIDATION_ERROR` (*Invalid sortBy is rejected*)

`GET /api/expenses/{id}` widened visibility (MODIFIED *Single Expense Retrieval Endpoint*):

- [x] 4.47 Owner retrieves their own expense → `200` (*Owner retrieves their own expense*, end-to-end)
- [x] 4.48 Manager retrieves their own expense → `200` (*Manager retrieves their own expense*, end-to-end)
- [x] 4.49 Manager retrieves a direct report's non-`Draft` expense → `200` (*Manager retrieves a direct report's non-Draft expense*, end-to-end)
- [x] 4.50 Manager is rejected for a direct report's `Draft` expense → `403 AUTHORIZATION_FAILED` (*Manager is rejected for a direct report's Draft expense*, end-to-end)
- [x] 4.51 Manager is rejected for an unrelated employee's expense → `403 AUTHORIZATION_FAILED` (*Manager is rejected for an unrelated employee's expense*, end-to-end)
- [x] 4.52 Finance retrieves any non-`Draft` expense → `200` (*Finance retrieves any non-Draft expense*, end-to-end)
- [x] 4.53 Finance is rejected for a `Draft` expense → `403 AUTHORIZATION_FAILED` (*Finance is rejected for a Draft expense*, end-to-end)
- [x] 4.54 Compliance retrieves an `Approved` Client Entertainment expense → `200` (*Compliance retrieves an Approved or Compliance Approved Client Entertainment expense*, end-to-end)
- [x] 4.55 Compliance retrieves a `ComplianceApproved` Client Entertainment expense → `200` (*Compliance retrieves an Approved or Compliance Approved Client Entertainment expense*, end-to-end)
- [x] 4.56 Compliance is rejected for a `Reimbursed` Client Entertainment expense → `403 AUTHORIZATION_FAILED` (*Compliance is rejected for a Reimbursed Client Entertainment expense*, end-to-end)
- [x] 4.57 Compliance is rejected for a non-Client-Entertainment expense → `403 AUTHORIZATION_FAILED` (*Compliance is rejected for a non-Client-Entertainment expense*, end-to-end)
- [x] 4.58 Nonexistent expense → `404 RESOURCE_NOT_FOUND` (*Nonexistent expense is rejected*, end-to-end)
- [x] 4.59 No `Authorization` header → `401 AUTHENTICATION_FAILED` (*Unauthenticated request is rejected*, end-to-end)

**Checkpoint result:** `dotnet build` 0 errors/0 warnings · `dotnet format --verify-no-changes` clean · `dotnet test` 290/290 passed (173 unit incl. 51 new, 117 integration incl. 29 new). Test-writing delegated to the `test-writer` subagent per `/implement`'s 45-minute-task rule; content independently reviewed (Read, not just re-run) before accepting — all 32 spec scenarios across `expense-visibility` and `expense-maintenance`'s MODIFIED requirement confirmed traceable to a test.

## 5. Archive

- [x] 5.1 Re-run the full quality gate one final time: `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test` — all green (290/290: 173 unit, 117 integration)
- [x] 5.2 Run `openspec archive et009-expenses-view`
- [x] 5.3 Leave `docs/TICKETS.md` ET009 `Status` as `In progress` — `/pr` sets `PR open (#N)`; `Done` is reserved for after merge
