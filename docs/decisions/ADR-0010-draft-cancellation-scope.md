# ADR-0010: Cancellation Scope Includes Draft Expenses

## Status

Accepted

## Context

`docs/FRS.md` §4.3.1 states: "The system shall allow employees to cancel their own pending
(`Submitted`) expenses" — restricting `Cancel` to `Submitted` only. `docs/SDS.md` §6.3's state
transition matrix agrees: its only `Cancel` row is `Submitted | Cancel | Expense Owner |
Cancelled*`, with no equivalent row for `Draft`.

However, root `AGENTS.md` §9 (a summary document, not one of the two source-of-truth specs)
describes the cancellation flow more broadly: "Cancellation flow: `Draft` or `Submitted` →
`Cancelled`." This was raised as an explicit ambiguity during `/spec ET008`, since the two
authoritative documents (FRS, SDS) disagree with the summary (`AGENTS.md`) on this one point.

Left unresolved, a `Draft` expense that an employee decides not to pursue has no terminal state
to move to — it would sit in `Draft` indefinitely with no way to be closed out, since `Draft`
also cannot be deleted (no delete endpoint exists or is planned; deletion is out of this
project's scope entirely).

## Decision

Cancellation applies to both `Draft` and `Submitted` expenses, per `AGENTS.md` §9's broader
phrasing, confirmed with the ticket owner during `/spec ET008`. `POST
/api/expenses/{id}/cancel` transitions either status to `Cancelled`; every other status
(`Approved`, `ComplianceApproved`, `Rejected`, `Reimbursed`, and an already-`Cancelled` expense)
is rejected with `422 BUSINESS_RULE_VIOLATION` (`ExpenseFailureReason.NotCancellable`).

This is a deliberate, logged deviation from the literal text of `docs/FRS.md` §4.3.1 and
`docs/SDS.md` §6.3 — not a silent implementation choice, per this repository's OpenSpec
proposal rule requiring any deviation from `docs/SDS.md` to be captured as an ADR. `docs/FRS.md`
and `docs/SDS.md` are left unedited as the historical record of the original requirement (the
same convention ADR-0008 established when `docs/SDS.md` §3.7 went out of date); this ADR, along
with `specs/expense-maintenance/spec.md`'s "Expense Cancellation Endpoint" requirement, is the
authoritative statement of the actual implemented behavior.

Rejected alternative: restrict `Cancel` to `Submitted` only, matching FRS/SDS literally, and
leave `Draft` expenses with no way to be closed out. Rejected by the ticket owner — an employee
should be able to abandon a saved-but-never-submitted expense outright, and there is no other
mechanism (no delete endpoint) that would let them do so otherwise.

## Consequences

- `ExpenseService.CancelAsync` checks `Status is ExpenseStatus.Draft or ExpenseStatus.Submitted`
  rather than `Status == ExpenseStatus.Submitted`.
- No schema change: `ExpenseStatus.Cancelled` already exists (ET002); only the *eligibility*
  check widens.
- Future tickets (and anyone reading FRS §4.3.1/SDS §6.3 literally) should treat this ADR and
  the `expense-maintenance` capability spec as authoritative over the original FRS/SDS wording
  for cancellation scope specifically — no other cancellation-adjacent behavior changes.
