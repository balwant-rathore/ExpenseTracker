## Context

ET007–ET012 already wired `INotificationService.NotifyAsync` into six `ExpenseService`
call sites (Approve/Reject/ComplianceApprove/ComplianceReject/Reimburse, plus the
`Reimbursed` one added in ET012), but only `NoOpNotificationService` is registered in DI —
no notification has ever actually been produced. Two more gaps surfaced during `/spec`:
`Submitted` (FRS §9.1.1) has no call site at all, and `INotificationService`/
`NotificationEvent` live in `Application.Notifications` even though SDS §2.1 and
AGENTS.md both name "notifications" as an `Infrastructure` responsibility — matching the
existing `IFileStorageService` (`Domain.Storage` → `Infrastructure.Storage`) and
`IMonthlyReimbursementReportGenerator` (`Domain.Reporting` → `Infrastructure.Reporting`)
precedent. `Infrastructure.csproj` has no reference to `Application.csproj`, so today
`Infrastructure` cannot implement that interface at all.

Both structural questions (interface location; how the notification service obtains
recipient data given inconsistent eager-loading across the 8 `ExpenseService` call sites)
were confirmed with the ticket owner before this design was written — see Decisions D1
and D3.

## Goals / Non-Goals

**Goals:**
- Real `HtmlNotificationService` replacing `NoOpNotificationService`, producing a
  well-formed, append-only HTML log entry for all 6 workflow events (FRS §9.1, SDS §7).
- Add the two missing `Submitted` call sites so FRS §9.1.1 is actually reachable.
- Recipient resolution (Employee/Manager/Finance) and rendering are unit-testable in
  isolation from `ExpenseService` and from the filesystem.
- Zero EF Core migration — no schema change.

**Non-Goals:**
- Log rotation, retention, or archival of the HTML file — SDS §7.4/§7.7 specify format
  and write mode only, nothing about size limits or rotation.
- Fixing the pre-existing, already-accepted app-wide gap between BR-10 ("all dates stored
  in company local timezone") and the fact that every workflow timestamp in this codebase
  is written/compared as raw UTC (`ET012` design.md D4 already established this as
  existing, accepted behavior, not a defect this ticket owns). Notification timestamps
  follow the same UTC-only convention — see Decision D8.
- Any frontend work (ET016–ET019) or real SMTP delivery (explicitly out of scope,
  FRS §12).

## Decisions

### D1 — Move `INotificationService`/`NotificationEvent` to `Domain.Notifications`
`src/Domain/Notifications/INotificationService.cs` and `NotificationEvent.cs` (moved from
`Application/Notifications/`, namespace `Application.Notifications` →
`Domain.Notifications`). `Application/Notifications/NoOpNotificationService.cs` is
deleted — it's replaced by the real implementation, not kept as a fallback.
**Alternative considered (rejected)**: add an `Infrastructure` → `Application` project
reference and keep the interface where it is. Rejected because it would be the first
`Infrastructure` → `Application` reference in the codebase, reversing the sibling-layer
relationship every other `Infrastructure` service (`Storage`, `Reporting`, `Persistence`)
follows. Confirmed with the ticket owner during `/plan`.

### D2 — Split into a scoped orchestrator and a singleton log writer
`HtmlNotificationService` (scoped, implements `INotificationService`) depends on
`IEmployeeRepository` (scoped, EF-Core-backed) for recipient/approver resolution, and on
`INotificationLogWriter`/`NotificationLogWriter` (**singleton**) for the actual file
append. A scoped service may depend on a singleton (not vice versa), so this is the only
way to (a) use the existing scoped `IEmployeeRepository` for recipient lookups and (b)
have one shared `SemaphoreSlim` serializing writes across *all* concurrent requests — a
scoped lock would only serialize writes within a single request, which is meaningless.
Mirrors the existing singleton/scoped split already in this codebase:
`OrphanAttachmentCleanupService` (singleton `IHostedService`) creates a DI scope
internally to resolve scoped dependencies each cleanup cycle.

### D3 — Recipient/approver resolution queries `IEmployeeRepository` directly, ignoring what the caller loaded
`HtmlNotificationService.NotifyAsync(NotificationEvent, Expense, CancellationToken)`
resolves everything itself from `expense.EmployeeId` (and, where relevant,
`expense.ApprovedByEmployeeId`/`RejectedByEmployeeId`/`ComplianceApprovedByEmployeeId`),
rather than relying on the `Expense.Employee`/`Employee.Manager` navigations the caller
may or may not have eager-loaded. Verified during `/plan`: `ComplianceApproveAsync`/
`ComplianceRejectAsync` call `GetByIdAsync` (zero includes), `ApproveAsync`/`RejectAsync`/
`ReimburseAsync` call `GetByIdWithEmployeeAsync` (Employee loaded, `Manager` not), and
`CreateAsync`/`SubmitAsync` load/construct the expense without the `Employee` navigation
at all. Two new `IEmployeeRepository` methods support this:
```csharp
Task<Employee?> GetByIdWithManagerAsync(Guid employeeId, CancellationToken cancellationToken);
Task<IReadOnlyList<Employee>> GetByRoleAsync(EmployeeRole role, CancellationToken cancellationToken);
```
`GetByRoleAsync` filters `IsActive == true` (matches "all active Finance-role employees"
from the approved spec delta). The approver's display name (for `Approved`/`Rejected`/
`ComplianceApproved`/`ComplianceRejected` templates) reuses the existing generic
`IRepository<Employee>.GetByIdAsync` — no new method needed there.
**Alternative considered (rejected)**: change all 8 `ExpenseService` fetch calls to
eager-load `Employee.Manager`. Rejected — touches every workflow action's query for a
concern (notifications) that doesn't otherwise need those queries to change; isolates
blast radius to the notification layer instead. Confirmed with the ticket owner.

### D4 — Recipient/template mapping per event (carried over from the approved spec delta)
| Event | To | Cc | Template fields (SDS §7.5, extended for the two compliance events per the proposal's decision) |
|---|---|---|---|
| Submitted | Employee | Manager (if any) | Expense Number, Employee, Category, Amount, Submission Date |
| Approved | Employee | Manager, all active Finance | Expense Number, Employee, Category, Approved By, Approval Date |
| Rejected | Employee | — | Expense Number, Employee, Rejection Comment, Rejected By, Rejection Date |
| ComplianceApproved | Employee | Manager, all active Finance | Expense Number, Employee, Category, Approved By (Compliance Officer), Approval Date |
| ComplianceRejected | Employee | — | Expense Number, Employee, Rejection Comment, Rejected By, Rejection Date |
| Reimbursed | Employee | Manager, all active Finance | Expense Number, Employee, Amount, Reimbursement Date |

A missing `ManagerId` (edge case beyond FRS §14's "every employee reports to exactly one
manager" assumption) simply omits the Manager Cc entry — never throws (see D7).

### D5 — HTML log entries are self-contained fragments, not a single growing document
Each call to `NotificationLogWriter.AppendAsync` appends one `<div class="notification">
...</div><hr/>` fragment (Timestamp, Event, To, Cc, Subject, Body) directly to the log
file — the file is never wrapped in a single `<html><body>` root that would need its
closing tags rewritten on every append (which `File.AppendAllTextAsync` cannot do
safely). Browsers render concatenated HTML fragments without a root document adequately
for this dev-only artifact. Every interpolated dynamic value (`Description`,
`RejectionComment`, employee names) is passed through `System.Net.WebUtility.HtmlEncode`
before interpolation, so a description or rejection comment containing `<`, `>`, or `&`
can never break the entry's markup — this is what "well-formed entry" (spec:
`HTML Notification Log` requirement) means in practice.

### D6 — Concurrency: singleton `SemaphoreSlim(1, 1)` around directory-create + append
`NotificationLogWriter` holds one `SemaphoreSlim(1, 1)` for its lifetime (process
lifetime, since it's a singleton). `AppendAsync` awaits the semaphore, ensures the
directory/file exist, appends via `File.AppendAllTextAsync`, then releases — guaranteeing
two concurrent `NotifyAsync` calls (e.g. two Managers approving different expenses at the
same moment) never interleave into a malformed entry (spec: `Concurrent Notification
Writes Do Not Corrupt the Log`).

### D7 — `NotifyAsync` never throws
`HtmlNotificationService.NotifyAsync` wraps its entire resolve → render → write pipeline
in one `try/catch (Exception)`, logging via `ILogger<HtmlNotificationService>` and
returning normally either way. This is a deliberate, narrowly-scoped exception to
`backend/CLAUDE.md`'s "don't catch exceptions just to swallow them" anti-pattern —
justified because AGENTS.md explicitly requires "notification failures must never roll
back or block a workflow transaction" and SDS §7.6 requires the workflow API's response
to be unaffected by notification failure. All 8 `ExpenseService` call sites (6 existing +
2 new) stay free of their own try/catch. Confirmed with the ticket owner.

### D8 — Notification timestamps use `DateTime.UtcNow`, not company-local time
`ICompanyClock`/`CompanyTimeZoneOptions` (the existing BR-10 timezone-conversion
machinery) both live in `Application.Expenses` — depending on either from
`Infrastructure.Notifications` would reintroduce exactly the cross-layer dependency D1
just eliminated, for a purely cosmetic formatting concern. Per the ET012 precedent (design
D4: this app-wide UTC-vs-company-local gap is pre-existing and accepted, not something an
unrelated ticket should fix piecemeal), the log's `Timestamp` field is
`DateTime.UtcNow.ToString(NotificationTimestampFormat)`.

### D9 — Configuration mirrors the existing `StorageOptions` pattern exactly
```csharp
public class NotificationOptions
{
    public const string SectionName = "Notification";
    public string LogDirectory { get; set; } = "storage/notifications";
    public string LogFileName { get; set; } = "notifications.html";
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss 'UTC'";
}
```
`NotificationLogWriter` resolves the absolute path the same way `FileStorageService`
resolves `_rootPath`: `Path.GetFullPath(Path.Combine(environment.ContentRootPath,
options.Value.LogDirectory, options.Value.LogFileName))`, computed once at construction.
The default `storage/notifications` path falls under the existing `.gitignore` rule
(`backend/src/Api/storage/`) — no new ignore entry needed.

### D10 — New `IEmployeeRepository` methods stay flat, `Infrastructure` implements them
Same shape as the existing `GetByEmployeeNumberAsync`/`GetPagedAsync` methods (flat
parameters, EF `.Include`/`.Where` composed directly in `Infrastructure`, per ET012
design D5's "Domain must not reference Application-layer types, and Include-and-filter
belongs in Infrastructure" rule). `GetByRoleAsync` is intentionally not generalized beyond
what's needed (no caching, no "all roles" variant) — it exists solely to resolve Finance
Cc recipients.

### D11 — New `NotificationServiceCollectionExtensions.AddNotificationFoundation`
Mirrors the one-DI-extension-per-feature convention (ET012 design D7: `Application`,
`Auth`, `Attachments`, `Reports`, `Dashboard` each have their own). `Program.cs` gets one
new line, `builder.Services.AddNotificationFoundation(builder.Configuration);`. The
now-orphaned `services.AddScoped<INotificationService, NoOpNotificationService>();` line
and its `using Application.Notifications;` are removed from
`ExpenseServiceCollectionExtensions.AddExpenseFoundation`.

### D12 — Two new `Submitted` call sites, same shape as the existing six
`ExpenseService.CreateAsync` (only when `status == ExpenseStatus.Submitted`, i.e.
`action=Submit`) and `ExpenseService.SubmitAsync` each gain
`await _notificationService.NotifyAsync(NotificationEvent.Submitted, expense,
cancellationToken);` immediately after their existing `ExecuteInTransactionAsync` commit
block — identical placement/pattern to the six existing call sites, not a new pattern.

## Risks / Trade-offs

- **[Risk]** An expense owner with no `ManagerId` (edge case beyond the FRS §14
  one-manager assumption) → the Cc list simply omits a manager entry rather than
  throwing. **Mitigation**: consistent with D7's "never throw" guarantee; not treated as
  an error condition.
- **[Risk]** Zero (or multiple) active Finance-role employees at notification time →
  `Approved`/`ComplianceApproved`/`Reimbursed` Cc list is correspondingly empty or has
  several addresses. **Mitigation**: this is the correct, spec-defined behavior ("all
  active Finance-role employees"), not an error case.
- **[Risk]** Unbounded log file growth (no rotation/retention). **Mitigation**: explicitly
  a Non-Goal — SDS specifies format and append-only write mode only; acceptable for a
  dev-only, non-production artifact (FRS §12 confirms no real email/SMTP is ever
  involved).
- **[Trade-off]** `HtmlNotificationService.NotifyAsync`'s broad `catch (Exception)` is
  normally an anti-pattern per `backend/CLAUDE.md`. **Mitigation**: it is the single,
  explicitly spec-mandated exception (D7), scoped to exactly one top-level orchestration
  method, not a pattern reused elsewhere in the codebase.
- **[Risk]** `NotificationLogWriter` resolves its absolute file path once at construction
  (singleton) — a config change to `NotificationLogDirectory` requires a process restart
  to take effect. **Mitigation**: identical, already-accepted trade-off as
  `FileStorageService`'s `_rootPath` caching.

## Migration Plan

No EF Core migration — no schema change. Purely additive/moved code plus one new
`appsettings.json` section with safe property-initializer defaults (missing config key
falls back to the defaults in D9, so existing deployments need no config update to keep
working — though the `NotificationLogDirectory` should still be added explicitly for
clarity). Rollback is a normal `git revert`; no data cleanup needed since nothing is
persisted to the database by this ticket.

## File Manifest

**New:**
- `src/Domain/Notifications/INotificationService.cs` (moved)
- `src/Domain/Notifications/NotificationEvent.cs` (moved)
- `src/Infrastructure/Notifications/NotificationOptions.cs`
- `src/Infrastructure/Notifications/INotificationLogWriter.cs`
- `src/Infrastructure/Notifications/NotificationLogWriter.cs`
- `src/Infrastructure/Notifications/HtmlNotificationService.cs`
- `src/Infrastructure/Notifications/NotificationTemplateRenderer.cs` (`public static`
  helper, not DI-registered — pure function, mirrors `ExpenseVisibility.BuildPredicate`;
  `public` rather than `internal` because this repo has no `InternalsVisibleTo` wired up
  for unit-testing internals, same reason `ExpenseVisibility` itself is `public`)
- `src/Api/Extensions/NotificationServiceCollectionExtensions.cs`
- `tests/UnitTests/Infrastructure/Notifications/NotificationTemplateRendererTests.cs`
- `tests/UnitTests/Infrastructure/Notifications/NotificationLogWriterTests.cs`
- `tests/UnitTests/Infrastructure/Notifications/HtmlNotificationServiceTests.cs`
- `tests/IntegrationTests/NotificationServiceCollectionExtensionsDiTests.cs`
- `tests/IntegrationTests/ExpenseNotificationTests.cs`

**Modified:**
- `src/Application/Expenses/ExpenseService.cs` — `using Domain.Notifications;`; add the
  two `Submitted` `NotifyAsync` calls (D12)
- `src/Api/Extensions/ExpenseServiceCollectionExtensions.cs` — remove the
  `NoOpNotificationService` registration and its `using`
- `src/Domain/Repositories/IEmployeeRepository.cs` — add `GetByIdWithManagerAsync`,
  `GetByRoleAsync`
- `src/Infrastructure/Persistence/Repositories/EmployeeRepository.cs` — implement both
- `src/Api/Program.cs` — `builder.Services.AddNotificationFoundation(builder.Configuration);`
- `src/Api/appsettings.json` / `appsettings.Development.json` — new `Notification` section
- `tests/UnitTests/Application/Expenses/ExpenseServiceTests.cs` — `using
  Domain.Notifications;`; add coverage for the two new `Submitted` call sites
- `docs/TICKETS.md` — status update (handled by `/implement`, not this design)

**Deleted:**
- `src/Application/Notifications/NoOpNotificationService.cs`
- `src/Application/Notifications/INotificationService.cs` (moved, see New)
- `src/Application/Notifications/NotificationEvent.cs` (moved, see New)

## Verification

```bash
# from backend/
dotnet build                                              # compile + analyzers
dotnet format --verify-no-changes                         # EditorConfig lint gate
dotnet test --filter FullyQualifiedName~UnitTests         # unit (template, log writer, service, repo)
dotnet test --filter FullyQualifiedName~IntegrationTests  # integration (DI registration, 8-event log assertions)
```
No frontend changes in this ticket — no `pnpm --filter frontend` gate applies. No E2E gate
— ET015 has no user-facing flow (Notifications has no frontend feature area per
`docs/SDS.md` §2.3's Feature Ownership Matrix).

## Open Questions

None outstanding. The two structural ambiguities identified during `/plan` (interface
location; recipient-resolution data access) were confirmed with the ticket owner and are
recorded as D1 and D3 above. The compliance-event recipient/template decisions were
already confirmed during `/spec` and are carried over unchanged in D4.
