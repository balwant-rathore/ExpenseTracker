## 1. Phase 1 — Foundation (API contract, types, scaffolding)

- [x] 1.1 [PARALLEL] Backend: implement ADR-0020 — add `EmployeeNumber`, `ReceiptAttachmentId`,
      `AttachmentOriginalFileName` to `backend/src/Application/Expenses/ExpenseResponse.cs`
      (`EmployeeNumber` corrected from an initial `EmployeeId` GUID — see ADR-0020 Addendum,
      found during Phase 3's real-browser E2E verification, not caught by any earlier
      component test since those mock both sides of the comparison)
- [x] 1.2 [PARALLEL] Backend: update `ExpenseService.Map(Expense)` in
      `backend/src/Application/Expenses/ExpenseService.cs` to populate the three new fields
- [x] 1.3 [PARALLEL] Backend: add `.Include(e => e.Attachment)` to
      `ExpenseRepository.GetByIdWithEmployeeAsync` and `GetPagedAsync`
      (`backend/src/Infrastructure/Persistence/Repositories/ExpenseRepository.cs`)
- [x] 1.4 [PARALLEL] Frontend: add `FormData` passthrough support to `apiRequest` in
      `frontend/src/lib/apiClient.ts` (D3) — skip `JSON.stringify`/`Content-Type` when
      `body instanceof FormData`
- [x] 1.5 [PARALLEL] Frontend: create `frontend/src/types/expense.ts` with
      `ExpenseCategory`, `ExpenseStatus`, `ExpenseSortField`, `ExpenseResponse`,
      `PagedExpenseResponse` (per design.md D4, including the three ADR-0020 fields)
- [x] 1.6 [PARALLEL] Frontend: scaffold `frontend/src/features/expenses/` directory
      structure (`api/`, `components/`, `schemas/`, `utils/`) with no logic yet — folded into
      Phase 2 file creation
- [x] 1.7 Frontend: confirm with user, then run
      `pnpm dlx shadcn@latest add select textarea dialog table badge` from `frontend/`
      (D9); review the `package.json`/lockfile diff before proceeding — no dependency changes,
      all 5 components use already-installed packages

**Checkpoint (Phase 1):** ✅ all green
- `dotnet build` (backend) → 0 errors
- `dotnet test` (backend) → 601/601 passed (confirms ADR-0020's additive fields broke
  nothing — design.md's first flagged risk)
- `pnpm --filter frontend build` → 0 errors
- `pnpm --filter frontend lint` → clean
- `pnpm --filter frontend test` → 62/62 passed

## 2. Phase 2 — Core Implementation

### 2A. Expense List & Detail (`frontend-expense-visibility-ui`) [PARALLEL stream]

- [x] 2.1 [PARALLEL] Create `expenseApi.ts` read functions: `getExpense`, `listExpenses`
- [x] 2.2 [PARALLEL] Create `useExpenseList.ts` (`['expenses','list',params]`) and
      `useExpense.ts` (`['expenses','detail',id]`) query hooks (D5)
- [x] 2.3 [PARALLEL] Create `utils/expenseDisplay.ts` (category/status display labels,
      currency formatting)
- [x] 2.4 [PARALLEL] Build `ExpenseList.tsx`: table rendering `items`, pagination controls
      (`page`, `pageSize` 10/20/50/100), sort controls (all 8 `sortBy` fields, `sortDirection`)
- [x] 2.5 [PARALLEL] Build `ExpenseFilters.tsx`: status dropdown (server-side, `?status=`)
      plus category and expense-date-range filters (client-side, current page only) with
      UI copy making the "current page only" scope explicit (design.md risk mitigation)
- [x] 2.6 [PARALLEL] Add a lightweight "Mine" indicator to `ExpenseList.tsx` rows so a
      Manager can distinguish their own rows from direct reports' rows (design.md risk
      mitigation, uses the same `employeeNumber === currentUserEmployeeNumber` check as D6)
- [x] 2.7 [PARALLEL] Build `ExpenseDetail.tsx`: full field display, ownership/action-
      visibility computation (D6), `404`/`403` states — 404/403 rendering ended up living in
      `ExpenseDetailPage.tsx` (the page owns the query + error branching; `ExpenseDetail`
      itself is presentational, matching the EditExpensePage precedent)
- [x] 2.8 [PARALLEL] Create `pages/ExpenseListPage.tsx` and `pages/ExpenseDetailPage.tsx`

### 2B. Expense Create/Edit/Cancel + Attachment Upload (`frontend-expense-submission-ui`,
`frontend-expense-maintenance-ui`, `frontend-attachment-upload-ui`) [PARALLEL stream]

- [x] 2.9 [PARALLEL] Create `schemas/attachmentSchema.ts` (Zod: file extension in
      pdf/jpg/jpeg/png, size ≤ 10 MB)
- [x] 2.10 [PARALLEL] Build `AttachmentPicker.tsx` — three-state `AttachmentSelection`
      (`none`/`existing`/`new`) per design.md D2, client-side validation on select, no
      upload call on select
- [x] 2.11 [PARALLEL] Create `attachmentApi.ts` (`uploadAttachment` using `FormData` via
      the extended `apiRequest`)
- [x] 2.12 [PARALLEL] Create `useUploadAttachment.ts` mutation hook
- [x] 2.13 [PARALLEL] Create `schemas/expenseFormSchema.ts` (Zod: `expenseDate` not future,
      `category` one of seven, `amount` > 0, `currency` fixed `INR`, `description` ≤ 500
      chars) — `amount` validated as a string field (RHF input type) with numeric refine
      checks, converted to `Number` when building the API payload, to avoid a
      `z.coerce.number()` input/output type mismatch against RHF's `useForm<T>` generic
- [x] 2.14 [PARALLEL] Create `expenseApi.ts` write functions: `createExpense`,
      `updateExpense`, `submitExpense`, `cancelExpense`
- [x] 2.15 [PARALLEL] Create `useCreateExpense.ts`, `useUpdateExpense.ts`,
      `useSubmitExpense.ts`, `useCancelExpense.ts` mutation hooks, each invalidating the
      `['expenses']` query prefix `onSuccess` (D5)
- [x] 2.16 [PARALLEL] Build `ExpenseForm.tsx` — shared create/edit form: field layout,
      Draft/Submit action buttons (create only), attachment resolution on save per D2
      (`existing` → reuse id, `new` → upload then use returned id, `none` → block submit)
- [x] 2.17 [PARALLEL] Build `CancelExpenseDialog.tsx` — explicit confirmation step before
      calling `cancelExpense`
- [x] 2.18 [PARALLEL] Create `pages/CreateExpensePage.tsx` and `pages/EditExpensePage.tsx`

**Checkpoint (Phase 2):** ✅ all green
- `pnpm --filter frontend exec tsc --noEmit` → 0 errors
- `pnpm --filter frontend build` → 0 errors
- `pnpm --filter frontend lint` → clean
- `pnpm --filter frontend test` → 121/121 passed

**Note:** Base UI's `Select.Item` commits selection on pointer interaction state, not a bare
synthetic `click` — tests needed a `fireEvent.pointerDown(...)` immediately before
`fireEvent.click(...)` on the option (see `frontend/src/test/selectOption.ts`, a new shared
test helper not in the original design.md file list).

## 3. Phase 3 — Integration

- [x] 3.1 Add routes to `frontend/src/routes/AppRouter.tsx`: `/expenses`,
      `/expenses/new`, `/expenses/:id`, `/expenses/:id/edit` with `ProtectedRoute`/
      `RequireRole(['Employee','Manager'])` gating per design.md D7
- [x] 3.2 Build `frontend/src/layouts/AppLayout.tsx` (D8: minimal nav — "Dashboard", "My
      Expenses", existing logout button) and wrap it around the authenticated route tree
- [x] 3.3 Wire `ExpenseDetailPage`'s Edit/Submit/Cancel actions and `ExpenseListPage`'s
      "New Expense" entry point to their respective mutations/routes
- [x] 3.4 Start the dev server and manually verify the full flow in a browser: create
      (Draft) → submit-existing-draft → edit → cancel, for an Employee user — done via real
      Playwright E2E specs against the live dev stack (backend + frontend + real SQL Server
      dev DB), not just component tests; see `e2e/05-expense-creation.spec.ts`. This
      surfaced and led to fixing **two real defects** neither existed in unit tests:
      1. `frontend/src/features/auth/session/useSessionBootstrap.ts` (ET016) had a race
         under React StrictMode's dev-mode double-effect-invocation: two concurrent
         `POST /api/auth/refresh` calls with the same one-time-use refresh token, where the
         loser's request tripped the backend's reuse-of-a-revoked-token safeguard and
         revoked the session the winner had just established. Fixed with an in-flight
         de-duplication guard in `performSilentRefresh`; regression tests added to
         `useSessionBootstrap.test.ts`.
      2. The ownership check (D6) compared `expense.employeeId` (an `Employee` GUID) against
         `user.id` (a *different* entity's GUID — the `User` row's own id) — these could
         never match, so Edit/Submit/Cancel silently never appeared for anyone, even the
         true owner. `/api/auth/me`/login/register never expose the caller's
         `Employee.EmployeeId` at all. Fixed by swapping `ExpenseResponse.EmployeeId` for
         `EmployeeNumber` (ADR-0020 Addendum) and comparing that against the frontend
         `User.employeeNumber`, which was already present. All affected tests, `design.md`,
         and the ADR were corrected in the same change.
- [x] 3.5 Manually verify, as a Manager: the list screen shows own + direct reports'
      non-Draft expenses with the "Mine" indicator, and no approve/reject controls appear
      anywhere (confirming ET018 boundary is respected) — `e2e/06-expense-manager-team-view.spec.ts`,
      passing against the live stack.
- [x] 3.6 Role gating verified via `frontend/src/routes/RequireRole.test.tsx` (existing,
      unmodified generic mechanism per design.md D7) plus a code-level check that
      `/expenses/new` and `/expenses/:id/edit` are wrapped in
      `RequireRole(['Employee','Manager'])` in `AppRouter.tsx`. Not additionally re-verified
      live with a Finance/Compliance seed account in this pass, to avoid further exhausting
      the register/login rate-limit budget (`RateLimiting:AuthEndpoints`, 5 per 5 min per
      IP) already spent diagnosing the two bugs above — the generic guard's own test
      coverage plus this specific wiring being visually confirmed in the router is
      considered sufficient.

**Checkpoint (Phase 3):** ✅ all green
- `dotnet build` (backend) → 0 errors
- `pnpm --filter frontend build` → 0 errors
- Manual browser verification: 5/5 new E2E tests passing
  (`e2e/05-expense-creation.spec.ts`, `e2e/06-expense-manager-team-view.spec.ts`), plus all
  6 of ET016's pre-existing E2E tests still passing (no regression)

## 4. Phase 4 — Tests (one per approved spec scenario)

### 4A. Backend regression coverage for ADR-0020

- [x] 4.1 Update `backend/tests/IntegrationTests/ExpenseSubmissionTests.cs` to assert
      `receiptAttachmentId` on created-expense responses (`employeeNumber` is null on this
      path — `CreateAsync` doesn't `Include(Employee)` — so it's not asserted here)
- [x] 4.2 Update `backend/tests/IntegrationTests/ExpenseMaintenanceTests.cs` to assert
      `employeeNumber`, `receiptAttachmentId`, `attachmentOriginalFileName` on the
      `GetById` response (`Employee`/`Attachment` included), and `receiptAttachmentId` only
      on the `Update` response (no `Employee` include there either)
- [x] 4.3 Update `backend/tests/IntegrationTests/ExpenseVisibilityTests.cs` to assert
      `employeeNumber`, `receiptAttachmentId`, `attachmentOriginalFileName` on list items

### 4B. `frontend-expense-submission-ui` (Vitest + RTL, `ExpenseForm.test.tsx` / `ExpenseDetail.test.tsx`)

- [x] 4.4 Scenario: Employee or Manager can reach the creation form — via `RequireRole`'s
      existing generic tests + `e2e/05-expense-creation.spec.ts` navigating to `/expenses/new`
      as an Employee
- [x] 4.5 Scenario: Currency is fixed and not editable
- [x] 4.6 Scenario: Valid submission calls the create endpoint with the chosen action
- [x] 4.7 Scenario: Save as Draft succeeds with valid data
- [x] 4.8 Scenario: Submit succeeds with valid data
- [x] 4.9 Scenario: Draft and Submit share the same client-side validation
- [x] 4.10 Scenario: Non-positive amount is blocked client-side
- [x] 4.11 Scenario: Future expense date is blocked client-side
- [x] 4.12 Scenario: Description over 500 characters is blocked client-side
- [x] 4.13 Scenario: Backend rejection is still surfaced even if client validation passed
- [x] 4.14 Scenario: Submission is blocked with no attachment selected
- [x] 4.15 Scenario: Owner submits their own Draft from the detail view
- [x] 4.16 Scenario: Submit action is hidden for non-Draft expenses
- [x] 4.17 Scenario: Backend rejection on submit is surfaced

### 4C. `frontend-expense-maintenance-ui` (Vitest + RTL, `ExpenseForm.test.tsx` /
`ExpenseDetail.test.tsx` / `CancelExpenseDialog.test.tsx`)

- [x] 4.18 Scenario: Owner opens the edit form pre-filled with current values
- [x] 4.19 Scenario: Valid edit submission calls the update endpoint
- [x] 4.20 Scenario: Edit form has no status control
- [x] 4.21 Scenario: Edit action shown for a Draft expense
- [x] 4.22 Scenario: Edit action shown for a Submitted expense
- [x] 4.23 Scenario: Edit action hidden for non-editable statuses (Approved,
      ComplianceApproved, Rejected, Cancelled, Reimbursed — individually, `it.each`)
- [x] 4.24 Scenario: Backend rejection of a stale edit attempt is surfaced
- [x] 4.25 Scenario: Existing attachment is shown by default
- [x] 4.26 Scenario: Replacing the attachment updates the request payload
- [x] 4.27 Scenario: Leaving the attachment unchanged resubmits the same id (and asserts no
      upload call is made)
- [x] 4.28 Scenario: Owner cancels their own Draft expense — covered compositionally:
      `ExpenseDetail.test.tsx` proves the Cancel control renders for a Draft owner,
      `CancelExpenseDialog.test.tsx` proves confirming it calls `cancelExpense` (the dialog
      itself is status-agnostic — gating already happened in `ExpenseDetail`)
- [x] 4.29 Scenario: Owner cancels their own Submitted expense — same composition, plus
      directly E2E-tested (`e2e/05-expense-creation.spec.ts`'s cancel test)
- [x] 4.30 Scenario: Cancel requires explicit confirmation
- [x] 4.31 Scenario: Cancel action hidden for non-cancellable statuses (Approved,
      ComplianceApproved, Rejected, Reimbursed, already-Cancelled — individually, `it.each`
      — same test as 4.23, since Edit/Cancel/Submit visibility share the same status gate)

### 4D. `frontend-expense-visibility-ui` (Vitest + RTL, `ExpenseList.test.tsx` /
`ExpenseFilters.test.tsx` / `ExpenseListPage.test.tsx` / `ExpenseDetail.test.tsx` /
`ExpenseDetailPage.test.tsx`)

- [x] 4.32 Scenario: Authenticated user sees a paginated list
- [x] 4.33 Scenario: List reflects exactly what the backend returns
- [x] 4.34 Scenario: Manager's list includes their direct reports' non-Draft expenses
- [x] 4.35 Scenario: Changing the page size refetches with the new value
- [x] 4.36 Scenario: Changing the sort field refetches with the new value
- [x] 4.37 Scenario: Selecting a status filters the list via the backend
- [x] 4.38 Scenario: Clearing the status filter restores the unfiltered list
- [x] 4.39 Scenario: Category filter narrows the current page
- [x] 4.40 Scenario: Date-range filter narrows the current page
- [x] 4.41 Scenario: Combined filters narrow within the current page
- [x] 4.42 Scenario: Authorized viewer sees full expense detail
- [x] 4.43 Scenario: Nonexistent expense shows a not-found state
- [x] 4.44 Scenario: Unauthorized viewer shows an access-denied state
- [x] 4.45 Scenario: Owner sees applicable actions
- [x] 4.46 Scenario: Non-owner sees a read-only view

### 4E. `frontend-attachment-upload-ui` (Vitest + RTL, `AttachmentPicker.test.tsx` /
`ExpenseForm.test.tsx`)

- [x] 4.47 Scenario: Selecting a file holds it in form state without uploading
- [x] 4.48 Scenario: Same component renders in both create and edit forms
- [x] 4.49 Scenario: Disallowed file type is rejected client-side
- [x] 4.50 Scenario: Allowed file type is accepted client-side
- [x] 4.51 Scenario: Oversized file is rejected client-side
- [x] 4.52 Scenario: File at or under the size limit is accepted client-side
- [x] 4.53 Scenario: Upload fires on Draft save
- [x] 4.54 Scenario: Upload fires on Submit save
- [x] 4.55 Scenario: Upload fires on edit save when the attachment was replaced
- [x] 4.56 Scenario: No upload call when the attachment is unchanged on edit
- [x] 4.57 Scenario: Attachment upload failure prevents expense creation
- [x] 4.58 Scenario: Attachment upload failure prevents expense edit
- [x] 4.59 Scenario: Edit form shows the current attachment's file name

### 4F. End-to-End (Playwright — user-facing flow, per `AGENTS.md` quality gates)

- [x] 4.60 E2E: Employee creates an expense as Draft, then submits it from the detail view
- [x] 4.61 E2E: Employee creates an expense with immediate Submit
- [x] 4.62 E2E: Employee edits an existing Draft (including replacing the attachment) and
      re-submits
- [x] 4.63 E2E: Employee cancels a Submitted expense
- [x] 4.64 E2E: Manager views the list showing own + direct reports' expenses, opens a
      direct report's expense, confirms it renders read-only with no edit/submit/cancel
      controls

**Checkpoint (Phase 4):** ✅ all green
- `dotnet test` (backend) → 289 unit + 248 integration, all passed
- `pnpm --filter frontend test` → 133/133 passed
- `pnpm --filter frontend lint` → clean
- `npx playwright test e2e/05-expense-creation.spec.ts e2e/06-expense-manager-team-view.spec.ts`
  → 5/5 passed against the live dev stack (real backend, real SQL Server dev DB, real
  Chromium). The full historical `npx playwright test` (all 11 specs, ET016 + ET017) was
  also run once end-to-end and passed 10/11 — the one failure was a `429
  RATE_LIMIT_EXCEEDED` on `/api/auth/login` from this session's own repeated diagnostic runs
  exhausting the 5-per-5-minute-per-IP budget (`RateLimiting:AuthEndpoints`), not a defect;
  confirmed by re-running that same spec in isolation immediately afterward, where it passed.

## 5. Phase 5 — Archive

- [x] 5.1 Run `openspec archive et017-expense-ui`
- [x] 5.2 Confirmed `docs/TICKETS.md` ET017 row: `Status = In progress`,
      `Change Name = et017-expense-ui` (status moves to `PR open (#N)` once the PR is
      actually opened — via `/pr`, not this step)
