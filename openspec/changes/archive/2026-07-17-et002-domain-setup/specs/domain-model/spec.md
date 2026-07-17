## ADDED Requirements

### Requirement: Employee Entity Schema
The `Domain` layer SHALL define an `Employee` entity with `EmployeeId` (Guid, primary key), a
unique `EmployeeNumber`, `FirstName`, `LastName`, a unique `Email`, `Role` (`EmployeeRole`), a
nullable self-referencing `ManagerId` (FK to `Employee.EmployeeId`), `IsActive`, and audit
`CreatedAt`/`UpdatedAt` fields, matching `docs/SDS.md` §3.2. The entity SHALL have no
create/update/delete API surface (`docs/FRS.md` §11, §14) — Employee data is populated only via
seed import.

#### Scenario: Employee schema enforces uniqueness
- **WHEN** two Employee rows are inserted with the same EmployeeNumber, or the same Email
  differing only by case
- **THEN** the second insert SHALL fail a unique constraint violation

#### Scenario: Employee self-references its manager
- **WHEN** an Employee row's ManagerId is inspected
- **THEN** it SHALL reference another row's EmployeeId in the same Employee table, or be null for
  an employee with no manager

### Requirement: User Entity Schema
The `Domain` layer SHALL define a `User` entity one-to-one with `Employee` (unique FK
`EmployeeId`), holding a case-insensitive-unique `Email` and a `PasswordHash`, plus
`CreatedAt`/`UpdatedAt`, matching `docs/SDS.md` §3.3. No password value SHALL ever be persisted
or logged in plaintext.

#### Scenario: One User per Employee
- **WHEN** a second User row is inserted referencing an EmployeeId that already has a User row
- **THEN** the insert SHALL fail a unique constraint violation

### Requirement: RefreshToken Entity Schema
The `Domain` layer SHALL define a `RefreshToken` entity many-to-one with `User`, storing only a
unique `TokenHash` (never the raw token), `ExpiresAt`, nullable `RevokedAt`, and `CreatedAt`,
matching `docs/SDS.md` §3.4.

#### Scenario: RefreshToken never stores the raw token value
- **WHEN** the RefreshToken entity's columns are inspected
- **THEN** only a `TokenHash` column SHALL exist for the token value — no plaintext token column
  SHALL be present

### Requirement: PasswordResetOtp Entity Schema
The `Domain` layer SHALL define a `PasswordResetOtp` entity many-to-one with `User`, storing only
an `OtpHash` (never the raw OTP), `ExpiresAt`, nullable `UsedAt`, and `CreatedAt`, matching
`docs/SDS.md` §3.5.

#### Scenario: PasswordResetOtp never stores the raw OTP value
- **WHEN** the PasswordResetOtp entity's columns are inspected
- **THEN** only an `OtpHash` column SHALL exist for the OTP value — no plaintext OTP column SHALL
  be present

### Requirement: Expense Entity Schema
The `Domain` layer SHALL define an `Expense` entity per `docs/SDS.md` §3.6 with a unique,
immutable `ExpenseNumber` (format `EXP-yyyyMMdd-XXXX` per BR-09), `EmployeeId`, `AttachmentId`,
`ExpenseDate`, `Category` (`ExpenseCategory`), `Amount` (decimal), `Currency`, `Description`
(maximum 500 characters per `docs/FRS.md` §4.1.1), `Status` (`ExpenseStatus`), the workflow audit
fields (`SubmittedAt`/`ApprovedAt`/`ComplianceApprovedAt`/`RejectedAt`/`ReimbursedAt`,
`ApprovedByEmployeeId`/`ComplianceApprovedByEmployeeId`/`RejectedByEmployeeId`/
`ReimbursedByEmployeeId`, `RejectionComment`), and `CreatedAt`/`UpdatedAt`. All audit fields SHALL
be system-managed only — no public API exposes them as client-writable (`docs/SDS.md` §6.8).

#### Scenario: ExpenseNumber is unique and immutable
- **WHEN** two Expense rows are inserted with the same ExpenseNumber
- **THEN** the second insert SHALL fail a unique constraint violation

#### Scenario: Description enforces the 500-character limit
- **WHEN** an Expense row is inserted with a Description longer than 500 characters
- **THEN** the insert SHALL fail a column length constraint

### Requirement: Attachment Entity Schema and Expense 1:1 Enforcement
The `Domain` layer SHALL define an `Attachment` entity per `docs/SDS.md` §3.7 (`FileName`,
`OriginalFileName`, `ContentType`, `FileExtension`, `FileSize`, `StoragePath`, `UploadedAt`), and
`Expense.AttachmentId` SHALL carry a unique constraint so at most one Expense can reference any
given Attachment — an open decision beyond the literal `docs/SDS.md` §3.6 index list, logged as
an ADR, needed to enforce the strict one-to-one cardinality shown in `docs/SDS.md` §3.1's
relationship diagram.

#### Scenario: An Attachment cannot back two Expenses
- **WHEN** a second Expense row is inserted referencing an AttachmentId already referenced by an
  existing Expense
- **THEN** the insert SHALL fail a unique constraint violation

### Requirement: Expense Category Enum
The `Domain` layer SHALL define `ExpenseCategory` with exactly seven values: `Travel`, `Hotel`,
`Meals`, `OfficeSupplies`, `ClientEntertainment`, `Training`, `Other` (`docs/FRS.md` §6.1). No
additional values SHALL exist.

#### Scenario: Only seven categories are defined
- **WHEN** the `ExpenseCategory` enum is inspected
- **THEN** it SHALL contain exactly the seven listed values and no others

### Requirement: Expense Status Enum
The `Domain` layer SHALL define `ExpenseStatus` with the seven states `Draft`, `Submitted`,
`Approved`, `ComplianceApproved`, `Cancelled`, `Reimbursed`, `Rejected` (`docs/SDS.md` §3.8).

#### Scenario: Status enum matches the defined workflow states
- **WHEN** the `ExpenseStatus` enum is inspected
- **THEN** it SHALL contain exactly the seven listed values and no others

### Requirement: Cascade Delete Disabled on Business Entities
Deleting an `Employee`, `User`, or `Expense` row SHALL NOT cascade-delete related child rows
(`Expense`, `Attachment`, `RefreshToken`, `PasswordResetOtp`); every such relationship SHALL be
configured with a non-cascading delete behavior (`docs/SDS.md` §3.10, `AGENTS.md` §5).

#### Scenario: Deleting a parent does not cascade
- **WHEN** a delete is attempted on an Employee, User, or Expense row that has related child rows
- **THEN** EF Core's configured delete behavior SHALL be `Restrict`/`NoAction`, not `Cascade`

### Requirement: Migration Creates the Full Business Schema
Running the new EF Core migration on top of ET001's empty `InitialCreate` SHALL create tables for
all six entities (`Employee`, `User`, `RefreshToken`, `PasswordResetOtp`, `Expense`,
`Attachment`) with the indexes and constraints described in this capability's other requirements.

#### Scenario: Migration applies cleanly against a local SQL Server instance
- **WHEN** a developer runs `dotnet ef database update` against a configured local SQL Server
  instance
- **THEN** the new migration applies without error and all six business entity tables exist
  afterward, with their configured indexes and constraints in place

### Requirement: Per-Aggregate Repository Abstractions
The `Infrastructure` layer SHALL provide one repository interface and EF Core-backed
implementation per aggregate — `IEmployeeRepository`, `IUserRepository`,
`IRefreshTokenRepository`, `IPasswordResetOtpRepository`, `IExpenseRepository`,
`IAttachmentRepository` — registered in DI as interfaces, to be consumed only by
`Application`-layer services in later tickets (`backend/CLAUDE.md`).

#### Scenario: Repositories are resolvable via DI
- **WHEN** the application's DI container is built
- **THEN** each of the six repository interfaces resolves to its EF Core-backed implementation
  without error
