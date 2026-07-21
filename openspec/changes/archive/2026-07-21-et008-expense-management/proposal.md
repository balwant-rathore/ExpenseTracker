## Why

ET007 delivered expense creation (Draft/Submit) but nothing lets an employee or manager
correct a mistake, fetch a single expense back for editing, or walk away from an expense
they no longer want to pursue. `docs/FRS.md` §4.2 (Expense Edit), §4.3 (Expense Cancel), and
§4.4 (Expense View, single-resource scope only — list/search/pagination stays with ET009 per
`docs/TICKETS.md`) require this now so the Draft/Submitted lifecycle started in ET007 has a
way to be maintained rather than only ever created.

## What Changes

- Add `GET /api/expenses/{id}` — returns a single expense to its owner, enforcing the same
  ownership rule edit/cancel use (`docs/FRS.md` §4.4.1, `docs/SDS.md` §5.2). Broader
  role-based visibility (Manager/Finance/Compliance views) remains ET009's scope.
- Add `PUT /api/expenses/{id}` — edits a `Draft` or `Submitted` expense owned by the caller
  (**BR-04**, `docs/FRS.md` §4.2.1–4.2.2). Re-runs the identical field-level and
  business-rule validation as create/submit (BR-01, BR-02, BR-03, category/currency/
  description-length), matching ET007's "Draft is not a relaxed-validation state"
  precedent. Allows replacing `receiptAttachmentId` with a different attachment, re-running
  the same not-already-linked/ownership checks ET007 uses at creation. Never changes
  `Status` — status transitions stay exclusively on their dedicated action endpoints
  (`docs/SDS.md` §1.3, §6.3 lists no Edit-triggered transition).
- Add `POST /api/expenses/{id}/cancel` — cancels a `Draft` or `Submitted` expense owned by
  the caller, setting `Status = Cancelled` (**BR-05**, `docs/FRS.md` §4.3). This is a
  **deviation from the literal text of `docs/FRS.md` §4.3.1 and `docs/SDS.md` §6.3**, both of
  which restrict cancellation to `Submitted` only — extending it to `Draft` follows root
  `AGENTS.md` §9's workflow summary ("Cancellation flow: `Draft` or `Submitted` →
  `Cancelled`") per explicit ticket-owner direction gathered during `/spec`. Logged as an
  open architecture decision requiring an ADR in `docs/decisions/` (not a silent choice) —
  see `design.md`.
- Enforce read-only status for every non-`Draft`/non-`Submitted` expense on both new
  mutation endpoints — `Approved`, `ComplianceApproved`, `Rejected`, `Cancelled`, and
  `Reimbursed` are all rejected identically (**BR-04**, **BR-07**, `docs/FRS.md` §4.2.2,
  §4.4.5). BR-07 ("Rejected expenses are read-only") is one instance of this broader
  BR-04 rule, not a separate check.
- Ownership validation on all three endpoints: the caller's `EmployeeId` (already resolved
  via ET007's `GetEmployeeId()` claim) must match `Expense.EmployeeId`, or the request is
  rejected — Managers may act only on their own expenses, not their team's (`docs/SDS.md`
  §6.4 Authorization Matrix).

## Capabilities

### New Capabilities
- `expense-maintenance`: single-expense retrieval, edit (with re-validation and attachment
  replacement), cancellation, and read-only enforcement for non-editable expense states.

### Modified Capabilities
(none — no existing capability's requirements change; `expense-submission` (create/submit)
and `domain-model` (schema) are consumed as-is, not altered)

## Impact

- **Api**: new `ExpensesController` actions — `GET {id}`, `PUT {id}`, `POST {id}/cancel` —
  added to the controller ET007 created.
- **Application**: `ExpenseService` gains `GetByIdAsync`, `UpdateAsync`, `CancelAsync`;
  reuses `IExpenseRepository`, `IAttachmentRepository`, `ExpenseFailureReason`/`Result`
  idiom, and `ICompanyClock` from ET007. New failure reasons for the not-editable/
  not-cancellable cases.
- **Domain/Infrastructure**: no schema changes — `ExpenseStatus.Cancelled` already exists
  (ET002); attachment swap only updates the existing `Expense.AttachmentId` FK.
- **Docs**: one new ADR in `docs/decisions/` documenting the Draft-cancellation deviation
  from FRS §4.3.1/SDS §6.3, and a correction noted against those sections' literal text.
- No frontend changes in this ticket (backend-only, per `docs/TICKETS.md` ET008 scope).
