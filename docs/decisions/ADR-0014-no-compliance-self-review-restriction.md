# ADR-0014: No BR-06-Equivalent Self-Review Restriction for Compliance Officer

## Status

Accepted

## Context

ET010's Manager review endpoints enforce BR-06 (a Manager cannot approve/reject their own
expense, `ADR-0011`). `docs/FRS.md` §5.2 (Compliance Officer Review) has no equivalent rule,
but the question was raised explicitly during `/spec ET011` since ET011's endpoints are
structurally analogous to ET010's.

## Decision

No self-review guard is implemented for `compliance-approve`/`compliance-reject`. Confirmed
with the ticket owner: this is unattainable, not merely undecided. `docs/SDS.md` §6.4's
Authorization Matrix permits only `Employee`/`Manager` to create expenses — a
`ComplianceOfficer` can never own an expense, so no code path can ever hit a self-review case.

## Consequences

- No `CanComplianceReview` branch checks `expense.EmployeeId == complianceOfficerId` (contrast
  with Manager's `CanReview`, which does check this for BR-06).
- If a future ticket ever allows Compliance Officers to create expenses, this decision must be
  revisited.
