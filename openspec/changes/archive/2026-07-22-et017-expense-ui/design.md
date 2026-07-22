## Context

ET017 builds the Employee/Manager-facing expense screens (create, edit, cancel, upload
receipt, list, detail) against the already-shipped backend APIs from ET006 (attachments),
ET007 (creation/submission), ET008 (edit/cancel), and ET009 (list/detail visibility). The
frontend has one prior feature module to follow as a pattern — `features/auth/` from
ET016 — which establishes the conventions this design reuses: one API-client file per
feature (`api/<feature>Api.ts` wrapping the shared `apiRequest` helper), one TanStack Query
hook per operation (`api/use<Verb>.ts`), a Zod schema per form (`schemas/`), route-level
pages in the top-level `frontend/src/pages/` (not nested under a feature module) composing
feature components, and Zustand reserved for the auth session only.

During planning, a gap was found between the already-approved ET017 spec delta and the
current backend contract: `ExpenseResponse` (backend/src/Application/Expenses/ExpenseResponse.cs)
exposes no owner ID and no attachment reference, which the approved
`frontend-expense-visibility-ui` (ownership-based action visibility) and
`frontend-attachment-upload-ui` (attachment-preview-on-edit) requirements both need. This
was raised with the ticket owner and resolved as **ADR-0020**
(`docs/decisions/ADR-0020-expense-response-owner-and-attachment-fields.md`): add
`EmployeeId`, `ReceiptAttachmentId`, and `AttachmentOriginalFileName` to `ExpenseResponse`.
This design assumes ADR-0020 lands as part of this same change (small, additive backend
edit, not a new endpoint or schema change) — see **Decision D1**.

## Goals / Non-Goals

**Goals:**
- Implement every requirement in `openspec/changes/et017-expense-ui/specs/**/*.md` exactly
  as approved — no scope beyond it.
- Reuse ET016's established patterns (`apiClient`, `authStore`, `ProtectedRoute`/
  `RequireRole`, RHF+Zod form conventions) rather than inventing new ones.
- Keep the backend touch to the minimum ADR-0020 requires; every workflow rule stays
  server-side and re-validated, per `docs/SDS.md` §1.3.

**Non-Goals:**
- Manager/Compliance/Finance **review actions** (approve, reject, compliance-approve/
  reject, reimburse) — ET018.
- Dashboard widgets, Finance search UI, monthly report download — ET019.
- Any new backend workflow rule, authorization policy, or DB schema/migration.
- A full application shell/navigation redesign — only the minimum nav needed to reach the
  new routes.

## Decisions

### D1: Extend `ExpenseResponse` per ADR-0020 (backend)
**Files:**
- `backend/src/Application/Expenses/ExpenseResponse.cs` — add `string? EmployeeNumber`,
  `Guid ReceiptAttachmentId`, `string? AttachmentOriginalFileName`.
- `backend/src/Application/Expenses/ExpenseService.cs` — `Map(Expense)` populates the three
  new fields from the already-loaded entity (`expense.Employee?.EmployeeNumber`,
  `expense.AttachmentId`, `expense.Attachment?.OriginalFileName`).
- `backend/src/Infrastructure/Persistence/Repositories/ExpenseRepository.cs` —
  `GetByIdWithEmployeeAsync` and `GetPagedAsync` add `.Include(e => e.Attachment)` (both
  currently `.Include(e => e.Employee)` only) so `Attachment.OriginalFileName` is loaded
  without an extra round-trip.
- Existing integration tests that assert on `ExpenseResponse`'s JSON (e.g.
  `backend/tests/IntegrationTests/ExpenseSubmissionTests.cs`,
  `ExpenseMaintenanceTests.cs`, `ExpenseVisibilityTests.cs`) get new field assertions.

**Why:** Additive, no migration, no new endpoint — reuses data the `Expense`/`Attachment`
entities already store. Alternative (string-name-based ownership guess, no attachment
preview) was rejected per the ticket owner's decision during `/plan` — see ADR-0020.

**Correction found during real-browser E2E verification (implementation phase):** the
original plan here used `Guid EmployeeId` (the `Expense.EmployeeId` FK) for the ownership
comparison in D6. That field is the wrong join key on the frontend: `/api/auth/me` (and
login/register) return a `User` object whose `id` is the *User* row's own GUID, not the
Employee's — the frontend has no way to obtain its own `Employee.EmployeeId` GUID to
compare against it. `ExpenseResponse` was changed to expose `EmployeeNumber` (a string
already mirrored on the frontend `User.employeeNumber`) instead, and D6's comparison was
corrected to match. See ADR-0020's "Addendum" section for the full account of how this was
found (a live E2E run against a real backend/DB, not a unit test — the mocked component
tests written first couldn't have caught this, since they set both sides of the comparison
by hand and never exercised the real `/api/auth/me` contract).

### D2: Deferred attachment upload lives in component-local state, not the RHF-registered fields
**Files:** `frontend/src/features/expenses/components/AttachmentPicker.tsx`

The picked file is *not* an RHF-registered input (RHF doesn't need to own a `File` object
for validation purposes here). `AttachmentPicker` holds one of three states and reports it
to its parent form via a callback/controlled prop:

```ts
type AttachmentSelection =
  | { kind: 'none' }
  | { kind: 'existing'; attachmentId: string; fileName: string }
  | { kind: 'new'; file: File }
```

- Create form starts at `{ kind: 'none' }`.
- Edit form starts at `{ kind: 'existing', attachmentId: expense.receiptAttachmentId, fileName: expense.attachmentOriginalFileName ?? 'Receipt' }`.
- Selecting a new file transitions either state to `{ kind: 'new', file }` after passing
  client-side type/size checks (`attachmentFileSchema`, Zod, in
  `features/expenses/schemas/attachmentSchema.ts`).
- On form save, the containing form (`ExpenseForm.tsx`) resolves `AttachmentSelection` to a
  `receiptAttachmentId` string:
  - `existing` → use `attachmentId` as-is, no upload call.
  - `new` → `await uploadAttachment(file)` first, then use the returned `attachmentId`.
  - `none` → validation error, block submission (create only — edit always starts
    `existing`).

**Why:** Matches the approved spec's "upload deferred until save" requirement exactly,
keeps `AttachmentPicker` reusable and free of API/mutation concerns itself (single
responsibility — it only picks and locally validates a file), and keeps the file-selection
step framework-agnostic to RHF's re-render/reset behavior.

### D3: `apiClient.apiRequest` gains `FormData` support (frontend)
**File:** `frontend/src/lib/apiClient.ts`

`apiRequest` currently always `JSON.stringify`s `body` and sets
`Content-Type: application/json`. Extend it: if `body instanceof FormData`, skip
`JSON.stringify` and skip setting `Content-Type` (the browser sets
`multipart/form-data; boundary=...` itself). This is the same endpoint contract ET006
already defined (`POST /api/attachments`, `multipart/form-data`) — no prior frontend
consumer existed for it until now.

```ts
interface ApiRequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  accessToken?: string | null
}
// body: FormData is passed through unchanged; JSON.stringify/Content-Type only applied
// when body is not FormData.
```

**Why:** Smallest change that unblocks attachment upload without duplicating `apiRequest`'s
error-envelope handling in a parallel upload function. Backward compatible — every existing
caller passes a plain object, never `FormData`.

### D4: New feature module `frontend/src/features/expenses/`
Mirrors `features/auth/`'s shape:

```
frontend/src/features/expenses/
 ├── api/
 │    ├── expenseApi.ts          # createExpense, getExpense, listExpenses, updateExpense,
 │    │                          # submitExpense, cancelExpense — thin apiRequest wrappers
 │    ├── attachmentApi.ts       # uploadAttachment
 │    ├── useCreateExpense.ts / useUpdateExpense.ts / useSubmitExpense.ts /
 │    │    useCancelExpense.ts   # mutations; each invalidates the 'expenses' query key on success
 │    ├── useExpenseList.ts      # useQuery(['expenses', 'list', params])
 │    ├── useExpense.ts          # useQuery(['expenses', 'detail', id])
 │    └── useUploadAttachment.ts # mutation, no cache entry (fire-and-return attachmentId)
 ├── components/
 │    ├── ExpenseForm.tsx        # shared create/edit form (D2 attachment resolution lives here)
 │    ├── AttachmentPicker.tsx   # shared file picker (D2)
 │    ├── ExpenseList.tsx        # table + pagination/sort controls
 │    ├── ExpenseFilters.tsx     # status (server) + category/date (client-side, current page)
 │    ├── ExpenseDetail.tsx      # detail view; computes action visibility (D7)
 │    └── CancelExpenseDialog.tsx
 ├── schemas/
 │    ├── expenseFormSchema.ts   # expenseDate/category/amount/currency/description (Zod)
 │    └── attachmentSchema.ts    # file type/size checks (Zod, used imperatively by D2)
 └── utils/
      └── expenseDisplay.ts      # category/status label maps, currency formatting
```

Route-level pages are **not** nested under this feature module — per the established
`pages/` convention (see intro above and D7), they live alongside every other route's page
in the top-level directory:

```
frontend/src/pages/
 ├── CreateExpensePage.tsx
 ├── EditExpensePage.tsx
 ├── ExpenseListPage.tsx
 └── ExpenseDetailPage.tsx
```

```ts
// frontend/src/types/expense.ts
export type ExpenseCategory =
  | 'Travel' | 'Hotel' | 'Meals' | 'OfficeSupplies'
  | 'ClientEntertainment' | 'Training' | 'Other'
export type ExpenseStatus =
  | 'Draft' | 'Submitted' | 'Approved' | 'ComplianceApproved'
  | 'Cancelled' | 'Reimbursed' | 'Rejected'
export type ExpenseSortField =
  | 'expenseDate' | 'expenseNumber' | 'createdAt' | 'amount'
  | 'submittedAt' | 'approvedAt' | 'reimbursedAt' | 'rejectedAt'

export interface ExpenseResponse {
  id: string
  expenseNumber: string
  expenseDate: string          // DateOnly -> 'yyyy-MM-dd'
  category: ExpenseCategory
  amount: number
  currency: 'INR'
  description: string
  status: ExpenseStatus
  submittedAt: string | null
  approvedAt: string | null
  complianceApprovedAt: string | null
  rejectedAt: string | null
  rejectionComment: string | null
  reimbursedAt: string | null
  createdAt: string
  employeeName: string | null
  employeeNumber: string | null              // ADR-0020
  receiptAttachmentId: string               // ADR-0020
  attachmentOriginalFileName: string | null // ADR-0020
}

export interface PagedExpenseResponse {
  items: ExpenseResponse[]
  page: number
  pageSize: number
  totalRecords: number
}
```

```ts
// frontend/src/features/expenses/api/expenseApi.ts (shape)
export interface ExpenseFormInput {
  expenseDate: string
  category: ExpenseCategory
  amount: number
  currency: 'INR'
  description: string
  receiptAttachmentId: string
}
export interface CreateExpenseInput extends ExpenseFormInput { action: 'Draft' | 'Submit' }
export interface ExpenseListParams {
  page?: number
  pageSize?: 10 | 20 | 50 | 100
  sortBy?: ExpenseSortField
  sortDirection?: 'asc' | 'desc'
  status?: ExpenseStatus
}

export function createExpense(input: CreateExpenseInput): Promise<{ expense: ExpenseResponse }>
export function updateExpense(id: string, input: ExpenseFormInput): Promise<{ expense: ExpenseResponse }>
export function submitExpense(id: string): Promise<{ expense: ExpenseResponse }>
export function cancelExpense(id: string): Promise<{ expense: ExpenseResponse }>
export function getExpense(id: string): Promise<{ expense: ExpenseResponse }>
export function listExpenses(params: ExpenseListParams): Promise<PagedExpenseResponse>
```

All wrap `apiRequest`, matching `authApi.ts`'s style (plain functions, no class, access
token threaded in by the calling hook exactly as `useLogin`/`useLogout` already do).

**Why:** This is the direct extension of ET016's established feature-module shape — no new
pattern introduced, matching `AGENTS.md`/`frontend/CLAUDE.md` conventions.

### D5: Query keys and cache invalidation
- List: `['expenses', 'list', params]` (params object as part of the key so each
  page/sort/status combination caches independently, following TanStack Query's standard
  key-by-params convention).
- Detail: `['expenses', 'detail', id]`.
- Every mutation (`useCreateExpense`, `useUpdateExpense`, `useSubmitExpense`,
  `useCancelExpense`) invalidates `['expenses']` (the whole prefix) `onSuccess`, so list and
  any open detail view refetch — per `frontend/CLAUDE.md`'s "always invalidate/refetch on
  success, never optimistic-update workflow endpoints."

### D6: Ownership-based action visibility (`ExpenseDetail.tsx`)
```ts
const isOwner = !!currentUserEmployeeNumber && expense.employeeNumber === currentUserEmployeeNumber
const canEdit = isOwner && (expense.status === 'Draft' || expense.status === 'Submitted')
const canSubmit = isOwner && expense.status === 'Draft'
const canCancel = isOwner && (expense.status === 'Draft' || expense.status === 'Submitted')
```
Compares `employeeNumber` (a string, already present on the frontend `User` type as
`employeeNumber`), **not** a GUID — corrected from the original plan of comparing
`expense.employeeId` against `user.id`, which compared two different entities' GUIDs (the
`Expense`'s owning `Employee` vs. the authenticated `User` row) and could never match. See
D1's correction note and ADR-0020's Addendum. `ExpenseDetailPage`/`ExpenseListPage` source
`currentUserEmployeeNumber` from `useAuthStore(state => state.user?.employeeNumber)`. No
approve/reject/compliance/reimburse controls are ever rendered here — those belong entirely
to ET018.

### D7: Route structure and role gating
**File:** `frontend/src/routes/AppRouter.tsx` (extended, not replaced)

```
/expenses            → ExpenseListPage      (ProtectedRoute only — any authenticated role)
/expenses/new         → CreateExpensePage   (ProtectedRoute + RequireRole(['Employee','Manager']))
/expenses/:id         → ExpenseDetailPage   (ProtectedRoute only)
/expenses/:id/edit    → EditExpensePage     (ProtectedRoute + RequireRole(['Employee','Manager']))
```

List/detail allow any authenticated role to route in — the backend's per-role visibility
predicate (`expense-visibility` capability) is what actually scopes the data; a
Finance/Compliance user hitting `/expenses/:id` for an expense outside their visibility
gets the existing `403` → access-denied state (`frontend-expense-visibility-ui`'s
"Unauthorized viewer" requirement), so an extra client-side role gate on those two routes
would be redundant, not protective. Create/edit *are* role-gated client-side (mirroring the
backend's `EmployeeOrManager` authorization policy) purely for UX — hiding an action a
Finance/Compliance user could never successfully complete — never as the authorization
mechanism itself.

### D8: Minimal `AppLayout` for navigation
**File:** `frontend/src/layouts/AppLayout.tsx` (new)

No layout exists yet (`layouts/` is empty; `DashboardPage` is ET016's unstyled placeholder
per its own comment). Add a small authenticated shell — a header with "Dashboard" and "My
Expenses" links plus the existing logout button — wrapping `ProtectedRoute`'s children in
`AppRouter.tsx`. This is intentionally minimal: just enough for a user to reach `/expenses`
and back; a full navigation/IA redesign is out of scope (ET019 owns the dashboard proper).

### D9: New shadcn/ui primitives required
**Not yet present** in `frontend/src/components/ui/` (currently only `button`, `card`,
`field`, `input`, `label`, `separator`): `select` (category/status pickers), `textarea`
(description), `dialog` (cancel confirmation), `table` (expense list), `badge` (status
pill). These are added via the already-configured `shadcn` CLI (`components.json` present,
`shadcn` already a dependency) — e.g. `pnpm dlx shadcn@latest add select textarea dialog
table badge` — run from `frontend/`. Per `CLAUDE.md`'s permission model, confirm before
running this (it can modify `package.json`/lockfile if a primitive pulls a new `@base-ui/react`
export) — flagged here as a **/implement-time checkpoint**, not pre-approved by this design.

## Risks / Trade-offs

- **[Risk]** ADR-0020's backend change touches shared `ExpenseResponse` consumers (list,
  detail — and indirectly anything else that reuses `ExpenseService.Map`).
  → **Mitigation:** purely additive fields; re-run the full backend test suite
  (`dotnet test`) as this ticket's first checkpoint, before writing any frontend code, to
  catch any test asserting an exact/closed JSON shape.
- **[Risk]** Category/date-range filters apply only to the currently loaded page (per the
  approved spec — the backend list endpoint has no category/date query params), which can
  read as "broken" to a user who expects a full-dataset filter.
  → **Mitigation:** UI copy makes this explicit (e.g. "Filtering current page"); no fix
  beyond what's approved — a server-side filter is a backend scope change outside ET017.
- **[Risk]** Deferred attachment upload means a late failure (after the user has filled the
  whole form) surfaces only at save time.
  → **Mitigation:** keep the picked `File` in `AttachmentPicker`'s local state on failure
  (don't reset it) so retry doesn't require re-selecting the file; show a distinct
  upload-error banner separate from field-level errors (per the approved spec's "Upload
  Failure Blocks the Expense Save" requirement).
- **[Risk]** New shadcn primitives (D9) could introduce an unreviewed dependency bump.
  → **Mitigation:** run the `shadcn add` command as an explicit, confirmed step before
  building components that need it; review the resulting `package.json` diff before
  continuing.
- **[Risk]** A Manager's expense list mixes their own rows with direct reports' rows with no
  visual distinction, which could read as confusing given only "own" rows are actionable.
  → **Mitigation:** add a lightweight "Mine" indicator (e.g. a badge or column) in
  `ExpenseList.tsx` — a small UX addition, not a new capability, consistent with D6's
  ownership check already being computed for the detail view.

## Migration Plan

- No EF Core migration — ADR-0020 changes a response projection only, no entity/schema
  change.
- Backend and frontend changes ship together in this ticket's branch/PR (this is a monorepo
  with a single ticket scope; no independent backend release step is defined elsewhere in
  this project's process).
- Rollback: revert the three backend files (D1) together with the frontend `types/expense.ts`
  fields that depend on them — they must move as one unit, since the frontend types assume
  the new fields are always present (non-optional `employeeNumber`/`receiptAttachmentId`).

## Open Questions

None outstanding. The one real gap found during planning (`ExpenseResponse` missing owner/
attachment fields) was raised with the ticket owner and resolved as ADR-0020 before this
design was written, per `AGENTS.md` §13's guardrail against silently deciding such gaps.
