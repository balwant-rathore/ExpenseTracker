## Context

ET007–ET011 established one pattern for the whole `Expense` aggregate: a single
`IExpenseService`/`ExpenseService` in `Application/Expenses/`, backed by
`IExpenseRepository`/`ExpenseRepository` in `Infrastructure/Persistence/Repositories/`,
with `IQueryable` composition for filtering/sorting/paging (`GetPagedAsync`), a shared
`ExpenseVisibility.BuildPredicate` for role-based visibility, a flat `ExpenseFailureReason`
enum mapped to HTTP responses once in `ExpensesController`, and audit-field-only DTOs
(`ExpenseResponse`) that never expose "who" fields, only "when" timestamps.

ET012 (FRS §7) extends this same aggregate with three Finance-only capabilities: search
(`finance-expense-search`), a `reimburse` workflow action (`expense-reimbursement`), and a
data-only report scaffold (`monthly-reimbursement-report`). No new aggregate, no schema
change — `Expense.ReimbursedAt`/`ReimbursedByEmployeeId` already exist
(`docs/SDS.md` §3.6, confirmed in `Domain/Entities/Expense.cs`) and
`ExpenseSortField.ReimbursedAt` is already defined — ET007–ET009 scaffolded these ahead of
need. This design's job is to follow the established pattern, not invent a new one.

## Goals / Non-Goals

**Goals:**
- `POST /api/expenses/search`, `POST /api/expenses/{id}/reimburse`,
  `GET /api/reports/monthly-reimbursement` implemented per the three approved spec deltas.
- Zero EF Core migrations — reuse existing columns/enum values.
- Follow the exact layering and reuse conventions from ET007–ET011 (`backend/CLAUDE.md`),
  so the diff reads as "more of the same," not a new pattern.

**Non-Goals:**
- Excel (`.xlsx`) generation — `GET /api/reports/monthly-reimbursement` returns JSON in
  this ticket; ET014 swaps the response format, confirmed ET012/ET014 boundary
  (proposal.md).
- Any frontend work (ET018/ET019).
- Fixing the pre-existing app-wide inconsistency where workflow timestamps
  (`CreatedAt`/`ApprovedAt`/etc.) are stored as raw `DateTime.UtcNow` rather than
  converted through `ICompanyClock`, despite BR-10. This ticket's new date-range filter
  and report-month boundary follow that same existing convention (plain UTC comparison)
  rather than introducing a one-off exception — see Decision D4.

## Decisions

### D1 — Extend `IExpenseService`/`ExpenseService`, do not create a new service
`SearchAsync` and `ReimburseAsync` are added to the existing `IExpenseService` interface
in `src/Application/Expenses/IExpenseService.cs` and implemented in
`src/Application/Expenses/ExpenseService.cs`, alongside `GetVisibleAsync`,
`ApproveAsync`, etc. **Alternative considered**: a new `IExpenseSearchService` /
`IExpenseWorkflowService` split by verb-family. Rejected — ET007–ET011 already
consolidated every Expense operation (create/query/workflow) into one service; splitting
now would be a structural deviation this ticket wasn't asked to make, and
`backend/CLAUDE.md`'s "single responsibility" guidance is already satisfied at the
aggregate level (one service per aggregate, matching the one-repository-per-aggregate
rule in `openspec/specs/domain-model/spec.md`).

### D2 — Category-conditioned reimbursement precondition, single failure reason
`ReimburseAsync` checks eligibility with one private predicate:
```csharp
private static bool IsEligibleForReimbursement(Expense expense) =>
    expense.Category == ExpenseCategory.ClientEntertainment
        ? expense.Status == ExpenseStatus.ComplianceApproved
        : expense.Status == ExpenseStatus.Approved;
```
A single new `ExpenseFailureReason.NotEligibleForReimbursement` covers every failing
case (wrong category/status combination, or a terminal status), mapped once in
`ExpensesController.FailureResult` to `422 BUSINESS_RULE_VIOLATION`. **Alternative
considered**: two reasons (`NotClientEntertainmentReady` / `NotApprovedForReimbursement`),
mirroring the two-reason split `CanComplianceReview` uses. Rejected — compliance review
splits on two *independently meaningful* checks (category first, then status, per the
existing code comment "these are two independently-tested business rules"). Reimbursement
has only one combined rule ("is this expense, given its own category, at the right
status"), so one reason keeps `ExpenseFailureReason` from growing reasons that all produce
the identical response.

### D3 — `ExpenseResponse` gains `ReimbursedAt`
`src/Application/Expenses/ExpenseResponse.cs` adds a `DateTime? ReimbursedAt` field,
populated in `ExpenseService.Map()`. Every other workflow timestamp
(`SubmittedAt`/`ApprovedAt`/`ComplianceApprovedAt`/`RejectedAt`) is already exposed;
`ReimbursedAt` was the one gap, unreachable until this ticket adds the transition that
sets it. This is additive (new nullable field, always `null` before this ticket) — no
existing consumer breaks. Per `AGENTS.md` §13's "same as X is a checklist" rule: since
"expose every workflow timestamp" is the established pattern across four prior tickets,
`ReimbursedAt` completes that checklist rather than leaving a fifth, silently-missing
item.

### D4 — Date filters compare raw UTC `DateTime`, no company-timezone conversion
FRS §7.1.1's "Date Range (created date range)" filters `Expense.CreatedAt`. `CreatedAt`
is written as `DateTime.UtcNow` (see `ExpenseService.CreateAsync`) — not converted
through `ICompanyClock`, unlike the user-entered `ExpenseDate`. The search request's
`FromDate`/`ToDate` (`DateOnly?`) are converted to UTC bounds with
`FromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)` and
`ToDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)` — i.e., treated as UTC calendar
days, matching how `CreatedAt` was written, not reinterpreted through
`Asia/Kolkata`. Same reasoning applies to the monthly report's month boundary
(`new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc)` .. `.AddMonths(1)`), filtered
against `ReimbursedAt` (also raw `DateTime.UtcNow`). **Alternative considered**:
convert through `ICompanyClock`'s `Asia/Kolkata` zone for a "true" company-local day
boundary. Rejected for this ticket — every other workflow timestamp in the codebase is
already UTC-only; introducing timezone conversion for just this one filter would filter
inconsistently against columns that were never timezone-adjusted at write time, and
fixing that mismatch app-wide is out of scope here (Non-Goals).

### D5 — New repository methods stay in `Infrastructure`, `IExpenseRepository` interface stays flat
Two additions to `src/Domain/Repositories/IExpenseRepository.cs` /
`src/Infrastructure/Persistence/Repositories/ExpenseRepository.cs`, mirroring the
existing `GetPagedAsync` shape (flat parameters, not a filter-object DTO — `Domain`
must not reference `Application`-layer types):
```csharp
Task<(IReadOnlyList<Expense> Items, int TotalRecords)> SearchPagedAsync(
    string? expenseNumber, string? employeeName, ExpenseCategory? category,
    ExpenseStatus? status, DateTime? createdFromUtc, DateTime? createdToUtc,
    ExpenseSortField sortBy, bool descending, int page, int pageSize,
    CancellationToken cancellationToken);

Task<IReadOnlyList<Expense>> GetReimbursedForReportAsync(
    DateTime rangeStartUtcInclusive, DateTime rangeEndUtcExclusive,
    CancellationToken cancellationToken);
```
`SearchPagedAsync` hardcodes `e.Status != ExpenseStatus.Draft` as its base predicate
(same expression `ExpenseVisibility.BuildPredicate` uses for `Finance`), then layers each
supplied filter with `.Where(...)` exactly like `GetPagedAsync` layers `statusFilter`.
Because the Draft-exclusion and an explicit `status: "Draft"` filter combine with AND,
`Status != Draft && Status == Draft` is always false — satisfying the "explicit
status=Draft returns empty, not ignored" scenario with no special-case branch.
**Alternative considered**: expose `IExpenseRepository.Query()` (already public) directly
to a new `Application`-layer LINQ composition. Rejected — `Application` has no EF Core
package reference (verified: `Application.csproj` has no
`Microsoft.EntityFrameworkCore` `PackageReference`), so it cannot call `.Include()` for
the `Employee` navigation the `employeeName` filter and response mapping both need;
doing the Include-and-filter in `Infrastructure` (like every existing repository method)
avoids adding that package reference.

### D6 — `employeeName` filter: case-insensitive contains on `"{FirstName} {LastName}"`
```csharp
filtered.Where(e => (e.Employee.FirstName + " " + e.Employee.LastName).Contains(employeeName))
```
Relies on SQL Server's default case-insensitive collation (the same assumption
`docs/SDS.md` §3.3's case-insensitive-unique `Email` already depends on) — no explicit
`.ToLower()` needed. This is an open decision beyond FRS/SDS's literal text (neither
specifies partial-vs-exact matching); confirmed as the finance-expense-search spec's
documented assumption during `/spec`, not reopened here.

### D7 — New `Application/Reports/` folder, new `ReportsController`, new DI extension
Mirrors the existing feature-folder convention (`Application/Expenses/`,
`Application/Auth/`, `Application/Notifications/` each have their own DI extension
method called from `Program.cs`):
- `src/Application/Reports/MonthlyReimbursementQuery.cs` — `record { int? Year; int? Month; }`
- `src/Application/Reports/MonthlyReimbursementQueryValidator.cs`
- `src/Application/Reports/MonthlyReimbursementRecord.cs`
- `src/Application/Reports/MonthlyReimbursementReportResponse.cs` — `record(IReadOnlyList<MonthlyReimbursementRecord> Items)`
- `src/Application/Reports/IReportService.cs` / `ReportService.cs`
- `src/Api/Extensions/ReportServiceCollectionExtensions.cs` — `AddReportFoundation`
- `src/Api/Controllers/ReportsController.cs`
`Program.cs` gets one new line, `builder.Services.AddReportFoundation();`, alongside the
existing `AddExpenseFoundation`/`AddAttachmentFoundation` calls. `ReportService` depends
only on `IExpenseRepository` (no new repository interface for a one-query read model).

### D8 — `MonthlyReimbursementRecord.ApprovalDate` via null-coalescing, not a category branch
```csharp
ApprovalDate = expense.ComplianceApprovedAt ?? expense.ApprovedAt
```
`ComplianceApprovedAt` is non-null if and only if the expense is `ClientEntertainment`
and has passed compliance review (the only path that sets it); every other reimbursed
expense has it `null` and falls back to `ApprovedAt`. This produces the exact
category-conditioned semantics the spec describes (`ComplianceApprovedAt` for Client
Entertainment, `ApprovedAt` otherwise) without re-deriving `Category`-based branching
that duplicates D2's logic for a different purpose.

### D9 — Validation, per-property (per `backend/CLAUDE.md` Gotchas)
Every new bound-from-request property, checked individually against the "missing JSON
key / missing query key" failure mode:

| DTO | Property | Type | Missing-key behavior | Explicit check needed? |
|---|---|---|---|---|
| `ExpenseSearchRequest` (JSON body) | `ExpenseNumber`, `EmployeeName`, `Category`, `Status` | `string?` | defaults to `null` → filter skipped | No — `null` is a valid "no filter" state, same pattern as `ExpenseListRequest.Status` |
| | `FromDate`, `ToDate` | `DateOnly?` | defaults to `null` → filter skipped | No |
| | `Page`, `PageSize`, `SortBy`, `SortDirection` | non-nullable w/ initializers | missing key → property initializer default applies (proven behavior, identical fields already in `ExpenseListRequest`) | No — reuses `ExpenseListRequestValidator.AllowedSortFields`/`AllowedSortDirections`; new `AllowedPageSizes = [20, 50, 100, 500]` per `docs/SDS.md` §5.4 (distinct from the list endpoint's `[10, 20, 50, 100]`) |
| `MonthlyReimbursementQuery` (query string) | `Year`, `Month` | `int?` | missing key → `null` | Yes — `NotNull()` on both (query-string binding of a non-nullable `int` would silently default to `0` instead of failing; `int?` makes "absent" observable to FluentValidation, avoiding that trap) plus `InclusiveBetween(1, 12)` on `Month` and `InclusiveBetween(1, 9999)` on `Year` (bounds `new DateTime(year, month, 1, ...)` in `ReportService` from throwing `ArgumentOutOfRangeException` → unhandled 500) |

No property on any new DTO uses `required` or a real C# enum — all follow the
`string?`/nullable-primitive + explicit-validator rule from `backend/CLAUDE.md` Gotchas.

### D10 — Controller wiring follows the existing `FailureResult` switch pattern
`ExpensesController` gains one `case ExpenseFailureReason.NotEligibleForReimbursement`
arm in its existing `FailureResult` switch (`422 BUSINESS_RULE_VIOLATION`, message "Only
Approved (non-Client Entertainment) expenses or Compliance Approved (Client
Entertainment) expenses can be reimbursed."), plus a `[HttpPost("{id:guid}/reimburse")]`
action and `[HttpPost("search")]` action, both `[Authorize(Policy =
AuthorizationPolicyNames.Finance)]` — that policy already exists
(`AuthServiceCollectionExtensions.cs:76`), no new policy registration needed.
`ReportsController` is a new, separate controller (`api/reports` base route, distinct
resource from `api/expenses`, matching `docs/SDS.md` §5.6's separate endpoint family) with
its own `[Authorize(Policy = AuthorizationPolicyNames.Finance)]` at the class level.

## Risks / Trade-offs

- **[Risk]** `employeeName`'s `Contains()` translates to a `LIKE '%...%'` with no
  supporting index (`docs/SDS.md` §3.6 indexes `EmployeeId`/`Status`/`Category`/
  `ExpenseDate`/`CreatedAt`/`SubmittedAt`, none on `Employee.FirstName`/`LastName`) →
  full scan on large datasets. **Mitigation**: acceptable at this project's scale (no
  FRS/SDS performance target stated for search); `backend/CLAUDE.md`'s "apply
  filtering/sorting/paging in the query itself" is satisfied (no in-memory
  materialization) even though the underlying scan itself isn't index-assisted — an
  index would be a separate, unrequested optimization.
- **[Risk]** D4's "no timezone conversion" choice means a search for `fromDate=2026-07-01`
  captures expenses created from 2026-07-01T00:00:00Z, which is 2026-07-01 05:30 IST
  onward, not company-midnight — a ~5.5-hour edge skew at each boundary.
  **Mitigation**: matches the pre-existing, already-shipped behavior of every other
  timestamp filter/comparison in the codebase; not a new defect introduced by this
  ticket, and consistent > locally-correct per Non-Goals.
- **[Trade-off]** `GET /api/reports/monthly-reimbursement` returning JSON now means its
  response shape will change (to a binary `.xlsx` stream) when ET014 lands — a breaking
  change to this endpoint's contract, but an intentional, already-confirmed one
  (proposal.md's ET012/ET014 boundary decision), not an accident.

## Migration Plan

No EF Core migration — no schema change. Reuses existing `Expense` columns
(`ReimbursedAt`, `ReimbursedByEmployeeId`) and existing `ExpenseSortField.ReimbursedAt`.
Purely additive code (new endpoints, new DTOs, one new field on an existing response
DTO) — safe to deploy without a rollback plan beyond a normal revert.

## File Manifest

**New:**
- `src/Application/Expenses/ExpenseSearchRequest.cs`
- `src/Application/Expenses/ExpenseSearchRequestValidator.cs`
- `src/Application/Reports/MonthlyReimbursementQuery.cs`
- `src/Application/Reports/MonthlyReimbursementQueryValidator.cs`
- `src/Application/Reports/MonthlyReimbursementRecord.cs`
- `src/Application/Reports/MonthlyReimbursementReportResponse.cs`
- `src/Application/Reports/IReportService.cs`
- `src/Application/Reports/ReportService.cs`
- `src/Api/Extensions/ReportServiceCollectionExtensions.cs`
- `src/Api/Controllers/ReportsController.cs`
- `tests/UnitTests/Application/Expenses/ExpenseSearchRequestValidatorTests.cs`
- `tests/UnitTests/Application/Reports/MonthlyReimbursementQueryValidatorTests.cs`
- `tests/UnitTests/Application/Reports/ReportServiceTests.cs`
- `tests/IntegrationTests/ExpenseSearchTests.cs`
- `tests/IntegrationTests/ExpenseReimbursementTests.cs`
- `tests/IntegrationTests/MonthlyReimbursementReportTests.cs`

**Modified:**
- `src/Application/Expenses/IExpenseService.cs` — add `SearchAsync`, `ReimburseAsync`
- `src/Application/Expenses/ExpenseService.cs` — implement both, add `Map()` field
- `src/Application/Expenses/ExpenseResponse.cs` — add `ReimbursedAt`
- `src/Application/Expenses/ExpenseFailureReason.cs` — add `NotEligibleForReimbursement`
- `src/Domain/Repositories/IExpenseRepository.cs` — add `SearchPagedAsync`,
  `GetReimbursedForReportAsync`
- `src/Infrastructure/Persistence/Repositories/ExpenseRepository.cs` — implement both
- `src/Api/Controllers/ExpensesController.cs` — add `Search`, `Reimburse` actions +
  `FailureResult` switch arm
- `src/Api/Program.cs` — `builder.Services.AddReportFoundation();`
- `tests/UnitTests/Application/Expenses/ExpenseServiceTests.cs` — add
  `SearchAsync`/`ReimburseAsync` coverage
- `docs/TICKETS.md` — status update (handled by `/implement`, not this design)

## Verification

```bash
# from backend/
dotnet build                                              # compile + analyzers
dotnet format --verify-no-changes                         # EditorConfig lint gate
dotnet test --filter FullyQualifiedName~UnitTests         # unit
dotnet test --filter FullyQualifiedName~IntegrationTests  # integration (search, reimburse, report)
```
No frontend changes in this ticket — no `pnpm --filter frontend` gate applies. No E2E
gate — ET012 has no user-facing flow (frontend Finance UI is ET018/ET019).

## Open Questions

None outstanding — the four ambiguities identified during `/spec` (reimburse
precondition, ET012/ET014 boundary, search Draft-visibility, self-reimbursement) were
resolved with the ticket owner and are recorded as decisions in proposal.md and the
approved spec deltas. This design introduces no further open decisions beyond D1–D10
above, each already justified against existing code/spec.
