# ADR-0011: BR-06 Blocks Both Approve and Reject on a Manager's Own Expense

## Status

Accepted

## Context

`docs/FRS.md` §5.1.6 states, literally: "**BR-06**: Managers cannot approve their own
expenses" — naming only the Approve action. The BR-06 glossary entry (`docs/FRS.md` §11)
repeats the same narrower wording: "Managers cannot approve their own expenses."

However, root `AGENTS.md` §11 (a summary document, not one of the two source-of-truth
specs) states the rule more broadly: "Managers cannot approve/reject their own expenses
(BR-06)" — explicitly including Reject.

This was raised as an explicit ambiguity during `/spec ET010`, since the two authoritative
documents (FRS glossary and FRS §5.1.6) disagree with the summary (`AGENTS.md`) on whether
Reject is covered. Left unresolved, a Manager could reject their own `Submitted` expense
outright (an unusual but literally-permitted self-adjudication under the narrower reading),
which contradicts the spirit of BR-06's purpose: preventing a Manager from having any
review authority — positive or negative — over their own expense.

## Decision

BR-06 blocks a Manager from both approving *and* rejecting their own expense, per
`AGENTS.md` §11's broader phrasing, confirmed with the ticket owner during `/spec ET010`.
Both `POST /api/expenses/{id}/approve` and `POST /api/expenses/{id}/reject` return `403
AUTHORIZATION_FAILED` (`ExpenseFailureReason.NotAuthorizedReviewer`) when the caller is the
expense's own owner, regardless of which action was requested.

This is a deliberate, logged deviation from the literal text of `docs/FRS.md` §5.1.6 and
§11 — not a silent implementation choice, per this repository's OpenSpec proposal rule
requiring any deviation from FRS/SDS wording to be captured as an ADR. `docs/FRS.md` is
left unedited as the historical record of the original requirement (the same convention
ADR-0008 and ADR-0010 established); this ADR, along with the `manager-expense-review`
capability spec's "Manager Self-Review Restriction (BR-06)" requirement, is the
authoritative statement of the actual implemented behavior.

Rejected alternative: block only Approve, matching FRS §5.1.6 literally, and allow a
Manager to reject their own expense. Rejected by the ticket owner — there is no scenario
where a Manager legitimately needs to reject (as opposed to simply cancelling) their own
expense, and permitting it would be an unnecessary, unused capability that only invites
confusion about whether self-review is or isn't allowed.

## Consequences

- `ExpenseService.CanReview` checks `expense.EmployeeId == managerId` and returns `false`
  for both `ApproveAsync` and `RejectAsync` via one shared private helper — not duplicated
  per-action logic (`AGENTS.md` §13's "handled identically" guardrail).
- No schema change: this is purely an authorization-check breadth decision.
- Future tickets (and anyone reading `docs/FRS.md` §5.1.6/§11 literally) should treat this
  ADR and the `manager-expense-review` capability spec as authoritative over the original
  FRS wording for BR-06's scope specifically — no other BR-06-adjacent behavior changes.
