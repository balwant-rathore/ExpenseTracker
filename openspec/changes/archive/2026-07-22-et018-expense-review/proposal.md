## Why

ET010–ET012 already built and shipped the Manager, Compliance, and Finance backend workflow
endpoints (`docs/FRS.md` §5, §7), but no frontend surface calls them yet —
`frontend-expense-visibility-ui`'s detail screen explicitly deferred approve/reject/reimburse
controls to this ticket. ET018 (`docs/TICKETS.md`) closes that gap, and also carries over an
ET017 finding: `EditExpensePage` renders the edit form for any fetched expense before checking
ownership, relying only on Edit-link visibility and the backend's authoritative `403` to keep a
non-owner from seeing the form rendered as editable. Separately, running the frontend (Vite dev
server) and backend (Kestrel) as two independent local processes currently produces a redirect
that silently drops the `Authorization` header — folded into this change per the ticket owner's
direction during `/spec`, since it blocks manually testing everything else in this ticket.

## What Changes

- Add Manager approve/reject actions to the existing `ExpenseDetail` component, visible only to
  the caller's direct manager (or skip-level, per `ADR-0012`) viewing a `Submitted` expense they
  don't own (`docs/FRS.md` §5.1, `manager-expense-review` capability).
- Add Compliance Officer approve/reject actions to the same `ExpenseDetail` component, visible to
  any `ComplianceOfficer` viewing an `Approved` `ClientEntertainment` expense (`docs/FRS.md` §5.2,
  `compliance-expense-review` capability).
- Add a Finance reimburse action to `ExpenseDetail`, visible to any `Finance` caller viewing an
  expense eligible per the category-conditioned precondition (`docs/FRS.md` §7.1.3,
  `expense-reimbursement` capability).
- All three reuse one rejection-comment dialog component (parameterized by target endpoint —
  `reject` vs `compliance-reject`) since both enforce an identical mandatory-comment rule
  (`manager-expense-review` and `compliance-expense-review` capabilities' validation
  requirements) — one component, not two copies that can drift.
- Add a Finance-only search page (`/finance/search`) built on `POST /api/expenses/search`
  (`docs/FRS.md` §7.1.1, `finance-expense-search` capability), separate from the general
  `/expenses` list, since it exposes different filters (`expenseNumber`, `employeeName`, date
  range) and explicitly excludes `Draft` expenses even when filtered for.
- Reuse the existing `GET /api/expenses` `status` query filter (already supported per
  `expense-visibility`) for a Manager/Compliance "awaiting my review" view — no new list endpoint
  or list-screen capability needed.
- Fix: add a route-level ownership guard on `/expenses/:id/edit` — a non-owner is redirected to
  `/expenses/:id` (the read-only detail view they're already permitted to see) instead of the edit
  form ever rendering.
- Fix: stop the backend's `UseHttpsRedirection()` middleware from issuing a cross-origin `307` to
  the frontend when the two run as separate local dev processes, which causes the browser to drop
  the `Authorization` header on the redirected request (browsers never forward `Authorization`
  across an origin change) and the request to land on a port the CORS policy doesn't allow — see
  `ADR-0021`.

## Capabilities

### New Capabilities
- `frontend-expense-review-actions-ui`: Manager/Compliance/Finance action controls (approve,
  reject, compliance-approve, compliance-reject, reimburse) surfaced on the existing expense
  detail screen, gated by the caller's role and the expense's current status/category.
- `frontend-finance-search-ui`: Finance-only search screen over `POST /api/expenses/search`,
  including its filters, pagination/sorting, and the reimburse action on each result.

### Modified Capabilities
- `frontend-expense-visibility-ui`: the "Action Visibility on Detail Reflects Ownership and
  Status" requirement changes — a non-owner with an applicable review role now sees role-specific
  action controls instead of a pure read-only view (still read-only for a non-owner with no
  applicable role/status match).
- `frontend-expense-maintenance-ui`: adds a route-level ownership check to the Expense Edit Form
  requirement — a non-owner navigating directly to the edit route is redirected away rather than
  ever seeing the form render.

## Impact

- **Frontend**: `ExpenseDetail.tsx` (new action buttons + role checks), new `RejectionDialog`
  component (generalizing `CancelExpenseDialog`'s confirm-dialog pattern), new
  `useApproveExpense`/`useRejectExpense`/`useComplianceApprove`/`useComplianceReject`/
  `useReimburseExpense` TanStack Query hooks, new `FinanceSearchPage` + `useFinanceSearch` hook +
  `financeSearch` API call, new route `/finance/search` (restricted to `Finance` via
  `RequireRole`), `EditExpensePage.tsx` (ownership check before render), `AppRouter.tsx` (new
  route).
- **Backend**: no functional change — `manager-expense-review`, `compliance-expense-review`,
  `expense-reimbursement`, and `finance-expense-search` endpoints already exist and are unchanged.
  Only `Program.cs`'s `UseHttpsRedirection()` call is affected (see `ADR-0021`); a new integration
  test pins the Development-only skip.
- **Dev tooling**: `frontend/vite.config.ts`'s proxy config is unaffected — per `ADR-0021`, its
  existing hardcoded `http://localhost:5158` target keeps working once the backend stops
  redirecting that port away in Development.
- **No schema change, no new capability on the backend, no new ExpenseCategory/ExpenseStatus
  values.**
