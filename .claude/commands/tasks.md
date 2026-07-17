Break down into tasks for: $ARGUMENTS

$ARGUMENTS is the Ticket (e.g. ET002). Look up its Change Name column in docs/TICKETS.md first (e.g. et002-user-auth) — use that kebab-case name in place of <change-name> for every openspec command and file path below, not the ticket.

Steps:
1. Read: `openspec/changes/<change-name>/proposal.md`
2. Read: `openspec/changes/<change-name>/design.md
3. Read `openspec/changes/<change-name>/specs/**/*.md`.
4. Run `openspec instructions tasks --change <change-name>` and follow its output exactly — tasks MUST use the `- [ ] X.Y Description` checkbox format (the archive step parses this; other formats are silently untracked)
5. Generate sequenced task checklist:
   - Phase 1: Foundation (API contracts/DTOs, EF Core migrations)
   - Phase 2: Core implementation - mark tasks [PARALLEL] when frontend and backend work can run simultaneouly in seprate worktrees
   - Phase 3: Integration
   - Phase 4: Tests (one test per spec scenario from spec delta)
     - Phase 5: Archive (`openspec archive <change-name>) and update `docs/TICKETS.md` status
     - Checkpoint after each phase:
      * npm run build (frontend) → 0 errors; dotnet build (backend) → 0 errors
      * npm run lint -- --max-warnings 0 (frontend); dotnet format --verify-no-changes (backend)
      * npm run test (frontend) → all green; dotnet test (backend) → all green
6. Save to: openspec/changes/<change-name>/tasks.md
7. Wait for approval before continuing

Format: /tasks ET0002