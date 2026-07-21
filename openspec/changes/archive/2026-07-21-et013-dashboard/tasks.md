## 1. Foundation (contracts, DTOs, policy)

- [x] 1.1 Add `EmployeeOrManagerOrFinance` constant to `backend/src/Api/Authorization/AuthorizationPolicyNames.cs`
- [x] 1.2 Create `backend/src/Application/Dashboard/EmployeeDashboardResponse.cs` — `record(int TotalSubmitted, int Approved, int Reimbursed)`
- [x] 1.3 Create `backend/src/Application/Dashboard/ManagerDashboardResponse.cs` — `record(int TotalSubmitted, int Approved, int Reimbursed, int PendingApprovals)`
- [x] 1.4 Create `backend/src/Application/Dashboard/FinanceDashboardResponse.cs` — `record(int TotalSubmitted, int Approved, int Reimbursed, int PendingApprovals, int PendingReimbursements)`
- [x] 1.5 Create `backend/src/Application/Dashboard/IDashboardService.cs` with `GetEmployeeDashboardAsync(Guid employeeId, CancellationToken)`, `GetManagerDashboardAsync(Guid managerId, CancellationToken)`, `GetFinanceDashboardAsync(CancellationToken)`
- [x] 1.6 Create `backend/src/Application/Dashboard/DashboardScope.cs` — `BuildPredicate(EmployeeRole role, Guid employeeId)` per design.md D2 (Employee: own only; Manager: direct reports only via `Employee.ManagerId`, excludes manager's own; Finance: unrestricted)

**Checkpoint 1** (backend only — no frontend change in this ticket):
- `dotnet build` → 0 errors
- `dotnet format --verify-no-changes`

## 2. Core Implementation

- [x] 2.1 Implement `backend/src/Application/Dashboard/DashboardService.cs`: metric computation over `IExpenseRepository.GetStatusCategoryCountsAsync` per design.md D1/D3 (`totalSubmitted`, `approved` = Approved+ComplianceApproved, `reimbursed`, `pendingApprovals` = totalSubmitted, `pendingReimbursements` = (Approved AND Category != ClientEntertainment) + ComplianceApproved). Also added `IExpenseRepository.GetStatusCategoryCountsAsync` + `ExpenseRepository` implementation + `StatusCategoryCount` record — a correction to the original design (see design.md D1 "Alternative considered"): `Application` has no EF Core reference, so the query itself must live in `Infrastructure` behind a repository method.
- [x] 2.2 Create `backend/src/Api/Extensions/DashboardServiceCollectionExtensions.cs` — `AddDashboardFoundation()` registering `IDashboardService`/`DashboardService` and the `EmployeeOrManagerOrFinance` policy (`RequireRole(Employee, Manager, Finance)`), mirroring `AttachmentServiceCollectionExtensions`
- [x] 2.3 Create `backend/src/Api/Controllers/DashboardController.cs` — `[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManagerOrFinance)]`, `GET /api/dashboard`, exhaustive `switch` on `User.GetRole()` dispatching to the matching `IDashboardService` method (unreachable discard arm throws, per design.md D5)

**Checkpoint 2**:
- `dotnet build` → 0 errors
- `dotnet format --verify-no-changes`

## 3. Integration

- [x] 3.1 Wire `builder.Services.AddDashboardFoundation();` into `backend/src/Api/Program.cs` alongside the other `Add*Foundation()` calls
- [x] 3.2 Manually verify `GET /api/dashboard` appears in the Scalar/OpenAPI UI (`dotnet run --project src/Api`, dev environment) for a quick sanity check of the three response shapes — confirmed via `/openapi/v1.json` listing `/api/dashboard` and an unauthenticated request returning 401

**Checkpoint 3**:
- `dotnet build` → 0 errors

## 4. Tests

### 4.1 Unit tests — `backend/tests/UnitTests/Application/Dashboard/DashboardScopeTests.cs`
(reuses `FakeExpenseRepository` from `UnitTests.Application.Expenses.ExpenseNumberGeneratorTests.cs` where useful)

- [x] 4.1.1 Employee predicate matches only the caller's own expenses, excludes another employee's expense
- [x] 4.1.2 Manager predicate matches a direct report's expense (`Employee.ManagerId == callerId`)
- [x] 4.1.3 Manager predicate excludes the manager's own expense (scenario: "Manager's own expenses are excluded from every metric")
- [x] 4.1.4 Manager predicate excludes an indirect (two-level) report's expense (scenario: "Manager does not see an indirect report's expense")
- [x] 4.1.5 Finance predicate matches expenses regardless of owner

### 4.2 Unit tests — `backend/tests/UnitTests/Application/Dashboard/DashboardServiceTests.cs`

- [x] 4.2.1 `GetEmployeeDashboardAsync`: `totalSubmitted` counts only the caller's `Submitted` expenses, excludes another employee's `Submitted` expenses (scenario: "Employee sees only their own counts")
- [x] 4.2.2 `GetEmployeeDashboardAsync`: `approved` counts both `Approved` and `ComplianceApproved` (scenario: "Employee approved count includes Compliance Approved")
- [x] 4.2.3 `GetEmployeeDashboardAsync`: `Draft` and `Cancelled` expenses contribute `0` to every metric (scenario: "Employee Draft and Cancelled expenses are never counted")
- [x] 4.2.4 `GetManagerDashboardAsync`: `totalSubmitted`/`pendingApprovals` are `0` when only the manager's own `Submitted` expense exists, no direct reports (scenario: "Manager's own expenses are excluded from every metric")
- [x] 4.2.5 `GetManagerDashboardAsync`: `totalSubmitted`/`pendingApprovals` count a direct report's `Submitted` expense (scenario: "Manager sees only direct reports' counts")
- [x] 4.2.6 `GetManagerDashboardAsync`: an indirect report's `Submitted` expense is not counted (scenario: "Manager does not see an indirect report's expense")
- [x] 4.2.7 `GetManagerDashboardAsync`: `approved` counts both `Approved` and `ComplianceApproved` for a direct report (scenario: "Manager approved count includes Compliance Approved")
- [x] 4.2.8 `GetManagerDashboardAsync`: `Draft`/`Cancelled` direct-report expenses contribute `0` to all four metrics (scenario: "Manager Draft and Cancelled expenses are never counted")
- [x] 4.2.9 `GetFinanceDashboardAsync`: `totalSubmitted` counts `Submitted` expenses across two different employees/managers (scenario: "Finance sees organization-wide counts")
- [x] 4.2.10 `GetFinanceDashboardAsync`: `pendingReimbursements` is `0` for an `Approved` `ClientEntertainment` expense awaiting compliance (scenario: "Pending reimbursements excludes Approved Client Entertainment awaiting compliance")
- [x] 4.2.11 `GetFinanceDashboardAsync`: `pendingReimbursements` includes a `ComplianceApproved` `ClientEntertainment` expense (scenario: "Pending reimbursements includes Compliance Approved Client Entertainment")
- [x] 4.2.12 `GetFinanceDashboardAsync`: `pendingReimbursements` includes an `Approved` non-`ClientEntertainment` expense (scenario: "Pending reimbursements includes Approved non-Client-Entertainment expenses")
- [x] 4.2.13 `GetFinanceDashboardAsync`: `pendingApprovals` is `0` when only an `Approved` `ClientEntertainment` expense exists (no `Submitted` expenses) (scenario: "Pending approvals excludes Approved Client Entertainment awaiting compliance")
- [x] 4.2.14 `GetFinanceDashboardAsync`: `approved` counts both an `Approved` and a `ComplianceApproved` expense (scenario: "Finance approved count includes Compliance Approved")
- [x] 4.2.15 `GetFinanceDashboardAsync`: `Draft`/`Cancelled` expenses contribute `0` to all five metrics (scenario: "Finance Draft and Cancelled expenses are never counted")

### 4.3 Integration tests — `backend/tests/IntegrationTests/DashboardTests.cs` (via `WebApplicationFactory`, mirroring `ExpenseVisibilityTests.cs`/`ExpenseSearchTests.cs` conventions)

- [x] 4.3.1 `Employee`/`Manager`/`Finance` callers each receive `200` from `GET /api/dashboard` with their role's metrics present (scenario: "Authenticated Employee/Manager/Finance caller receives a dashboard")
- [x] 4.3.2 No `Authorization` header → `401 AUTHENTICATION_FAILED` (scenario: "Unauthenticated request is rejected")
- [x] 4.3.3 `ComplianceOfficer` caller → `403 AUTHORIZATION_FAILED` (scenario: "Compliance Officer is rejected")
- [x] 4.3.4 Employee response body has no `pendingApprovals` or `pendingReimbursements` property (scenario: "Employee response omits Manager/Finance-only fields")
- [x] 4.3.5 Manager response body has no `pendingReimbursements` property (scenario: "Manager response omits the Finance-only field")

**Checkpoint 4**:
- `dotnet build` → 0 errors
- `dotnet test --filter FullyQualifiedName~UnitTests` → all green (258/258)
- `dotnet test --filter FullyQualifiedName~IntegrationTests` → all green (228/228)
- `dotnet format --verify-no-changes`

## 5. Archive

- [x] 5.1 Run `openspec archive et013-dashboard` once all tests pass and the implementation matches `design.md`
- [x] 5.2 Update `docs/TICKETS.md`: ET013 `Status` stays `In progress` per `/implement`'s explicit instruction — archiving happens before the PR exists, so `PR open (#N)` is set later by `/pr`, not here
