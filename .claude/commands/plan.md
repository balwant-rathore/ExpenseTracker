Create technical plan for: $ARGUMENTS

Steps:
1. Read: openspec/changes/$ARGUMENTS/proposal.md
2. Read: openspec/changes/$ARGUMENTS/specs/
3. Read: docs/SDS.md (architecture decisions + DB schema + API contracts)
4. Read: AGENTS.md + domain CLAUDE.md
5. Scan existing codebase for reusable patterns
6. Generate plan covering:
   - Exact file paths to create/modify
   - TypeScript interfaces (frontend) + C# DTOs/records (backend), matching SDS contracts
   - Architecture decisions with reasoning
   - DB changes (backward compatible?)
   - Reuse of existing frontend/backend code (no shared package — TS frontend and C# backend are separate codebases)
   - Build + test + lint for frontend and backend (as applicable) checkpoint commands
7. Save to: openspec/changes/$ARGUMENTS/plan.md
8. Wait for approval before any implementation

Format: /plan AB-1042-user-registration