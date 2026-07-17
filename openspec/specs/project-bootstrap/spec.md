# project-bootstrap Specification

## Purpose
TBD - created by archiving change et001-project-bootstrap. Update Purpose after archive.
## Requirements
### Requirement: Monorepo Workspace Structure
The repository SHALL be organized as a pnpm workspace, defined in a root `pnpm-workspace.yaml`,
containing a `frontend` package and a `backend` package.

#### Scenario: Workspace install resolves both packages
- **WHEN** a developer runs `pnpm install` from the repository root
- **THEN** dependencies for both the `frontend` and `backend` workspace packages install without
  error

### Requirement: Backend Solution Layering
The backend SHALL be an ASP.NET Core 9 Web API solution composed of `Api`, `Application`,
`Domain`, `Infrastructure`, and `Shared` projects, targeting `net9.0` with `Nullable` and
`ImplicitUsings` enabled, referencing each other only as `Api` → `Application` → `Domain`, with
`Infrastructure` depending on `Domain` and every project able to depend on `Shared`.

#### Scenario: Solution builds
- **WHEN** a developer runs `dotnet build` from `backend/`
- **THEN** all five projects compile successfully with zero errors

#### Scenario: Layering is not violated
- **WHEN** the `Domain` project is inspected for project references
- **THEN** it references no other project in the solution (it has no outgoing dependencies)

### Requirement: Frontend Application Skeleton
The frontend SHALL be a React 19 + TypeScript application scaffolded with Vite (`react-ts`
template), with Tailwind CSS, shadcn/ui, React Router, TanStack Query, Zustand, and React Hook
Form + Zod installed and minimally configured. The skeleton SHALL contain no feature UI.

#### Scenario: Dev server starts
- **WHEN** a developer runs `pnpm --filter frontend dev`
- **THEN** the Vite dev server starts and serves the empty shell application without build errors

#### Scenario: Production build succeeds
- **WHEN** a developer runs `pnpm --filter frontend build`
- **THEN** the build completes with zero TypeScript or bundler errors

### Requirement: EF Core Database Wiring
The backend SHALL wire an `ApplicationDbContext` to SQL Server via EF Core 9, configured through a
connection string that targets an existing SQL Server 2022 instance (no bundled
container/orchestration is provided). Schema changes are applied exclusively through EF Core
migrations, layered incrementally on top of the empty `InitialCreate` migration (ET001) as the
business domain grows.

#### Scenario: Migrations apply cleanly
- **WHEN** a developer runs `dotnet ef database update` against a configured local SQL Server
  instance
- **THEN** all migrations up to the latest apply without error, and the database reflects the
  currently modeled `ApplicationDbContext` schema — see the `domain-model` capability for the
  specific business entity tables and constraints it defines

### Requirement: Baseline Dev Tooling
The repository SHALL enforce formatting conventions via a root `.editorconfig` and SHALL run
Husky git hooks: a `pre-commit` hook that lint-stages changed files (`dotnet format` for staged
`.cs` files, `oxlint` for staged frontend files) and a `commit-msg` hook that validates the
commit message against the Conventional Commits format scoped to a ticket ID defined in
`CLAUDE.md`.

#### Scenario: Pre-commit hook blocks a failing lint/format check
- **WHEN** a developer commits a staged file that fails its configured linter or formatter
- **THEN** the `pre-commit` hook fails and the commit is rejected

#### Scenario: Commit-msg hook enforces Conventional Commits
- **WHEN** a developer commits with a message that does not match the Conventional Commits
  format (`type(ticket-id): summary`)
- **THEN** the `commit-msg` hook fails and the commit is rejected

#### Scenario: Commit-msg hook accepts a correctly-formatted message
- **WHEN** a developer commits with a message of the form `feat(ET00X): summary`
- **THEN** the `commit-msg` hook passes and the commit proceeds

### Requirement: Employee CSV Seed Import
The backend SHALL implement the `ISeedRunner` abstraction (defined in ET001) to perform the real
`Employee` CSV import from `docs/EmployeeSeedData.csv` (`docs/FRS.md` §14, `docs/SDS.md` §3.11):
insert `Employee` rows whose `EmployeeNumber` is not already present in the database (idempotent
skip of existing records), then resolve each inserted row's `ManagerId` by looking up its CSV
`Manager` value against imported `EmployeeNumber`s in a second pass. An unresolvable `Manager`
reference SHALL be left `null` on that row and logged as a warning rather than failing the run.
The import SHALL continue to execute automatically at Development startup.

#### Scenario: Seed runner imports new Employee rows and skips existing ones
- **WHEN** the application starts in the Development environment and `docs/EmployeeSeedData.csv`
  contains rows whose EmployeeNumber does not yet exist in the database
- **THEN** the seed runner inserts those rows, and any row whose EmployeeNumber already exists in
  the database is left unmodified

#### Scenario: Seed runner resolves manager references after insert
- **WHEN** all CSV rows have been inserted and a row's Manager column references another row's
  EmployeeNumber
- **THEN** that row's ManagerId SHALL be set to the referenced Employee's EmployeeId, regardless
  of the CSV's row order

#### Scenario: Unresolvable manager reference is skipped with a warning
- **WHEN** a row's Manager column references an EmployeeNumber that does not exist among the
  imported rows
- **THEN** that row's ManagerId SHALL remain null, a warning SHALL be logged, and the import
  SHALL continue processing remaining rows

