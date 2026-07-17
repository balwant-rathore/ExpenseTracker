## Why

ET001 delivered an `ApplicationDbContext` with zero `DbSet` members and a no-op `ISeedRunner`
scaffold — there is no real schema yet. Per `docs/TICKETS.md`'s fixed build order, ET003
(authentication infrastructure) and every expense-workflow ticket from ET006 onward (attachment
upload, expense creation/edit/query, manager/compliance/finance review, dashboard, reporting) all
require the actual `Employee`, `User`, `RefreshToken`, `PasswordResetOtp`, `Expense`, and
`Attachment` tables, EF Core configurations, and repository abstractions to exist first. ET002
(this change) builds that shared domain/database foundation, per `docs/FRS.md` §2, 4, 5, 6, 11, 14
and `docs/SDS.md` §3, so downstream tickets build against real, migrated tables instead of ad hoc
entity definitions.

## What Changes

- Add `Domain` entities `Employee`, `User`, `RefreshToken`, `PasswordResetOtp`, `Expense`,
  `Attachment` matching `docs/SDS.md` §3.2–§3.7 field-for-field, including the
  `Employee.ManagerId` self-reference and the `Expense` audit-trail fields
  (`SubmittedAt/ApprovedAt/ComplianceApprovedAt/RejectedAt/ReimbursedAt` +
  `...ByEmployeeId`/`RejectionComment`), all system-managed and non-client-editable
  (FRS §9, §11 BR-09/BR-10; SDS §6.8).
- Add enums `EmployeeRole`, `ExpenseCategory` (exactly the 7 fixed values — FRS §6.1), and
  `ExpenseStatus` (7 states) per `docs/SDS.md` §3.8.
- Add EF Core `IEntityTypeConfiguration<T>` classes in `Infrastructure` for every entity, wiring
  all indexes/constraints from `docs/SDS.md` §3: unique `EmployeeNumber`/`Email`
  (case-insensitive)/`ExpenseNumber`/`TokenHash`, the `ManagerId` self-referencing FK, and cascade
  delete disabled on all business entities (SDS §3.10, AGENTS.md §5).
- **Open architecture decision (deviation from SDS §3.6's literal index list, requires ADR):** add
  a unique index on `Expense.AttachmentId` to enforce the strict 1:1 Expense↔Attachment
  cardinality shown in SDS §3.1's relationship diagram, which the §3.6 index list omits.
- Extend `ApplicationDbContext` (currently empty per ET001) with `DbSet<T>` for all six entities
  and apply the new configurations.
- Add a new EF Core migration, generated via `dotnet ef migrations add`, layered on top of ET001's
  empty `InitialCreate`, creating the full business schema.
- Add per-aggregate repository interfaces and EF Core-backed implementations —
  `IEmployeeRepository`, `IUserRepository`, `IRefreshTokenRepository`,
  `IPasswordResetOtpRepository`, `IExpenseRepository`, `IAttachmentRepository` — registered in DI
  as interfaces per `backend/CLAUDE.md`.
- Replace ET001's no-op `ISeedRunner` scaffold with the real `Employee` CSV import (FRS §14,
  SDS §3.11): parse `docs/EmployeeSeedData.csv`, insert employees whose `EmployeeNumber` is not
  already present (idempotent skip), then resolve each row's `ManagerId` by looking up its CSV
  `Manager` value against imported `EmployeeNumber`s in a second pass; an unresolvable `Manager`
  reference is skipped (left `null`) with a logged warning rather than failing the run. Continues
  to execute automatically at Development startup, no manual trigger.
- No Employee CRUD APIs are added (FRS §11 "Do NOT Do"; SDS §3.11) — the table remains
  application-internal, read-only after seeding.

## Capabilities

### New Capabilities
- `domain-model`: The full EF Core domain — entities, enums, entity configurations, DbContext
  wiring, migrations, and per-aggregate repositories for Employee, User, RefreshToken,
  PasswordResetOtp, Expense, and Attachment.

### Modified Capabilities
- `project-bootstrap`: The "Seed Runner Scaffold" requirement changes from "executes as a no-op"
  (ET001) to performing the real, idempotent Employee CSV import with manager-reference
  skip-and-warn behavior. Expressed in the spec delta as REMOVED ("Seed Runner Scaffold", the old
  no-op requirement) + ADDED ("Employee CSV Seed Import", the real import behavior) rather than
  MODIFIED, because the OpenSpec CLI's MODIFIED-merge validation rejects dropping a named
  scenario ("Seed runner scaffold executes as a no-op") even when its removal is intentional, and
  separately rejects reusing the same requirement name across ADDED and REMOVED in one delta —
  see `specs/project-bootstrap/spec.md`. Net effect in `openspec/specs/project-bootstrap/spec.md`
  is the same as a MODIFIED requirement, just under a new requirement name.

## Impact

- **Domain** (`backend/src/Domain/`): 6 new entity classes, 3 new enums.
- **Infrastructure** (`backend/src/Infrastructure/`): new EF configurations, updated
  `ApplicationDbContext`, new migration, 6 new repository implementations, replacement of the
  ET001 `ISeedRunner` no-op with the real CSV import.
- **Api**: no controller or API surface changes — this ticket is Domain/Infrastructure-only,
  consumed by ET003 onward.
- **docs/decisions/**: one new ADR documenting the `Expense.AttachmentId` unique-index decision.
- **Dependencies**: CSV parsing may need a new NuGet package (e.g. CsvHelper) or may be
  hand-rolled against the existing 6-column format — if a package is added, that requires explicit
  user confirmation per `CLAUDE.md`'s permission model before running `dotnet add package`.
