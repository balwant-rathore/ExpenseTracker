# ADR-0012: Skip-Level Escalation for a Manager's Own Submitted Expense

## Status

Accepted

## Context

BR-06 (see ADR-0011) blocks a Manager from approving or rejecting their own expense.
Neither `docs/FRS.md` nor `docs/SDS.md` names an escalation path for this case: if a
Manager submits their own expense, and the Manager cannot review it, who can? Left
unresolved, every Manager-owned expense would be permanently stuck in `Submitted` with no
valid reviewer — a gap neither document anticipated.

This was raised as an explicit open question during `/spec ET010` and confirmed with the
ticket owner: the expense owner's own reporting manager (`Employee.ManagerId`) should be
authorized to approve/reject it instead.

## Decision

When a `Submitted` expense's owner is itself a `Manager`, that owner's own `ManagerId` is
authorized to approve or reject the expense. Concretely, `ExpenseService.CanReview` reads
`expense.Employee.ManagerId` — the **same single field** already used for the ordinary
case (an `Employee`'s direct manager). No special-cased traversal is needed: because
`Employee.ManagerId` always means "who this person reports to" regardless of that person's
own role, a Manager who owns an expense and their own manager who reviews it are already
covered by the ordinary "expense owner's `ManagerId` == caller" check.

**A materially incorrect earlier draft of this decision, caught and corrected during
`/implement` before merge**: the original design (see `design.md` D2, since corrected)
proposed reading `expense.Employee.Manager?.ManagerId` — the owner's manager's manager —
as a third fallback authorization branch, backed by a `.ThenInclude(emp => emp.Manager)`
EF Core include. That was a genuine bug, not merely unneeded complexity: it would have
authorized an indirect (grandparent) manager to approve/reject an *ordinary* employee's
expense too (e.g., in a chain Alice → Bob → Carol, Carol could act on Alice's expense),
directly contradicting the "direct reports only, no indirect reports" rule the
`expense-visibility` capability (ET009, see `openspec/specs/expense-visibility/spec.md`)
already establishes and this ticket's own spec repeats. The bug was found by re-deriving
the authorization logic against the `manager-expense-review` spec's own scenario text
("the owner's own `ManagerId`") while writing the skip-level integration tests, and was
removed — along with the now-unnecessary `.ThenInclude` — before any test was written
against the buggy behavior.

## Consequences

- `ExpenseService.CanReview`: `expense.EmployeeId == managerId` → `false` (BR-06);
  otherwise `expense.Employee.ManagerId == managerId` → the single authorization check,
  covering both the ordinary and skip-level cases identically.
- `IExpenseRepository.GetByIdWithEmployeeAsync`'s existing `.Include(e => e.Employee)` is
  unchanged — no second hop, no additional join, no schema change.
- **Known, accepted gap**: if the expense owner (a Manager) has a null `ManagerId` (top of
  the reporting hierarchy), no caller can approve/reject that Manager's own expense — it
  remains `Submitted` indefinitely. This is intentional for ET010's scope, not a defect;
  covered by an explicit integration test documenting the gap rather than silently leaving
  it untested.
- Future tickets relying on manager-relationship authorization checks should re-derive
  against the specific spec scenarios rather than assuming "skip-level" implies an extra
  graph hop — as this ADR shows, it did not, for this particular case.
