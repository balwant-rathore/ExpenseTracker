**Scope note**: ET018 is almost entirely frontend (`docs/TICKETS.md` Domain = Frontend). The one
backend task (1.1–1.2) is independent of every frontend task — it touches only `Program.cs` and a
new integration test, no shared file — so it is marked `[PARALLEL]` and should run in its own
worktree per `CLAUDE.md`'s "frontend and backend work must run in separate worktrees" rule.

## 1. Foundation: Backend Fix, Shared Frontend Utils, and Type/Schema Contracts

- [x] 1.1 [PARALLEL] Guard `app.UseHttpsRedirection()` in `backend/src/Api/Program.cs` behind
      `if (!app.Environment.IsDevelopment())` (design.md D5, `ADR-0021`)
- [x] 1.2 [PARALLEL] New integration test `backend/tests/IntegrationTests/HttpsRedirectionTests.cs`:
      `WebApplicationFactory<Program>` (already defaults to `Development`, per
      `OpenApiDocumentationTests`) with `ASPNETCORE_HTTPS_PORT` configured explicitly asserts a
      plain-HTTP request does **not** receive a `307` (`ADR-0021` Consequences)
- [x] 1.3 Add `FinanceSearchParams` interface to `frontend/src/types/expense.ts` (design.md D3) —
      deliberately not shared/extended from `ExpenseListParams` (distinct `pageSize` union: 20/50/
      100/500 vs. 10/20/50/100)
- [x] 1.4 Add `frontend/src/features/expenses/utils/reviewEligibility.ts`: `isExpenseOwner(expense,
      currentUserEmployeeNumber)` and `getApplicableReviewActions(expense, role, isOwner)` per
      design.md D1's exact eligibility rules
- [x] 1.5 Unit tests `reviewEligibility.test.ts`: `isExpenseOwner` true/false; `getApplicableReviewActions`
      returns the correct action list for every role × status/category combination named in D1
      (Manager/Submitted/non-owner, Manager/other-status, Compliance/Approved+ClientEntertainment,
      Compliance/other-category, Compliance/other-status, Finance/Approved-non-CE,
      Finance/ComplianceApproved-CE, Finance/Approved-CE (ineligible), owner (always `[]`),
      no-role/undefined)
- [x] 1.6 Add `frontend/src/features/expenses/schemas/rejectionCommentSchema.ts` (Zod: `trim().min(1).max(500)`,
      design.md D2)
- [x] 1.7 Unit tests `rejectionCommentSchema.test.ts`: rejects empty, whitespace-only, and >500-char
      input; accepts a valid trimmed comment
- [x] 1.8 Add `approveExpense`, `rejectExpense`, `complianceApproveExpense`, `complianceRejectExpense`,
      `reimburseExpense`, and `searchExpenses` functions to `frontend/src/features/expenses/api/expenseApi.ts`
      (design.md File-Level Change List — thin `apiRequest` wrappers, matching the existing
      `cancelExpense`/`submitExpense` shape)

**Checkpoint 1**: `dotnet build` → 0 errors (backend); `pnpm exec tsc --noEmit` (frontend) → 0
errors; `pnpm test` (frontend, new files only) → all green.

## 2. Core Implementation: Hooks, Shared Dialog, Finance Search Components [PARALLEL-safe within frontend]

- [x] 2.1 Add `useApproveExpense(id)`, `useRejectExpense(id)`, `useComplianceApprove(id)`,
      `useComplianceReject(id)`, `useReimburseExpense(id)` — one file each in
      `frontend/src/features/expenses/api/`, mirroring `useCancelExpense.ts`'s
      `useMutation` + `invalidateQueries(['expenses'])` pattern
- [x] 2.2 Add `useFinanceSearch(params: FinanceSearchParams)` in
      `frontend/src/features/expenses/api/useFinanceSearch.ts`, mirroring `useExpenseList.ts`'s
      `useQuery` pattern with `queryKey: ['expenses', 'search', params]`
- [x] 2.3 Add `frontend/src/features/expenses/components/RejectionDialog.tsx` (design.md D2):
      `{ expenseId, action: 'managerReject' | 'complianceReject', onRejected? }`, RHF + Zod-bound
      comment field, mirrors `CancelExpenseDialog`'s open/confirm/error structure, disabled submit
      until valid
- [x] 2.4 Component test `RejectionDialog.test.tsx`: submit disabled on empty/whitespace comment;
      `action="managerReject"` calls `useRejectExpense`; `action="complianceReject"` calls
      `useComplianceReject`; error from the mutation is displayed without closing the dialog
- [x] 2.5 Add `frontend/src/features/expenses/components/FinanceSearchFilters.tsx`: form for
      `expenseNumber`, `employeeName`, `category`, `status`, `fromDate`/`toDate`, emitting a
      `FinanceSearchParams`-shaped change (design.md D3)
- [x] 2.6 Component test `FinanceSearchFilters.test.tsx`: each filter field individually produces
      the correct param on change; combining category + status produces both in one emitted value;
      clearing all filters emits an empty params object
- [x] 2.7 Add `frontend/src/features/expenses/components/FinanceSearchResults.tsx`: paginated
      results table, each row a `Link` to `/expenses/{id}`, **no** inline reimburse control
      (design.md D3, spec's "Reimbursement Happens on the Detail Screen" requirement)
- [x] 2.8 Component test `FinanceSearchResults.test.tsx`: renders `items`/`page`/`pageSize`/
      `totalRecords`; row navigates to the expense's detail route; asserts no "Reimburse" button
      renders anywhere in the table
- [x] 2.9 Add `frontend/src/pages/FinanceSearchPage.tsx`: composes `FinanceSearchFilters` +
      `FinanceSearchResults` + `useFinanceSearch`, page/pageSize/sortBy/sortDirection `useState`
      mirroring `ExpenseListPage.tsx`'s existing pattern
- [x] 2.10 Page test `FinanceSearchPage.test.tsx`: initial render calls `searchExpenses` with an
      empty filter body (defaults); changing page size/sortBy/sortDirection re-issues the search
      with updated values; only 20/50/100/500 are offered as page-size options

**Checkpoint 2**: `pnpm exec tsc --noEmit` → 0 errors; `pnpm lint -- --max-warnings 0` → 0
warnings; `pnpm test` → all green.

## 3. Integration: Detail Screen Actions, Edit Ownership Guard, Routing, Nav

- [x] 3.1 Modify `frontend/src/features/expenses/components/ExpenseDetail.tsx`: replace the static
      "This is a read-only view." non-owner branch with `getApplicableReviewActions(...)`-driven
      rendering — Manager Approve button + `RejectionDialog action="managerReject"`; Compliance
      Approve button + `RejectionDialog action="complianceReject"`; Finance Reimburse button; still
      falls back to the read-only message when the action list is empty
- [x] 3.2 Modify `frontend/src/pages/EditExpensePage.tsx`: after `useExpense` resolves, compute
      `isExpenseOwner(data.expense, user?.employeeNumber)` (reading `user` from `useAuthStore`) and
      render `<Navigate to={`/expenses/${id}`} replace />` instead of `ExpenseForm` when false
      (design.md D4)
- [x] 3.3 Add route `{ path: '/finance/search', element: <RequireRole
      allowedRoles={['Finance']}><FinanceSearchPage /></RequireRole> }` to
      `frontend/src/routes/AppRouter.tsx`, under the existing `ProtectedRoute`/`AppLayout` branch
- [x] 3.4 Add a "Finance Search" nav link to `frontend/src/layouts/AppLayout.tsx`, rendered only
      when `useAuthStore((s) => s.user?.role) === 'Finance'`
- [x] 3.5 Smoke-checked with real `pnpm --filter frontend dev` + `dotnet run --project src/Api`
      processes (the exact split-process shape `ADR-0021` targets) — superseded the planned manual
      click-through with automated Playwright coverage instead (`e2e/06-expense-manager-team-view.spec.ts`,
      `e2e/07-expense-compliance-and-finance.spec.ts`): Manager approves/rejects a report's expense;
      Compliance approves/rejects a Client Entertainment expense; Finance reimburses from the detail
      screen and finds it via `/finance/search`; a non-owner is redirected away from
      `/expenses/{id}/edit`; both dev processes talk to each other with no auth/redirect failure

**Checkpoint 3**: `pnpm build` → 0 errors; `dotnet build` → 0 errors; `pnpm lint -- --max-warnings 0`;
`dotnet format --verify-no-changes`.

## 4. Tests: Manager Approve and Reject Actions (`frontend-expense-review-actions-ui`)

- [x] 4.1 Manager sees Approve/Reject on a direct report's `Submitted` expense
- [x] 4.2 Manager does not see Approve/Reject on their own `Submitted` expense
- [x] 4.3 Manager does not see Approve/Reject on a direct report's non-`Submitted` expense
- [x] 4.4 Clicking Approve calls `POST /api/expenses/{id}/approve` and refetches on success
- [x] 4.5 Clicking Reject opens the shared dialog and does not call the endpoint before a valid
      comment is submitted
- [x] 4.6 A `403`/`422` from approve or reject is displayed without a false-success state

## 5. Tests: Compliance Approve and Reject Actions (`frontend-expense-review-actions-ui`)

- [x] 5.1 Compliance sees Approve/Reject on an `Approved` `ClientEntertainment` expense
- [x] 5.2 Compliance does not see Approve/Reject on a non-`ClientEntertainment` expense
- [x] 5.3 Compliance does not see Approve/Reject on a `ClientEntertainment` expense whose `Status`
      is not `Approved`
- [x] 5.4 Clicking Approve calls `POST /api/expenses/{id}/compliance-approve` and refetches on
      success
- [x] 5.5 Clicking Reject opens the shared dialog and does not call `compliance-reject` before a
      valid comment is submitted
- [x] 5.6 A `403`/`422` from compliance-approve or compliance-reject is displayed without a
      false-success state

## 6. Tests: Finance Reimburse Action (`frontend-expense-review-actions-ui`)

- [x] 6.1 Finance sees Reimburse on an `Approved` non-`ClientEntertainment` expense
- [x] 6.2 Finance sees Reimburse on a `ComplianceApproved` `ClientEntertainment` expense
- [x] 6.3 Finance does not see Reimburse on an `Approved` (not yet Compliance Approved)
      `ClientEntertainment` expense
- [x] 6.4 Clicking Reimburse calls `POST /api/expenses/{id}/reimburse` and refetches on success
- [x] 6.5 A `422` from reimburse is displayed without a false-success state

## 7. Tests: Shared Rejection Comment Dialog (`frontend-expense-review-actions-ui`)

- [x] 7.1 Empty comment disables submission
- [x] 7.2 Whitespace-only comment disables submission
- [x] 7.3 Valid comment submitted from a Manager's dialog calls `POST /api/expenses/{id}/reject`
- [x] 7.4 Valid comment submitted from a Compliance's dialog calls
      `POST /api/expenses/{id}/compliance-reject`

## 8. Tests: Finance Search Screen (`frontend-finance-search-ui`)

- [x] 8.1 Navigating to `/finance/search` calls `POST /api/expenses/search` with an empty filter
      body and renders `items`/`page`/`pageSize`/`totalRecords`
- [x] 8.2 A non-`Finance` role cannot reach `/finance/search` (redirected away)
- [x] 8.3 Each result row links to `/expenses/{id}`

## 9. Tests: Finance Search Filter, Pagination, and Sorting Controls (`frontend-finance-search-ui`)

- [x] 9.1 `expenseNumber` filter is passed as an exact-match field in the request
- [x] 9.2 `employeeName` filter is passed as a substring-match field in the request
- [x] 9.3 Category and status filters combine into one request
- [x] 9.4 A from/to date range is passed as `fromDate`/`toDate`
- [x] 9.5 Clearing all filters re-issues a request with no filter fields set
- [x] 9.6 Changing page size re-issues the search with the new value
- [x] 9.7 Changing `sortBy`/`sortDirection` re-issues the search with updated values
- [x] 9.8 The page-size control offers only 20/50/100/500 (not 10, which the general list allows)

## 10. Tests: Reimbursement Stays on the Detail Screen, Not Inline (`frontend-finance-search-ui`)

- [x] 10.1 The search results table renders no inline "Reimburse" control
- [x] 10.2 Navigating from a search result to its detail and reimbursing there calls
      `POST /api/expenses/{id}/reimburse` exactly as the Finance Reimburse Action requirement
      specifies

## 11. Tests: Non-Owner Action Visibility on Detail (`frontend-expense-visibility-ui` MODIFIED)

- [x] 11.1 Owner sees Edit/Submit/Cancel and no review action (regression — confirm existing
      ET017 test still passes unmodified)
- [x] 11.2 A non-owner with an applicable review role (e.g. Manager on a report's `Submitted`
      expense) sees that role's review action and no Edit/Submit/Cancel action
- [x] 11.3 A non-owner with no applicable review role or status (e.g. Manager viewing a report's
      `Approved` expense) sees a read-only view with no action controls at all

## 12. Tests: Edit Route Ownership Guard (`frontend-expense-maintenance-ui` MODIFIED)

- [x] 12.1 Regression: owner still sees the pre-filled edit form (existing ET017 scenario, confirm
      unaffected by the new guard)
- [x] 12.2 Regression: valid edit submission still calls `PUT /api/expenses/{id}` (existing ET017
      scenario)
- [x] 12.3 Regression: the edit form still exposes no `Status` control (existing ET017 scenario)
- [x] 12.4 New: a non-owner navigating directly to `/expenses/{id}/edit` is redirected to
      `/expenses/{id}` and the edit form never renders

## 13. Tests: Dev-Split Auth Redirect Fix (`ADR-0021`)

- [x] 13.1 (Covered in 1.2) Integration test confirms no `307` under `WebApplicationFactory<Program>`'s
      default `Development` environment, with `ASPNETCORE_HTTPS_PORT` configured so the middleware
      has a port to redirect to
- [x] 13.2 Regression: the full existing integration test suite still passes unmodified — none of
      them configure `ASPNETCORE_HTTPS_PORT`, so `UseHttpsRedirection` (guarded or not) had no
      determinable port and was already a no-op for every one of them

**Checkpoint 4**: `dotnet build` → 0 errors; `dotnet format --verify-no-changes`; `dotnet test` →
all green; `pnpm build` → 0 errors; `pnpm lint -- --max-warnings 0`; `pnpm test` → all green;
`npx playwright test` (repo root — this ticket touches user-facing review flows) → all green.

## 14. Archive & Ticket Tracking

- [x] 14.1 Re-run the full quality gate once more end-to-end immediately before archiving (backend
      build → format → test; frontend lint → typecheck → build → test; Playwright)
- [x] 14.2 Confirm `docs/decisions/ADR-0021-dev-split-https-redirect-auth-loss.md` accurately
      reflects the as-implemented fix (already written during `/spec`; update only if
      implementation deviated)
- [x] 14.3 Run `openspec archive et018-expense-review`
- [x] 14.4 Update `docs/TICKETS.md`: ET018's `Status` stays `In progress` here — `/pr` sets
      `PR open (#N)` once the PR is opened, per this repo's archive-before-PR convention
- [ ] 14.5 Open the PR referencing ET018 and `docs/FRS.md` §5, §7 — out of `/implement`'s scope,
      handled by `/pr`
