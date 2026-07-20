## Context

`Attachment` (entity, EF configuration, `IAttachmentRepository`) already exists from ET002 —
see `openspec/specs/domain-model/spec.md`. ET006 adds the API/service/storage layer on top of
it: `POST /api/attachments`, file validation, filesystem persistence, and orphan cleanup, per
`openspec/changes/et006-file-attachments/proposal.md` and its spec delta
(`specs/attachment-upload/spec.md`). Contracts: `docs/FRS.md` §4.1.3–4.1.4 (BR-03),
`docs/SDS.md` §3.7, §5.3, §1.4.

The codebase already has established patterns this design reuses rather than reinvents:
- Controller → FluentValidation validator → `Application` service → DTO (`AuthController.cs`).
- `Options` classes bound via `services.Configure<T>(configuration.GetSection(T.SectionName))`
  (`JwtOptions`, `PasswordResetOtpOptions`).
- Per-feature DI extension method (`AuthServiceCollectionExtensions.AddAuthFoundation`).
- `IRepository<TEntity>` stays minimal (`GetByIdAsync`, `AddAsync` only); per-aggregate
  interfaces add what they need (`IExpenseRepository.Query()`).
- `ErrorResponse`/`ErrorDetail` envelope, role-name constants in `AuthorizationPolicyNames`,
  `IUnitOfWork.ExecuteInTransactionAsync` for multi-step transactions.
- `DateTime.UtcNow` is the timestamp convention used everywhere in `Application`/`Infrastructure`
  (`AuthService`, `RefreshTokenService`, `PasswordResetOtpService`) — `Attachment.UploadedAt`
  follows the same convention.

## Goals / Non-Goals

**Goals:**
- Implement `POST /api/attachments` exactly per `docs/SDS.md` §5.3.
- Validate type (PDF/JPG/PNG) and size (≤10 MB) before touching disk or the database.
- Store the file on the local filesystem under a collision-safe, non-guessable path; store only
  metadata + relative path in SQL Server.
- Add the orphan-cleanup background service (ADR-0006) and storage path scheme (ADR-0005).
- Zero changes to `Domain.Entities.Attachment`, its EF configuration, or the EF migration
  history — this ticket is additive only.

**Non-Goals:**
- Linking an `Attachment` to an `Expense` — that's ET007's `POST /api/expenses`.
- Virus/malware scanning or binary signature (magic-byte) verification — FRS/SDS specify
  extension + Content-Type checking only; deeper validation was explicitly declined during
  `/spec` clarification.
- Attachment download/retrieval endpoint — not in FRS §4.1 or SDS §5.3 scope for this ticket.
- Multiple attachments per expense (out of scope per `docs/FRS.md` §12).

## Decisions

### D1 — New capability lives in `Application/Attachments/` and `Infrastructure/Storage/`
Mirrors the existing `Application/Auth/` grouping. New/modified files:

**Domain** (modify only — no entity/schema change):
- `src/Domain/Repositories/IAttachmentRepository.cs` — add `IQueryable<Attachment> Query()`
  (mirrors `IExpenseRepository.Query()`) and `void Remove(Attachment attachment)` (first
  delete-capable repository in the codebase — added narrowly here rather than on the shared
  `IRepository<TEntity>`, since no other aggregate currently deletes rows).

**Application** (new):
- `src/Application/Attachments/UploadAttachmentRequest.cs` — **framework-agnostic** (discovered
  during `/implement`: `Application.csproj` is a plain `Microsoft.NET.Sdk` library with no
  ASP.NET Core `FrameworkReference`, so `IFormFile` isn't available there without leaking a
  hosting dependency into `Application`). The controller maps `IFormFile` → this DTO instead:
  ```csharp
  public record UploadAttachmentRequest
  {
      public required string FileName { get; init; }
      public required string ContentType { get; init; }
      public required long FileSize { get; init; }
      public required Stream Content { get; init; }
  }
  ```
- `src/Application/Attachments/UploadAttachmentRequestValidator.cs` — `AbstractValidator<UploadAttachmentRequest>`:
  file not null/empty; extension ∈ `{.pdf, .jpg, .jpeg, .png}`; `ContentType` ∈
  `{application/pdf, image/jpeg, image/png}`; `Length <= 10_485_760`.
- `src/Application/Attachments/AttachmentUploadResponse.cs`
  ```csharp
  public record AttachmentUploadResponse(Guid AttachmentId);
  ```
- `src/Application/Attachments/IAttachmentService.cs`
  ```csharp
  public interface IAttachmentService
  {
      Task<Guid> UploadAsync(UploadAttachmentRequest request, CancellationToken cancellationToken);
  }
  ```
- `src/Application/Attachments/AttachmentService.cs` — implementation (see D3).
- `src/Infrastructure/BackgroundServices/AttachmentCleanupOptions.cs` — **lives in
  `Infrastructure`**, not `Application` (corrected during `/implement`: its only consumer,
  `OrphanAttachmentCleanupService`, lives in `Infrastructure`, which has no `ProjectReference` to
  `Application` — same co-location fix as `IFileStorageService`/`StorageOptions`).
  ```csharp
  public class AttachmentCleanupOptions
  {
      public const string SectionName = "AttachmentCleanup";
      public int IntervalMinutes { get; set; } = 60;
      public int OrphanThresholdHours { get; set; } = 24;
  }
  ```

**Infrastructure** (new + one modify):
- `src/Infrastructure/Storage/StorageOptions.cs`
  ```csharp
  public class StorageOptions
  {
      public const string SectionName = "Storage";
      public string AttachmentsRootPath { get; set; } = "storage/attachments";
  }
  ```
- `src/Domain/Storage/IFileStorageService.cs` — **interface lives in `Domain`**, not
  `Infrastructure` (corrected during `/implement`: `Application.csproj` has no `ProjectReference`
  to `Infrastructure`, so `AttachmentService` couldn't have depended on an
  `Infrastructure`-declared interface — same reason `IAttachmentRepository` lives in
  `Domain.Repositories` with its EF implementation in `Infrastructure`).
  ```csharp
  public interface IFileStorageService
  {
      Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken);
      Task DeleteAsync(string relativePath, CancellationToken cancellationToken);
  }
  ```
- `src/Infrastructure/Storage/FileStorageService.cs` — implements `Domain.Storage.IFileStorageService`; resolves the absolute root once via
  `IHostEnvironment.ContentRootPath` + `StorageOptions.AttachmentsRootPath` (same
  `Path.GetFullPath(Path.Combine(...))` pattern `Program.cs` already uses for
  `SeedData:EmployeeCsvPath`); `SaveAsync` builds `{yyyy}/{MM}/{guid}{ext}`, creates the
  subfolder if missing, writes the stream, returns the relative path.
- `src/Infrastructure/Persistence/Repositories/AttachmentRepository.cs` — modify to add
  `Query()` (`DbContext.Set<Attachment>().AsQueryable()`) and `Remove()`
  (`DbContext.Set<Attachment>().Remove(attachment)`), matching `ExpenseRepository`'s shape.
- `src/Infrastructure/BackgroundServices/OrphanAttachmentCleanupService.cs` — `BackgroundService`
  (see D4).

**Api** (new + modify):
- `src/Api/Controllers/AttachmentsController.cs` — new (see D2).
- `src/Api/Authorization/AuthorizationPolicyNames.cs` — modify: add
  `public const string EmployeeOrManager = "EmployeeOrManager";`.
- `src/Api/Extensions/AttachmentServiceCollectionExtensions.cs` — new `AddAttachmentFoundation`,
  mirroring `AddAuthFoundation`: binds `StorageOptions`/`AttachmentCleanupOptions`, registers
  `IFileStorageService`, `IAttachmentService`, the `EmployeeOrManager` policy, and
  `AddHostedService<OrphanAttachmentCleanupService>()`.
- `src/Api/Program.cs` — modify: add `builder.Services.AddAttachmentFoundation(builder.Configuration);`
  (`IAttachmentRepository` is already registered at `Program.cs:27` — no change needed there).
- `src/Api/appsettings.json` — modify: add
  ```json
  "Storage": { "AttachmentsRootPath": "storage/attachments" },
  "AttachmentCleanup": { "IntervalMinutes": 60, "OrphanThresholdHours": 24 }
  ```

**DB changes:** none. No new migration — fully backward compatible with the ET002 schema.

### D2 — Controller: role check via a new combined policy, not an inline check
`docs/FRS.md` §4.1.5 allows both `Employee` and `Manager` to upload. Existing
`AuthorizationPolicyNames` only has one-role-per-policy entries. Rather than inline
`if (role == ...)` (forbidden by `backend/CLAUDE.md`) or a raw `[Authorize(Roles = "Employee,Manager")]`
string next to the controller, add one named policy —
`options.AddPolicy(AuthorizationPolicyNames.EmployeeOrManager, p => p.RequireRole(AuthorizationPolicyNames.Employee, AuthorizationPolicyNames.Manager))`
— registered from `AddAttachmentFoundation`. `RequireRole` with multiple arguments is OR
semantics (either role satisfies it), matching FRS 4.1.5 exactly. Controller:
```csharp
[ApiController]
[Route("api/attachments")]
[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]
public class AttachmentsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        var request = new UploadAttachmentRequest
        {
            FileName = file?.FileName ?? string.Empty,
            ContentType = file?.ContentType ?? string.Empty,
            FileSize = file?.Length ?? 0,
            Content = file?.OpenReadStream() ?? Stream.Null,
        };

        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return ValidationErrorResult(validation); // same helper shape as AuthController

        var attachmentId = await _attachmentService.UploadAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new AttachmentUploadResponse(attachmentId));
    }
}
```
(`IFormFile` stays in the `Api` layer only — the controller is the framework boundary that
translates it into the framework-agnostic `UploadAttachmentRequest`.)
No custom request-size middleware/config is needed: ASP.NET Core's default multipart body
length limit (128 MB) already exceeds the 10 MB cap, and the FluentValidation rule rejects
oversized files with `400 VALIDATION_ERROR` before any service/storage work happens.

### D3 — Write-then-persist ordering, with compensation on DB failure
`AttachmentService.UploadAsync`:
1. `var extension = Path.GetExtension(request.FileName).ToLowerInvariant();`
2. `var relativePath = await _fileStorage.SaveAsync(request.Content, extension, cancellationToken);` — file hits disk first.
3. Build the `Attachment` entity (`FileName` = generated name from `relativePath`,
   `OriginalFileName` = `request.FileName`, `ContentType` = `request.ContentType`,
   `FileExtension` = extension, `FileSize` = `(int)request.FileSize`,
   `StoragePath` = `relativePath`, `UploadedAt` = `DateTime.UtcNow`).
4. `await _attachmentRepository.AddAsync(attachment, cancellationToken);`
   `await _unitOfWork.SaveChangesAsync(cancellationToken);`
5. If step 4 throws, `catch` and call `_fileStorage.DeleteAsync(relativePath, CancellationToken.None)`
   before rethrowing — the DB is the source of truth for what attachments "exist", so a file
   without a DB row must not be left behind. (The reverse ordering — DB insert, then file write —
   would risk the opposite failure: a DB row pointing at a file that was never written, which is
   worse because it would silently break ET007's later attachment lookup.)

This isn't a single atomic operation across two systems (filesystem + SQL Server can't share a
transaction) — see Risks.

### D4 — Orphan cleanup as a `BackgroundService`, not a request-triggered sweep
ADR-0006 (from `/spec` clarification): a `BackgroundService` polling on
`AttachmentCleanupOptions.IntervalMinutes` (default 60) is simpler to reason about and test than
piggybacking cleanup on the hot upload path, and matches the existing `IHostedService` pattern
already in this codebase for `EmployeeRoleResolutionMiddleware`-adjacent DI (registered the same
way via the extension method).

**Split into a thin loop + a testable sweeper** (refactored during `/implement` once test-writing
exposed the problem: the sweep logic was originally a `private` method on the `BackgroundService`
itself, which made it untestable without actually running the timed loop). The actual
query/delete/save logic now lives in `IOrphanAttachmentSweeper`/`OrphanAttachmentSweeper`
(`Infrastructure/BackgroundServices/`), unit-testable directly with fakes:
```csharp
public class OrphanAttachmentSweeper : IOrphanAttachmentSweeper
{
    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddHours(-_options.OrphanThresholdHours);
        var orphans = _attachmentRepository.Query() // sync ToList(), not ToListAsync() — see note below
            .Where(a => a.Expense == null && a.UploadedAt < cutoff)
            .ToList();

        foreach (var orphan in orphans)
        {
            await _fileStorage.DeleteAsync(orphan.StoragePath, cancellationToken);
            _attachmentRepository.Remove(orphan);
        }

        if (orphans.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
```
**Sync `ToList()`, not `ToListAsync()`** (bug caught by the unit tests in `OrphanAttachmentSweeperTests.cs`):
EF Core's `ToListAsync()` requires the underlying `IQueryable` to implement `IAsyncEnumerable`,
which only a real EF-backed queryable does — the `FakeAttachmentRepository` test double's
LINQ-to-Objects `IQueryable` doesn't, so the sweeper threw at test time. Since `IAttachmentRepository.Query()`
is a provider-agnostic abstraction (by design, so it's fake-able in unit tests), calling an
EF-specific async extension on it was a leaky assumption. Switched to synchronous `.ToList()`,
which works identically against both a real `DbSet<Attachment>` and an in-memory fake — an
acceptable trade-off since this runs on an hourly background tick, not a request hot path.

`OrphanAttachmentCleanupService` (the `BackgroundService`) now only owns the timing loop: each
tick, it creates a DI scope, resolves `IOrphanAttachmentSweeper`, and calls `SweepAsync`, wrapped
in a `try/catch` that logs and continues on any exception (per
`specs/attachment-upload/spec.md`'s "Orphaned Attachment Cleanup" requirement: cleanup failure
must never affect in-flight requests — it runs in its own scope/loop, entirely decoupled).

### D5 — ADRs to add under `docs/decisions/`
Per the proposal, both are genuine additions beyond the literal SDS text and get their own ADR
files (written as part of `/tasks` → `/implement`, not this design doc):
- `ADR-0005-attachment-storage-path-scheme.md`: GUID filename + `yyyy/MM` subfolder, configurable
  root, rejected alternatives = flat directory (unbounded dir growth) and original-filename-on-disk
  (path traversal / collision risk).
- `ADR-0006-orphan-attachment-cleanup.md`: hourly `BackgroundService`, 24h threshold, rejected
  alternative = on-demand sweep piggybacked on the next upload request (adds latency/complexity
  to the hot path for a cold-path concern).

## Risks / Trade-offs

- **[Risk]** Filesystem write and DB insert aren't atomic — a crash between step 2 and step 4 in
  D3 leaves an orphaned file with no DB row. → **Mitigation**: harmless by construction — it's
  indistinguishable from a normal orphan and gets swept by the same D4 cleanup job once it ages
  past the threshold; no separate reconciliation job needed.
- **[Risk]** `IFormFile.FileName` is client-supplied and untrusted (could contain path-traversal
  sequences like `../../`). → **Mitigation**: `OriginalFileName` is stored as opaque metadata
  only, never used to construct a filesystem path — `FileStorageService` always derives the
  on-disk name from a fresh `Guid`, never from client input (D1/D3).
  `Path.GetExtension` is applied only to extract the extension for the allow-list check, not
  to build a path.
- **[Risk]** Multiple app instances running the `BackgroundService` concurrently (future
  horizontal scaling) could double-process the same sweep. → **Mitigation**: not a concern at
  current single-instance deployment scale; `Remove` on an already-deleted row is idempotent
  (EF no-ops on `SaveChanges` if untracked/missing), so worst case is a harmless duplicate
  file-delete attempt, which `DeleteAsync` should treat as a no-op if the file is already gone.
- **[Trade-off]** Extension + declared `Content-Type` validation (no magic-byte sniffing) means a
  maliciously renamed file (e.g., an executable named `receipt.pdf` with a forged
  `Content-Type: application/pdf` header) will pass validation. Accepted per FRS/SDS literal
  scope and the `/spec` clarification; flagged here for visibility, not re-litigated.

## Migration Plan

No DB migration. Rollout is a standard deploy: new config sections have defaults baked into the
`Options` classes, so an unmodified `appsettings.json` in another environment still works
(`Storage:AttachmentsRootPath` defaults to `storage/attachments` relative to content root). No
rollback concerns beyond a normal code revert — no data is transformed, only newly created.

## Open Questions

None outstanding — both prior open questions were resolved during `/plan` review:

- **`.jpeg` extension** (was Q1): confirmed — the validator allow-lists both `.jpg` and `.jpeg`
  extensions (both map to `Content-Type: image/jpeg`), per D1/D3, since they're the same format
  under two conventional extensions.
- **`FileStorageService.DeleteAsync` on a missing file** (was Q2): confirmed — it treats an
  already-missing file as success (no-op, does not throw), and logs at `Debug` level for
  observability. This keeps the D4 cleanup sweep and the D3 compensating-delete path idempotent
  without surfacing noise at `Information`/`Warning` level for what is an expected condition.
