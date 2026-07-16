Break down into tasks for: $ARGUMENTS

Steps:
1. Read: openspec/changes/$ARGUMENTS/proposal.md
2. Read: openspec/changes/$ARGUMENTS/plan.md
3. Generate sequenced task checklist:
   - Phase 1: Foundation (API contracts/DTOs, EF Core migrations)
   - Phase 2: Core implementation [mark PARALLEL tasks]
   - Phase 3: Integration
   - Phase 4: Tests (one test per spec scenario)
   - Checkpoint after each phase:
     * npm run build (frontend) → 0 errors; dotnet build (backend) → 0 errors
     * npm run lint -- --max-warnings 0 (frontend); dotnet format --verify-no-changes (backend)
     * npm run test (frontend) → all green; dotnet test (backend) → all green
4. Save to: openspec/changes/$ARGUMENTS/tasks.md
5. Wait for approval

Format: /tasks AB-1042-user-registration