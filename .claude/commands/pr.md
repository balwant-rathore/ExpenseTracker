Prepare PR for: $ARGUMENTS

$ARGUMENTS is the Ticket (e.g. ET002). Look up its Change Name column in docs/TICKETS.md first (e.g. et002-user-auth) — use that kebab-case name in place of <change-name> for every openspec command and file path below, not the ticket.

Steps:
1. Run in order:
   - `pnpm build` (frontend) && `dotnet build` (backend) - 0 errors, 0  warnings
   - `pnpm lint --max-warnings 0` (frontend) && `dotnet format --verify-no-changes` (backend)
   - `pnpm test --coverage` (frontend) && `dotnet test` (backend)
   - `npx commitlint --from HEAD~1`
   (Fix any failures before proceeding)
2. Confirm the change is archived (it should be, from the end of `/implement`) — find it with glob
   `openspec/changes/archive/*-<change-name>` (date-prefixed, e.g.
   `openspec/changes/archive/2026-07-04-ab-1002-user-auth/` — NOT a top-level
   `openspec/archive/`). If it's still under `openspec/changes/<change-name>/` instead, run
   `openspec archive <change-name>` now.
3. Confirm `/review $ARGUMENTS` was run in a fresh terminal and returned all ✅.
4. Run: `git diff main --stat`
5. Read: `proposal.md` and `specs/**/*.md` from the archive directory found in step 2.
6. Generate commit:
   ```
   feat(scope): description $ARGUMENTS

   -bullet 1
   -bullet 2

   Relates to <change-name>
   ```
7. Ask: "Run git add . && git commit? [y/n]"
8. Generate PR description:
   ## What
   ## FRS Requirements Covered
   ## Spec Artifacts
   ## Checklist
   ## Test Coverage
7. Ask: "Run git push? [y/n]"
8. Update this ticket's `Status` to `PR open (#N)` (the PR number from step 7) in
    `docs/TICKETS.md` and include that update in the same commit. Do NOT set `Done` here —
    that's reserved for after the PR is actually merged (update it manually, or via a follow-up
    commit, once merge happens).

PR description MUST list every FRS requirement covered and every spec scenario tested (no
exceptions — this is required, not optional, for review).

Format: /pr AB-1042-user-registration