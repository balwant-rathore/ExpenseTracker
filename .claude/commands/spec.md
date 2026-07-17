---
argument-hint: [ticket] [change-name]
description: resolve ticket and kebab-case change name
---

Run OpenSpec proposal creation for: $2 

Steps:
1. grep $1's row in docs/TICKETS.md. If it's not listed there, STOP and ask the user for the ticket's scope before doing anything else — do not guess or invent one.
2. Run: `openspec list` to see active changes
3. Read: `openspec/specs/` (current system state) to see affected capabilities for this ticket
4. Read: docs/FRS.md → find relevant requirements for this ticket is `docs/TICKETS.md`
5. Read: docs/SDS.md → find relevant design decisions this ticket must follow
6. Read: AGENTS.md (constraints) and the relevant domain CLAUDE.md for constraints
7. Ask clarifying questions — minimum 3, maximum 8; Do not guess at ambiguous requirements;
8. Use $2 as <change-name>. If `openspec/changes/<change-name>/` does not exist yet, run: `openspec new change <change-name> --description "<one-line ticket summary from docs/TICKETS.md>"`
9. Run `openspec instructions proposal --change <change-name>` and follow its output exactly to write `openspec/changes/<change-name>/proposal.md`. Every capability/spec delta MUST trace to a numbered FRS requirement — cite the requirement IDs in the proposal.
10. Run `openspec instructions specs --change <change-name>` and follow its output exactly to write the scenario delta file(s) at `openspec/changes/<change-name>/specs/<capability>/spec.md` (ADDED/MODIFIED/REMOVED, #### Scenario: with exactly 4 hashtags, WHEN/THEN format).
11. Run `openspec validate <change-name>` — must pass before continuing.
12. Show the generated proposal.md and spec delta in full.
13. Update this ticket's row in docs/TICKETS.md: set `Status` to `In progress` and record `<change-name>` in the `Change Name` column — this is the only place the ticket ID ↔ OpenSpec change name mapping is recorded, and /plan, /tasks, /implement, /review, and /pr all depend on it.
14. Do NOT proceed to /plan or write any implementation code.

Format: /spec ET002 et0012-user-registration