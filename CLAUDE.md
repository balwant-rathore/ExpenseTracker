@AGENTS.md

Claude Code-specific operating rules for this repo. Everything about the product, stack, and
code conventions lives in `AGENTS.md` above — this file only covers how Claude Code itself should behave here.

## Permission Model

**Proceed without asking:**
- Reading/searching files, `git status`/`diff`/`log`, running existing lint/test/build commands.
- Editing files within `frontend/` or `backend/` as part of the ticket in progress.
- Creating local git commits (not pushing).
- Running EF Core migrations against a local/dev database.

**Always ask first:**
- `git push` (including force-push — force-push to `main` requires explicit confirmation each time).
- `git reset --hard`, `git clean`, deleting files/branches, overwriting uncommitted changes.
- Adding/upgrading/removing a NuGet or pnpm dependency.
- Any `dotnet ef database update` against a non-local database.
- Opening, merging, or closing a GitHub PR/issue; modifying CI/CD workflow files.
- Touching `.env`, secrets, or `appsettings.*.json` connection strings/keys.

A prior approval covers only that instance — repeat actions of the same kind still ask, unless
the user says otherwise for the session.

## Context Management

- Treat ~60k tokens of conversation as the working budget for a single ticket/session. When approaching it: run `/compact` (exactly once) when context usage hits ~70%. Never wait for context to fill.
- When single ticket approaches working budget twice - summarize current progress (what's done, what's left, open decisions) into a short note in the ticket's plan or a scratch file, then suggest the user run `/clear` (or start a fresh session) before continuing. 
- Don't let unrelated ticket history accumulate in one context — one ticket per context where practical — run /clear between tickets. No exceptions.
- For any task estimated to take longer than 45 minutes, delegate to a sub-agent. Never use a session-context.md file as a workaround.

## Parallel Work
- For any two tasks marked `[PARALLEL]` in `tasks.md`, use `/parallel` to spin up separate git worktrees. Frontend and backend work must run in separate worktrees, never interleaved in one working tree.

## Code Library Validation
- Always use Context7 to fetch current documentation before generating code that uses an external library — don't rely on training data for framework/library APIs.

## Thinking Depth

- **High effort**: auth/token logic, expense workflow/state transitions, authorization checks,
  business rule enforcement (BR-01…BR-10), anything touching money/amount calculations or audit
  fields. These are the areas the FRS/SDS treat as non-negotiable — get them right the first time.
- **Low/default effort**: CRUD scaffolding, DTO/type definitions, UI layout, formatting, renames,
  test boilerplate, adding a field to an existing pattern.
- When a task mixes both (e.g., an approval endpoint), think hardest about the state-transition
  and authorization logic; treat the controller wiring around it as mechanical.

## Commit Message Format

Conventional Commits, scoped to the ticket ID from `docs/TICKETS.md`:

```
<type>(<ticket-id>): <short imperative summary>

<optional body: why, not what>
```

`type` ∈ `feat|fix|test|refactor|chore|docs`. Example: `feat(ET007): add expense creation and
draft/submit workflow`. Every commit that implements ticket scope must reference its ticket ID;
commits that don't map to a ticket (e.g. a straight typo fix) may omit it.

## Branch Naming

`ticket/<ticket-id>-<kebab-short-description>`, e.g. `ticket/ET007-expense-creation`. One branch
per ticket; don't combine multiple tickets on one branch.

## Quality Gates

Run in this order before considering a ticket's implementation done or opening a PR — stop and
fix at the first failure rather than pushing through:

1. Lint (`pnpm --filter frontend lint` / backend analyzers via `dotnet build`)
2. Build (`dotnet build`, `pnpm --filter frontend build`)
3. Unit tests (`dotnet test` unit projects, `pnpm --filter frontend test`)
4. Integration tests (`dotnet test` integration projects)
5. E2E tests (`npx playwright test`) — only when the ticket touches a user-facing flow

A ticket is not "done" (per `docs/TICKETS.md` status conventions) until it passes all gates
applicable to its scope.

## Commands Requiring [y/n] Permission

Beyond the permission model above, always prompt before running:
- `git push`, `git push --force*`
- `git reset --hard`, `git checkout -- .`, `git clean -f*`
- `dotnet ef database update` (non-local target)
- `pnpm add|remove`, `dotnet add package|remove package`
- `gh pr create|merge|close`, `gh issue close`
- Any command that deletes files/branches or writes outside this repo's working tree
