## ADDED Requirements

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
The backend SHALL wire an `ApplicationDbContext` containing no `DbSet` members to SQL Server via
EF Core 9, configured through a connection string that targets an existing SQL Server 2022
instance (no bundled container/orchestration is provided), and SHALL include an empty
`InitialCreate` migration whose sole purpose is to prove the EF Core → SQL Server pipeline works.

#### Scenario: Migration applies cleanly
- **WHEN** a developer runs `dotnet ef database update` against a configured local SQL Server
  instance
- **THEN** the `InitialCreate` migration applies without error and only the EF Core migrations
  history table exists afterward — no business entity tables are created

### Requirement: Seed Runner Scaffold
The backend SHALL define a seed-runner abstraction (e.g. `ISeedRunner`) with a skeleton
implementation that performs no data import in this ticket. The real `Employee` CSV import is
out of scope here and is implemented by a later ticket against this abstraction.

#### Scenario: Seed runner scaffold executes as a no-op
- **WHEN** the application starts in the Development environment
- **THEN** the seed-runner scaffold executes without throwing and performs no database writes

### Requirement: Baseline Dev Tooling
The repository SHALL enforce formatting conventions via a root `.editorconfig` and SHALL run
Husky git hooks: a `pre-commit` hook that lint-stages changed files (`dotnet format` for staged
`.cs` files, ESLint/Prettier for staged frontend files) and a `commit-msg` hook that validates the
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
