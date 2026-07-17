## Context

ET001 delivered a 5-project backend solution (`Api → Application, Infrastructure, Shared`;
`Application → Domain, Shared`; `Infrastructure → Domain, Shared`; `Domain`/`Shared` reference
nothing) with an empty `ApplicationDbContext` (zero `DbSet`s), an empty `InitialCreate` migration,
and a no-op `ISeedRunner`/`NoOpSeedRunner` pair wired into `Api/Program.cs`. `backend/tests/UnitTests`
already exists (xUnit, references all four non-Api projects) with two tests:
`DomainLayeringTests` (reflection-based, asserts `Domain` has zero outgoing project references)
and `NoOpSeedRunnerTests` (DI-resolution smoke test). This design covers ET002's concrete file
list, entity/enum/repository shapes, EF Core configuration approach, and the real CSV seed import,
per `openspec/changes/et002-domain-setup/proposal.md` and its two spec deltas
(`specs/domain-model/spec.md` new capability, `specs/project-bootstrap/spec.md` modified "Seed
Runner Scaffold" requirement).

Authoritative references: `docs/SDS.md` §3 (full schema), §3.10 (EF Core constraints), §3.11
(seed data); `docs/FRS.md` §4 (expense fields), §6 (categories), §11 (BR glossary), §14
(assumptions); `AGENTS.md` §5–§9; `backend/CLAUDE.md` (framework patterns/anti-patterns).

## Goals / Non-Goals

**Goals:**
- Model all six entities (`Employee`, `User`, `RefreshToken`, `PasswordResetOtp`, `Expense`,
  `Attachment`) and three enums exactly per `docs/SDS.md` §3.2–§3.8, so every later ticket
  (ET003–ET015) builds against real, migrated tables.
- Wire EF Core configurations enforcing every constraint `docs/SDS.md` §3 specifies (uniqueness,
  self-reference, non-cascading deletes) plus the one already-approved deviation
  (`Expense.AttachmentId` unique index, ADR-0001).
- Stand up per-aggregate repository interfaces (Domain-defined, per the existing
  Infrastructure-has-no-Application-dependency layering) with EF Core implementations, ready for
  `Application`-layer services in ET003+ to consume.
- Replace `NoOpSeedRunner` with a real, idempotent Employee CSV importer with two-pass manager
  resolution and skip-and-warn behavior, per the confirmed `/spec` decisions.

**Non-Goals:**
- No `Application`-layer services, DTOs, validators, or controllers — this ticket is
  Domain/Infrastructure only (ET003 onward consumes what's built here).
- No workflow transition methods on `Expense` (`Submit()`/`Approve()`/etc.) — entities stay
  anemic POCOs this ticket; transition behavior lands incrementally with the tickets that
  implement each transition (ET007–ET012), consistent with "don't build ahead of the fixed
  order" (`AGENTS.md` §11).
- No DB-level `CHECK` constraints for business rules that are the `Application` layer's job per
  the architecture (BR-01 amount > 0, BR-02 date not in future) — those are validated in
  `Application` services in later tickets, not encoded in the schema here.
- No DB-level default value for `Expense.Currency = "INR"` — FRS §4.1.1's "default to INR" is an
  API/Application-layer default applied at expense creation (ET007), not schema behavior.
- No Employee CRUD APIs (`AGENTS.md` §11) — the table is populated only by the seed importer.

## Decisions

### D1 — File layout
```
backend/src/Domain/
  Entities/
    Employee.cs  User.cs  RefreshToken.cs  PasswordResetOtp.cs  Expense.cs  Attachment.cs
  Enums/
    EmployeeRole.cs  ExpenseCategory.cs  ExpenseStatus.cs
  Repositories/
    IRepository.cs  IUnitOfWork.cs
    IEmployeeRepository.cs  IUserRepository.cs  IRefreshTokenRepository.cs
    IPasswordResetOtpRepository.cs  IExpenseRepository.cs  IAttachmentRepository.cs

backend/src/Infrastructure/
  Persistence/
    ApplicationDbContext.cs                 (MODIFIED — add DbSets + OnModelCreating)
    Configurations/
      EmployeeConfiguration.cs  UserConfiguration.cs  RefreshTokenConfiguration.cs
      PasswordResetOtpConfiguration.cs  ExpenseConfiguration.cs  AttachmentConfiguration.cs
    Repositories/
      Repository.cs  UnitOfWork.cs
      EmployeeRepository.cs  UserRepository.cs  RefreshTokenRepository.cs
      PasswordResetOtpRepository.cs  ExpenseRepository.cs  AttachmentRepository.cs
    Migrations/
      <timestamp>_AddDomainEntities.cs (+ .Designer.cs, snapshot update)
  Seeding/
    ISeedRunner.cs                          (unchanged)
    EmployeeCsvRow.cs  IEmployeeCsvParser.cs  EmployeeCsvParser.cs
    EmployeeSeedPlan.cs  EmployeeSeedPlanner.cs
    EmployeeCsvSeedRunner.cs                (NEW — replaces NoOpSeedRunner)
    NoOpSeedRunner.cs                        (DELETED — see D9)

backend/src/Api/
  Program.cs                                (MODIFIED — DI registrations, see D8/D9)
  appsettings.Development.json              (MODIFIED — add SeedData:EmployeeCsvPath, see D10)

backend/tests/UnitTests/
  NoOpSeedRunnerTests.cs                     (DELETED — see D9)
  Domain/Enums/ExpenseCategoryTests.cs
  Domain/Enums/ExpenseStatusTests.cs
  Infrastructure/Persistence/*ConfigurationTests.cs   (one per entity, see D7)
  Infrastructure/Repositories/RepositoryDiRegistrationTests.cs
  Infrastructure/Seeding/EmployeeCsvParserTests.cs
  Infrastructure/Seeding/EmployeeSeedPlannerTests.cs
  Infrastructure/Seeding/EmployeeCsvSeedRunnerTests.cs

docs/decisions/
  ADR-0001-expense-attachment-unique-index.md   (NEW)
  ADR-0002-user-normalized-email.md              (NEW)
```
One public class per file (`AGENTS.md` §6); `Domain` keeps zero outgoing project references
(`DomainLayeringTests` continues to pass unmodified).

### D2 — Entities are anemic POCOs; enums are plain `enum`
Per Non-Goals, entities carry only the fields from `docs/SDS.md` §3.2–§3.7 (plus `User.NormalizedEmail`,
confirmed with the user — see D4) with public getters/setters — no behavior methods yet. Example shape:

```csharp
// Domain/Entities/Employee.cs
public class Employee
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public EmployeeRole Role { get; set; }
    public Guid? ManagerId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Employee? Manager { get; set; }
    public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();
    public User? User { get; set; }
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
```
`Expense`, `Attachment`, `User`, `RefreshToken`, `PasswordResetOtp` follow the same shape, field
lists taken verbatim from `docs/SDS.md` §3.3–§3.7 (`Expense` additionally carries every audit
field from SDS §6.8's table). Enums (`EmployeeRole`, `ExpenseCategory`, `ExpenseStatus`) are
copied verbatim from `docs/SDS.md` §3.8 — exactly the members listed, no extras.

### D3 — `ApplicationDbContext` gains `DbSet`s + auto-discovered configurations
```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetOtp> PasswordResetOtps => Set<PasswordResetOtp>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
```
`ApplyConfigurationsFromAssembly` auto-discovers every `IEntityTypeConfiguration<T>` in
`Infrastructure` — avoids a manually-maintained registration list that's easy to forget an entry
in.

### D4 — Per-entity EF configuration (constraints from SDS §3, indexes)
Each `Configurations/*Configuration.cs` implements `IEntityTypeConfiguration<T>`. Key rules,
consistent across all six:

| Entity | Constraints configured |
|---|---|
| `Employee` | `HasKey(EmployeeId)`; `HasIndex(EmployeeNumber).IsUnique()`; `HasIndex(Email).IsUnique()`; self-FK `HasOne(Manager).WithMany(DirectReports).HasForeignKey(ManagerId).OnDelete(Restrict)` |
| `User` | `HasIndex(EmployeeId).IsUnique()` (1:1); `HasIndex(NormalizedEmail).IsUnique()` — `NormalizedEmail` is `Email.ToUpperInvariant()`, confirmed with user, logged as ADR-0002; FK to `Employee` `.OnDelete(Restrict)` |
| `RefreshToken` | `HasIndex(TokenHash).IsUnique()`; FK to `User` `.OnDelete(Restrict)` |
| `PasswordResetOtp` | `HasIndex(UserId)` (non-unique, per SDS §3.5); FK to `User` `.OnDelete(Restrict)` |
| `Expense` | `HasIndex(ExpenseNumber).IsUnique()`; `HasIndex(Status)`, `HasIndex(Category)`, `HasIndex(ExpenseDate)`, `HasIndex(CreatedAt)`, `HasIndex(SubmittedAt)` (SDS §3.6 index list); `HasIndex(AttachmentId).IsUnique()` (ADR-0001); FK to `Employee` `.OnDelete(Restrict)`; FK to `Attachment` `.OnDelete(Restrict)`; `Property(Description).HasMaxLength(500)` (FRS §4.1.1); `Property(Amount).HasColumnType("decimal(18,2)")`; `Property(Currency).HasMaxLength(3)` |
| `Attachment` | `HasKey(Id)`; no FK to `Expense` (the FK lives on `Expense.AttachmentId`, matching SDS §3.6/§3.7's asymmetric navigation) |

Column-length decisions not stated in `docs/SDS.md` (assumptions, documented here rather than
silently guessed): `EmployeeNumber`/`ExpenseNumber` `nvarchar(20)`, `FirstName`/`LastName`
`nvarchar(100)`, `Email` `nvarchar(256)`, `RejectionComment` `nvarchar(1000)`,
`Attachment.FileName`/`OriginalFileName` `nvarchar(255)`, `ContentType` `nvarchar(100)`,
`FileExtension` `nvarchar(10)`, `StoragePath` `nvarchar(500)`, `RefreshToken.TokenHash` /
`PasswordResetOtp.OtpHash` `nvarchar(128)` (SHA-256 hex digest is 64 chars; headroom for a future
hash algorithm change).

**Cascade delete**: every FK above uses `DeleteBehavior.Restrict`, applied uniformly (not just to
the entities SDS §3.10 calls "business entities") — simpler single policy, and prevents an
`Employee`/`User` delete from silently orphaning or destroying `RefreshToken`/`PasswordResetOtp`
rows too. Matches `docs/SDS.md` §3.10 and `AGENTS.md` §5.

### D5 — Repository interfaces live in `Domain`, not `Application`
ET001 locked `Infrastructure`'s project references to `Domain` + `Shared` only — it does **not**
reference `Application`. For `Infrastructure` to implement repository interfaces, those
interfaces must live somewhere `Infrastructure` already depends on: `Domain`. `Application`
(future `Application` services in ET003+) already depends on `Domain` too, so it can consume the
same interfaces without a new reference. This is the standard "ports in the innermost layer"
shape and requires no changes to ET001's established project references.

```csharp
// Domain/Repositories/IRepository.cs
public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken);
}

// Domain/Repositories/IEmployeeRepository.cs
public interface IEmployeeRepository : IRepository<Employee>
{
    Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken);
}
```
`IUserRepository` adds `GetByNormalizedEmailAsync`; `IRefreshTokenRepository` adds
`GetByTokenHashAsync`; `IPasswordResetOtpRepository` adds `GetActiveForUserAsync`;
`IExpenseRepository` adds `Query(): IQueryable<Expense>` (so ET009/ET012's filtering/sorting/
paging compose against it directly, per `backend/CLAUDE.md`'s "apply filtering/sorting/paging in
the query itself" rule, without a repository method per filter combination);
`IAttachmentRepository` stays with just the base `IRepository<Attachment>` members (no extra
lookups needed yet).

`AddAsync` only calls `DbSet<T>.AddAsync` (tracks, does not commit) — see D6.

### D6 — `IUnitOfWork` so multi-repository writes share one `SaveChanges`
Not mentioned in `docs/SDS.md` (which is silent on persistence-commit mechanics, so this isn't a
deviation requiring an ADR — just an implementation detail this ticket must settle since it
shapes the repository contract every later service depends on). `AGENTS.md` §5 requires
state-changing operations to run in one DB transaction; if each repository's `AddAsync` called
`SaveChangesAsync` itself, a service touching two repositories in one workflow step (e.g., ET007
creating an `Expense` + updating an `Attachment`) couldn't commit both atomically.

```csharp
// Domain/Repositories/IUnitOfWork.cs
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
```
`Infrastructure/Persistence/Repositories/UnitOfWork.cs` wraps `ApplicationDbContext.SaveChangesAsync`.
Registered in DI; `Application` services (ET003+) inject the repositories they need plus
`IUnitOfWork`, call repository methods to stage changes, then call `SaveChangesAsync` once.

### D7 — Constraint tests use `ApplicationDbContext.Model`, not a live database
Building an EF Core model (accessing `.Model`) runs `OnModelCreating` without opening a real
connection — `new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=unused;").Options`
is enough to construct a context and inspect `dbContext.Model.FindEntityType(typeof(Expense))`
for indexes, uniqueness, `DeleteBehavior`, and `MaxLength` facets. This lets
`Infrastructure/Persistence/*ConfigurationTests.cs` assert every constraint in D4's table
deterministically, in `tests/UnitTests`, **without adding any new NuGet package** (the `SqlServer`
provider is already referenced by `Infrastructure.csproj`) and without a live SQL Server instance.
Actually applying the migration against a real local SQL Server instance (per the
`domain-model` spec's "Migration Creates the Full Business Schema" scenario) remains a manual
verification step, exactly like ET001's empty-migration scenario — `dotnet test` cannot spin up
SQL Server 2022 itself.

### D8 — Employee CSV import: pure planner + thin DB-orchestrating runner
The interesting logic (dedupe by `EmployeeNumber`, two-pass manager resolution, skip-and-warn) is
isolated into a pure, EF-Core-free class so it's unit-testable with plain C# collections — no
test database, no new package:

```csharp
// Infrastructure/Seeding/EmployeeCsvRow.cs
public sealed record EmployeeCsvRow(
    string EmployeeNumber, string FirstName, string LastName,
    string Email, EmployeeRole Role, string? ManagerEmployeeNumber);

// Infrastructure/Seeding/EmployeeSeedPlan.cs
public sealed record EmployeeSeedPlan(
    IReadOnlyList<Employee> EmployeesToInsert,
    IReadOnlyList<string> UnresolvedManagerWarnings);

// Infrastructure/Seeding/EmployeeSeedPlanner.cs
public sealed class EmployeeSeedPlanner
{
    public EmployeeSeedPlan BuildPlan(
        IReadOnlyList<EmployeeCsvRow> csvRows,
        IReadOnlyDictionary<string, Guid> existingEmployeesByNumber);
}
```
`BuildPlan`: (1) skip any row whose `EmployeeNumber` is already a key in
`existingEmployeesByNumber`; (2) create `Employee` entities (new `Guid`, `ManagerId = null`) for
the rest; (3) build a combined `EmployeeNumber → Guid` map (existing + newly created); (4) for
each newly created row with a non-empty `ManagerEmployeeNumber`, resolve `ManagerId` from the
combined map — found ⇒ set it; not found ⇒ leave `null` and add a warning message to
`UnresolvedManagerWarnings`. Resolving against the *combined* map (not just this batch) correctly
handles both intra-CSV manager references and references to employees imported in a prior run.

`EmployeeCsvSeedRunner` (implements `ISeedRunner`, replaces `NoOpSeedRunner`) is the thin
orchestrator: read the CSV via `IEmployeeCsvParser`, query existing `EmployeeNumber → EmployeeId`
pairs from `ApplicationDbContext`, call `EmployeeSeedPlanner.BuildPlan`, log each warning via
`ILogger<EmployeeCsvSeedRunner>.LogWarning`, `AddRangeAsync` the plan's new employees, then
`SaveChangesAsync` — all inside one `Database.BeginTransactionAsync` so a mid-run failure doesn't
leave a half-imported batch.

### D9 — Hand-rolled CSV parsing; delete `NoOpSeedRunner`
`docs/EmployeeSeedData.csv` is a fixed 6-column, comma-delimited file with no embedded
commas/quotes in any sampled field (verified against the actual file). Adding `CsvHelper` (or any
NuGet package) requires explicit user confirmation per `CLAUDE.md`'s permission model — choosing a
~15-line hand-rolled `string.Split(',')`-based parser avoids that ask entirely for a format this
simple and stable. **Risk accepted**: if a future name/field ever contains an embedded comma, this
parser breaks; documented in Risks below, with `CsvHelper` as the fallback if that happens.

`NoOpSeedRunner.cs` and `NoOpSeedRunnerTests.cs` are deleted, not kept alongside the real
implementation — the `project-bootstrap` spec's "Seed Runner Scaffold" requirement is `MODIFIED`
(not additive) to describe the real import, so the no-op scenario it used to test no longer
describes any real behavior in the system (`AGENTS.md`/`CLAUDE.md` house style: don't keep
superseded scaffolding "just in case").

### D10 — CSV file path via configuration, resolved from `ContentRootPath`
`docs/EmployeeSeedData.csv` lives outside `backend/` (at the repo root), so a hardcoded relative
path from the `Api` project is fragile across launch methods. Add a non-secret configuration key
(not a connection string/secret, so `CLAUDE.md`'s "always ask first" for `appsettings.*.json`
does not apply) to `appsettings.Development.json`:
```json
{ "SeedData": { "EmployeeCsvPath": "../../../docs/EmployeeSeedData.csv" } }
```
Resolved in `EmployeeCsvSeedRunner` via
`Path.GetFullPath(Path.Combine(env.ContentRootPath, configuration["SeedData:EmployeeCsvPath"]!))`.
`IWebHostEnvironment.ContentRootPath` is the `Api` project directory when launched via
`dotnet run`/`dotnet watch`, matching the confirmed layout. A missing file throws
`FileNotFoundException` including the resolved absolute path, so a wrong relative traversal is
easy to diagnose (see Risks).

## Risks / Trade-offs

- **[Risk]** Hand-rolled CSV parser breaks on any field containing an embedded comma or quote. →
  **Mitigation**: current seed data has none (verified); if this changes, swap in `CsvHelper`
  (flag as a new NuGet dependency requiring confirmation at that time).
- **[Risk]** `SeedData:EmployeeCsvPath`'s relative traversal depends on `ContentRootPath` behaving
  as expected across launch methods (`dotnet run` vs. IIS Express vs. published binary). →
  **Mitigation**: Development-only code path; clear `FileNotFoundException` message with the
  resolved absolute path makes a wrong traversal immediately diagnosable; not used in any
  non-Development environment.
- **[Risk]** Uniform `DeleteBehavior.Restrict` on every FK (including `RefreshToken`/
  `PasswordResetOtp` → `User`) means a `User` can never be deleted while it has any token/OTP
  history, even though ET002 exposes no delete APIs for any of these anyway. →
  **Mitigation**: acceptable — no delete path exists yet; revisit if a later ticket needs
  cascading token cleanup (e.g., account deletion, which is out of scope per FRS/SDS anyway).
- **[Risk]** Column-length assumptions (`nvarchar(100)`/`nvarchar(500)`/etc.) aren't in
  `docs/SDS.md` — if the real requirement differs (e.g., longer names), a follow-up migration is
  needed. → **Mitigation**: additive migrations are cheap; documented here so the choice is
  visible and intentional, not accidental.
- **[Risk]** EF Core model-metadata tests (D7) prove configuration *intent* (index exists, is
  unique, delete behavior is `Restrict`) but not that SQL Server *actually* enforces it at
  runtime. → **Mitigation**: the one live-database scenario (`dotnet ef database update` against
  a local instance) remains a manual verification step, consistent with ET001's precedent.

## Migration Plan

Purely additive: one new migration creates six new tables and their indexes/FKs on top of ET001's
empty `InitialCreate` — no existing table is altered, no data migration is needed (the tables
don't exist yet in any environment). Sequence:
1. Add `Domain` entities + enums.
2. Add `Domain` repository/unit-of-work interfaces.
3. Add `Infrastructure` EF configurations; update `ApplicationDbContext`.
4. `dotnet ef migrations add AddDomainEntities --project src/Infrastructure --startup-project src/Api`.
5. Add `Infrastructure` repository implementations + DI registration in `Api/Program.cs`.
6. Add the CSV parser/planner/runner; delete `NoOpSeedRunner` + its test; update `Program.cs`'s
   `ISeedRunner` registration.
7. Add the `EmployeeSeedData:EmployeeCsvPath` config key to `appsettings.Development.json`.
8. Write `docs/decisions/ADR-0001-expense-attachment-unique-index.md` and
   `docs/decisions/ADR-0002-user-normalized-email.md`.

Each step is independently buildable and revertable via `git revert`.

**Checkpoint commands** (per `CLAUDE.md` Quality Gates order, backend-only — no frontend changes):
- `dotnet build` (from `backend/`) — after steps 1–3, and again after steps 5–7.
- `dotnet ef migrations add AddDomainEntities ...` then, manually against a local SQL Server
  instance, `dotnet ef database update --project src/Infrastructure --startup-project src/Api`.
- `dotnet test` (from `backend/`) — after every step from 4 onward; must cover the new
  `*ConfigurationTests`, `EmployeeCsvParserTests`, `EmployeeSeedPlannerTests`,
  `EmployeeCsvSeedRunnerTests`, `RepositoryDiRegistrationTests`, `ExpenseCategoryTests`,
  `ExpenseStatusTests`, and the existing `DomainLayeringTests` (still zero Domain references).
- Integration/E2E: not applicable — no controller/API surface in this ticket.

## Open Questions

None outstanding. The one open question this design surfaced — how to enforce `User.Email`'s
"unique (case-insensitive)" requirement — was confirmed with the user:

**Confirmed: `User.NormalizedEmail` column.** `User` gets a `NormalizedEmail` field
(`Email.ToUpperInvariant()`, populated by whichever ET003/ET004 service creates/updates a `User`)
with the unique index on `NormalizedEmail` instead of `Email` — collation-independent, correct
regardless of the target SQL Server instance's collation. This is a field beyond `docs/SDS.md`
§3.3's literal list, so it is logged as `ADR-0002` (see D4/Migration Plan step 8).

`Employee.Email` stays a plain `HasIndex(Email).IsUnique()` — `docs/SDS.md` §3.2 and `AGENTS.md`
§9 list it as plain "Unique" (not case-insensitive), unlike `User.Email`, so it deliberately does
**not** get the same `NormalizedEmail` treatment. This asymmetry is a deliberate reading of the
spec text, not an oversight.
