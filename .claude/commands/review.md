Review implementation for: $ARGUMENTS

$ARGUMENTS is the Ticket (e.g. ET002). Look up its Change Name column in docs/TICKETS.md first (e.g. et002-user-auth) — use that kebab-case name in place of <change-name>, not the ticket.

Run this in a fresh terminal (new Claude Code instance, clean context) — never reuse the session that implemented the ticket.

Delegate this entire review to the reviewer sub-agent (.claude/agents/reviewer.md) — it is read-only and MUST NOT modify any files.

Steps for the sub-agent:
1. use Glob to find `openspec/changes/archive/*-<change-name>` (archived  changes are date-prefixed, e.g. `openspec/changes/archive/2026-07-04-ab-1002-user-auth/` —   NOT a top-level `openspec/archive/`, and the date can't be predicted in advance, hence the glob). Read its `proposal.md`, `specs/**/*.md`, `design.md`, `tasks.md`. If no match is found, check `openspec/changes/<change-name>/` instead and flag that archiving has not happened yet.
2. Read: docs/FRS.md (original requirements)
3. Compare implementation against spec scenarios AND FRS acceptance criteria
4. Output, a line per item:
   ✅ Implemented: [scenario] → [file:line-num]
   ❌ Missing: [scenario]
   ⚠️ Drifted: [scenario — spec says X, code does Y]
   🔒 Security: [concern]
   📋 FRS gap: [requirement not addressed]
5. No style feedback — compliance only

Format: `/review ET0002`