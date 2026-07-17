Create technical plan for: $ARGUMENTS

$ARGUMENTS is the Ticket (e.g. ET002). Look up its Change Name column in docs/TICKETS.md first (e.g. et002-user-auth) — use that kebab-case name in place of <change-name> for every openspec command and file path below, not the ticket.

Steps:
1. Read: openspec/changes/<change-name>/proposal.md
2. Read: openspec/changes/<change-name>/specs/**/*.md (the approved spec delta — do not deviate from it)
3. Read: docs/SDS.md (architecture decisions + DB schema + API contracts) - design MUST follow this doc. Ambiguity/gaps to be flagged as open question and NO silent design changes.
4. Read: AGENTS.md + domain > CLAUDE.md
5. Scan existing codebase for reusable patterns (existing services, middleware, components, utility) before proposing new ones.
6. Run `openspec instructions design --change <change-name>` and follow its output exactly to write `openspec/changes/<change-name>/design.md`, covering:
   - Exact file paths to create/modify
   - TypeScript interfaces / Zod schemas (frontend) + C# DTOs/records (backend), matching SDS contracts
   - Key technical/architectural decisions with reasoning
   - DB changes (note backward compatible?)
   - Reuse of existing frontend/backend code (no shared package — TS frontend and C# backend are separate codebases)
   - Build + test + lint (frontend) and Build + test (backend) checkpoint commands
7. Run `openspec validate <change-name>` (Must Pass) 
8. Wait for approval before any implementation

Format: /plan ET0002