Implement: $ARGUMENTS

$ARGUMENTS is the Ticket (e.g. ET002). Look up its Change Name column in docs/TICKETS.md first (e.g. et002-user-auth) — use that kebab-case name in place of <change-name> for every openspec command and file path below, not the ticket.

Before writing ONE line of code, read:
1. AGENTS.md
2. docs/FRS.md (business rules for this feature)
3. docs/SDS.md (API contracts, DB schema, design decisions)
4. Domain's CLAUDE.md (frontend/backend)
5. openspec/changes/<change-name>/proposal.md
6. openspec/changes/<change-name>/design.md
7. openspec/changes/<change-name>/tasks.md

Rules:
- Ask [y/n] before every file write - NO SILENT WRITES
- Work through tasks.md phase by phase, checking off each `- [ ]` as it's completed.
- After every phase: `pnpm build && `pnpm lint` && `pnpm run test` (frontend) → `dotnet build` && `dotnet test` (backend)
- Write tests BEFORE or ALONGSIDE implementation. NEVER AFTER. 
- Never skip a failing test. Fix the root cause
- If a task is estimated to take longer than 45 minutes, delegate it to a sub-agent. Never use a session-context.md file as a workaround.
- At ~70\% context usage (execute exactly once): run /compact → run /context → continue
- At context limit : save to session-context.md → /clear → resume → read session-context.md
- use Context7 to verify every libarary/API usage - no hallucinated methods
- When every phase is complete and all checkpoints are green, run `openspec archive <change-name>` and keep docs/TICKETS.md's status for this ticket to `In progress`. (DO NOT mark Done here — archiving happens before the PR exists; `/pr` sets `PR open (#N)`, and Done is reserved for after the PR actually merges).

Output when done:
## Files Changed + why
## Spec Scenarios Covered (scenario → test name)
## FRS Requirements Covered (requirement ID → implementation)
## Assumptions Made
## Follow-up Tasks

Format: /implement ET002