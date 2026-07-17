## Why

The repo currently contains only `docs/` — no application code has been bootstrapped yet (per
`AGENTS.md` §"Status" note and confirmed by inspection: `backend/` and `frontend/` each contain
only a `CLAUDE.md`, no solution/app files exist). Every FRS-driven ticket from ET002 onward
depends on the pnpm monorepo, backend solution skeleton, frontend app skeleton, EF Core wiring,
and baseline dev tooling existing first, per `docs/SDS.md` §1 (Architecture Overview) and §2
(Tech Stack). This ticket builds that foundation so later tickets can start writing FRS-traceable
feature code immediately.

**Note on FRS traceability**: `docs/TICKETS.md` lists ET001's FRS Sections as `N/A` — this ticket
is pure platform/infrastructure scaffolding with no functional/business behavior, so there are no
FRS acceptance criteria to cite. Scope here is governed by the ET001 row in `docs/TICKETS.md` and
by `docs/SDS.md` §1, §2, §2.1, §2.2 instead.

## What Changes

- Initialize the pnpm workspace (`pnpm-workspace.yaml`, root `package.json`) with `frontend` and
  `backend` workspaces per `docs/SDS.md` §1 and §2.1.
- Scaffold the ASP.NET Core 9 Web API solution under `backend/` with `Api`, `Application`,
  `Domain`, `Infrastructure`, `Shared` projects (project references only, per the layering in
  `docs/SDS.md` §2.1 and `AGENTS.md` §5), targeting `net9.0`/C# 13, `Nullable` and
  `ImplicitUsings` enabled per `AGENTS.md` §6.
- Scaffold the React 19 + TypeScript + Vite app (`react-ts` template) under `frontend/`, with
  Tailwind CSS, shadcn/ui `init`, React Router, TanStack Query, Zustand, and React Hook Form + Zod
  installed and minimally wired (empty shell app, no feature UI — feature UI starts at ET016 per
  `docs/TICKETS.md`).
- Add EF Core 9 packages to `Infrastructure` and wire an empty `ApplicationDbContext` (no
  `DbSet`s yet — the `Employee` entity and CSV seed import are explicitly ET002 scope, confirmed
  with the user) against a connection string pointing at an existing/external SQL Server 2022
  instance (no `docker-compose`, confirmed with the user). Generate and apply an empty
  `InitialCreate` migration solely to prove the EF Core → SQL Server pipeline works end-to-end.
- Add a seed-runner scaffold (an `ISeedRunner`-style interface + skeleton no-op implementation)
  that ET002 will populate with the real `docs/EmployeeSeedData.csv` import logic — this ticket
  wires the mechanism, not the Employee data itself.
- Add root `.editorconfig` enforcing the C#/TypeScript conventions in `AGENTS.md` §6.
- Add Husky with a `pre-commit` hook (lint-staged: `dotnet format` on staged `.cs` files,
  ESLint/Prettier on staged frontend files) and a `commit-msg` hook (commitlint enforcing
  Conventional Commits scoped to a ticket ID, per `CLAUDE.md` "Commit Message Format").
- **Revise the ET001 row in `docs/TICKETS.md`** to match what was decided and already exists:
  drop "9 slash commands, 3 Claude subagents" (7 commands / 2 subagents already exist from prior
  commits — confirmed with the user, no further command/subagent work is in scope) and drop
  "CI pipeline" / "GitHub Actions" (explicitly deferred out of this ticket, confirmed with the
  user). `AGENTS.md`, `CLAUDE.md`, and `OpenSpec init` are already complete (verified this
  session) and are not redone here.

## Capabilities

### New Capabilities
- `project-bootstrap`: infrastructure-only capability covering monorepo scaffolding existence,
  backend/frontend skeleton existence and buildability, EF Core wiring plus the empty initial
  migration, the seed-runner scaffold, and baseline dev tooling (EditorConfig, Husky hooks). No
  FRS requirement IDs apply (see "Why" above); scenarios verify structural/build outcomes, not
  business behavior.

### Modified Capabilities
None — `openspec/specs/` is currently empty; this is the first ticket to introduce a capability.

## Impact

- **New files/directories**: `pnpm-workspace.yaml`, root `package.json`; `backend/*.sln`,
  `backend/src/{Api,Application,Domain,Infrastructure,Shared}/*.csproj`,
  `backend/src/Infrastructure/Migrations/*_InitialCreate.cs`; `frontend/` Vite app files;
  `.editorconfig`; `.husky/`; a commitlint config file.
- **Modified files**: `docs/TICKETS.md` (ET001 row scope description only — status/change-name
  columns are updated separately per the `/spec` workflow).
- **No existing capability specs are affected** — none exist yet.
- **Dependencies to be added** (NuGet: `Microsoft.EntityFrameworkCore.SqlServer`, `.Design`,
  `.Tools`; pnpm: Tailwind CSS, shadcn/ui, React Router, TanStack Query, Zustand, React Hook Form,
  Zod, Husky, lint-staged, commitlint) — flagged here for visibility; actually running
  add/install commands during `/implement` still requires separate confirmation per `CLAUDE.md`'s
  "Always ask first" permission rule.
- **No CI/GitHub Actions changes** in this ticket (explicitly deferred, confirmed with the user).
- **No `docker-compose` for SQL Server** — developers point `ConnectionStrings` at their own
  existing SQL Server 2022 instance via `appsettings.Development.json`/user secrets (not
  committed), confirmed with the user.
