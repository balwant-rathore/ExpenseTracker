Implement: $ARGUMENTS

Before writing ONE line of code, read:
1. AGENTS.md
2. docs/FRS.md (business rules for this feature)
3. docs/SDS.md (API contracts, DB schema, design decisions)
4. Domain CLAUDE.md
5. openspec/changes/$ARGUMENTS/proposal.md
6. openspec/changes/$ARGUMENTS/plan.md
7. openspec/changes/$ARGUMENTS/tasks.md

Rules:
- Ask [y/n] before every file write
- After every phase: npm run build && npm run lint && npm run test (frontend) → dotnet build && dotnet test (backend)
- Write tests BEFORE or ALONGSIDE implementation
- Never skip a failing test
- At 50k tokens (execute exactly once): run /compact → run /context → continue
- At 60k tokens : save to session-context.md → /clear → resume → read session-context.md
- When complete: openspec archive $ARGUMENTS

Output when done:
## Files Changed + why
## Spec Scenarios Covered (scenario → test name)
## FRS Requirements Covered (requirement ID → implementation)
## Assumptions Made
## Follow-up Tasks

Format: /implement AB-1042-user-registration