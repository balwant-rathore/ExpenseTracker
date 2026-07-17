Backend-only ticket — no frontend work exists in this scope, so no `[PARALLEL]` frontend/backend
split applies. Frontend checkpoint commands (`pnpm --filter frontend ...`) are marked N/A per
phase.

## 1. Foundation — Domain model, EF configurations, migration

- [x] 1.1 Add `EmployeeRole`, `ExpenseCategory`, `ExpenseStatus` enums in
      `backend/src/Domain/Enums/` (verbatim members per `docs/SDS.md` §3.8; design.md D2)
- [x] 1.2 Add `Employee`, `User` (incl. `NormalizedEmail` per ADR-0002), `RefreshToken`,
      `PasswordResetOtp`, `Expense`, `Attachment` entities in `backend/src/Domain/Entities/`
      (design.md D2)
- [x] 1.3 Add `IRepository<TEntity>` and `IUnitOfWork` in `backend/src/Domain/Repositories/`
      (design.md D5/D6)
- [x] 1.4 Add `IEmployeeRepository`, `IUserRepository`, `IRefreshTokenRepository`,
      `IPasswordResetOtpRepository`, `IExpenseRepository`, `IAttachmentRepository` in
      `backend/src/Domain/Repositories/` (design.md D5)
- [x] 1.5 Add `IEntityTypeConfiguration<T>` classes for all six entities in
      `backend/src/Infrastructure/Persistence/Configurations/`, wiring every constraint in
      design.md D4's table (unique indexes, self-FK, `DeleteBehavior.Restrict`, column
      lengths/precision, the `Expense.AttachmentId` unique index from ADR-0001, and the
      `User.NormalizedEmail` unique index from ADR-0002)
- [x] 1.6 Update `ApplicationDbContext`: add the six `DbSet<T>` properties and override
      `OnModelCreating` to call `ApplyConfigurationsFromAssembly` (design.md D3)
- [x] 1.7 Generate migration:
      `dotnet ef migrations add AddDomainEntities --project src/Infrastructure --startup-project src/Api`
- [x] 1.8 Checkpoint: `dotnet build` (from `backend/`) → 0 errors. Frontend: N/A.

## 2. Core Implementation — Repositories, Unit of Work, CSV seed import

- [x] 2.1 Implement `Repository<TEntity>` base class in
      `backend/src/Infrastructure/Persistence/Repositories/` (`GetByIdAsync`/`AddAsync` against
      `ApplicationDbContext`, no `SaveChanges` call — design.md D5)
- [x] 2.2 Implement `EmployeeRepository`, `UserRepository`, `RefreshTokenRepository`,
      `PasswordResetOtpRepository`, `ExpenseRepository` (incl. `Query(): IQueryable<Expense>`),
      `AttachmentRepository`, each extending the base and adding its own lookup method per
      design.md D5
- [x] 2.3 Implement `UnitOfWork` wrapping `ApplicationDbContext.SaveChangesAsync` (design.md D6)
- [x] 2.4 Register all six repositories + `IUnitOfWork` as scoped services in `Api/Program.cs`
- [x] 2.5 Add `EmployeeCsvRow` record and `IEmployeeCsvParser`/`EmployeeCsvParser` (hand-rolled
      comma-split parser, no new NuGet package — design.md D9) in
      `backend/src/Infrastructure/Seeding/`
- [x] 2.6 Add `EmployeeSeedPlan` record and `EmployeeSeedPlanner` (pure class: dedupe by
      `EmployeeNumber`, two-pass manager resolution against a combined existing+new map,
      skip-and-warn on unresolved managers — design.md D8) in
      `backend/src/Infrastructure/Seeding/`
- [x] 2.7 Add `EmployeeCsvSeedRunner` implementing `ISeedRunner`: resolve CSV path from
      `SeedData:EmployeeCsvPath` config + `ContentRootPath`, query existing
      `EmployeeNumber → EmployeeId` pairs, call `EmployeeSeedPlanner.BuildPlan`, log each
      warning via `ILogger`, persist inside one `Database.BeginTransactionAsync` (design.md
      D8/D10)
- [x] 2.8 Add `SeedData:EmployeeCsvPath` key to `appsettings.Development.json` (design.md D10)
- [x] 2.9 Update `Api/Program.cs`: replace the `ISeedRunner` → `NoOpSeedRunner` registration with
      `ISeedRunner` → `EmployeeCsvSeedRunner`
- [x] 2.10 Delete `backend/src/Infrastructure/Seeding/NoOpSeedRunner.cs` and
       `backend/tests/UnitTests/NoOpSeedRunnerTests.cs` (design.md D9 — superseded scaffold, not
       kept alongside the real implementation)
- [x] 2.11 Checkpoint: `dotnet build` (from `backend/`) → 0 errors; `dotnet format
       --verify-no-changes` → 0 changes. Frontend: N/A.

## 3. Integration — Apply schema, log architecture decisions

- [x] 3.1 Manually apply the migration against a local SQL Server 2022 instance:
      `dotnet ef database update --project src/Infrastructure --startup-project src/Api`; confirm
      all six business tables + indexes/FKs exist and no unrelated table is affected
- [x] 3.2 Manually run the API in Development (`dotnet run --project src/Api`) and confirm the
      seed importer populates `Employee` from `docs/EmployeeSeedData.csv` without throwing, then
      re-run and confirm existing rows are left unmodified (idempotency)
- [x] 3.3 Write `docs/decisions/ADR-0001-expense-attachment-unique-index.md` documenting the
      `Expense.AttachmentId` unique-index deviation from the literal `docs/SDS.md` §3.6 index list
- [x] 3.4 Write `docs/decisions/ADR-0002-user-normalized-email.md` documenting the
      `User.NormalizedEmail` column added beyond `docs/SDS.md` §3.3's literal field list

## 4. Tests — One per spec delta scenario

- [x] 4.1 `EmployeeConfigurationTests`: "Employee schema enforces uniqueness" — unique
      `EmployeeNumber` and case-differing `Email` both configured unique via model metadata
      (design.md D7)
- [x] 4.2 `EmployeeConfigurationTests`: "Employee self-references its manager" — `ManagerId` FK
      targets `Employee.EmployeeId` in model metadata
- [x] 4.3 `UserConfigurationTests`: "One User per Employee" — unique index on `User.EmployeeId`
- [x] 4.4 `RefreshTokenConfigurationTests`: "RefreshToken never stores the raw token value" — only
      a `TokenHash` property/column exists, no plaintext token property
- [x] 4.5 `PasswordResetOtpConfigurationTests`: "PasswordResetOtp never stores the raw OTP value"
      — only an `OtpHash` property/column exists
- [x] 4.6 `ExpenseConfigurationTests`: "ExpenseNumber is unique and immutable" — unique index on
      `Expense.ExpenseNumber`
- [x] 4.7 `ExpenseConfigurationTests`: "Description enforces the 500-character limit" —
      `Description` max length is configured as 500
- [x] 4.8 `ExpenseConfigurationTests`: "An Attachment cannot back two Expenses" — unique index on
      `Expense.AttachmentId` (ADR-0001)
- [x] 4.9 `ExpenseCategoryTests`: "Only seven categories are defined" — enum has exactly the 7
      FRS §6.1 values
- [x] 4.10 `ExpenseStatusTests`: "Status enum matches the defined workflow states" — enum has
       exactly the 7 SDS §3.8 values
- [x] 4.11 `CascadeDeleteTests`: "Deleting a parent does not cascade" — every FK on `Employee`,
       `User`, `Expense` (self-ref, `User→Employee`, `RefreshToken/PasswordResetOtp→User`,
       `Expense→Employee`, `Expense→Attachment`) is configured `DeleteBehavior.Restrict` in model
       metadata
- [x] 4.12 `RepositoryDiRegistrationTests`: "Repositories are resolvable via DI" — build a
       `ServiceCollection` with all DI registrations from `Program.cs` and resolve each of the six
       repository interfaces + `IUnitOfWork` without error
- [x] 4.13 `EmployeeCsvParserTests`: parses `docs/EmployeeSeedData.csv`'s 6-column format into
       `EmployeeCsvRow` values correctly (supporting unit for the scenarios below)
- [x] 4.14 `EmployeeSeedPlannerTests`: "Seed runner imports new Employee rows and skips existing
       ones" — rows whose `EmployeeNumber` is already in the existing map are excluded from
       `EmployeesToInsert`; new rows are included
- [x] 4.15 `EmployeeSeedPlannerTests`: "Seed runner resolves manager references after insert" —
       a new row's `ManagerId` is resolved from the combined existing+newly-inserted map
       regardless of CSV row order
- [x] 4.16 `EmployeeSeedPlannerTests`: "Unresolvable manager reference is skipped with a warning"
       — a row referencing an unknown `Manager` EmployeeNumber keeps `ManagerId == null` and
       produces a warning message, without blocking the rest of the plan
- [x] 4.17 Confirm `DomainLayeringTests` (existing, ET001) still passes unmodified — `Domain`
       still has zero outgoing project references after adding entities/enums/repository
       interfaces
- [x] 4.18 Checkpoint: `dotnet test` (from `backend/`) → all green. Frontend: N/A.

## 5. Archive

- [x] 5.1 Run `openspec archive et002-domain-setup` (syncs `specs/domain-model/spec.md` as new
      and applies the `project-bootstrap` MODIFIED delta into `openspec/specs/`)
- [ ] 5.2 Update `docs/TICKETS.md`: set ET002's `Status` to `PR open (#N)` once the PR is opened
