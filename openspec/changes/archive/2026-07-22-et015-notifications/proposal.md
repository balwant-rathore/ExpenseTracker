## Why

The expense workflow (ET007–ET012) already calls `INotificationService.NotifyAsync` after
committing Approve/Reject/ComplianceApprove/ComplianceReject/Reimburse transitions, but the only
implementation registered in DI is `NoOpNotificationService` — no notification is ever actually
produced. FRS §9 (Status Change Notifications) requires each workflow status change to notify the
correct recipients via an HTML log entry (SDS §7), and that requirement is currently unmet. In
addition, the `Submitted` transition — the first event in FRS §9.1.1 — has no `NotifyAsync` call
site at all today (neither `ExpenseService.CreateAsync`'s submit path nor `SubmitAsync` invoke it),
so it would remain silently unimplemented even after a real notification service exists.

## What Changes

- Implement a real `HtmlNotificationService` (replacing `NoOpNotificationService` in DI) that
  resolves recipients, renders a template, and appends a well-formed HTML entry (Timestamp, Event,
  To, CC, Subject, Body) to a configurable log file — created automatically if missing, write mode
  append (SDS §7.4, FRS §9.1.6).
- Add the two missing call sites for `NotificationEvent.Submitted`: `ExpenseService.CreateAsync`
  (when `action=Submit`) and `ExpenseService.SubmitAsync`, both firing after their existing
  transaction commit — same root-cause fix applied everywhere the pattern is missing (AGENTS.md
  §13), not just one call site.
- Add `IEmployeeRepository.GetByRoleAsync(EmployeeRole, CancellationToken)` to resolve all active
  Finance-role employees for CC on Approved/ComplianceApproved/Reimbursed notifications, and load
  the `Employee.Manager` navigation where notification recipient resolution needs the reporting
  manager's email (currently not eager-loaded by `GetByIdWithEmployeeAsync`).
- Define notification templates and recipients for six events per the decisions below:
  - `Submitted` → To: Employee, CC: Manager (FRS 9.1.1)
  - `Approved` → To: Employee, CC: Manager, Finance (FRS 9.1.2)
  - `Rejected` → To: Employee (FRS 9.1.3)
  - `ComplianceApproved` → To: Employee, CC: Manager, Finance — **decision**: extends the FRS
    4-event matrix; Compliance-approving a Client Entertainment expense is the functional
    equivalent of manager approval finishing (moves the expense to the state Finance now needs to
    act on), so it reuses the Approved recipient pattern. Not contradicting FRS/SDS — the matrix
    only explicitly enumerates the 4 non-compliance events, it does not prohibit notifying on
    ComplianceApproved.
  - `ComplianceRejected` → To: Employee — **decision**: the resulting `Status` is `Rejected`
    regardless of whether a Manager or the Compliance Officer rejected it, so it reuses the
    `Rejected` template/recipients (FRS 9.1.3 does not distinguish by rejecting role).
  - `Reimbursed` → To: Employee, CC: Manager, Finance (FRS 9.1.4)
- `NotifyAsync` never throws out of the service: all recipient-resolution and file-write failures
  are caught and logged internally (`Microsoft.Extensions.Logging`), so the six (soon eight)
  `ExpenseService` call sites need no per-call try/catch — matches AGENTS.md "Notification
  failures must never roll back or block a workflow transaction" and SDS §7.6.
- Concurrent writes to the shared HTML log file are serialized with an in-process async lock
  (`SemaphoreSlim`) inside the notification service so simultaneous workflow actions never produce
  interleaved/corrupted HTML entries.
- Add `NotificationOptions` (`NotificationLogDirectory`, `NotificationLogFileName`,
  `NotificationTimestampFormat`) bound from `appsettings.json`, per SDS §7.7.

## Capabilities

### New Capabilities
- `expense-notifications`: Post-commit HTML notification logging for expense workflow status
  changes — recipient resolution, template rendering, append-only HTML log, failure isolation.

### Modified Capabilities
(none — no existing `openspec/specs/*/spec.md` currently documents notification behavior; the
missing `Submitted` call sites are an implementation gap being fixed under the new capability
above, not a change to previously-specified behavior of `expense-submission`.)

## Impact

- **Application/Notifications**: `INotificationService` contract unchanged; new
  `HtmlNotificationService` implementation replaces `NoOpNotificationService` in
  `ExpenseServiceCollectionExtensions.AddExpenseFoundation`. New `NotificationTemplate`
  rendering + `NotificationRecipients` resolution helpers.
- **Application/Expenses**: `ExpenseService.CreateAsync` and `SubmitAsync` gain
  `NotifyAsync(NotificationEvent.Submitted, ...)` calls after their existing commit.
- **Domain/Repositories**: `IEmployeeRepository` gains `GetByRoleAsync`.
- **Infrastructure/Persistence/Repositories**: `EmployeeRepository` implements `GetByRoleAsync`;
  `ExpenseRepository.GetByIdWithEmployeeAsync` (or a new overload) includes `Employee.Manager`.
- **Infrastructure**: new notification log writer (filesystem, HTML, append, locked).
- **Api**: `appsettings.json`/`appsettings.Development.json` gain a `Notification` config section;
  DI registration swaps `NoOpNotificationService` → `HtmlNotificationService`.
- **Tests**: unit tests for template rendering, recipient resolution (incl. compliance-event
  decisions above), failure-swallowing behavior; integration tests asserting the HTML log file
  gains a well-formed entry after each of the 6 workflow transitions including the two new
  `Submitted` call sites.
- No breaking changes to any public API contract — `INotificationService` signature and all
  `/api/expenses/*` endpoint contracts are unchanged.
