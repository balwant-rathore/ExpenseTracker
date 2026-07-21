# ADR-0015: Category-Conditioned Reimbursement Precondition

## Status

Accepted

## Context

`docs/FRS.md` §7.1.3 states: "**BR-08** compliant. Mark only `Approved` or `Compliance
Approved` expenses as `Reimbursed`" — read literally, this permits reimbursing *any*
expense sitting at either status, regardless of category. But `docs/SDS.md` §6.1/§6.3
define two distinct workflow paths: non-`ClientEntertainment` expenses go
`Approved → Reimbursed`, while `ClientEntertainment` expenses must go
`Approved → ComplianceApproved → Reimbursed`. Taken literally, FRS §7.1.3 would let
Finance reimburse a `ClientEntertainment` expense that is merely `Approved` — skipping
Compliance Officer review entirely, which contradicts the whole point of the special
workflow path.

This conflict was raised explicitly during `/spec ET012`.

## Decision

`POST /api/expenses/{id}/reimburse`'s eligibility check is category-conditioned:

- `ClientEntertainment` expenses require `Status == ComplianceApproved`.
- Every other category requires `Status == Approved`.

Any other combination (including a `ClientEntertainment` expense still at `Approved`,
or a non-`ClientEntertainment` expense somehow at `ComplianceApproved`) is rejected with
`422 BUSINESS_RULE_VIOLATION`. Confirmed with the ticket owner during `/spec`: the SDS
workflow diagram governs over FRS §7.1.3's literal, category-agnostic wording.

Implemented as `ExpenseService.IsEligibleForReimbursement`
(`backend/src/Application/Expenses/ExpenseService.cs`).

## Consequences

- A `ClientEntertainment` expense can never be reimbursed while skipping Compliance
  Officer review, even though FRS §7.1.3's literal text would have allowed it.
- The `NotEligibleForReimbursement` failure reason covers both "wrong status for this
  category" and ordinary terminal/non-terminal status rejections (e.g. `Submitted`,
  `Rejected`, `Cancelled`, already-`Reimbursed`) with a single check and message, rather
  than a category check and a status check reported separately.
- If a future ticket changes which categories require Compliance review, this predicate
  is the single place that needs updating.
