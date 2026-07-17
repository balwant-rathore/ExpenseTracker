# Tickets

The fixed build order for this project. Build strictly in this order — no skipping, no
reordering. `/spec <ticket-id>` reads this file first to know what a ticket ID actually means —
the ID itself (`ET-xxxx`) carries no meaning on its own; this file is the mapping.

Each row: ticket ID, one-line scope, the `docs/FRS.md` sections it implements, domain, status,
linked GitHub issue

| Ticket | Scope| FRS Sections | Domain | Status  | Issue | Change Name |
| ------ |--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------ | -------------- | ------- | ------------------- |------------------------|
| ET001   | Project bootstrap, pnpm monorepo, ASP.NET Core solution, React app, EF Core wiring against an existing SQL Server instance, empty initial migration, seed-runner scaffold (Employee entity/import is ET002), AGENTS.md, CLAUDE.md, OpenSpec init, 7 slash commands, 2 Claude subagents (already delivered), EditorConfig, Husky. CI/GitHub Actions deferred out of this ticket. | N/A | Platform | Done | [#1](https://github.com/balwant-rathore/ExpenseTracker/pull/1) | et001-project-bootstrap |
| ET002   | Domain model, entities, enums, EF configurations, DbContext, repositories, SQL Server schema, attachment model, seed CSV import | 2, 4, 5, 6, 11, 14 | Domain   | PR open (#2) | [#2](https://github.com/balwant-rathore/ExpenseTracker/pull/2) | et002-domain-setup |
| ET003   | Authentication infrastructure, JWT, refresh tokens, BCrypt, authorization policies, middleware, rate limiting  | 3   | Authentication | Planned | Security foundation |  |
| ET004   | User registration, login, refresh, logout APIs  | 3.1–3.3 | Authentication | Planned | Auth APIs |  |
| ET005   | Forgot password, OTP lifecycle, reset password, refresh token revocation  | 3.4–3.5  | Authentication | Planned | Password recovery   |  |
| ET006   | Attachment upload API, filesystem storage, validation, metadata persistence | 4.1   | Attachments    | Planned | File upload |  |
| ET007   | Expense creation (Draft/Submit), expense number generation, validation, workflow initialization   | 4.1, 6, 11  | Expenses | Planned | Expense submission  |  |
| ET008   | Expense edit, cancel, ownership validation, read-only enforcement | 4.2–4.4, 11  | Expenses    | Planned | Expense maintenance |  |
| ET009   | Expense query APIs, visibility filtering, pagination, sorting  | 4.4 | Expenses  | Planned | Expense viewing     |  |
| ET010   | Manager approval/rejection workflow including rejection comments  | 5.1  | Workflow  | Planned | Manager review  |
| ET011   | Compliance approval/rejection workflow for Client Entertainment   | 5.2   | Workflow       | Planned | Compliance review   |  |
| ET012   | Finance search, reimbursement workflow, reimbursement export APIs   | 7  | Finance        | Planned | Finance processing  |  |
| ET013   | Dashboard aggregation APIs for Employee, Manager and Finance  | 8  | Dashboard      | Planned | Dashboard metrics   |  |
| ET014   | Monthly reimbursement Excel generation using ClosedXML  | 10   | Reporting      | Planned | Reporting  |  |
| ET015   | HTML notification logging, notification templates, post-commit notification service | 9  | Notifications  | Planned | Notifications  |  |
| ET016   | React authentication module (routing, login, registration, reset password, protected routes) | 3 | Frontend   | Planned | Authentication UI   |  |
| ET017   | Expense management UI (create, edit, cancel, upload receipt, list, detail)   | 4  | Frontend       | Planned | Expense UI  |  |
| ET018   | Manager, Compliance and Finance review screens   | 5, 7  | Frontend  | Planned | Review UI |  |
| ET019   | Dashboard, finance search, monthly report download UI | 7, 8, 10 | Frontend  | Planned | Dashboard & Reports |  |
| ET020   | Unit tests, integration tests, Playwright E2E, FRS traceability matrix, CI quality gates, release readiness  | All  | Quality  | Planned | Verification  |  |

## Out of Scope (all tickets)

Do not build, on any ticket, anything from `docs/FRS.md` Section 12: 
- Payment processing
- Multiple receipt attachments
- Additional expense categories
- Actual email notification
- Email server integration
- Sending Live OTP to actual emails

## Notes

- "ET" is not an acronym — it's an arbitrary ticket-prefix carried over verbatim from the
  original assignment brief. It has no meaning beyond being this project's ticket ID format.
- Update the `Status` column as tickets complete: `Planned` → `In progress` →
  `PR open (#N)` (implementation + archive done, awaiting review/merge) → `Done` (merged).
  `Done` means merged, not just implemented — `openspec archive` happening before the PR is
  raised (by design, per `/implement`) does not mean the ticket itself is done yet.