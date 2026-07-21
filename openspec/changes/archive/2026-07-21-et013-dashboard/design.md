## Context

ET013 adds `GET /api/dashboard` (`docs/FRS.md` §8, `docs/SDS.md` §8.1–8.2), a read-only
aggregation endpoint. No new entities or schema changes are needed — it counts existing
`Expense` rows, grouped by `Status`/`Category`, scoped per the caller's role. The codebase
already has directly reusable precedent for every piece this needs:

- `Application/Expenses/ExpenseVisibility.cs` — role-based `Expression<Func<Expense, bool>>`
  predicate builder, used by `ExpenseService` for `GET /api/expenses` scoping.
- `IExpenseRepository.GetPagedAsync`/`SearchPagedAsync`/`GetReimbursedForReportAsync`
  (`Infrastructure/Persistence/Repositories/ExpenseRepository.cs`) — the established pattern
  for a query the `Application` layer needs but can't run itself: `Application` has no
  reference to the `Microsoft.EntityFrameworkCore` package (only `Domain`/`Shared`), so any
  `.ToListAsync()`/EF-translated `GroupBy` must live in `Infrastructure`, behind a
  `Domain.Repositories.IExpenseRepository` method the service calls. (`IExpenseRepository.
  Query()` does exist and returns a plain `IQueryable<Expense>`, but its only existing caller,
  `OrphanAttachmentSweeper`, lives in `Infrastructure` too — it is not an Application-layer
  escape hatch, since consuming it with `.ToListAsync()` requires the EF Core package that
  `Application` deliberately doesn't reference. Discovered as a build failure during
  implementation; see the `GetStatusCategoryCountsAsync` addition in D1.)
- `Api/Authorization/AuthorizationPolicyNames.cs` + a feature-local
  `services.AddAuthorization(options => options.AddPolicy(...))` block (see
  `AttachmentServiceCollectionExtensions.AddAttachmentFoundation`) — the established pattern
  for a multi-role policy. `EnvelopeAuthorizationMiddlewareResultHandler` already turns any
  `Forbid()` from a failed role policy into `403 AUTHORIZATION_FAILED` with no per-endpoint
  code needed — this is how Compliance Officer's rejection (spec requirement "Compliance
  Officer Has No Dashboard") is satisfied for free, the same way `ComplianceOfficer` is
  already rejected from `POST /api/expenses/search` (Finance-only) today.
- `Api/Extensions/ReportServiceCollectionExtensions.cs` — the minimal-DI-registration
  pattern for a single-service, no-new-entity feature; `DashboardServiceCollectionExtensions`
  follows the same shape.

## Goals / Non-Goals

**Goals:**
- Implement `GET /api/dashboard` exactly per `openspec/changes/et013-dashboard/specs/
  dashboard-summary/spec.md` — Employee (3 metrics), Manager (4 metrics, direct-reports-only,
  excludes the manager's own expenses), Finance (5 metrics, org-wide), Compliance Officer
  rejected with `403`.
- Single DB round-trip per request: one `GROUP BY (Status, Category)` query, scoped by role,
  with all five metrics computed from that one result set in memory (at most 7×7 = 49 rows).
- Zero schema/migration changes.

**Non-Goals:**
- No new EF Core entities, migrations, or indexes (existing `Status`, `Category`,
  `EmployeeId`, `ManagerId` indexes already cover this query shape).
- No caching of dashboard results — SDS §8 requires metrics generated from live/committed
  data on every call, same as Finance search and the monthly report.
- No frontend work (dashboard UI is ET019) and no OpenAPI doc beyond what `[ApiController]` +
  the DTOs auto-generate via the existing `AddOpenApi()` pipeline.

## Decisions

**D1 — One `GROUP BY (Status, Category)` query per request via a new
`IExpenseRepository.GetStatusCategoryCountsAsync` method, metrics derived in memory from its
result, instead of N separate `COUNT` queries per metric.**
A naive implementation could run 3–5 separate `CountAsync` calls per role (one per metric).
Instead, `DashboardService` calls one repository method:
```csharp
// Domain/Repositories/IExpenseRepository.cs
Task<IReadOnlyList<StatusCategoryCount>> GetStatusCategoryCountsAsync(
    Expression<Func<Expense, bool>> scopePredicate,
    CancellationToken cancellationToken);

// Infrastructure/Persistence/Repositories/ExpenseRepository.cs
public async Task<IReadOnlyList<StatusCategoryCount>> GetStatusCategoryCountsAsync(
    Expression<Func<Expense, bool>> scopePredicate,
    CancellationToken cancellationToken)
{
    return await DbContext.Expenses
        .Where(scopePredicate)
        .GroupBy(e => new { e.Status, e.Category })
        .Select(g => new StatusCategoryCount(g.Key.Status, g.Key.Category, g.Count()))
        .ToListAsync(cancellationToken);
}
```
`StatusCategoryCount` (`record(ExpenseStatus Status, ExpenseCategory Category, int Count)`) is
a small new type in `Domain/Repositories/`, alongside the existing `ExpenseInsertOutcome`
precedent for repository-support types. `DashboardService` then computes every metric from
the resulting ≤49-row list — one DB round-trip instead of up to five, with all
filtering/grouping done in the database per `backend/CLAUDE.md`'s "never materialize a full
table then filter in memory" rule.
*Alternative considered (original design, superseded during implementation)*: call
`IExpenseRepository.Query()` directly from `DashboardService` and compose
`.Where(...).GroupBy(...).ToListAsync(...)` there. **Rejected on discovering it doesn't
compile**: `Application.csproj` references only `Domain` and `Shared` — not
`Microsoft.EntityFrameworkCore` — so `.ToListAsync()` isn't available in `Application` at all.
`Query()`'s only existing caller (`OrphanAttachmentSweeper`) lives in `Infrastructure`, which
does reference EF Core; it was never usable as an Application-layer escape hatch the way the
original D1 assumed. A dedicated repository method (this decision) is the correct fix and
matches `GetPagedAsync`/`SearchPagedAsync`/`GetReimbursedForReportAsync`'s existing shape
exactly — `DashboardService`'s own tests still cover the metric-computation logic directly,
via the same `FakeExpenseRepository` (`UnitTests.Application.Expenses`) other
`Application`-layer tests already use.

**D2 — New `DashboardScope` predicate builder, separate from `ExpenseVisibility`, despite
both being "role → `Expression<Func<Expense, bool>>`" functions.**
`ExpenseVisibility.BuildPredicate` (used by `GET /api/expenses`) intentionally includes the
Manager's own expenses in Manager scope. The confirmed dashboard requirement is the opposite
— Manager dashboard metrics exclude the manager's own expenses entirely (SDS §8.1, confirmed
during `/spec`). Reusing `ExpenseVisibility` and special-casing the dashboard's Manager
behavior on top of it would be more confusing than a small parallel type with the same
shape. `DashboardScope` lives in `Application/Dashboard/DashboardScope.cs`:
```csharp
public static class DashboardScope
{
    public static Expression<Func<Expense, bool>> BuildPredicate(EmployeeRole role, Guid employeeId) => role switch
    {
        EmployeeRole.Employee => e => e.EmployeeId == employeeId,
        EmployeeRole.Manager => e => e.Employee.ManagerId == employeeId,
        EmployeeRole.Finance => e => true,
        _ => e => false,
    };
}
```
`Draft`/`Cancelled` rows are not excluded at the predicate level — they simply never match
any metric's `Status` bucket in D3 below, matching how `ExpenseVisibility` also doesn't
special-case every status it doesn't care about.

**D3 — Metrics computed via small pure helpers over the `IReadOnlyList<StatusCategoryCount>`
result, not a switch-per-role branch tree.**
```csharp
totalSubmitted        = counts[Status == Submitted]
approved               = counts[Status == Approved] + counts[Status == ComplianceApproved]
reimbursed             = counts[Status == Reimbursed]
pendingApprovals       = totalSubmitted   // Manager/Finance only — identical definition, confirmed during /spec
pendingReimbursements  = counts[Status == Approved && Category != ClientEntertainment]
                         + counts[Status == ComplianceApproved]   // Finance only
```
`pendingApprovals` reuses `totalSubmitted`'s value directly (not a separate query) since the
spec confirms they're the same definition for both Manager and Finance scope — computing it
twice would risk the two silently diverging later (AGENTS.md §13's "same as X" guardrail).

**D4 — Three narrow response DTOs (`EmployeeDashboardResponse`, `ManagerDashboardResponse`,
`FinanceDashboardResponse`) and three `IDashboardService` methods, not one flat DTO / one
method with a role parameter.**
Per spec, each role's response must contain *only* its applicable fields (no zeroed-out
unused fields). Three records + three service methods make the "Employee response has no
`pendingApprovals` field" requirement a compile-time guarantee (the type simply has no such
property) rather than a runtime "don't serialize this" convention to remember.
*Alternative considered*: one `DashboardResponse` record with nullable fields, omitted from
JSON via `JsonIgnoreCondition.WhenWritingNull`. Rejected — it would make "field present" an
implicit contract enforced only by remembering to leave the right fields null, which is
exactly the kind of divergence-over-time risk AGENTS.md §13 flags.

**D5 — Controller dispatches on `User.GetRole()` after a single composite authorization
policy, rather than three separate `[Authorize(Policy=...)]`-decorated actions.**
`GET /api/dashboard` is one route for three different response shapes. A new policy
`AuthorizationPolicyNames.EmployeeOrManagerOrFinance` (`RequireRole(Employee, Manager,
Finance)`) is registered in a new `DashboardServiceCollectionExtensions.AddDashboardFoundation`,
mirroring `AttachmentServiceCollectionExtensions`'s existing `EmployeeOrManager` policy. A
`ComplianceOfficer` request never reaches the controller body — the ASP.NET Core
authorization middleware rejects it via `EnvelopeAuthorizationMiddlewareResultHandler` before
the action runs, the same mechanism already covering Finance-only and Manager-only endpoints
today. Inside the action, an exhaustive `switch` on `User.GetRole()` (`EmployeeRole.Employee`
/ `.Manager` / `.Finance`) calls the matching service method; the discard arm
(`_ => throw new InvalidOperationException(...)`) is unreachable because the policy already
guarantees only those three roles arrive, and documents that invariant rather than silently
returning a wrong shape if it's ever violated.

## Files to Create / Modify

**New files (all `backend/src/`):**
| File | Purpose |
|------|---------|
| `Domain/Repositories/StatusCategoryCount.cs` | `record(ExpenseStatus Status, ExpenseCategory Category, int Count)` (D1) |
| `Application/Dashboard/DashboardScope.cs` | Role → `Expression<Func<Expense,bool>>` (D2) |
| `Application/Dashboard/IDashboardService.cs` | Service contract (3 methods, D4/D5) |
| `Application/Dashboard/DashboardService.cs` | Metric computation over `GetStatusCategoryCountsAsync` (D1/D3) |
| `Application/Dashboard/EmployeeDashboardResponse.cs` | `record(int TotalSubmitted, int Approved, int Reimbursed)` |
| `Application/Dashboard/ManagerDashboardResponse.cs` | `record(int TotalSubmitted, int Approved, int Reimbursed, int PendingApprovals)` |
| `Application/Dashboard/FinanceDashboardResponse.cs` | `record(int TotalSubmitted, int Approved, int Reimbursed, int PendingApprovals, int PendingReimbursements)` |
| `Api/Controllers/DashboardController.cs` | `GET /api/dashboard`, role dispatch (D5) |
| `Api/Extensions/DashboardServiceCollectionExtensions.cs` | DI + `EmployeeOrManagerOrFinance` policy registration |

**Modified files:**
| File | Change |
|------|--------|
| `Domain/Repositories/IExpenseRepository.cs` | Add `GetStatusCategoryCountsAsync(...)` (D1) |
| `Infrastructure/Persistence/Repositories/ExpenseRepository.cs` | Implement `GetStatusCategoryCountsAsync` with `GroupBy` + `ToListAsync` (D1) |
| `Api/Authorization/AuthorizationPolicyNames.cs` | Add `public const string EmployeeOrManagerOrFinance = "EmployeeOrManagerOrFinance";` |
| `Api/Program.cs` | Add `builder.Services.AddDashboardFoundation();` alongside the other `Add*Foundation()` calls |
| `tests/UnitTests/Application/Expenses/ExpenseNumberGeneratorTests.cs` | `FakeExpenseRepository` implements the new `GetStatusCategoryCountsAsync` interface member (LINQ-to-Objects, reused by `DashboardServiceTests`) |

No schema/migration changes — `Expense`/`Employee` entities and their existing indexes are
used as-is; only the repository *interface* gains one new read method.

## DB Changes

None. No new migration. Reuses existing `Status`, `Category`, `EmployeeId`, and `ManagerId`
(via `Employee.ManagerId`) indexes already defined in `ExpenseConfiguration`/
`EmployeeConfiguration` for the `GROUP BY` query.

## API Contract (for reference — full scenarios in the spec delta)

```
GET /api/dashboard
Authorization: Bearer <access-token>
```
- `Employee` → 200 `{ "totalSubmitted": 0, "approved": 0, "reimbursed": 0 }`
- `Manager` → 200 `{ "totalSubmitted": 0, "approved": 0, "reimbursed": 0, "pendingApprovals": 0 }`
- `Finance` → 200 `{ "totalSubmitted": 0, "approved": 0, "reimbursed": 0, "pendingApprovals": 0, "pendingReimbursements": 0 }`
- `ComplianceOfficer` → 403 `{ "error": { "code": "AUTHORIZATION_FAILED", ... } }`
- No token → 401 `{ "error": { "code": "AUTHENTICATION_FAILED", ... } }`

(Field casing shown per the existing global `System.Text.Json` camelCase policy already in
effect for every other endpoint's response, e.g. `ExpenseResponse`.)

## Risks / Trade-offs

- **[Risk]** `pendingApprovals == totalSubmitted` for both Manager and Finance scope reads as
  a redundant pair of fields in the response body. → **Mitigation**: this redundancy is the
  confirmed spec definition (both represent "awaiting a Manager decision" under different
  scopes); documented in D3 and the spec so a future change doesn't "simplify" it away
  without re-checking the spec first.
- **[Risk]** `DashboardScope`'s Manager predicate (`e.Employee.ManagerId == employeeId`,
  excluding the manager's own expenses) looks like a bug next to `ExpenseVisibility`'s Manager
  predicate (which includes the manager's own expenses) if read out of context. →
  **Mitigation**: D2's comment block and the spec's "Manager Dashboard Metrics" requirement
  both call out the divergence explicitly, with the SDS §8.1 citation, so a future reviewer
  doesn't "fix" it to match `ExpenseVisibility`.
- **[Risk]** In-memory LINQ aggregation over `GetStatusCategoryCountsAsync`'s result assumes
  the grouped row count stays small (≤49 buckets: 7 statuses × 7 categories). → **Mitigation**:
  this bound is structural (fixed enum sizes), not data-dependent, so it can't grow as the
  `Expense` table grows — the `GROUP BY` itself is what stays cheap regardless of row count.
- **[Trade-off]** No dashboard result caching, so every dashboard view re-scans the caller's
  visible `Expense` rows. Accepted per SDS §8's "generated from committed expense data"
  requirement (same trade-off already accepted for Finance search and the monthly report);
  revisit only if a future ticket reports measured latency problems.

## Checkpoint Commands

Run from `backend/`, in this order, per `AGENTS.md` §Quality Gates — stop and fix at the
first failure:
1. `dotnet build` — compiles, surfaces analyzer warnings.
2. `dotnet test --filter FullyQualifiedName~UnitTests` — `DashboardScope`/`DashboardService`
   metric-computation unit tests.
3. `dotnet test --filter FullyQualifiedName~IntegrationTests` — `DashboardController` role
   authorization (incl. Compliance Officer 403, unauthenticated 401) and response-shape
   integration tests via `WebApplicationFactory`.
4. `dotnet format` — apply EditorConfig formatting before considering the ticket done.

No frontend, lint, or E2E gate applies to this ticket (backend-only, no user-facing UI in
ET013 — that's ET019).

## Open Questions

None outstanding — every ambiguity identified during `/spec` (metric definitions, Manager
scope, Compliance Officer access, response shape) was resolved with the ticket owner and is
recorded in `proposal.md` and the spec delta.
