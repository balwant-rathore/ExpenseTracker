## Why

Employees and Managers can create, edit, cancel, and view expenses (ET007–ET009), but no
endpoint lets a Manager act on a direct report's `Submitted` expense. Without ET010, every
expense that reaches `Submitted` is a dead end — the workflow described in `docs/FRS.md`
§5.1 and `docs/SDS.md` §6.1/§6.3 has no way to reach `Approved` or `Rejected`. This change
implements Ticket **ET010** (`docs/TICKETS.md`): the Manager approval/rejection workflow,
including the mandatory rejection comment.

## What Changes

- Add `POST /api/expenses/{id}/approve` (`docs/SDS.md` §5.2): transitions a `Submitted`
  expense to `Approved`, restricted to `Manager` role, setting `ApprovedAt` and
  `ApprovedByEmployeeId` (`docs/SDS.md` §6.8).
- Add `POST /api/expenses/{id}/reject` (`docs/SDS.md` §5.2): transitions a `Submitted`
  expense to `Rejected`, restricted to `Manager` role, requiring a mandatory
  `rejectionComment` (`docs/FRS.md` §5.1.4), setting `RejectedAt`, `RejectedByEmployeeId`,
  `RejectionComment`.
- Enforce **BR-06** on both actions: a Manager SHALL NOT approve or reject their own
  expense (broader than the literal FRS §5.1.6 text, which names only Approve — confirmed
  with the ticket owner during `/spec`; matches `AGENTS.md` §11's "Managers cannot
  approve/reject their own expenses (BR-06)").
- Add a **skip-level escalation** authorization rule (open decision beyond literal FRS/SDS
  text, confirmed with the ticket owner, logged as an ADR in `docs/decisions/`): when the
  expense owner is itself a Manager, that owner's own `ManagerId` is authorized to
  approve/reject the expense, in addition to the standard "expense owner's direct manager"
  case already implied by the reporting hierarchy.
- Enforce **BR-07**/FRS §5.1.5: `Rejected` is terminal — no later approval of a rejected
  expense (already partly covered by `expense-maintenance`'s read-only-after-terminal-state
  rule; this change adds the action-endpoint side of that guarantee).
- Reject approve/reject attempts against any expense whose `Status` is not `Submitted`
  with `422 BUSINESS_RULE_VIOLATION` (no backward transitions, `docs/FRS.md` §7.2.4).
- Add a `status` query filter to the existing `GET /api/expenses` list endpoint (open
  decision beyond ET009's literal scope, confirmed with the ticket owner) so Managers can
  request `?status=Submitted` to see only expenses awaiting their review, per FRS §5.1.1.
- Add a call site for a placeholder `INotificationService.NotifyAsync(...)` no-op
  implementation, fired after each successful commit (never blocking or rolling back the
  transaction on failure, per `docs/SDS.md` §7.6), so ET015 (Notifications, currently
  Planned) only has to implement the interface, not find call sites. No actual
  notification behavior (HTML log, templates, recipients) is implemented or specified by
  this change — that is entirely ET015's scope.

## Capabilities

### New Capabilities
- `manager-expense-review`: Manager approve/reject action endpoints, BR-06 self-review
  restriction with skip-level escalation, mandatory rejection comment validation, terminal
  `Rejected` state enforcement, and audit field population (`docs/FRS.md` §5.1,
  `docs/SDS.md` §5.2, §6.3, §6.8).

### Modified Capabilities
- `expense-visibility`: `GET /api/expenses` gains an optional `status` query filter so a
  Manager (or any role) can narrow the existing visibility-scoped list to a single
  `ExpenseStatus`, supporting FRS §5.1.1's "view expenses submitted by employees reporting
  to them." No existing requirement's behavior changes — this is an additive filter on top
  of the already-specified per-role default visibility rules.

## Impact

- **Api**: two new controller actions on the existing expenses controller
  (`approve`, `reject`); `status` query parameter added to the list endpoint's binding.
- **Application**: new service methods for approve/reject workflow transitions and their
  authorization checks (BR-06 + skip-level escalation); a `FluentValidation` validator for
  the reject request body (`rejectionComment` required, non-empty/non-whitespace, ≤500
  chars); a placeholder `INotificationService` interface and no-op implementation
  registered in DI.
- **Domain**: no schema changes — `Expense`'s `ApprovedAt`/`ApprovedByEmployeeId`/
  `RejectedAt`/`RejectedByEmployeeId`/`RejectionComment` fields already exist per
  `docs/SDS.md` §3.6 (ET002).
- **Infrastructure**: none.
- **Tests**: new integration tests per approve/reject scenario (own-expense rejection,
  skip-level escalation, non-Submitted rejection, missing/oversized rejection comment,
  terminal-Rejected non-reapproval, status filter), unit tests for the authorization and
  validation logic.
- **Decisions**: two open decisions beyond literal FRS/SDS text (BR-06 scope broadened to
  Reject; skip-level escalation path; `status` filter addition) will be logged as ADRs in
  `docs/decisions/` per this repo's OpenSpec proposal rules.
