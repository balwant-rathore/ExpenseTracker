## Context

The repository currently contains only `docs/`, `AGENTS.md`, `CLAUDE.md`, `openspec/`, and stub
`backend/CLAUDE.md` / `frontend/CLAUDE.md` files — no solution, no app, no packages. This design
covers the concrete files/commands needed to satisfy the `project-bootstrap` capability
(`openspec/changes/et001-project-bootstrap/specs/project-bootstrap/spec.md`) per the decisions
already confirmed with the user during `/spec`:

- No `Employee` entity or CSV import yet (ET002).
- No docker-compose for SQL Server — connection string targets an existing instance.
- No CI/GitHub Actions in this ticket.
- No new slash commands/subagents (7 commands / 2 subagents already exist).
- Empty `InitialCreate` migration only.
- Husky: `pre-commit` (lint-staged) + `commit-msg` (commitlint).
- Frontend: Vite scaffold + Tailwind/shadcn/Router/TanStack Query/Zustand/RHF+Zod installed, no
  feature UI.
- Connection string via .NET user secrets (not committed `appsettings.*.json`); a `GET
  /api/health` endpoint and an empty `backend/tests/UnitTests` xUnit project are included.

Authoritative references: `docs/SDS.md` §1 (Architecture Overview), §2 (Tech Stack), §2.1
(Solution Structure), §2.2 (Development Standards), §3.10/§3.11 (EF Core constraints / seed data —
for the parts explicitly deferred to ET002).

## Goals / Non-Goals

**Goals:**
- Stand up the pnpm workspace, backend solution (5 layered projects), and frontend Vite app
  exactly matching the folder names in `AGENTS.md` §2 / `docs/SDS.md` §2.1, so ET002+ tickets add
  files into an already-correct structure with zero rework.
- Prove the EF Core → SQL Server pipeline works via an empty migration, without introducing any
  business entity.
- Wire baseline dev tooling (EditorConfig, Husky) so every subsequent commit is
  format-/message-checked from day one.

**Non-Goals:**
- No `Employee`/`User`/`Expense`/`Attachment` entities, no DbSets, no seed data import (ET002).
- No auth, no controllers beyond the `GET /api/health` check (see Decision D3).
- No CI/GitHub Actions, no docker-compose, no additional slash commands/subagents.
- No feature UI, no API client code, no Zustand stores with real state (ET016+).

## Decisions

### D1 — Backend solution layout
```
backend/
  ExpenseTracker.sln
  src/
    Api/                Api.csproj            (Microsoft.NET.Sdk.Web, net9.0)
      Program.cs
      appsettings.json
      appsettings.Development.json
    Application/        Application.csproj    (Microsoft.NET.Sdk, net9.0)
    Domain/              Domain.csproj         (Microsoft.NET.Sdk, net9.0 — zero project refs)
    Infrastructure/      Infrastructure.csproj (Microsoft.NET.Sdk, net9.0)
      Persistence/ApplicationDbContext.cs
      Persistence/Migrations/*_InitialCreate.cs
      Seeding/ISeedRunner.cs
      Seeding/NoOpSeedRunner.cs
    Shared/              Shared.csproj         (Microsoft.NET.Sdk, net9.0 — zero project refs)
  tests/
    UnitTests/           UnitTests.csproj      (xUnit, net9.0, refs Domain/Application/Shared)
  CLAUDE.md              (already exists)
```
Project references: `Api` → `Application`, `Infrastructure`, `Shared`; `Application` → `Domain`,
`Shared`; `Infrastructure` → `Domain`, `Shared`; `Domain` and `Shared` reference nothing (matches
the `Api → Application → Domain ← Infrastructure` layering in `docs/SDS.md` §1.3/§2.1 and the
"zero outgoing dependencies from Domain" scenario in the spec delta).
All projects: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>` per
`AGENTS.md` §6.

**Alternative considered**: single `Api` project with folders instead of 5 projects. Rejected —
contradicts the explicit layered-project structure mandated in `docs/SDS.md` §2.1.

### D2 — `ApplicationDbContext` with zero `DbSet`s + empty migration, connection string via user secrets
`Infrastructure/Persistence/ApplicationDbContext.cs` extends `DbContext`, constructor takes
`DbContextOptions<ApplicationDbContext>`, no `DbSet` properties. Registered in `Api/Program.cs`
via `AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connectionString))`.

**Confirmed with user**: the connection string is stored per-developer via .NET user secrets, not
in any committed `appsettings.*.json` file. Setup: `dotnet user-secrets init --project src/Api`
(adds a `UserSecretsId` to `Api.csproj`, no connection-string content committed), then each
developer runs `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;..."
--project src/Api` locally against their own existing SQL Server 2022 instance. `appsettings.json`
/ `appsettings.Development.json` are created by the Web API template as usual (logging config
etc.) but carry no `ConnectionStrings` key — this avoids CLAUDE.md's "always ask first — touching
appsettings.*.json connection strings/keys" entirely, since no connection string is ever written
into a tracked file. `Program.cs` adds user secrets as a configuration source in the Development
environment (the ASP.NET Core Web API template does this automatically when `UserSecretsId` is
present).

Run `dotnet ef migrations add InitialCreate --project src/Infrastructure --startup-project
src/Api` — with no `DbSet`s this generates a migration with an empty `Up()`/`Down()`, only
creating the `__EFMigrationsHistory` table on `dotnet ef database update`.

### D3 — Minimal health endpoint to observably verify wiring
**Confirmed with user.** The spec's EF Core scenario is verified by running
`dotnet ef database update` directly, but nothing else in the ticket exercises
`Api → Infrastructure → SQL Server` together at runtime. Add one minimal `GET /api/health`
endpoint (in `Api/Controllers/HealthController.cs` or a minimal API route in `Program.cs`) that
calls `dbContext.Database.CanConnectAsync()` and returns `200 OK` / `503`. This contains no
business logic (pure infra check) and doesn't violate "controllers have no business logic."

### D4 — Seed-runner scaffold
`Infrastructure/Seeding/ISeedRunner.cs`:
```csharp
public interface ISeedRunner
{
    Task SeedAsync(CancellationToken cancellationToken);
}
```
`NoOpSeedRunner` — empty body, does not touch the database. Registered in DI
(`AddScoped<ISeedRunner, NoOpSeedRunner>()`) and invoked once from `Program.cs` at startup in
Development. ET002 replaces `NoOpSeedRunner` with the real CSV-backed implementation against this
same interface — no call-site changes needed later.

### D5 — Frontend scaffold
```
frontend/
  package.json            (name, private:true, scripts: dev/build/preview/lint/test)
  vite.config.ts           (react plugin + vitest test config, test.passWithNoTests: true)
  tsconfig.json / tsconfig.app.json / tsconfig.node.json
  tailwind.config.ts, postcss.config.js
  components.json          (shadcn/ui init output)
  index.html
  src/
    main.tsx, App.tsx, index.css
    api/ .gitkeep           components/ .gitkeep      features/ .gitkeep
    hooks/ .gitkeep         layouts/ .gitkeep          pages/ .gitkeep
    routes/ .gitkeep        store/ .gitkeep            types/ .gitkeep      utils/ .gitkeep
```
Installed via `pnpm create vite frontend -- --template react-ts`, then add Tailwind CSS +
`npx shadcn@latest init`, `react-router-dom`, `@tanstack/react-query`, `zustand`,
`react-hook-form`, `zod`, `@hookform/resolvers`. Test stack added now (Vitest + React Testing
Library + jsdom) even though no components/tests exist yet, so `pnpm --filter frontend test`
succeeds (`passWithNoTests`) rather than failing on "no test files found" — this keeps the Quality
Gates command usable immediately for ET016+ without a config change later.
Empty folders use `.gitkeep` since Git does not track empty directories, preserving the exact
`AGENTS.md` §2 folder layout from day one.

**Alternative considered**: skip Tailwind/shadcn/Router/TanStack/Zustand/RHF+Zod installation
until ET016 actually needs them. Rejected per explicit user decision in `/spec` — installing now
avoids every subsequent frontend ticket re-doing dependency setup.

### D6 — EditorConfig
Root `.editorconfig`: `charset = utf-8`, `end_of_line = lf`, `insert_final_newline = true`;
`[*.cs]` → 4-space indent, `csharp_new_line_before_open_brace = all` (matches default C#
convention); `[*.{ts,tsx,js,jsx,json}]` → 2-space indent; matches `AGENTS.md` §6 naming/formatting
conventions (this file governs whitespace only — naming conventions are enforced by
analyzers/ESLint, not EditorConfig).

### D7 — Husky hooks + commitlint
- `package.json` (root) devDependencies: `husky`, `lint-staged`, `@commitlint/cli`,
  `@commitlint/config-conventional`.
- `.husky/pre-commit` → `pnpm exec lint-staged`.
- root `lint-staged` config (in `package.json` or `.lintstagedrc.json`):
  - `backend/**/*.cs` → `dotnet format backend/ExpenseTracker.sln --include` (explicit solution
    path required — `lint-staged` runs from the repo root, where no `.sln`/`.csproj` exists;
    fixed during implementation after the bare `dotnet format --include` failed with "Could not
    find a MSBuild project file or solution file")
  - `frontend/**/*.{ts,tsx}` → `pnpm --filter frontend exec oxlint`

**Revised during implementation**: the Vite `react-ts` template now ships `oxlint` (Rust-based)
instead of ESLint by default. Confirmed with the user to keep `oxlint` rather than manually
installing ESLint — faster, zero-config, and matches the template's current default. The
`frontend/package.json` `lint` script (`oxlint`) and this `lint-staged` entry both use it.
- `.husky/commit-msg` → `pnpm exec commitlint --edit "$1"`.
- `commitlint.config.js` extends `@commitlint/config-conventional` but overrides:
  - `type-enum` restricted to exactly `[feat, fix, test, refactor, chore, docs]` (matches
    `CLAUDE.md` "Commit Message Format" — narrower than the default conventional set).
  - custom `scope-enum`/rule requiring the scope, when present, to match `^ET\d{3,4}$`; scope is
    optional (commits not mapped to a ticket may omit it, per `CLAUDE.md`).

### D8 — Backend test project scaffold
**Confirmed with user.** Add `backend/tests/UnitTests/UnitTests.csproj` (xUnit, `net9.0`),
referencing `Domain`, `Application`, and `Shared`. Added to `ExpenseTracker.sln` alongside the
five `src/` projects.

**Revised during implementation**: also added a reference to `Infrastructure`, confirmed with the
user. The spec's "seed runner scaffold executes as a no-op" scenario needed an actual test
against `NoOpSeedRunner`, which lives in `Infrastructure`. Since `NoOpSeedRunner` has zero EF
Core/DB dependencies, this doesn't pull real infrastructure concerns into the unit test project —
it's still a plain, fast unit test. True integration tests referencing the full stack
(`WebApplicationFactory`, a live `Api`) are still deferred to whichever ticket first needs them.

## Risks / Trade-offs

- **[Risk]** Empty EF Core migration provides weak signal — it proves DB *connectivity*, not
  correct schema generation for real entities. → **Mitigation**: ET002's first real migration is
  the actual test of entity mapping; this ticket only needs to prove the pipeline is wired.
- **[Risk]** Installing Tailwind/shadcn/Router/TanStack/Zustand/RHF+Zod now, before any component
  uses them, risks version drift by the time ET016 starts. → **Mitigation**: pin exact versions
  (per `docs/SDS.md` §2.2 "Dependency versions shall be explicitly specified"); ET016 bumps if
  needed, not a rewrite.
- **[Risk]** `UnitTests` project has zero test cases in this ticket, so `dotnet test` passes
  trivially (0 ran) rather than proving anything. → **Mitigation**: acceptable — the goal here is
  solution-shape parity (project exists, wired into the `.sln`, correct references) so ET002+
  only add `[Fact]` methods, never create-project ceremony.
- **[Risk]** User secrets are per-machine and untracked, so a fresh clone has no working
  connection string until the developer runs `dotnet user-secrets set` themselves. →
  **Mitigation**: document the one-time `dotnet user-secrets init` + `set` commands in
  `backend/CLAUDE.md` or a README note as part of implementation, so onboarding isn't a guessing
  game.

## Migration Plan

No production migration — this is the first-ever scaffolding of the repo, nothing to roll back
to. Sequence: (1) pnpm workspace + root tooling → (2) backend solution + EF wiring → (3) frontend
scaffold → (4) TICKETS.md scope-text correction (drop the "9 commands/3 subagents"/CI wording,
per the proposal). Each step is independently buildable/revertable via `git revert` if a later
step reveals a problem with an earlier one.

**Checkpoint commands** (run after each relevant step, per CLAUDE.md Quality Gates order):
- Frontend: `pnpm --filter frontend lint` → `pnpm --filter frontend build` → `pnpm --filter
  frontend test` (passes trivially, no test files yet).
- Backend: `dotnet build` (from `backend/`) → `dotnet test` (passes trivially — `UnitTests`
  project exists, 0 tests collected).
- Integration/E2E: not applicable to this ticket (no user-facing flow, no API business logic).

## Open Questions

All three prior open questions were resolved with the user before implementation:
1. Health endpoint — **included** (D3).
2. Backend test project — **included**, `backend/tests/UnitTests` (D8).
3. Per-developer connection string — **user secrets**, not `appsettings.*.json` (D2).

No outstanding open questions remain.
