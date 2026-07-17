## 1. Foundation — Workspace & Backend EF Core Wiring

- [x] 1.1 Create root `pnpm-workspace.yaml` declaring the `frontend` package (backend is a .NET
      solution, not a pnpm package member, and is not listed here)
- [x] 1.2 Create root `package.json` (private, root scripts, devDependencies: `husky`,
      `lint-staged`, `@commitlint/cli`, `@commitlint/config-conventional`) — adding these
      dependencies requires explicit confirmation per CLAUDE.md's "Always ask first" rule
- [x] 1.3 Add root `.editorconfig` (UTF-8, LF, final newline; 4-space indent for `*.cs`,
      2-space indent for `*.{ts,tsx,js,jsx,json}`, per `AGENTS.md` §6 / design.md D6)
- [x] 1.4 Backend: create `ExpenseTracker.sln` and the five layered projects (`Api`,
      `Application`, `Domain`, `Infrastructure`, `Shared`) under `backend/src/`, `net9.0`,
      `Nullable`/`ImplicitUsings` enabled, with project references exactly as design.md D1
      (`Api`→`Application`,`Infrastructure`,`Shared`; `Application`→`Domain`,`Shared`;
      `Infrastructure`→`Domain`,`Shared`; `Domain`/`Shared` reference nothing)
- [x] 1.5 Backend: add EF Core 9 packages to `Infrastructure`
      (`Microsoft.EntityFrameworkCore.SqlServer`, `.Design`, `.Tools`) — confirm the package
      additions first per CLAUDE.md
- [x] 1.6 Backend: create `Infrastructure/Persistence/ApplicationDbContext.cs` (zero `DbSet`
      properties) and register it in `Api/Program.cs` via `AddDbContext<ApplicationDbContext>`
- [x] 1.7 Backend: run `dotnet user-secrets init --project src/Api`; document the one-time
      `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."` command each
      developer must run locally in `backend/CLAUDE.md` (design.md D2 — no connection string is
      ever written to a tracked `appsettings.*.json`)
- [x] 1.8 Backend: generate the empty migration —
      `dotnet ef migrations add InitialCreate --project src/Infrastructure --startup-project
      src/Api`

**Checkpoint 1** (backend only — no frontend exists yet):
- [x] 1.9 `dotnet build` from `backend/` → 0 errors
- [x] 1.10 `dotnet format --verify-no-changes` from `backend/` → no changes required

## 2. Core Implementation

Backend lane and frontend lane have no dependency on each other in this ticket — run in separate
worktrees per `CLAUDE.md`'s Parallel Work rule.

- [x] 2.1 [PARALLEL] Backend: create `backend/tests/UnitTests/UnitTests.csproj` (xUnit, `net9.0`,
      references `Domain`, `Application`, `Shared`), add to `ExpenseTracker.sln` (design.md D8)
- [x] 2.2 [PARALLEL] Backend: implement `Infrastructure/Seeding/ISeedRunner.cs` +
      `NoOpSeedRunner.cs`, register in DI (`AddScoped<ISeedRunner, NoOpSeedRunner>()`), invoke
      once from `Program.cs` at startup in the Development environment (design.md D4)
- [x] 2.3 [PARALLEL] Backend: implement `GET /api/health` (in `Api/Controllers/HealthController.cs`
      or a minimal API route) calling `dbContext.Database.CanConnectAsync()`, returning `200 OK`
      / `503` (design.md D3)
- [x] 2.4 [PARALLEL] Frontend: scaffold the Vite `react-ts` app under `frontend/`
      (`pnpm create vite frontend -- --template react-ts`)
- [x] 2.5 [PARALLEL] Frontend: install and configure Tailwind CSS
- [x] 2.6 [PARALLEL] Frontend: install and run `shadcn@latest init` (produces `components.json`)
- [x] 2.7 [PARALLEL] Frontend: install `react-router-dom`, `@tanstack/react-query`, `zustand`,
      `react-hook-form`, `zod`, `@hookform/resolvers` — confirm dependency additions per
      CLAUDE.md
- [x] 2.8 [PARALLEL] Frontend: install and configure Vitest + React Testing Library + `jsdom`;
      set `test.passWithNoTests: true` in `vite.config.ts` so `pnpm --filter frontend test`
      succeeds with zero test files (design.md D5)
- [x] 2.9 [PARALLEL] Frontend: create the standard empty folder skeleton with `.gitkeep`
      (`src/{api,components,features,hooks,layouts,pages,routes,store,types,utils}`) matching
      `AGENTS.md` §2

**Checkpoint 2**:
- [x] 2.10 `dotnet build` from `backend/` → 0 errors; `dotnet test` from `backend/` → 0 failures
      (no test cases yet, trivially green)
- [x] 2.11 `pnpm --filter frontend lint --max-warnings=0` → 0 warnings
- [x] 2.12 `pnpm --filter frontend build` → 0 errors
- [x] 2.13 `pnpm --filter frontend test` → all green (0 tests, `passWithNoTests`)

## 3. Integration

- [x] 3.1 Root: wire `.husky/pre-commit` running `pnpm exec lint-staged`; configure `lint-staged`
      (`backend/**/*.cs` → `dotnet format backend/ExpenseTracker.sln --include`;
      `frontend/**/*.{ts,tsx}` → `pnpm --filter frontend exec oxlint`) (design.md D7 — revised
      from ESLint to oxlint, and the `dotnet format` command needed an explicit solution path;
      both confirmed/fixed during implementation)
- [x] 3.2 Root: wire `.husky/commit-msg` running `pnpm exec commitlint --edit "$1"`; author
      `commitlint.config.js` extending `@commitlint/config-conventional` with `type-enum`
      restricted to `[feat, fix, test, refactor, chore, docs]` and an optional scope rule
      requiring `^ET\d{3,4}$` when a scope is present (design.md D7)
- [x] 3.3 Backend: run `dotnet ef database update --project src/Infrastructure
      --startup-project src/Api` against the developer's local SQL Server instance (connection
      string from user secrets) to prove the `InitialCreate` migration applies
- [x] 3.4 Backend: run the API locally (`dotnet run --project src/Api`) and confirm
      `GET /api/health` returns `200 OK` against the same local database
- [x] 3.5 Docs: revise the ET001 scope text in `docs/TICKETS.md` — drop "9 slash commands, 3
      Claude subagents" (7 commands / 2 subagents already exist) and "CI pipeline" / "GitHub
      Actions" (explicitly deferred) wording, per the proposal's "What Changes" and the
      decisions confirmed during `/spec`

**Checkpoint 3**:
- [x] 3.6 `dotnet build` → 0 errors; `dotnet format --verify-no-changes` → no changes required
- [x] 3.7 `pnpm --filter frontend lint --max-warnings=0` → 0 warnings;
      `pnpm --filter frontend build` → 0 errors

## 4. Tests (one per spec scenario)

- [x] 4.1 Scenario "Workspace install resolves both packages": run `pnpm install` from the repo
      root; verify it completes with zero errors and both workspace packages resolve
- [x] 4.2 Scenario "Solution builds": run `dotnet build` from `backend/`; verify all five
      projects compile with zero errors
- [x] 4.3 Scenario "Layering is not violated": add an xUnit test in `UnitTests` asserting the
      `Domain` assembly has zero referenced assemblies belonging to this solution
- [x] 4.4 Scenario "Dev server starts": run `pnpm --filter frontend dev`; verify the Vite dev
      server starts and serves the shell app with no build errors
- [x] 4.5 Scenario "Production build succeeds": run `pnpm --filter frontend build`; verify zero
      TypeScript/bundler errors
- [x] 4.6 Scenario "Migration applies cleanly": after `dotnet ef database update`, query the
      configured database and verify only `__EFMigrationsHistory` exists — no business entity
      tables were created
- [x] 4.7 Scenario "Seed runner scaffold executes as a no-op": add an xUnit test resolving
      `NoOpSeedRunner` from DI, calling `SeedAsync`, asserting no exception is thrown and no
      database writes occur
- [x] 4.8 Scenario "Pre-commit hook blocks a failing lint/format check": stage a deliberately
      malformed file and attempt a commit; verify the `pre-commit` hook rejects it
- [x] 4.9 Scenario "Commit-msg hook enforces Conventional Commits": attempt a commit with a
      message that doesn't match `type(ticket-id): summary`; verify the `commit-msg` hook
      rejects it
- [x] 4.10 Scenario "Commit-msg hook accepts a correctly-formatted message": commit with a
      message of the form `feat(ET001): ...`; verify the `commit-msg` hook accepts it

**Checkpoint 4**:
- [x] 4.11 `dotnet test` from `backend/` → all green (includes the two new xUnit tests from 4.3
      and 4.7)
- [x] 4.12 `pnpm --filter frontend test` → all green

## 5. Archive

- [x] 5.1 Run the full Quality Gates sequence one final time: `pnpm --filter frontend lint
      --max-warnings=0` → `pnpm --filter frontend build` → `pnpm --filter frontend test`;
      `dotnet format --verify-no-changes` → `dotnet build` → `dotnet test` — all green
- [x] 5.2 Run `openspec archive et001-project-bootstrap`
- [x] 5.3 Note in `docs/TICKETS.md`: per the repo's status convention, archiving does not mean
      "Done" — `Status` moves to `PR open (#N)` when `/pr` raises the pull request, and only to
      `Done` once that PR is merged; do not set `Done` as part of this task list
