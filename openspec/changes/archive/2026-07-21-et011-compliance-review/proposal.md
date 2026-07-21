## Why

`docs/FRS.md` §5.2 requires that Manager-`Approved` Client Entertainment expenses pass
through a dedicated Compliance Officer review step before they can be reimbursed. ET010
delivered the Manager review step (`docs/FRS.md` §5.1) and ET009 already delivered the
Compliance Officer's read-only visibility into `Approved`/`ComplianceApproved` Client
Entertainment expenses (`openspec/specs/expense-visibility/spec.md`, "Compliance Officer
Default Visibility"). What's missing is the two write endpoints that actually let a
Compliance Officer act on those expenses — without them, a Client Entertainment expense
can never leave `Approved` and reach `Reimbursed` (`docs/TICKETS.md` ET011, `Planned`).

## What Changes

- Add `POST /api/expenses/{id}/compliance-approve` (`docs/FRS.md` §5.2.3, `docs/SDS.md`
  §5.2, §6.3): transitions a `Client Entertainment` expense from `Approved` to
  `ComplianceApproved`. Restricted to the `ComplianceOfficer` role.
- Add `POST /api/expenses/{id}/compliance-reject` (`docs/FRS.md` §5.2.4–5.2.5): transitions
  a `Client Entertainment` expense from `Approved` to `Rejected`, requiring a mandatory
  `rejectionComment` (reusing the existing `RejectExpenseRequest`/
  `RejectExpenseRequestValidator` contract introduced in ET010 — identical validation
  rules: required, non-empty after trim, max 500 chars). Restricted to the
  `ComplianceOfficer` role.
- Both endpoints reject, with `422 BUSINESS_RULE_VIOLATION`, a target expense that is not
  `ClientEntertainment` category (any status) or not currently `Approved` status (any
  category) — these are two independently-checked business rules, not one combined
  check. `Rejected` expenses remain terminal (`docs/FRS.md` §5.2.6, BR-07 already
  established in ET010 applies identically here — no re-approval path exists).
- No BR-06-style self-review restriction is implemented: `docs/SDS.md` §6.4's
  Authorization Matrix shows only `Employee`/`Manager` can create expenses, so a
  `ComplianceOfficer` can never own an expense — self-review is unattainable, confirmed
  with the ticket owner during `/spec`, and no dead authorization branch is added for it.
- Audit fields `ComplianceApprovedAt`/`ComplianceApprovedByEmployeeId` (already defined on
  `Expense`, `docs/SDS.md` §3.6, unused until now) are populated on compliance-approve.
  Compliance-reject populates the same `RejectedAt`/`RejectedByEmployeeId`/
  `RejectionComment` fields ET010's Manager rejection already uses — `Rejected` has one
  terminal meaning regardless of which role rejected it.
- Extend the ET010 notification placeholder: add `ComplianceApproved`/`ComplianceRejected`
  to `NotificationEvent` (`Application/Notifications/NotificationEvent.cs`) so ET015's
  eventual templates can distinguish a Compliance Officer's action from a Manager's,
  confirmed with the ticket owner during `/spec`.

## Capabilities

### New Capabilities
- `compliance-expense-review`: Compliance Officer approval/rejection endpoints for
  Manager-`Approved` Client Entertainment expenses, mandatory rejection comment
  validation, category/status business-rule guards, and audit field population.

### Modified Capabilities
(none — `expense-visibility`'s Compliance Officer read visibility was already fully
specified and implemented in ET009; this ticket adds write endpoints only, no visibility
requirement changes.)

## Impact

- `Api/Controllers/ExpensesController.cs`: two new actions, `FailureResult` switch
  extended with two new `ExpenseFailureReason` mappings.
- `Application/Expenses/IExpenseService.cs` + `ExpenseService.cs`: two new methods
  (`ComplianceApproveAsync`, `ComplianceRejectAsync`), reusing the existing
  `RejectExpenseRequest` type.
- `Application/Expenses/ExpenseFailureReason.cs`: add `NotClientEntertainment`,
  `NotApprovedForCompliance`.
- `Application/Notifications/NotificationEvent.cs`: add `ComplianceApproved`,
  `ComplianceRejected`.
- `Api/Authorization/AuthorizationPolicyNames.cs`: `ComplianceOfficer` policy already
  exists (added in ET003) — reused, not modified.
- No EF Core migration needed — `ComplianceApprovedAt`/`ComplianceApprovedByEmployeeId`/
  `RejectedAt`/`RejectedByEmployeeId`/`RejectionComment` all already exist on `Expense`
  from ET002/ET010.
- Tests: unit tests for `ExpenseService` compliance methods; integration tests for both
  endpoints (role gating, category guard, status guard, terminal-rejection, audit fields).
