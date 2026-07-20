Backend-only ticket (`docs/TICKETS.md` domain: Attachments) — no frontend scope. All
`[PARALLEL]` opportunities in `docs/TICKETS.md`/CLAUDE.md's parallel-work policy apply to
simultaneous *backend* work streams here, not frontend/backend split (attachment upload UI is
ET017). Frontend build/lint/test checkpoint commands are therefore marked N/A per phase rather
than silently omitted.

## 1. Foundation

- [x] 1.1 Modify `Domain/Repositories/IAttachmentRepository.cs`: add `IQueryable<Attachment> Query()` and `void Remove(Attachment attachment)` (design D1).
- [x] 1.2 Modify `Infrastructure/Persistence/Repositories/AttachmentRepository.cs` to implement `Query()`/`Remove()` (mirrors `ExpenseRepository`).
- [x] 1.3 Add `Infrastructure/Storage/StorageOptions.cs` (`SectionName = "Storage"`, `AttachmentsRootPath` default `storage/attachments`).
- [x] 1.4 Add `Infrastructure/BackgroundServices/AttachmentCleanupOptions.cs` (`SectionName = "AttachmentCleanup"`, `IntervalMinutes` default 60, `OrphanThresholdHours` default 24) — moved from `Application` to `Infrastructure` during implementation (task 2.6/2.7) to co-locate with its only consumer, `OrphanAttachmentCleanupService`.
- [x] 1.5 Modify `Api/appsettings.json`: add `Storage` and `AttachmentCleanup` sections (design D1).
- [x] 1.6 Add `Application/Attachments/UploadAttachmentRequest.cs` — framework-agnostic (`FileName`/`ContentType`/`FileSize`/`Content` stream), not `IFormFile` — corrected during implementation since `Application.csproj` has no ASP.NET Core `FrameworkReference`; `design.md` updated to match.
- [x] 1.7 Add `Application/Attachments/AttachmentUploadResponse.cs` (`record(Guid AttachmentId)`).
- [x] 1.8 Confirm no EF Core migration is required (schema unchanged from ET002) — record this explicitly in the PR description.

**Checkpoint 1**
- [x] `dotnet build` (backend) → 0 errors
- [x] `dotnet format --verify-no-changes` (backend) → clean
- Frontend build/lint: N/A (no frontend scope this ticket)

## 2. Core Implementation

- [x] 2.1 Add `Application/Attachments/UploadAttachmentRequestValidator.cs`: file required; extension ∈ `{.pdf, .jpg, .jpeg, .png}`; `ContentType` ∈ `{application/pdf, image/jpeg, image/png}`; size ≤ 10,485,760 bytes (design D1, resolved open question — `.jpeg` included).
- [x] 2.2 Add `Domain/Storage/IFileStorageService.cs` (`SaveAsync`, `DeleteAsync`) — moved from `Infrastructure` to `Domain` during implementation; `Application` has no project reference to `Infrastructure`, mirroring why `IAttachmentRepository` lives in `Domain.Repositories`.
- [x] 2.3 Add `Infrastructure/Storage/FileStorageService.cs`: implements `Domain.Storage.IFileStorageService`; resolves absolute root from `IHostEnvironment.ContentRootPath` + `StorageOptions.AttachmentsRootPath`; `SaveAsync` writes to `{yyyy}/{MM}/{guid}{ext}`; `DeleteAsync` no-ops (logs at `Debug`) if the file is already gone (design D1/D4, resolved open question). Required adding NuGet package `Microsoft.Extensions.Hosting.Abstractions` 9.0.8 to `Infrastructure.csproj` for `IHostEnvironment`.
- [x] 2.4 Modify `Api/Authorization/AuthorizationPolicyNames.cs`: add `EmployeeOrManager` constant.
- [x] 2.5 Add `Application/Attachments/IAttachmentService.cs` + `AttachmentService.cs`: write-then-persist flow with compensating delete on `SaveChangesAsync` failure (design D3).
- [x] 2.6 Add `Api/Extensions/AttachmentServiceCollectionExtensions.cs` (`AddAttachmentFoundation`): binds `StorageOptions`/`AttachmentCleanupOptions`, registers `IFileStorageService`, `IAttachmentService`, the `EmployeeOrManager` policy, `AddHostedService<OrphanAttachmentCleanupService>()`.
- [x] 2.7 Add `Infrastructure/BackgroundServices/OrphanAttachmentCleanupService.cs`: `BackgroundService` polling on `IntervalMinutes`, deletes rows/files where `Expense == null` and `UploadedAt < now - OrphanThresholdHours`, wrapped in try/catch-and-continue per sweep (design D4). Refactored during Phase 4 test-writing: sweep logic extracted into `IOrphanAttachmentSweeper`/`OrphanAttachmentSweeper` (own file each) so it's directly unit-testable; the `BackgroundService` now only owns the timing loop and resolves the sweeper per tick from a DI scope. Registered in `AddAttachmentFoundation`.
- [x] 2.8 Add `Api/Controllers/AttachmentsController.cs`: `POST /api/attachments`, `[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]`, validate → service → `201 Created` with `AttachmentUploadResponse` (design D2). Validator fix: uses `OverridePropertyName("file")`, not `WithName("file")` — confirmed via Context7 that `WithName` only changes the error message text, not `ValidationFailure.PropertyName` (which populates the `fields` array).

**Checkpoint 2**
- [x] `dotnet build` (backend) → 0 errors
- [x] `dotnet format --verify-no-changes` (backend) → clean
- Frontend build/lint: N/A

## 3. Integration

- [x] 3.1 Modify `Api/Program.cs`: add `builder.Services.AddAttachmentFoundation(builder.Configuration);` (`IAttachmentRepository` DI registration at `Program.cs:27` already exists — no change there).
- [x] 3.2 Manual smoke test via Scalar OpenAPI UI (`/scalar` in Development): confirmed by user — valid upload returned `201` with `{ "attachmentId": "15018cea-865f-4dba-91ae-11a115b46bc9" }`.
- [x] 3.3 Write `docs/decisions/ADR-0005-attachment-storage-path-scheme.md` (GUID filename + `yyyy/MM` subfolder; rejected alternatives per design D5).
- [x] 3.4 Write `docs/decisions/ADR-0006-orphan-attachment-cleanup.md` (hourly `BackgroundService`, 24h threshold; rejected alternative per design D5).

**Checkpoint 3**
- [x] `dotnet build` (backend) → 0 errors
- Frontend build: N/A

## 4. Tests (one per spec scenario, `specs/attachment-upload/spec.md`)

### Attachment Upload Endpoint
- [x] 4.1 Integration test: valid upload (PDF, Employee or Manager role) returns `201` + `attachmentId`; `Attachment` row exists with `Expense` null — *Scenario: Valid upload returns an attachmentId*.
- [x] 4.2 Integration test: request with no/invalid bearer token → `401 AUTHENTICATION_FAILED` — *Scenario: Unauthenticated request is rejected*.
- [x] 4.3 Integration test: authenticated user with role `Finance`/`ComplianceOfficer` → `403 AUTHORIZATION_FAILED` — *Scenario: Role outside Employee/Manager is rejected*.

### File Type and Size Validation
- [x] 4.4 Unit test (`UploadAttachmentRequestValidator`): `.docx` (or other disallowed extension/Content-Type) → validation fails — *Scenario: Disallowed file type is rejected*.
- [x] 4.5 Unit test (`UploadAttachmentRequestValidator`): file > 10 MB → validation fails — *Scenario: Oversized file is rejected*.
- [x] 4.6 Unit test (`UploadAttachmentRequestValidator`): file at exactly 10,485,760 bytes → validation passes — *Scenario: File at exactly the 10 MB boundary is accepted*.
- [x] 4.7 Integration test: `POST /api/attachments` with a disallowed/oversized file → `400 VALIDATION_ERROR` with a `fields` entry, and no `Attachment` row or file created (covers both rejection scenarios end-to-end).

### Filesystem Storage and Metadata Persistence
- [x] 4.8 Unit test (`FileStorageService`): `SaveAsync` writes under `{root}/{yyyy}/{MM}/{guid}{ext}` and returns the matching relative path — *Scenario: Uploaded file is written under the configured root*.
- [x] 4.9 Unit test (`AttachmentService`): uploading a file named `My Receipt (final).PDF` persists `OriginalFileName` verbatim while the on-disk file uses a generated GUID name — *Scenario: Original filename is preserved as metadata only*.
- [x] 4.10 Unit test (`AttachmentService`): `SaveChangesAsync` failure triggers a compensating `DeleteAsync` of the just-written file (design D3 risk mitigation, not a spec scenario but load-bearing for D3's correctness).

### Orphaned Attachment Cleanup
(Writing these tests caught a real bug: `OrphanAttachmentSweeper` was calling EF Core's `ToListAsync()` on the repository's abstract `IQueryable<Attachment>`, which throws against any non-EF-backed queryable, including the unit test's fake repository. Fixed by switching to synchronous `.ToList()` — see design.md D4.)
- [x] 4.11 Unit test (`OrphanAttachmentSweeper`): unlinked `Attachment` with `UploadedAt` > 24h old is deleted (row + file) — *Scenario: Orphaned attachment older than 24 hours is deleted*.
- [x] 4.12 Unit test (`OrphanAttachmentSweeper`): unlinked `Attachment` with `UploadedAt` < 24h old is retained — *Scenario: Recently uploaded orphan is retained*.
- [x] 4.13 Unit test (`OrphanAttachmentSweeper`): `Attachment` linked to an `Expense`, regardless of age, is never deleted — *Scenario: Linked attachment is never deleted*.
- [x] 4.14 Unit test (`FileStorageService`): `DeleteAsync` on an already-missing file returns success (no-op, no throw) — resolved open question, backs scenarios 4.11/4.13's idempotency.

### Supporting/wiring tests (not spec scenarios, but required by `backend/CLAUDE.md` DI conventions)
- [x] 4.15 DI registration test (mirrors `AuthServiceCollectionExtensionsDiTests`): `AddAttachmentFoundation` resolves `IFileStorageService`, `IAttachmentService`, `IOrphanAttachmentSweeper`, and the hosted cleanup service without error.
- [x] 4.16 Authorization test (added to `RoleBasedAuthorizationTests`, new `/__test/employee-or-manager-only` endpoint in `TestEndpointsStartupFilter`): `EmployeeOrManager` policy accepts `Employee` and `Manager`, rejects `Finance` and `ComplianceOfficer`.

**Checkpoint 4**
- [x] `dotnet build` (backend) → 0 errors
- [x] `dotnet format --verify-no-changes` (backend) → clean
- [x] `dotnet test` unit tests → 70/70 green
- [x] `dotnet test` integration tests → 56/56 green
- E2E (Playwright): N/A — no user-facing flow in this ticket (attachment upload has no UI until ET017)
- Frontend build/lint/test: N/A

## 5. Archive

- [x] 5.1 Run `openspec archive et006-file-attachments`.
- [ ] 5.2 Update `docs/TICKETS.md`: ET006 `Status` → `PR open (#N)` once the PR is raised (per the Status convention in `docs/TICKETS.md` Notes — archive happens before the PR, so `Status` moves to `PR open` only when the PR actually exists, not at archive time).
- [ ] 5.3 Open the PR referencing ADR-0005 and ADR-0006. Note: the current branch
  (`feature/backend/ET006-file-attachments`) predates this `/tasks` run and doesn't match
  `CLAUDE.md`'s `ticket/<ticket-id>-<kebab-description>` naming convention — flagged for
  awareness, not auto-renamed (renaming an active branch is a manual/destructive-adjacent
  action requiring explicit confirmation, out of scope for this checklist).
