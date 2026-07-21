## Context

ET008 already built `Application/Expenses/` (`ExpenseService`, `IExpenseService`,
`ExpenseResult`/`ExpenseFailureReason`, `ExpenseResponse`/`ExpenseEnvelopeResponse`) and
`Api/Controllers/ExpensesController.cs` (`POST ""`, `POST "{id}/submit"`, `GET "{id}"`
owner-only, `PUT "{id}"`, `POST "{id}/cancel"`), plus `IExpenseRepository`/
`ExpenseRepository` (`Infrastructure/Persistence/Repositories/`). This ticket adds one new
action (`GET ""`, list) to the *same* controller/service, and widens the existing
`GetById` action's visibility rather than introducing a new module.

Constraints from `docs/SDS.md`/`backend/CLAUDE.md` that shape this design: role is never
trusted from the JWT — `EmployeeRoleResolutionMiddleware` (ET003/ET004) already re-derives
it fresh from the `Employee` record every request and stamps a `ClaimTypes.Role` claim onto
the request's `ClaimsPrincipal`; all list/search queries must filter, sort, and page in the
database query itself, never in memory (`backend/CLAUDE.md` "Framework Patterns"); the
`Application` project has no `Microsoft.EntityFrameworkCore` package reference (confirmed
via `Application.csproj`), so EF-only operators (`Include`, `ToListAsync`, `CountAsync`)
must stay inside `Infrastructure`, not `Application`.

## Goals / Non-Goals

**Goals:**
- `GET /api/expenses` per `specs/expense-visibility/spec.md`: paged, sorted, per-role
  default visibility (Employee/Manager/Finance/ComplianceOfficer).
- Widen `GET /api/expenses/{id}` (`specs/expense-maintenance/spec.md` MODIFIED
  requirement) to the same per-role visibility rules.
- A single, shared visibility rule implementation reused by both endpoints (no duplicated
  role-branching logic — AGENTS.md §13).

**Non-Goals** (per `docs/TICKETS.md` build order):
- `POST /api/expenses/search` (Finance advanced search with filters) — ET012.
- Manager approve/reject, compliance actions, reimbursement — ET010–ET012.
- Dashboard aggregation — ET013. Frontend consumption of these endpoints — ET017/ET019.
- Recursive/indirect-report visibility — explicitly out of scope per `/spec` clarification
  (direct reports only).

## Decisions

### D1 — One shared visibility predicate, reused by both endpoints
A new `ExpenseVisibility.BuildPredicate(EmployeeRole role, Guid employeeId)` (`Application/
Expenses/ExpenseVisibility.cs`) returns a single `Expression<Func<Expense, bool>>` per role
(`docs/FRS.md` §4.4.1–4.4.4, extended per `/spec` clarification for Compliance — see D-below
on ADR waiver). `GetVisibleAsync` passes this expression straight to the repository as a SQL
`Where` clause (list). `GetByIdAsync` compiles the *same* expression and invokes it in
memory against the one already-fetched `Expense`. One implementation, two call sites — not
two independently-maintained copies of the same role logic, per `AGENTS.md` §13's "same as
X is a checklist, not a phrase."

Alternative rejected: hand-roll the role `switch` separately inside `GetByIdAsync` and
`GetVisibleAsync`. Rejected — this is exactly the "verify one instance, assume the rest
follow" failure mode §13 was written to prevent; a future change to one role's rule could
silently drift from the other endpoint's copy.

### D2 — New typed repository methods, not `Query()` exposed to Application
`IExpenseRepository` gains `GetByIdWithEmployeeAsync` and `GetPagedAsync` — both accept
plain values (or an `Expression<Func<Expense,bool>>`, which is BCL `System.Linq.Expressions`,
not an EF Core type) and return already-materialized results. All EF-specific composition
(`Include`, `OrderBy`/`OrderByDescending` switch, `Skip`/`Take`, `CountAsync`,
`ToListAsync`) stays inside `ExpenseRepository` (`Infrastructure`). `Query()` (used today
only by `Infrastructure/BackgroundServices/OrphanAttachmentSweeper`) is left untouched and
not additionally exposed to `Application` — `Application.csproj` has no EF Core package
reference, and giving `Application` a `Microsoft.EntityFrameworkCore`-typed `IQueryable` to
call `.Include()`/`.ToListAsync()` on would either require adding that package reference to
`Application` (violating the Api→Application→Domain→Infrastructure layering, where EF Core
is Infrastructure's concern) or silently fail to compile.

Alternative rejected: expose `IExpenseRepository.Query()` to `ExpenseService` and compose
`.Include()`/`.ToListAsync()` there. Rejected for the layering reason above.

### D3 — `ExpenseResponse.EmployeeName` is nullable, populated only where `Employee` is loaded
`ExpenseResponse` gains `string? EmployeeName`, computed in the existing `Map()` helper as
`$"{expense.Employee.FirstName} {expense.Employee.LastName}"` when `expense.Employee` is
non-null, else `null`. `GetByIdAsync` (via the new `GetByIdWithEmployeeAsync`) and
`GetVisibleAsync` (via `GetPagedAsync`, which eager-loads `Employee`) always populate it —
these are exactly the multi-employee-spanning views the proposal calls for. `CreateAsync`/
`SubmitAsync`/`UpdateAsync`/`CancelAsync` are untouched (still call the plain
`GetByIdAsync`/`TryAddAsync`/`TryUpdateAsync` repository methods with no `Employee` include)
and continue to return `EmployeeName: null` — those flows are always the caller acting on
their own expense, so the caller already knows their own name and an extra join buys
nothing.

Alternative rejected: eager-load `Employee` in every write-path method too, for a uniformly
non-null field. Rejected as an unrequested extra join on four methods that have no use for
it — outside this proposal's stated scope.

### D4 — New `NotVisible` failure reason, kept distinct from `NotOwner`
`GetByIdAsync`'s rejection case is no longer strictly "you are not the owner" once
Manager/Finance/Compliance can also succeed (e.g. Finance is rejected for a `Draft`
expense it doesn't own *or* co-manage — "not owner" would misdescribe that). A new
`ExpenseFailureReason.NotVisible` is added, mapped in `ExpensesController.FailureResult`
to the same `403 AUTHORIZATION_FAILED` response text as `NotOwner` (identical HTTP
behavior, accurate internal naming). `SubmitAsync`/`UpdateAsync`/`CancelAsync` keep
`NotOwner` unchanged — those three remain strictly ownership-scoped, untouched by this
ticket.

### D5 — Role passed explicitly into `IExpenseService`, sourced from the already-trustworthy request claim
`EmployeeRoleResolutionMiddleware` already re-derives the caller's role fresh from the
`Employee` record on every request (never from the raw JWT) and stamps a `ClaimTypes.Role`
claim onto that request's principal — this is the existing, established trust boundary
(`AGENTS.md` §11, already relied on by every `[Authorize(Policy=...)]` role check in this
codebase). A new `ClaimsPrincipalExtensions.GetRole()` reads that claim (mirroring the
existing `GetEmployeeId()`), and `ExpensesController` passes the resolved `EmployeeRole`
into `IExpenseService.GetByIdAsync`/`GetVisibleAsync` as an explicit parameter.

Alternative rejected: have `ExpenseService` call `IEmployeeRepository.GetByIdAsync` itself
to re-derive role. Rejected as a redundant DB round-trip duplicating what the middleware
already guarantees fresh on every request.

### D6 — Controller authorization: bare `[Authorize]` at class level, `EmployeeOrManager` pushed onto mutating actions
`ExpensesController`'s class-level attribute changes from `[Authorize(Policy =
EmployeeOrManager)]` to bare `[Authorize]` (any authenticated caller — there are exactly 4
roles, and two of them, `Finance`/`ComplianceOfficer`, must now reach `GetById`/the new
`GetAll`). `[Authorize(Policy = EmployeeOrManager)]` is added explicitly at the action
level on `Create`, `Submit`, `Update`, `Cancel` — the four actions that must stay
Employee/Manager-only. ASP.NET Core combines controller- and action-level `[Authorize]`
policies with AND, so this is not a loosening for those four actions; `GetById`/`GetAll`
simply have no additional policy beyond "authenticated," which is exactly correct since no
5th role exists.

Alternative rejected: introduce a new `AnyRole` policy and apply it to `GetById`/`GetAll`.
Rejected as pure ceremony — a role-enumerating policy that lists all 4 existing roles is
equivalent to no role restriction at all, and ASP.NET's bare `[Authorize]` already expresses
that directly.

### D7 — Manager visibility is direct-reports-only; Compliance visibility extended to `ComplianceApproved` (no ADR)
Both confirmed with the ticket owner during `/spec`. Manager: `e.Employee.ManagerId ==
employeeId` (a direct FK comparison via the `Expense.Employee` navigation — EF translates
this to a SQL join against `Employees`; no recursive CTE). Compliance:
`Status == Approved || Status == ComplianceApproved` (excludes `Reimbursed`) — this extends
past `docs/FRS.md` §4.4.4's literal text ("can only view `Approved` expenses"), but per the
ticket owner's explicit direction during `/spec` this is recorded here and in
`proposal.md`/the spec delta rather than raised as a separate ADR (unlike ET008's
Draft-cancellation deviation, which the ticket owner did require as an ADR) — the ticket
owner distinguished this as a narrower, self-contained scope note not warranting the
overhead of a standalone decision record.

### D8 — Query parameters validated before the service is ever called
A new `ExpenseListRequestValidator` (FluentValidation, auto-registered by the existing
`AddValidatorsFromAssemblyContaining<RegisterRequestValidator>()` scan — no DI change)
rejects out-of-range `page`, `pageSize` not in `{10, 20, 50, 100}`, `sortBy` outside the 8
allowed fields, or `sortDirection` outside `{asc, desc}` with `400 VALIDATION_ERROR`, before
`ExpenseService.GetVisibleAsync` is ever invoked — mirroring the existing
`CreateExpenseRequestValidator`/`UpdateExpenseRequestValidator` field-level-validation-first
pattern.

## File Plan

**New — `backend/src/Application/Expenses/`:**
- `ExpenseListRequest.cs` — query-bound request record with defaulted properties (`Page = 1`,
  `PageSize = 20`, `SortBy = "expenseDate"`, `SortDirection = "desc"`).
- `ExpenseListRequestValidator.cs` — FluentValidation (D8).
- `PagedExpenseResponse.cs` — `record PagedExpenseResponse(IReadOnlyList<ExpenseResponse>
  Items, int Page, int PageSize, int TotalRecords)`, matching `docs/SDS.md` §5.2's response
  shape.
- `ExpenseVisibility.cs` — shared predicate builder (D1).

**New — `backend/src/Domain/Enums/`:**
- `ExpenseSortField.cs` — `ExpenseDate, ExpenseNumber, CreatedAt, Amount, SubmittedAt,
  ApprovedAt, ReimbursedAt, RejectedAt` (matches `docs/SDS.md` §5.2's allowed `sortBy`
  values exactly).

**Modified:**
- `backend/src/Application/Expenses/ExpenseResponse.cs` — add `string? EmployeeName` (D3).
- `backend/src/Application/Expenses/ExpenseFailureReason.cs` — add `NotVisible` (D4).
- `backend/src/Application/Expenses/IExpenseService.cs` — `GetByIdAsync` gains an
  `EmployeeRole role` parameter (D5); add `Task<PagedExpenseResponse>
  GetVisibleAsync(Guid employeeId, EmployeeRole role, ExpenseListRequest request,
  CancellationToken cancellationToken)`.
- `backend/src/Application/Expenses/ExpenseService.cs` — rewire `GetByIdAsync` (D1/D4);
  implement `GetVisibleAsync` (D1); update `Map()` for `EmployeeName` (D3).
- `backend/src/Domain/Repositories/IExpenseRepository.cs` — add `GetByIdWithEmployeeAsync`,
  `GetPagedAsync` (D2).
- `backend/src/Infrastructure/Persistence/Repositories/ExpenseRepository.cs` — implement
  both (D2).
- `backend/src/Api/Authentication/ClaimsPrincipalExtensions.cs` — add `GetRole()` (D5).
- `backend/src/Api/Controllers/ExpensesController.cs` — class attribute → bare
  `[Authorize]`; add `[Authorize(Policy = EmployeeOrManager)]` on `Create`/`Submit`/
  `Update`/`Cancel` (D6); add `GetAll` (`[HttpGet]`) action; update `GetById` to resolve
  and pass `User.GetRole()`; inject `IValidator<ExpenseListRequest>`; extend
  `FailureResult`'s switch with `NotVisible` → `403` (D4).

**Test doubles requiring updates (new `IExpenseRepository` members):**
- `backend/tests/UnitTests/Application/Expenses/ExpenseNumberGeneratorTests.cs`'s
  `FakeExpenseRepository` must implement `GetByIdWithEmployeeAsync`/`GetPagedAsync` (same
  pattern as ET008's `TryUpdateAsync` fix) or the unit test project fails to build.

## DTOs / Records

```csharp
// ExpenseListRequest.cs — query-string bound; non-nullable with defaults (safe for
// [FromQuery] simple-type binding, unlike the `required`-on-JSON-body pitfall from
// backend/CLAUDE.md's ET007 gotcha, which only applies to body deserialization).
public record ExpenseListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string SortBy { get; init; } = "expenseDate";
    public string SortDirection { get; init; } = "desc";
}

// PagedExpenseResponse.cs — matches docs/SDS.md §5.2's { items, page, pageSize, totalRecords }
public record PagedExpenseResponse(
    IReadOnlyList<ExpenseResponse> Items,
    int Page,
    int PageSize,
    int TotalRecords);

// ExpenseResponse.cs — EmployeeName appended (D3); nullable, see Map() below
public record ExpenseResponse(
    Guid Id,
    string ExpenseNumber,
    DateOnly ExpenseDate,
    string Category,
    decimal Amount,
    string Currency,
    string Description,
    string Status,
    DateTime? SubmittedAt,
    DateTime CreatedAt,
    string? EmployeeName);

// ExpenseFailureReason.cs — NotVisible added (D4)
public enum ExpenseFailureReason
{
    None, AttachmentNotFound, AttachmentAlreadyLinked, AttachmentNotOwned,
    AmountNotPositive, ExpenseDateInFuture, ExpenseNotFound, NotOwner, NotDraft,
    CurrencyInvalid, DescriptionTooLong, NotEditable, NotCancellable, NotVisible,
}

// ExpenseSortField.cs
public enum ExpenseSortField
{
    ExpenseDate, ExpenseNumber, CreatedAt, Amount, SubmittedAt, ApprovedAt, ReimbursedAt, RejectedAt,
}

// ExpenseVisibility.cs (D1) — the single source of truth for role-based visibility
public static class ExpenseVisibility
{
    public static Expression<Func<Expense, bool>> BuildPredicate(EmployeeRole role, Guid employeeId) => role switch
    {
        EmployeeRole.Employee => e => e.EmployeeId == employeeId,
        EmployeeRole.Manager => e => e.EmployeeId == employeeId
            || (e.Employee.ManagerId == employeeId && e.Status != ExpenseStatus.Draft),
        EmployeeRole.Finance => e => e.Status != ExpenseStatus.Draft,
        EmployeeRole.ComplianceOfficer => e => e.Category == ExpenseCategory.ClientEntertainment
            && (e.Status == ExpenseStatus.Approved || e.Status == ExpenseStatus.ComplianceApproved),
        _ => e => false,
    };
}
```

`IExpenseRepository` additions:

```csharp
Task<Expense?> GetByIdWithEmployeeAsync(Guid id, CancellationToken cancellationToken);

Task<(IReadOnlyList<Expense> Items, int TotalRecords)> GetPagedAsync(
    Expression<Func<Expense, bool>> visibilityPredicate,
    ExpenseSortField sortBy,
    bool descending,
    int page,
    int pageSize,
    CancellationToken cancellationToken);
```

`IExpenseService` additions/changes:

```csharp
Task<ExpenseResult> GetByIdAsync(Guid employeeId, EmployeeRole role, Guid expenseId, CancellationToken cancellationToken); // MODIFIED: +role
Task<PagedExpenseResponse> GetVisibleAsync(Guid employeeId, EmployeeRole role, ExpenseListRequest request, CancellationToken cancellationToken); // NEW
```

`ExpenseService` method logic:

- **`GetByIdAsync`**: `GetByIdWithEmployeeAsync` → null → `ExpenseNotFound`. Compile
  `ExpenseVisibility.BuildPredicate(role, employeeId)` and invoke against the fetched
  expense → `false` → `NotVisible` (D4). Otherwise `Success(Map(expense))`.
- **`GetVisibleAsync`**: build the predicate (uncompiled — passed as an `Expression` for SQL
  translation); parse `request.SortBy`/`request.SortDirection` (safe — `ExpenseListRequestValidator`
  already guarantees valid values, D8, mirroring the existing "non-null by this point"
  comment convention from `CreateAsync`/`UpdateAsync`); call `GetPagedAsync`; map each
  returned `Expense` through the existing `Map()` helper; return `PagedExpenseResponse`.

`Map()` updated:

```csharp
private static ExpenseResponse Map(Expense expense)
{
    var employeeName = expense.Employee is not null
        ? $"{expense.Employee.FirstName} {expense.Employee.LastName}"
        : null;

    return new ExpenseResponse(
        expense.Id, expense.ExpenseNumber, expense.ExpenseDate, expense.Category.ToString(),
        expense.Amount, expense.Currency, expense.Description, expense.Status.ToString(),
        expense.SubmittedAt, expense.CreatedAt, employeeName);
}
```

Controller failure-reason → HTTP mapping addition: `NotVisible` → `403
AUTHORIZATION_FAILED` ("You do not have permission to perform this action." — same text as
`NotOwner`, D4). All other existing mappings unchanged.

## DB Changes

None. `Expense.Employee`/`Employee.ManagerId` navigation already exists (ET002); no new
column, index, or migration. `GetPagedAsync`'s `Where`/`OrderBy`/`Skip`/`Take` composition
runs against the existing `Expenses`/`Employees` tables.

## Reuse

Confirmed already in place, reused as-is: `ClaimsPrincipalExtensions.GetEmployeeId()`,
`EmployeeRoleResolutionMiddleware`'s per-request `ClaimTypes.Role` claim (D5),
`ExpensesController.FailureResult`/`ValidationErrorResult`/error-envelope pattern,
FluentValidation assembly-scan auto-registration (no DI change for
`ExpenseListRequestValidator`), the `ExpenseResult`/`ExpenseFailureReason`/controller-`switch`
idiom from ET008, `Expense.Employee`/`Employee.ManagerId` (already modeled, ET002). No new
NuGet package, no new authorization policy (D6), no `Program.cs` change (all new types live
in already-registered `Application`/`Infrastructure` assemblies).

## Risks / Trade-offs

- **[New `IExpenseRepository` members break the `FakeExpenseRepository` test double]** →
  Mitigation: implement both new methods on the fake in the same change (ET008 hit and
  fixed this identical issue for `TryUpdateAsync`); caught immediately at the first
  `dotnet build` checkpoint.
- **[`EmployeeName` is nullable — populated on read endpoints, `null` on write-path
  responses]** → Mitigation: explicitly scoped and documented (D3); the only callers of
  write-path responses are the caller themself acting on their own expense, where the name
  is redundant.
- **[Manager visibility predicate relies on EF translating `e.Employee.ManagerId` into a SQL
  join correctly]** → Mitigation: this is a direct FK-equality comparison through an
  existing, already-configured navigation property — a standard EF Core translation
  pattern, not a novel query shape; verified via integration test assertions on
  `totalRecords`/`items` contents for the direct-report scenarios.
- **[Changing the controller's class-level `[Authorize]` from `EmployeeOrManager` to bare
  `[Authorize]` could look, out of context, like a loosening]** → Mitigation: every
  mutating action (`Create`/`Submit`/`Update`/`Cancel`) explicitly re-declares
  `[Authorize(Policy = EmployeeOrManager)]` at the action level (D6); documented here so a
  future action added to this controller must consciously choose a policy.

## Migration Plan

1. Add `ExpenseSortField.cs` (Domain), `NotVisible` to `ExpenseFailureReason.cs`.
2. Add `ExpenseVisibility.cs` (D1).
3. Add `IExpenseRepository.GetByIdWithEmployeeAsync`/`GetPagedAsync`; implement in
   `ExpenseRepository` (D2); update `FakeExpenseRepository` test double.
4. Add `ExpenseListRequest.cs`/`ExpenseListRequestValidator.cs`/`PagedExpenseResponse.cs`
   (D8).
5. Update `ExpenseResponse.cs` (D3) and `ExpenseService.Map()`.
6. Update `IExpenseService`/`ExpenseService`: rewire `GetByIdAsync` (D1/D4/D5); add
   `GetVisibleAsync` (D1/D5).
7. Add `ClaimsPrincipalExtensions.GetRole()` (D5).
8. Update `ExpensesController`: class attribute, per-action policies (D6), new `GetAll`
   action, `GetById` call-site update, `FailureResult` switch extension.

Rollback: revert the commit(s); no data migration exists to unwind (no schema change).

## Build/Test/Lint Checkpoints

Run after each numbered step above, in this order, stopping at the first failure
(`backend/CLAUDE.md`, root `CLAUDE.md` Quality Gates):

1. `dotnet build` (from `backend/`) — after every file change.
2. `dotnet test --filter FullyQualifiedName~UnitTests` — `ExpenseListRequestValidator`
   (field-level rules), `ExpenseVisibility.BuildPredicate` (one test per role, both
   allow/deny branches), `ExpenseService.GetByIdAsync`/`GetVisibleAsync` (ownership/
   visibility/pagination/sorting), and confirm `FakeExpenseRepository`'s new members don't
   break existing `ExpenseNumberGeneratorTests`.
3. `dotnet test --filter FullyQualifiedName~IntegrationTests` — `WebApplicationFactory`
   tests for `GET /api/expenses` and the widened `GET /api/expenses/{id}` covering every
   scenario in `specs/expense-visibility/spec.md` and the MODIFIED requirement in
   `specs/expense-maintenance/spec.md`.
4. `dotnet test` (full suite) — confirms no regression in ET007/ET008's existing
   `ExpenseSubmissionTests`/`ExpenseMaintenanceTests`/`ExpenseServiceTests` from the
   `ExpenseResponse`/`IExpenseService` signature changes.

No frontend or E2E gate applies — this ticket is backend-only (no `frontend/` changes, per
`docs/TICKETS.md` ET009 scope).

## Open Questions

None outstanding — the six ambiguities raised during `/spec` (Manager scope, Manager's own
expenses in the list, `pageSize` default, Compliance scope, `GetById` parity, out-of-scope
response code) were all resolved with the ticket owner and are recorded in `proposal.md`
above.
