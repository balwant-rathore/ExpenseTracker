# ADR-0013: Optional `status` Query Filter on `GET /api/expenses`

## Status

Accepted

## Context

`docs/FRS.md` §5.1.1 requires that "managers ... view expenses `Submitted` by employees
reporting to them." ET009's `GET /api/expenses` (the `expense-visibility` capability)
already returns a Manager's own expenses merged with their direct reports' non-`Draft`
expenses, but exposes no way to narrow that list to a single `Status` — a Manager wanting
only their pending approvals would have to page through and filter client-side across
every status. Only the Finance-only `POST /api/expenses/search` endpoint (`docs/SDS.md`
§5.4) has status filtering today.

This was raised as an explicit open question during `/spec ET010` and confirmed with the
ticket owner: add a `status` query filter to the general list endpoint rather than
building a separate "pending approvals" endpoint or leaving Managers to filter
client-side.

## Decision

`GET /api/expenses` accepts an optional `status` query parameter (one of the seven
`ExpenseStatus` values). When present, it narrows the caller's already-visible set (per
their role's existing default visibility rules, unchanged) to only expenses matching that
`Status`. An invalid value returns `400 VALIDATION_ERROR`. Omitting the parameter
reproduces the exact pre-ET010 behavior — this is a purely additive, backward-compatible
change to an existing endpoint, not a new endpoint or a change to any role's visibility
rules.

This is a deliberate, logged deviation from ET009's originally literal scope (`docs/FRS.md`
§4.4, `docs/SDS.md` §5.2) — not a silent implementation choice, per this repository's
OpenSpec proposal rule requiring any deviation from FRS/SDS wording to be captured as an
ADR. The `expense-visibility` capability's new "Optional Status Filter on the Expense List
Endpoint" requirement (added via this ticket's spec delta) is the authoritative statement
of the actual implemented behavior.

Rejected alternative: a dedicated `GET /api/expenses/pending-approvals` endpoint. Rejected
by the ticket owner as an unnecessary new endpoint/duplicate visibility logic when the
existing list endpoint's per-role visibility predicate already computes the correct base
set — a filter parameter reuses that predicate directly rather than reimplementing it.

## Consequences

- `ExpenseListRequest` gains an optional `Status` (`string?`) property; `ExpenseListRequestValidator`
  validates it via the same `Enum.TryParse<T>` idiom already used for `Category`/`Action`
  in `CreateExpenseRequestValidator`.
- `IExpenseRepository.GetPagedAsync` gains an `ExpenseStatus? statusFilter` parameter,
  applied as an additional `.Where(e => e.Status == statusFilter.Value)` immediately after
  the existing visibility predicate — the filter can only narrow, never expand, what a
  role is already permitted to see.
- No schema change, no new endpoint, no change to any role's default visibility rules.
- Every other role (`Employee`, `Finance`, `ComplianceOfficer`) can also use `?status=`,
  not only `Manager` — the filter is generic to the list endpoint, not Manager-specific,
  even though FRS §5.1.1 was the motivating use case.
