## Context

ET002 already built the `Expense`/`Attachment` entities, `ExpenseCategory`/`ExpenseStatus`
enums, EF configurations (including the unique `AttachmentId` index enforcing 1:1), and
`IExpenseRepository`/`IAttachmentRepository` (registered in `Program.cs`). ET006 built
`POST /api/attachments` end-to-end (`AttachmentsController` → `AttachmentService` →
`IAttachmentRepository` → `IFileStorageService`), with the `EmployeeOrManager` authorization
policy already registered. ET007 is otherwise greenfield on the `Application`/`Api` side: no
`Expenses/` folder, no `ExpensesController`, no request/response DTOs exist yet.

Constraints from `docs/SDS.md` that shape this design: backend is sole source of truth for
business rules (§1.3); status transitions only via dedicated action endpoints, never a generic
status field (§1.3, `AGENTS.md` §11); state-changing operations run in a DB transaction (§11.1);
audit fields are system-managed only (§6.8); role is re-resolved from `Employee` every request,
never trusted from the JWT (§4.1, §4.6).

## Goals / Non-Goals

**Goals:**
- `POST /api/expenses` (create, `action: Draft | Submit`) and `POST /api/expenses/{id}/submit`
  (submit an existing Draft), per the `/spec` clarifications.
- Server-generated, immutable, unique `ExpenseNumber`.
- BR-01/BR-02/BR-03 and field-level validation, identical for Draft and Submit.
- Attachment ownership validation (closing the gap identified in `/spec`), including the
  supporting `Attachment.UploadedByEmployeeId` schema change.

**Non-Goals** (per `docs/TICKETS.md` build order):
- Expense edit/cancel (ET008), list/search/view (ET009), any review action — manager approve/
  reject, compliance approve/reject, reimburse (ET010-ET012).
- Any workflow transition beyond `Draft → Submitted`.
- Notifications (ET015) — not wired to expense creation in this ticket.

## Decisions

### D1 — Expense number generation: retry-on-conflict, no new table
See `docs/decisions/ADR-0007-expense-number-generation.md`. `yyyyMMdd` = `CreatedAt` (generation
date); `XXXX` = count of same-day-prefixed `ExpenseNumber`s + 1, zero-padded to 4 digits, with
retry (up to 5 attempts) on a unique-constraint violation during insert. Implemented as a small,
independently unit-testable `IExpenseNumberGenerator`/`ExpenseNumberGenerator`
(`Application/Expenses/`), injected into `ExpenseService` rather than inlined — isolates the
one piece of this ticket with real concurrency-correctness risk so it can be tested (including
simulated collisions) without exercising the full create flow.

### D2 — Attachment ownership: new `UploadedByEmployeeId` column
See `docs/decisions/ADR-0008-attachment-uploader-tracking.md`. `Attachment` gains a required
`UploadedByEmployeeId` (Guid, FK to `Employee`, `OnDelete: Restrict`), set only server-side.

### D3 — Resolving the caller's `EmployeeId`: extend the existing Role-claim pattern
`ClaimsPrincipalExtensions.GetUserId()` only extracts the JWT `sub` claim (a `UserId`, not an
`EmployeeId`) — there is currently no cheap way for a controller to get the acting employee's
`EmployeeId` without a fresh DB round-trip. `EmployeeRoleResolutionMiddleware` already loads
`User.Employee` on every authenticated request (to derive `Role`) but discards everything except
the role string.

**Decision:** extend `EmployeeRoleResolutionMiddleware` to also add a second claim —
`new Claim("employee_id", user.Employee.EmployeeId.ToString())` — right where it already adds
the `Role` claim, from the same already-loaded `Employee`, at zero extra DB cost. Add
`ClaimsPrincipalExtensions.GetEmployeeId()` (same pattern as the existing `GetUserId()`) to read
it back in controllers/services.

This claim is added to the in-memory `ClaimsPrincipal` during the request pipeline — it is
**not** embedded in the JWT itself (the access token payload is unchanged: still `sub`-only,
15 min, HS256, per `docs/SDS.md` §4.3 and `AGENTS.md` §7) — so this does not violate "no
roles/permissions in the access token." It's the exact same mechanism already used for the
`Role` claim, just carrying one more already-fetched value.

Alternative rejected: re-query `IUserRepository.GetByIdWithEmployeeAsync` inside
`AttachmentService`/`ExpenseService` from the `sub` claim. Works, but duplicates a DB call the
middleware already made on the same request for no benefit.

### D4 — Service failure signaling: reuse the `Result` record + `FailureReason` enum idiom
`AuthService`/`AuthController` already establish the pattern this codebase uses for
business/domain failures that aren't simple field validation: a `record Result(bool Succeeded,
..., FailureReason)` returned from the service, with the controller mapping `FailureReason` to
the correct HTTP status/error code via a `switch`-based private helper (see
`AuthController.FailureResult`). `GlobalExceptionHandler` only ever returns `500` — it is not
used for expected business outcomes (wrong owner, wrong state, invalid attachment), matching how
`AttachmentsController`/`AuthController` never throw for those cases.

**Decision:** `ExpenseService` follows the identical shape —
`ExpenseCreationResult(bool Succeeded, ExpenseResponse? Expense, ExpenseFailureReason
FailureReason, IReadOnlyList<string> FailureFields)` for create, and a matching
`ExpenseSubmitResult` for the submit-draft endpoint (or one shared `ExpenseFailureReason` enum
covering both — see File Plan) — with `ExpensesController` mapping reasons to `400`/`403`/`404`/
`422` exactly as `AuthController.FailureResult` does for auth outcomes.

### D5 — Draft and Submit share one validation path
Per `/spec`, BR-01/BR-02/BR-03 and field-level validation apply identically regardless of
`action`. Implementation: `CreateExpenseRequestValidator` (FluentValidation) covers field-level
concerns (category enum, currency == `INR`, description length) and runs before the service is
called, exactly like `UploadAttachmentRequestValidator`; `ExpenseService.CreateAsync` then runs
the BR-01/BR-02 and attachment-linkage/ownership checks (business rules, not shape validation)
itself, returning `ExpenseFailureReason` values that map to `422`. `action` only decides the
final `Status`/`SubmittedAt` — it never changes which checks run.

### D6 — Ownership authorization for `/submit` is a manual check, not a policy
`[Authorize(Policy = ...)]` policies in this codebase are role-based (`Employee`, `Manager`,
`EmployeeOrManager`) — they can't express "caller must be *this specific resource's* owner."
`POST /api/expenses/{id}/submit` uses `[Authorize(Policy = AuthorizationPolicyNames.
EmployeeOrManager)]` for the role gate, then `ExpenseService.SubmitAsync` loads the expense and
compares `Expense.EmployeeId` to the caller's `EmployeeId` (via D3), returning a `NotOwner`
failure reason that the controller maps to `403 AUTHORIZATION_FAILED` — the same shape as every
other business failure (D4), not a new authorization mechanism.

## File Plan

**New — `backend/src/Application/Expenses/`:**
- `IExpenseService.cs` — `Task<ExpenseCreationResult> CreateAsync(Guid employeeId,
  CreateExpenseRequest request, CancellationToken ct)`;
  `Task<ExpenseSubmitResult> SubmitAsync(Guid employeeId, Guid expenseId, CancellationToken ct)`.
- `ExpenseService.cs` — depends on `IExpenseRepository`, `IAttachmentRepository`,
  `IExpenseNumberGenerator`, `IUnitOfWork`.
- `IExpenseNumberGenerator.cs` / `ExpenseNumberGenerator.cs` — `Task<string>
  GenerateAsync(CancellationToken ct)`, per D1.
- `CreateExpenseRequest.cs` — request record (see DTOs below).
- `CreateExpenseRequestValidator.cs` — FluentValidation, field-level only (D5).
- `ExpenseResponse.cs` — response record mapped from `Expense`.
- `ExpenseFailureReason.cs` — shared enum (D4): `None, AttachmentNotFound,
  AttachmentAlreadyLinked, AttachmentNotOwned, AmountNotPositive, ExpenseDateInFuture,
  ExpenseNotFound, NotOwner, NotDraft`.
- `ExpenseCreationResult.cs`, `ExpenseSubmitResult.cs` — `Result` records per D4.

**New — `backend/src/Api/`:**
- `Controllers/ExpensesController.cs` — `[Route("api/expenses")]`,
  `[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]`; `POST ""` (create),
  `POST "{id}/submit"`.
- `Extensions/ExpenseServiceCollectionExtensions.cs` — `AddExpenseFoundation(this
  IServiceCollection services, IConfiguration configuration)`: registers `IExpenseService`,
  `IExpenseNumberGenerator`, and (added post-implementation, see `ICompanyClock` above)
  `ICompanyClock` plus `CompanyTimeZoneOptions` bound from the `CompanyTimeZone` configuration
  section — the `IConfiguration` parameter was not in this artifact's original draft (added when
  `ICompanyClock` was introduced) (FluentValidation validators auto-register via the existing
  `AddValidatorsFromAssemblyContaining<...>()` scan — no extra line needed there).

**Modified:**
- `backend/src/Domain/Entities/Attachment.cs` — add `public Guid UploadedByEmployeeId { get;
  set; }` (D2).
- `backend/src/Infrastructure/Persistence/Configurations/AttachmentConfiguration.cs` — configure
  the new FK (`HasOne(...).WithMany().HasForeignKey(a => a.UploadedByEmployeeId)
  .OnDelete(DeleteBehavior.Restrict)`; no inverse collection on `Employee`, per ADR-0008).
- New EF Core migration (`AddAttachmentUploader`) via `dotnet ef migrations add`.
- `backend/src/Api/Authentication/EmployeeRoleResolutionMiddleware.cs` — add the `employee_id`
  claim (D3).
- `backend/src/Api/Authentication/ClaimsPrincipalExtensions.cs` — add `GetEmployeeId()` (D3).
- `backend/src/Application/Attachments/UploadAttachmentRequest.cs` — add `required Guid
  UploadedByEmployeeId`.
- `backend/src/Application/Attachments/AttachmentService.cs` — set
  `attachment.UploadedByEmployeeId = request.UploadedByEmployeeId`.
- `backend/src/Api/Controllers/AttachmentsController.cs` — pass `User.GetEmployeeId()` into the
  constructed `UploadAttachmentRequest`.
- `backend/src/Api/Program.cs` — add `builder.Services.AddExpenseFoundation();`.

## DTOs / Records (matching `docs/SDS.md` §5.2)

**Post-implementation correction:** `CreateExpenseRequest.Category`/`.Action` and
`ExpenseResponse.Category`/`.Status` are typed as `string`, not the `ExpenseCategory`/
`ExpenseAction`/`ExpenseStatus` enums shown in this section's original draft below. Reason,
found during implementation: this project's `[ApiController]` uses ASP.NET Core's default
automatic `400` on model-binding failure — if these were real enums, an invalid JSON value would
fail model binding *before* `CreateExpenseRequestValidator` or the controller ever runs,
producing ASP.NET's default `ValidationProblemDetails` shape instead of this app's required
`{"error": {...}}` envelope (`AGENTS.md` §6). Binding as `string` and validating with
`Enum.TryParse` inside `CreateExpenseRequestValidator` guarantees the standard envelope for every
invalid value. `ExpenseResponse.Category`/`.Status` are `string` for a related but separate
reason: this codebase's convention (`UserDto.Role`) is to expose enum-like values as strings in
JSON responses, since there's no global `JsonStringEnumConverter` configured — a raw enum would
otherwise serialize as an integer (caught live during Phase 3 manual verification, tasks.md §3.1).
Both `ExpenseService.CreateAsync` and the entity itself still use the real `ExpenseCategory`/
`ExpenseStatus` enums internally — only the request/response DTO boundary is `string`.

**Post-implementation correction (round 3):** the DTO originally still marked all seven
properties `required`. A `/review` found this has the *same* model-binding-vs-envelope problem
as the enum-typing issue above, but for a different trigger: C#'s `required` member modifier is
enforced by `System.Text.Json` regardless of property type (value type or reference type) — a
request that genuinely omits a required JSON key (not merely sends an empty/zero value) fails
deserialization with a raw `JsonException` before `CreateExpenseRequestValidator` or the
controller ever runs, again producing ASP.NET's default shape instead of the `{"error": {...}}`
envelope. Fixed by removing `required` from every property. Each field's "missing → default"
value is still safely rejected, but via whichever layer already owned that concern rather than a
uniform blanket rule:
- `ExpenseDate` missing → `default(DateOnly)` → new `NotEmpty()` rule in
  `CreateExpenseRequestValidator` (400) — nothing else would have caught a year-0001 date.
- `Description` missing → `null` → new `NotEmpty()` rule, ahead of the existing `MaximumLength`
  (400) — `MaximumLength` alone treats `null` as valid (it's a length check, not a presence
  check), so this one needed an explicit addition, not just removing `required`.
- `Category`/`Action` missing → `null` → the existing `Enum.TryParse<T>(string?, out T)` calls
  already handle `null` safely (return `false`, no exception) — no rule change needed.
- `Currency` missing → `null` → the existing `.Equal(RequiredCurrency)` rule already treats
  `null` as "not equal" — no rule change needed.
- `Amount`/`ReceiptAttachmentId` missing → `0m`/`Guid.Empty` → deliberately **not** given a new
  FluentValidation rule, because the existing service-layer checks (BR-01 "amount not positive",
  attachment-not-found) already reject these exact default values as `422
  BUSINESS_RULE_VIOLATION` — consistent with the archived spec's own scenarios, which document
  `amount: 0` as a 422 case, not 400. Adding a validator-level `NotEmpty()` for `Amount` would
  have *changed* that documented behavior for the explicit-zero case, not just fixed the
  missing-key case.

`ExpenseService.CreateAsync` uses the null-forgiving operator (`request.Category!`, etc.) at its
four usage sites, since the controller always runs the validator (which rejects all null cases)
before the service is ever called — the compiler can't see that guarantee, so the `!` is a
deliberate, narrow assertion of it, not a suppressed bug.

```csharp
// CreateExpenseRequest.cs — no `required` members (see note below); string fields are
// nullable (`string?`) since a genuinely-omitted JSON key binds to null, not an exception
public record CreateExpenseRequest
{
    public DateOnly ExpenseDate { get; init; }
    public string? Category { get; init; } // parsed via Enum.TryParse<ExpenseCategory> in the validator
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
    public string? Description { get; init; }
    public Guid ReceiptAttachmentId { get; init; }
    public string? Action { get; init; } // "Draft" or "Submit", parsed via Enum.TryParse<ExpenseAction>
}

// ExpenseResponse.cs — mirrors docs/SDS.md §5.2's `{ "expense": { ... } }` success shape
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
    DateTime CreatedAt);

// ExpenseEnvelopeResponse.cs — the `{ "expense": {...} }` wrapper, added during implementation
// so the wrapper is a named DTO rather than an anonymous object, per this codebase's convention
public record ExpenseEnvelopeResponse(ExpenseResponse Expense);

// ExpenseFailureReason.cs — CurrencyInvalid/DescriptionTooLong added post-implementation
// (see "Submit re-validation" below) so SubmitAsync can re-run field-level checks, not just
// business-rule checks, against a stored Draft
public enum ExpenseFailureReason
{
    None, AttachmentNotFound, AttachmentAlreadyLinked, AttachmentNotOwned,
    AmountNotPositive, ExpenseDateInFuture, ExpenseNotFound, NotOwner, NotDraft,
    CurrencyInvalid, DescriptionTooLong,
}

// ExpenseCreationResult.cs
public record ExpenseCreationResult(bool Succeeded, ExpenseResponse? Expense, ExpenseFailureReason FailureReason)
{
    public static ExpenseCreationResult Success(ExpenseResponse expense) => new(true, expense, ExpenseFailureReason.None);
    public static ExpenseCreationResult Failure(ExpenseFailureReason reason) => new(false, null, reason);
}

// ExpenseSubmitResult.cs — same shape, reused for the /submit endpoint
public record ExpenseSubmitResult(bool Succeeded, ExpenseResponse? Expense, ExpenseFailureReason FailureReason)
{
    public static ExpenseSubmitResult Success(ExpenseResponse expense) => new(true, expense, ExpenseFailureReason.None);
    public static ExpenseSubmitResult Failure(ExpenseFailureReason reason) => new(false, null, reason);
}
```

**Submit re-validation (post-implementation correction):** a `/review` found `SubmitAsync`
originally re-ran only BR-01/BR-02/attachment checks against the stored Draft, not the
field-level checks (`Currency == "INR"`, `Description` ≤ 500 chars) the "Submit Existing Draft
Endpoint" requirement calls for. Fixed: `SubmitAsync` now also re-checks `Currency`/`Description`
against the stored entity, returning `CurrencyInvalid`/`DescriptionTooLong` on failure. `Category`
needs no re-check — it's stored as the real `ExpenseCategory` enum, which structurally cannot
hold an invalid value.

**Company-local date basis (post-implementation correction):** a `/review` also found the BR-02
future-date check and the expense-number date prefix used `DateTime.UtcNow` directly, not
company-local time as BR-10 requires. Fixed via a new `ICompanyClock`/`CompanyClock`
(`Application/Expenses/`), configurable through `CompanyTimeZone:TimeZoneId` in
`appsettings.json` (default `Asia/Kolkata`) — see `docs/decisions/ADR-0009-company-timezone-
handling.md`. `ExpenseService` and `ExpenseNumberGenerator` both take `ICompanyClock` as a
constructor dependency; `CreatedAt`/`UpdatedAt`/`SubmittedAt` remain UTC instants (out of scope
for this fix — see the ADR).

Controller failure-reason → HTTP mapping (mirrors `AuthController.FailureResult`):
`AttachmentNotFound/AttachmentAlreadyLinked/AttachmentNotOwned/AmountNotPositive/
ExpenseDateInFuture/CurrencyInvalid/DescriptionTooLong/NotDraft` → `422
BUSINESS_RULE_VIOLATION`; `ExpenseNotFound` → `404 RESOURCE_NOT_FOUND`; `NotOwner` → `403
AUTHORIZATION_FAILED`. Field-level shape failures on *creation* (category/action `Enum.TryParse`,
currency, description length) go through `CreateExpenseRequestValidator` → the existing
`ValidationErrorResult` → `400 VALIDATION_ERROR` pattern, never reaching the service; on
*submission* of a stored Draft, the equivalent currency/description checks run inside
`SubmitAsync` itself (there is no request body to validate against) and map to `422`, not `400`.

## DB Changes

One new migration, **not backward compatible with existing `Attachment` rows**: adds a
required, non-nullable `UploadedByEmployeeId` with no default. Any local/dev database that
already has `Attachment` rows from ET006 testing must have that table cleared (`DELETE FROM
Attachments` or equivalent) before running `dotnet ef database update`, or the migration will
fail applying the `NOT NULL` constraint. Acceptable per ADR-0008: no production data exists yet,
and existing rows are ephemeral test uploads. This is a **manual one-time step for each
developer's local DB**, not an automated migration step — call it out explicitly when running
the migration (see Build/Test/Lint Checkpoints).

**Post-implementation correction:** `dotnet ef migrations add`'s scaffolder automatically
supplied `defaultValue: new Guid("00000000-...")` on the generated `AddColumn` call — its own
safety convenience for adding a non-nullable column to a possibly-non-empty table, not something
requested in this design. A `/review` caught that this silently backfills existing rows with
`Guid.Empty` instead of failing outright as documented above. Fixed by removing the
`defaultValue` argument from the migration file (the EF model itself never declared a default,
confirmed via `ApplicationDbContextModelSnapshot.cs`) and dropping the resulting default
constraint from the local dev DB that had already applied it.

## Reuse

Confirmed already in place, reused as-is: `IExpenseRepository`/`IAttachmentRepository`
(registered in `Program.cs`), `EmployeeOrManager` policy, `IUnitOfWork.
ExecuteInTransactionAsync`, the `ValidationErrorResult`/error-envelope pattern, FluentValidation
assembly-scan registration, and the `Result` + `FailureReason` + controller-`switch` idiom from
`AuthService`/`AuthController`. No new NuGet package, no new authorization policy type, no new
error-handling mechanism.

## Risks / Trade-offs

- **[Retry-on-conflict expense numbering could exhaust its retry budget under a burst of same-
  day concurrent creates]** → Mitigation: 5-attempt retry is expected to comfortably cover this
  system's real concurrency (internal tool); revisit (raise the limit, or move to a counter
  table) if integration/load testing shows otherwise. Logged as an explicit trade-off in
  ADR-0007, not a silent gap.
- **[`UploadedByEmployeeId` migration requires a manual dev-DB cleanup step]** → Mitigation:
  called out explicitly in this design and in the migration task; no production data exists yet,
  so there's no real data-loss risk, only a one-time local inconvenience.
- **[Re-validating a stored Draft at submit time (D5/D6) could reject a Draft that was valid
  when saved but whose linked attachment was since removed]** → Mitigation: this is the intended
  behavior per `/spec` (BR-03 must hold at submit time too); ET008 (edit) will need to keep this
  consistent when it lets Drafts be modified.

## Migration Plan

1. Add `UploadedByEmployeeId` to `Attachment.cs` and `AttachmentConfiguration.cs`.
2. Developer step (manual, once per local DB): clear existing `Attachment` rows in the local dev
   database.
3. `dotnet ef migrations add AddAttachmentUploader --project src/Infrastructure --startup-project src/Api`
4. `dotnet ef database update --project src/Infrastructure --startup-project src/Api`
5. Implement `EmployeeRoleResolutionMiddleware`/`ClaimsPrincipalExtensions` claim addition (D3).
6. Update `AttachmentService`/`UploadAttachmentRequest`/`AttachmentsController` to set/pass
   `UploadedByEmployeeId` (D2/D3).
7. Build the new `Application/Expenses/` and `ExpensesController` (D1, D4, D5, D6).
8. Register in `Program.cs`.

Rollback: revert the migration (`dotnet ef database update <previous-migration-name>`) and the
code changes; no data migration/backfill exists to unwind since the new column has no default
and no dependent data is created until this ticket's code runs.

## Build/Test/Lint Checkpoints

Run after each numbered step above, in this order, stopping at the first failure
(`backend/CLAUDE.md`, root `CLAUDE.md` Quality Gates):

1. `dotnet build` (from `backend/`) — after every file change, catches analyzer warnings early.
2. `dotnet ef database update --project src/Infrastructure --startup-project src/Api` — after
   the migration step, against local SQL Server.
3. `dotnet test --filter FullyQualifiedName~UnitTests` — unit tests for
   `ExpenseNumberGenerator` (format + retry-on-collision), `CreateExpenseRequestValidator`
   (field-level rules), and `ExpenseService` (BR-01/BR-02/attachment-ownership branches,
   Draft/Submit parity).
4. `dotnet test --filter FullyQualifiedName~IntegrationTests` — `WebApplicationFactory` tests for
   `POST /api/expenses` and `POST /api/expenses/{id}/submit` covering every scenario in
   `specs/expense-submission/spec.md`.
5. `dotnet test` (full suite) — confirms no regression in `AttachmentsController`/`AuthController`
   integration tests from the `UploadAttachmentRequest`/`Attachment` signature changes.

No frontend or E2E gate applies — this ticket is backend-only (no `frontend/` changes).

## Open Questions

None outstanding — the four ambiguities raised during `/spec` (Draft validation strictness,
submit-from-draft scope, expense-number date basis, attachment ownership) were resolved with the
ticket owner and are recorded in the proposal and ADR-0007/ADR-0008 above.
