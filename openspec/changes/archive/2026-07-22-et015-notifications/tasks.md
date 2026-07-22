## 1. Foundation

- [x] 1.1 Move `INotificationService` from `src/Application/Notifications/INotificationService.cs` to `src/Domain/Notifications/INotificationService.cs`, namespace `Application.Notifications` → `Domain.Notifications` (design D1)
- [x] 1.2 Move `NotificationEvent` from `src/Application/Notifications/NotificationEvent.cs` to `src/Domain/Notifications/NotificationEvent.cs`, namespace `Application.Notifications` → `Domain.Notifications` (design D1)
- [x] 1.3 Delete `src/Application/Notifications/NoOpNotificationService.cs` and the now-empty `src/Application/Notifications/` folder (design D1)
- [x] 1.4 Add `IEmployeeRepository.GetByIdWithManagerAsync(Guid, CancellationToken)` and `GetByRoleAsync(EmployeeRole, CancellationToken)` signatures to `src/Domain/Repositories/IEmployeeRepository.cs` (design D3, D10)
- [x] 1.5 Add `src/Infrastructure/Notifications/NotificationOptions.cs` (`SectionName`, `LogDirectory`, `LogFileName`, `TimestampFormat` with defaults) (design D9)
- [x] 1.6 Add `Notification` config section (`LogDirectory: "storage/notifications"`, `LogFileName: "notifications.html"`, `TimestampFormat: "yyyy-MM-dd HH:mm:ss 'UTC'"`) to `src/Api/appsettings.json` (deviation: `appsettings.Development.json` doesn't override `Storage`/`AttachmentCleanup`/`CompanyTimeZone` either, so `Notification` follows that same existing precedent and is base-only) (design D9)

## 2. Core Implementation

- [x] 2.1 Implement `EmployeeRepository.GetByIdWithManagerAsync` in `src/Infrastructure/Persistence/Repositories/EmployeeRepository.cs` (`.Include(e => e.Manager)`) (design D3)
- [x] 2.2 Implement `EmployeeRepository.GetByRoleAsync` in the same file (`.Where(e => e.Role == role && e.IsActive)`) (design D3, D10)
- [x] 2.3 Add `src/Infrastructure/Notifications/INotificationLogWriter.cs` (`Task AppendAsync(string htmlEntry, CancellationToken)`) (design D2)
- [x] 2.4 Implement `src/Infrastructure/Notifications/NotificationLogWriter.cs` — singleton, resolves absolute path from `IOptions<NotificationOptions>` + `IHostEnvironment.ContentRootPath` once at construction (mirrors `FileStorageService`), `SemaphoreSlim(1, 1)` guarding directory/file creation + `File.AppendAllTextAsync` (design D6, D9)
- [x] 2.5 Implement `src/Infrastructure/Notifications/NotificationTemplateRenderer.cs` — `public static` helper (not `internal`: this repo has no `InternalsVisibleTo` wired up, matching why `ExpenseVisibility` is also `public`) producing `(string Subject, string HtmlBody)` per `NotificationEvent`, per the recipient/template table (design D4); `System.Net.WebUtility.HtmlEncode` every interpolated dynamic value (`RejectionComment`, employee names — note: `Description` is not part of any SDS §7.5 template field, so it is not rendered at all) (design D5)
- [x] 2.6 Implement `src/Infrastructure/Notifications/HtmlNotificationService.cs` — scoped `INotificationService`, resolves owner via `GetByIdWithManagerAsync(expense.EmployeeId, ct)`, resolves Finance Cc via `GetByRoleAsync(EmployeeRole.Finance, ct)` for Approved/ComplianceApproved/Reimbursed, resolves the approver's display name via `GetByIdAsync` (`ApprovedByEmployeeId`/`RejectedByEmployeeId`/`ComplianceApprovedByEmployeeId`) for Approved/Rejected/ComplianceApproved/ComplianceRejected, renders via `NotificationTemplateRenderer`, writes via `INotificationLogWriter`, wraps the whole pipeline in one `try/catch (Exception)` logging via `ILogger<HtmlNotificationService>` and never rethrowing (design D2, D3, D4, D7, D8)
- [x] 2.7 Add `src/Api/Extensions/NotificationServiceCollectionExtensions.cs` (`AddNotificationFoundation(IConfiguration)`): `services.Configure<NotificationOptions>(...)`, `services.AddSingleton<INotificationLogWriter, NotificationLogWriter>()`, `services.AddScoped<INotificationService, HtmlNotificationService>()` (design D11)
- [x] 2.8 Wire `builder.Services.AddNotificationFoundation(builder.Configuration);` into `src/Api/Program.cs`; remove `services.AddScoped<INotificationService, NoOpNotificationService>();` and its now-unused `using Application.Notifications;` from `src/Api/Extensions/ExpenseServiceCollectionExtensions.cs` (design D11)

## 3. Integration

- [x] 3.1 Update `using Application.Notifications;` → `using Domain.Notifications;` in `src/Application/Expenses/ExpenseService.cs`
- [x] 3.2 Add `await _notificationService.NotifyAsync(NotificationEvent.Submitted, expense, cancellationToken);` in `ExpenseService.CreateAsync`, immediately after the existing commit, only on the branch where `status == ExpenseStatus.Submitted` (design D12)
- [x] 3.3 Add the same `NotifyAsync(NotificationEvent.Submitted, ...)` call in `ExpenseService.SubmitAsync`, immediately after its existing commit (design D12)
- [x] 3.4 Update `using Application.Notifications;` → `using Domain.Notifications;` in `tests/UnitTests/Application/Expenses/ExpenseServiceTests.cs` (`FakeNotificationService`, `NotificationEvent` references)

## 4. Tests

### 4.1 `NotificationTemplateRendererTests` (new, unit)

- [x] 4.1.1 Approved/ComplianceApproved/Reimbursed templates include their SDS §7.5 fields (Expense Number, Employee, Category/Amount, Approved By/Reimbursement Date as applicable) — covers spec scenario "Log entry contains all required fields"
- [x] 4.1.2 Rejected/ComplianceRejected templates include Rejection Comment and Rejected By, and render identical To/Cc-relevant content regardless of rejecting role — covers spec scenario "ComplianceRejected notification matches Rejected recipients"
- [x] 4.1.3 Rendered output never contains a raw `EmployeeId`/`ExpenseId` GUID for any event — covers spec scenario "Log entry omits internal identifiers"
- [x] 4.1.4 A `RejectionComment` containing `<`, `>`, `&` is HTML-encoded in the rendered body (design D5; note: `Description` is never rendered by any template per SDS §7.5, so it needed no encoding test)

### 4.2 `NotificationLogWriterTests` (new, unit)

- [x] 4.2.1 `AppendAsync` creates the configured log file when it does not yet exist — covers spec scenario "Log file is created on first notification"
- [x] 4.2.2 A second `AppendAsync` call appends after existing content without truncating it — covers spec scenario "Subsequent notifications append rather than overwrite"
- [x] 4.2.3 Two concurrent `AppendAsync` calls (`Task.WhenAll`) both land as distinct, non-interleaved, well-formed entries — covers spec scenario "Two simultaneous approvals both produce well-formed entries"

### 4.3 `HtmlNotificationServiceTests` (new, unit; fakes for `IEmployeeRepository` and `INotificationLogWriter`)

- [x] 4.3.1 `Submitted` resolves To=owner, Cc=manager (when `ManagerId` is set)
- [x] 4.3.2 `Submitted` omits the Cc entry when the owner has no `ManagerId` (design D4 edge case, Risks)
- [x] 4.3.3 `Approved` Cc includes the manager and every active Finance-role employee returned by `GetByRoleAsync` — covers spec scenario "Approved notification CCs every active Finance-role employee"
- [x] 4.3.4 `Rejected` has an empty Cc list — covers spec scenario "Rejected notification has no CC recipients"
- [x] 4.3.5 `ComplianceApproved` Cc includes the manager and all active Finance-role employees — covers spec scenario "ComplianceApproved notification includes Finance"
- [x] 4.3.6 `ComplianceRejected` has an empty Cc list, matching `Rejected` — covers spec scenario "ComplianceRejected notification matches Rejected recipients"
- [x] 4.3.7 When `INotificationLogWriter.AppendAsync` throws, `NotifyAsync` does not throw and logs the failure — covers spec scenario "Log write failure does not fail the API request"
- [x] 4.3.8 A failed `NotifyAsync` call does not affect a subsequent, unrelated `NotifyAsync` call — covers spec scenario "One failed notification does not block later notifications"

### 4.4 `EmployeeRepository` coverage (deviation: folded into integration tests, not a standalone unit test file)

- [x] 4.4.1/4.4.2 — deviation from the original plan: this repo has no lightweight in-memory/SQLite repository-unit-test harness (`TestDbContextFactory.Create()` only builds a `DbContext` for EF configuration/model tests against an unreachable connection string, and every other repository — `ExpenseRepository.GetPagedAsync`, `SearchPagedAsync`, etc. — is likewise verified only through `IntegrationTests` against the real local dev SQL Server, per `backend/CLAUDE.md`'s local DB setup). `GetByIdWithManagerAsync` (Manager Cc resolution) and `GetByRoleAsync` (Finance Cc resolution) are exercised for real, against real seeded/created Employee rows with real `ManagerId` relationships, by `ExpenseNotificationTests` (4.7) below — consistent with the existing precedent rather than introducing a new one

### 4.5 `ExpenseServiceTests` additions (extend existing file)

- [x] 4.5.1 `CreateAsync` with `action: "Submit"` calls `NotifyAsync(NotificationEvent.Submitted, ...)` after the create commits — covers spec scenario "Creating an expense directly as Submitted triggers a notification"
- [x] 4.5.2 `CreateAsync` with `action: "Draft"` does **not** call `NotifyAsync` — no notification for a Draft save
- [x] 4.5.3 `SubmitAsync` on an existing `Draft` expense calls `NotifyAsync(NotificationEvent.Submitted, ...)` after the submit commits — covers spec scenario "Submitting an existing Draft expense triggers a notification"
- [x] 4.5.4 A failed workflow action (`SubmitAsync` called by a non-owner) results in zero `NotifyAsync` calls — covers spec scenario "No notification is triggered when a transition fails"

### 4.6 `NotificationServiceCollectionExtensionsDiTests` (new, integration)

- [x] 4.6.1 `AddNotificationFoundation` registers `INotificationService` (resolvable as `HtmlNotificationService`) and `INotificationLogWriter` (resolvable as a singleton) from a minimal `ServiceCollection` + in-memory configuration, mirroring `AttachmentServiceCollectionExtensionsDiTests`

### 4.7 `ExpenseNotificationTests` (new, integration; `WebApplicationFactory` with `Notification:LogDirectory` overridden to a temp folder per instance)

- [x] 4.7.1 `POST /api/expenses` with `action: "Submit"` results in a `Submitted` entry appended to the log file — covers spec scenario "Creating an expense directly as Submitted triggers a notification"
- [x] 4.7.2 `POST /api/expenses/{id}/submit` on a `Draft` expense results in a `Submitted` entry — covers spec scenario "Submitting an existing Draft expense triggers a notification"
- [x] 4.7.3 `POST /api/expenses/{id}/approve` results in an `Approved` entry — covers spec scenario "Manager approval triggers a notification"
- [x] 4.7.4 `POST /api/expenses/{id}/reject` results in a `Rejected` entry — covers spec scenario "Manager rejection triggers a notification"
- [x] 4.7.5 `POST /api/expenses/{id}/compliance-approve` results in a `ComplianceApproved` entry — covers spec scenario "Compliance approval triggers a notification"
- [x] 4.7.6 `POST /api/expenses/{id}/compliance-reject` results in a `ComplianceRejected` entry — covers spec scenario "Compliance rejection triggers a notification"
- [x] 4.7.7 `POST /api/expenses/{id}/reimburse` results in a `Reimbursed` entry — covers spec scenario "Finance reimbursement triggers a notification"
- [x] 4.7.8 A failed transition (BR-06 self-approval) leaves the log file unchanged — covers spec scenario "No notification is triggered when a transition fails"
- [x] 4.7.9 Every entry written by 4.7.1–4.7.7 contains Timestamp, Event, To, Cc, Subject, and Body (`AssertWellFormedEntry`), and the Approved/Reimbursed entries assert the raw `ExpenseId` GUID is absent — covers spec scenario "Log entry contains all required fields" / "Log entry omits internal identifiers" end-to-end

## 5. Archive

- [x] 5.1 Run backend quality gate in order: `dotnet build` → `dotnet format --verify-no-changes` → `dotnet test --filter FullyQualifiedName~UnitTests` → `dotnet test --filter FullyQualifiedName~IntegrationTests`; fix and re-run at the first failure rather than proceeding (final run: 0 build warnings/errors, format clean, 287/287 unit + 241/241 integration passed)
- [x] 5.2 Run `openspec validate et015-notifications` — must pass (passed)
- [x] 5.3 Run `openspec archive et015-notifications`
- [x] 5.4 Confirm `docs/TICKETS.md`'s ET015 row still shows `Change Name: et015-notifications`; leave `Status` as `In progress` (transitions to `PR open (#N)` at `/pr`, `Done` on merge — archiving now does not mean done yet, per `docs/TICKETS.md` Notes)
