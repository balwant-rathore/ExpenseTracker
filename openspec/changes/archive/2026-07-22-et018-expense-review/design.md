## Context

ET010–ET012 already implemented and shipped every backend endpoint this ticket's UI needs
(`POST /api/expenses/{id}/{approve,reject,compliance-approve,compliance-reject,reimburse}`,
`POST /api/expenses/search` — all confirmed present, unchanged, and covered by existing backend
tests in `backend/src/Api/Controllers/ExpensesController.cs`). ET018 is a **frontend-only**
change, plus one backend one-line fix (`ADR-0021`). No EF Core migration, no new DTO, no new
`Application`-layer service method.

Current frontend state relevant to this change (read directly, not assumed):
- `ExpenseDetail.tsx` already computes `isOwner` via `expense.employeeNumber ===
  currentUserEmployeeNumber` (the only owner-identifying field `ExpenseResponse` exposes — there
  is no `employeeId` on the DTO) and renders Edit/Submit/Cancel only for the owner, a
  `CancelExpenseDialog`-shaped confirm dialog pattern, and a static "This is a read-only view."
  message for every non-owner regardless of role.
- `EditExpensePage.tsx` calls `useExpense(id)` and renders `ExpenseForm` unconditionally on
  success — no ownership check at all today.
- `expenseApi.ts` / `useCancelExpense.ts` establish the exact mutation pattern this change reuses:
  a thin `apiRequest` wrapper function per endpoint, a `useMutation` hook that reads
  `accessToken` from `useAuthStore` and calls `queryClient.invalidateQueries({ queryKey:
  ['expenses'] })` on success — no optimistic updates (matches `frontend/CLAUDE.md`'s anti-pattern
  guidance).
- `RequireRole` already exists and is the exact mechanism needed to restrict `/finance/search` to
  `Finance` — no new route-guard primitive required.
- Per `docs/SDS.md` §2.3's Feature Ownership Matrix, Finance search is an **Expense** API
  (§5.4 lives under the same "Expense APIs" section as approve/reject/reimburse) — its frontend
  code belongs in `features/expenses/`, not a new `features/finance/` module.

## Goals / Non-Goals

**Goals:**
- Manager/Compliance/Finance can act on an expense from the same detail screen an Employee
  already uses, with no duplicated ownership/review-eligibility logic across components.
- Finance gets a search screen matching `docs/SDS.md` §5.4's distinct filter/pagination contract.
- A non-owner can never see `/expenses/{id}/edit` render as an editable form.
- Frontend/backend can be run as two separate local processes without losing the `Authorization`
  header on redirect.

**Non-Goals:**
- No new backend endpoint, DTO, or business rule — all five workflow actions and search already
  exist and are authoritative server-side.
- No "pending my review" dashboard widget or URL-driven filter preset — Manager/Compliance reuse
  the existing `ExpenseFilters` status dropdown on `/expenses` manually; a preset link would
  require wiring `ExpenseListPage`'s filter state to the URL query string, which nothing in the
  approved spec delta requires.
- No inline reimburse control in the Finance search results table (explicit spec requirement —
  reimbursement happens on the detail screen only, to avoid two implementations of the same
  action).
- No production HTTPS-redirection behavior change — `ADR-0021` only skips the middleware in
  `Development`.

## Decisions

### D1 — Review-action eligibility and ownership checks live in one shared utility, not per-component
**Decision:** Add `frontend/src/features/expenses/utils/reviewEligibility.ts`, exported functions:

```ts
export function isExpenseOwner(
  expense: Pick<ExpenseResponse, 'employeeNumber'>,
  currentUserEmployeeNumber: string | undefined,
): boolean

export type ReviewAction =
  | { type: 'managerApprove' | 'managerReject' }
  | { type: 'complianceApprove' | 'complianceReject' }
  | { type: 'financeReimburse' }

export function getApplicableReviewActions(
  expense: Pick<ExpenseResponse, 'status' | 'category'>,
  role: EmployeeRole | undefined,
  isOwner: boolean,
): ReviewAction[]
```

`getApplicableReviewActions` encodes exactly the eligibility rules from the approved spec delta
(`frontend-expense-review-actions-ui`): Manager → `['managerApprove', 'managerReject']` iff
`role === 'Manager' && !isOwner && expense.status === 'Submitted'`; Compliance →
`['complianceApprove', 'complianceReject']` iff `role === 'ComplianceOfficer' &&
expense.category === 'ClientEntertainment' && expense.status === 'Approved'`; Finance →
`['financeReimburse']` iff `role === 'Finance' && ((expense.category !== 'ClientEntertainment' &&
expense.status === 'Approved') || (expense.category === 'ClientEntertainment' && expense.status
=== 'ComplianceApproved'))`. Returns `[]` otherwise (including for the owner — mutually exclusive
with Edit/Submit/Cancel, per the `frontend-expense-visibility-ui` delta).

**Why:** `frontend/CLAUDE.md`'s explicit anti-pattern: "Don't hardcode role-based UI branching in
multiple components — centralize role checks in one place." Both `ExpenseDetail.tsx` (renders the
actions) and `EditExpensePage.tsx` (needs only the ownership half, for its guard) would otherwise
each reimplement the `employeeNumber` comparison — exactly the kind of drift `AGENTS.md` §13 warns
about ("re-tracing both code paths side by side" is only needed if there are two code paths; there
is one here by construction).
**Alternative considered:** Compute eligibility inline in `ExpenseDetail.tsx` (as `isOwner` already
is today) and duplicate the ownership half in `EditExpensePage.tsx`. Rejected — this is the exact
shape of drift risk the guardrail exists to prevent, for a two-line function.

### D2 — One shared `RejectionDialog`, parameterized by target action
**Decision:** `frontend/src/features/expenses/components/RejectionDialog.tsx`:

```ts
interface RejectionDialogProps {
  expenseId: string
  action: 'managerReject' | 'complianceReject'
  onRejected?: () => void
}
```

Internally calls `useRejectExpense(expenseId)` or `useComplianceReject(expenseId)` based on
`action`. Mirrors `CancelExpenseDialog`'s open/confirm/error-display structure exactly, adding a
`Textarea` bound to a Zod-validated comment field.

**Why:** `manager-expense-review` and `compliance-expense-review` enforce byte-identical
validation (non-empty after trim, ≤500 chars) — the approved spec delta explicitly requires one
component, not two, citing `AGENTS.md` §13's "same as X is a checklist" guardrail.
**Alternative considered:** `ManagerRejectDialog` + `ComplianceRejectDialog` as separate
components. Rejected per the spec delta and the guardrail it cites — two components enforcing an
identical rule are exactly the divergence risk being guarded against.

New Zod schema, `frontend/src/features/expenses/schemas/rejectionCommentSchema.ts`:
```ts
export const rejectionCommentSchema = z.object({
  rejectionComment: z.string().trim().min(1, 'A comment is required').max(500, 'Maximum 500 characters'),
})
export type RejectionCommentInput = z.infer<typeof rejectionCommentSchema>
```
UX-only — mirrors, does not replace, the backend's authoritative check in
`RejectExpenseRequestValidator`.

### D3 — Finance search is a new page in `features/expenses/`, not a new feature module
**Decision:** `frontend/src/pages/FinanceSearchPage.tsx` (route composition, mirrors
`ExpenseListPage.tsx`'s page/pageSize/sortBy/sortDirection `useState` pattern) +
`frontend/src/features/expenses/components/FinanceSearchFilters.tsx` (server-side filter form:
`expenseNumber`, `employeeName`, `category`, `status`, `fromDate`, `toDate`) +
`frontend/src/features/expenses/components/FinanceSearchResults.tsx` (results table, each row a
`Link` to `/expenses/{id}`, no inline action) + `frontend/src/features/expenses/api/useFinanceSearch.ts`.

**Why:** `docs/SDS.md` §2.3 places Finance search's backend under the same "Expense APIs" section
(§5.4) as every other expense endpoint, and the Feature Ownership Matrix maps the whole Expenses
feature to one frontend folder. A new `features/finance/` module would fragment one feature across
two folders for no architectural reason.
**Alternative considered:** Reuse `ExpenseFilters`/`ExpenseList` components with a `mode` prop
switching between client-side (`/expenses`) and server-side (`/finance/search`) filtering.
Rejected — `ExpenseFilters` narrows the *already-fetched page* client-side (per
`frontend-expense-visibility-ui`), while Finance search filters narrow the *server query itself*
with a different allowed-value set (`pageSize`: 20/50/100/500 vs. 10/20/50/100) and different
fields (`expenseNumber`, `employeeName` have no equivalent in the general list) — forcing one
component to branch between two genuinely different contracts is more complex than two small,
separately testable components.

New types in `frontend/src/types/expense.ts`:
```ts
export interface FinanceSearchParams {
  expenseNumber?: string
  employeeName?: string
  category?: ExpenseCategory
  status?: ExpenseStatus
  fromDate?: string
  toDate?: string
  page?: number
  pageSize?: 20 | 50 | 100 | 500
  sortBy?: ExpenseSortField
  sortDirection?: 'asc' | 'desc'
}
```
(Deliberately a distinct type from `ExpenseListParams` — different `pageSize` union — not a
shared/extended type, so a future change to one endpoint's allowed values can't silently loosen
the other's.)

### D4 — Ownership guard redirects post-fetch, inside `EditExpensePage`, not a new route wrapper
**Decision:** `EditExpensePage.tsx` computes `isExpenseOwner(data.expense, user?.employeeNumber)`
(D1's shared util) once `useExpense` resolves, and renders `<Navigate to={`/expenses/${id}`}
replace />` instead of `ExpenseForm` when false — added as one more branch alongside the existing
`isLoading`/`isError` branches, not a new `RequireOwnership` wrapper component.

**Why:** Ownership can only be known after the expense loads (unlike `RequireRole`'s role check,
which is available synchronously from the auth store) — a wrapper component would need to
duplicate the same `useExpense` call `EditExpensePage` already makes, fetching the expense twice.
**Alternative considered:** A generic `<RequireOwnership expense={...}>` wrapper, analogous to
`RequireRole`. Rejected — `RequireRole` wraps a route *before* any data fetch; here the guard needs
data the page itself already fetches, so the natural place is a branch in the page, not a
route-level wrapper around it.

### D5 — Backend fix scoped to `Program.cs`, no `vite.config.ts` change (`ADR-0021`)
**Decision:** `if (!app.Environment.IsDevelopment()) { app.UseHttpsRedirection(); }` in
`backend/src/Api/Program.cs`, replacing the current unconditional call. No change to
`frontend/vite.config.ts` — its existing `http://localhost:5158` proxy target starts working
correctly once that port stops redirecting away.
**Why / Alternatives:** Full reasoning and rejected alternatives (pointing the proxy at the HTTPS
port; reimplementing `fetch` without automatic redirect-following) are in `ADR-0021`, written
during `/spec` per this repo's convention of logging any FRS/SDS-literal-text deviation as an ADR
at proposal time.

## File-Level Change List

**Backend**
- `backend/src/Api/Program.cs` — modify: guard `UseHttpsRedirection()` (D5/`ADR-0021`).
- `backend/tests/IntegrationTests/HttpsRedirectionTests.cs` — new: `WebApplicationFactory<Program>`
  already defaults to `Development` (see `OpenApiDocumentationTests`, which relies on that for its
  `/scalar`/`/openapi` routes); this test configures `ASPNETCORE_HTTPS_PORT` explicitly so
  `UseHttpsRedirection` has a determinable port to redirect to, then asserts no `307` comes back —
  pinning the guard rather than a no-op (see `ADR-0021` Consequences for why an unconfigured port
  would pass either way).

**Frontend — new files**
- `features/expenses/utils/reviewEligibility.ts` + `.test.ts` (D1)
- `features/expenses/schemas/rejectionCommentSchema.ts` + `.test.ts` (D2)
- `features/expenses/components/RejectionDialog.tsx` + `.test.tsx` (D2)
- `features/expenses/api/useApproveExpense.ts`, `useRejectExpense.ts`,
  `useComplianceApprove.ts`, `useComplianceReject.ts`, `useReimburseExpense.ts` — one file each,
  matching `useCancelExpense.ts`'s existing one-hook-per-file convention
- `features/expenses/api/useFinanceSearch.ts` (D3)
- `features/expenses/components/FinanceSearchFilters.tsx` + `.test.tsx` (D3)
- `features/expenses/components/FinanceSearchResults.tsx` + `.test.tsx` (D3)
- `pages/FinanceSearchPage.tsx` + `.test.tsx` (D3)

**Frontend — modified files**
- `features/expenses/api/expenseApi.ts` — add `approveExpense`, `rejectExpense`,
  `complianceApproveExpense`, `complianceRejectExpense`, `reimburseExpense` (all
  `POST /expenses/{id}/...`, no body except the two reject variants' `{ rejectionComment }`), and
  `searchExpenses(params, accessToken): Promise<PagedExpenseResponse>` → `POST /expenses/search`.
- `features/expenses/components/ExpenseDetail.tsx` + its `.test.tsx` — replace the static
  non-owner read-only branch with `getApplicableReviewActions(...)`-driven rendering of the
  matching action button(s)/`RejectionDialog`.
- `pages/EditExpensePage.tsx` + its test — add the D4 ownership redirect.
- `routes/AppRouter.tsx` — add `{ path: '/finance/search', element: <RequireRole
  allowedRoles={['Finance']}><FinanceSearchPage /></RequireRole> }` under the existing
  `ProtectedRoute`/`AppLayout` branch.
- `layouts/AppLayout.tsx` — add a "Finance Search" nav link, rendered only when
  `useAuthStore((s) => s.user?.role) === 'Finance'`.
- `types/expense.ts` — add `FinanceSearchParams` (D3).

**E2E (repo root `e2e/`)**
- `e2e/06-expense-manager-team-view.spec.ts` — rewritten: the pre-ET018 assertion ("non-owner
  always sees a read-only view") is no longer true for a Manager on a direct report's `Submitted`
  expense, so this file now covers Approve, Reject-with-comment, the edit-route ownership guard,
  and a Manager's own expense showing no review action — one shared `beforeAll` login, not
  `beforeEach`, to stay within the auth endpoints' rate limit.
- `e2e/07-expense-compliance-and-finance.spec.ts` — new: the full `ClientEntertainment` chain
  (Employee submits → Manager approves → Compliance approves → Finance reimburses →
  `/finance/search` finds it → a non-Finance role is redirected away from that route).
- `e2e/expenseTestData.ts` — add reserved `EXPENSE_E2E_COMPLIANCE_*` (`EMP001`) and
  `EXPENSE_E2E_FINANCE_*` (`EMP004`) constants, following the file's existing freshness-check
  convention (`EMP002`/`EMP003` turned out to already have a Users row from prior manual testing).

**No changes:** any backend `Application`/`Domain`/`Infrastructure` file, any EF Core migration,
`frontend/vite.config.ts`, `frontend/src/lib/apiClient.ts`.

## Risks / Trade-offs

- **[Risk]** `ExpenseDetail.tsx` accumulates five action branches (Edit/Submit/Cancel plus five
  review actions) → **Mitigation:** all eligibility branching is a single
  `getApplicableReviewActions` call (D1); the component itself only maps the returned action list
  to buttons, keeping its own logic flat.
- **[Risk]** A future endpoint could redefine "eligible" differently than
  `getApplicableReviewActions` assumes, silently drifting from the backend's authoritative check
  → **Mitigation:** the backend remains authoritative regardless (a stale frontend eligibility
  check only hides/shows a button; the endpoint still enforces its own rule and returns `403`/`422`
  either way, surfaced per the spec delta's "Backend rejection is surfaced" scenarios).
- **[Risk]** Disabling `UseHttpsRedirection` in Development could mask a real HTTPS-only bug that
  would only surface in Production → **Mitigation:** the guard is Development-only; Test and
  Production environments keep the middleware unconditionally, so any environment that actually
  matters for HTTPS enforcement is unaffected — see `ADR-0021`'s Consequences.

## Migration Plan

No data migration. Deploy order: backend `Program.cs` change ships independently of the frontend
(pure Development-only middleware change, safe to deploy anytime); frontend changes ship as one PR
per usual ET-ticket flow. Rollback: revert either commit independently — neither depends on schema
or data state.

## Open Questions

None outstanding — all decisions above were confirmed with the ticket owner during `/spec` (D1–D5
map directly to the four clarifying questions answered there) and `ADR-0021` is filed.

## Checkpoints

Run in this order; stop and fix at the first failure (`CLAUDE.md` Quality Gates):

**Backend** (from `backend/`):
```bash
dotnet build
dotnet test --filter FullyQualifiedName~IntegrationTests
```

**Frontend** (from `frontend/`):
```bash
pnpm lint
pnpm exec tsc --noEmit
pnpm build
pnpm test
```

**E2E** (from repo root, since this ticket touches user-facing review flows):
```bash
npx playwright test
```
