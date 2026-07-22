## 1. Foundation — Contracts & Types

- [x] 1.1 Backend: add `AttachmentDownloadFailureReason` enum (`Application/Attachments/AttachmentDownloadFailureReason.cs`) with `None`, `AttachmentNotFound`, `NotVisible`
- [x] 1.2 Backend: add `AttachmentDownloadResult` record (`Application/Attachments/AttachmentDownloadResult.cs`) mirroring `ExpenseResult`'s `Success`/`Failure` factory shape
- [x] 1.3 Backend: add `DownloadAsync(Guid employeeId, EmployeeRole role, Guid attachmentId, CancellationToken)` to `IAttachmentService`
- [x] 1.4 Backend: add `GetByAttachmentIdWithEmployeeAsync(Guid attachmentId, CancellationToken)` to `IExpenseRepository`
- [x] 1.5 Backend: add `OpenReadAsync(string relativePath, CancellationToken)` to `IFileStorageService`
- [x] 1.6 Backend: draft `docs/decisions/ADR-0022-attachment-download-endpoint.md` (context: no download endpoint exists today; decision: new `GET /api/attachments/{id}` reusing `ExpenseVisibility`; consequences), matching the existing ADR format in `docs/decisions/`
- [x] 1.7 Backend: update `docs/SDS.md` §5.3 to add the `GET /api/attachments/{id}` endpoint row (auth: any authenticated role, authorization via expense-visibility reuse)
- [x] 1.8 Frontend: add `apiRequestBlob` + `BlobResponse` export to `lib/apiClient.ts` (design.md D1)
- [x] 1.9 Frontend: add `lib/blobDownload.ts` with `saveBlob(blob, fileName)` and `openBlobInNewTab(blob): Window | null` (design.md D2)
- [x] 1.10 Frontend: add `EmployeeDashboard`/`ManagerDashboard`/`FinanceDashboard` TypeScript interfaces (design.md D4)

> Note: interface-only additions (1.3–1.5) don't compile standalone in C# — Checkpoint 1 was
> effectively run together with Phase 2A's implementations rather than in true isolation; both
> checkpoints passed.

**Checkpoint 1** (regression-only — no new behavior wired yet):
```bash
# Backend
dotnet build
dotnet format --verify-no-changes
dotnet test --filter FullyQualifiedName~UnitTests

# Frontend
pnpm --filter frontend lint
pnpm --filter frontend exec tsc --noEmit
pnpm --filter frontend build
```

## 2. Core Implementation

Backend and frontend tasks in this phase touch disjoint files and share only the contracts fixed
in Phase 1 — safe to run as two parallel workstreams in separate git worktrees per AGENTS.md
("Frontend and backend work must run in separate worktrees, never interleaved in one working
tree"). Use `/parallel` to spin up the two worktrees before starting this phase.

### 2A. Backend — `attachment-download` capability [PARALLEL]

- [x] 2.1 [PARALLEL] Implement `ExpenseRepository.GetByAttachmentIdWithEmployeeAsync` — mirror the existing `GetByIdWithEmployeeAsync`'s `Include(e => e.Employee).Include(e => e.Attachment)` pattern, filtering on `e.AttachmentId == attachmentId`
- [x] 2.2 [PARALLEL] Implement `FileStorageService.OpenReadAsync` — reuse the existing private `ToAbsolutePath` helper, return a read-only `FileStream`
- [x] 2.3 [PARALLEL] Implement `AttachmentService.DownloadAsync` — load attachment (404 if missing) → load owning expense via 2.1 (404 if unlinked) → `ExpenseVisibility.BuildPredicate(role, employeeId).Compile().Invoke(expense)` (403 if false) → open stream via 2.2 → return `Success(stream, attachment.ContentType, attachment.OriginalFileName)`
- [x] 2.4 [PARALLEL] In `AttachmentsController`, move `[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]` from the class level onto the `Upload` action only; leave the class with a bare `[Authorize]` (design.md D3 — required so Finance/Compliance aren't blocked by the class-level policy on the new action)
- [x] 2.5 [PARALLEL] Add `AttachmentsController.Download` action: `[HttpGet("{id:guid}")]`, call `_attachmentService.DownloadAsync`, map `AttachmentNotFound` → 404 `RESOURCE_NOT_FOUND`, `NotVisible` → 403 `AUTHORIZATION_FAILED`, else set `Response.Headers.ContentDisposition = "inline; filename=\"...\""` and `return File(result.Content!, result.ContentType!)`

### 2B. Frontend — dashboard, reports, breadcrumb, attachment viewer [PARALLEL]

- [x] 2.6 [PARALLEL] `features/dashboard/api/dashboardApi.ts` (`getDashboard`) + `features/dashboard/api/useDashboard.ts` (TanStack Query hook, mirrors `useExpense.ts`)
- [x] 2.7 [PARALLEL] `features/dashboard/components/DashboardMetrics.tsx` — renders 3/4/5 metric tiles based on the authenticated user's `role` from `authStore` (never inferred from response shape)
- [x] 2.8 [PARALLEL] `features/reports/api/reportsApi.ts` (`downloadMonthlyReimbursementReport(year, month, accessToken)` via `apiRequestBlob`)
- [x] 2.9 [PARALLEL] `features/reports/components/MonthYearPicker.tsx` — two shadcn `Select` controls, current month/year pre-selected
- [x] 2.10 [PARALLEL] `pages/MonthlyReportPage.tsx` — composes 2.8/2.9, download button as a mutation calling `saveBlob` on success
- [x] 2.11 [PARALLEL] `store/breadcrumbStore.ts` (Zustand: `trail`, `push`, `truncateTo`, `reset`)
- [x] 2.12 [PARALLEL] `components/Breadcrumbs.tsx` + `useBreadcrumb(label)` hook
- [x] 2.13 [PARALLEL] Add `viewAttachment(id, accessToken)` to `features/expenses/api/attachmentApi.ts` using `apiRequestBlob`
- [x] 2.14 [PARALLEL] `features/expenses/components/ViewReceiptLink.tsx` — calls `viewAttachment` then `openBlobInNewTab`; on a `null` return (popup blocked), renders a retry affordance; on a thrown `ApiError`, displays it without attempting to open a tab

> Note: implemented directly in the main session rather than via `/parallel` worktrees — both
> streams touched disjoint files as designed, so sequencing them serially carried no correctness
> risk and avoided worktree-merge overhead for a change this size.

**Checkpoint 2** (new units compile and are independently testable; not yet wired into pages/routes):
```bash
# Backend
dotnet build
dotnet format --verify-no-changes
dotnet test --filter FullyQualifiedName~UnitTests

# Frontend
pnpm --filter frontend lint
pnpm --filter frontend exec tsc --noEmit
pnpm --filter frontend build
```

## 3. Integration

- [x] 3.1 Backend: confirm `AddAttachmentFoundation` needs no new DI/policy registration — `Download` resolves through the existing `IAttachmentService` binding (design.md D8); add a quick DI-resolution test if the project's existing `AttachmentServiceCollectionExtensionsDiTests` pattern covers this
- [x] 3.2 Frontend: replace the `pages/DashboardPage.tsx` stub with the real implementation (2.6/2.7), redirecting `ComplianceOfficer` to `/expenses` via `<Navigate>` before calling `useDashboard`
- [x] 3.3 Frontend: add the `/reports/monthly-reimbursement` route to `routes/AppRouter.tsx`, guarded by `RequireRole allowedRoles={['Finance']}` (same pattern as `/finance/search`)
- [x] 3.4 Frontend: update `layouts/AppLayout.tsx` — hide the "Dashboard" nav link and show a `/expenses` link instead for `ComplianceOfficer`; render `<Breadcrumbs/>` above `<Outlet/>`
- [x] 3.5 Frontend: wire `useBreadcrumb(label)` into `ExpenseListPage`, `FinanceSearchPage`, `ExpenseDetailPage`, `DashboardPage`, and `MonthlyReportPage`
- [x] 3.6 Frontend: render `<ViewReceiptLink/>` inside `features/expenses/components/ExpenseDetail.tsx`, next to the existing `attachmentOriginalFileName` display
- [x] 3.7 (added during implementation) Frontend: call `useBreadcrumbStore.getState().reset()` in `useLogout`'s `onSettled` (design.md D6) so a subsequent login in the same SPA session doesn't inherit the previous user's trail
- [x] 3.8 (added during implementation) Frontend: fix `MonthYearPicker`'s month `Select` to render the month name via `SelectValue`'s formatter function — Base UI's `Select.Value` renders the raw `value` by default (confirmed via Context7), so without this the trigger showed "3" instead of "March"

**Checkpoint 3** (full existing suite must stay green with new wiring in place):
```bash
# Backend
dotnet build
dotnet format --verify-no-changes
dotnet test

# Frontend
pnpm --filter frontend lint
pnpm --filter frontend exec tsc --noEmit
pnpm --filter frontend build
pnpm --filter frontend test
```

## 4. Tests (one per approved spec scenario)

### 4.1 `attachment-download` (backend, xUnit integration tests)

- [x] 4.1.1 Authorized caller downloads an existing attachment → `200` with file content
- [x] 4.1.2 Unauthenticated request → `401 AUTHENTICATION_FAILED`
- [x] 4.1.3 Nonexistent attachment id → `404 RESOURCE_NOT_FOUND`
- [x] 4.1.4 Owner can download their own expense's attachment regardless of status
- [x] 4.1.5 Manager can download a direct report's non-Draft expense's attachment
- [x] 4.1.6 Manager cannot download an indirect report's attachment → `403`
- [x] 4.1.7 Finance can download any non-Draft expense's attachment
- [x] 4.1.8 Compliance Officer can download an Approved ClientEntertainment expense's attachment
- [x] 4.1.9 Compliance Officer is rejected for a non-eligible expense's attachment → `403`
- [x] 4.1.10 Draft expense's attachment is not visible to a non-owner Manager → `403`
- [x] 4.1.11 PDF attachment downloads with its stored `Content-Type` and `inline` disposition
- [x] 4.1.12 Image attachment preserves its `OriginalFileName` in `Content-Disposition`

### 4.2 `frontend-dashboard-ui` (Vitest + React Testing Library)

- [x] 4.2.1 Employee sees three metrics (`totalSubmitted`, `approved`, `reimbursed`)
- [x] 4.2.2 Manager sees four metrics (+ `pendingApprovals`)
- [x] 4.2.3 Finance sees five metrics (+ `pendingReimbursements`)
- [x] 4.2.4 Loading state renders while `GET /api/dashboard` is in flight
- [x] 4.2.5 Fetch error renders an error state, not zeroed/stale tiles
- [x] 4.2.6 Compliance Officer sees no "Dashboard" nav link (`layouts/AppLayout.test.tsx`)
- [x] 4.2.7 Compliance Officer navigating to `/dashboard` redirects to `/expenses` without calling `GET /api/dashboard`
- [x] 4.2.8 Employee/Manager/Finance navigating to `/dashboard` render normally, no redirect

### 4.3 `frontend-monthly-report-ui` (Vitest + React Testing Library)

- [x] 4.3.1 Finance sees the report screen with the current month/year pre-selected, no auto-download
- [x] 4.3.2 Non-Finance role is redirected away from `/reports/monthly-reimbursement`
- [x] 4.3.3 Clicking Download calls `GET /api/reports/monthly-reimbursement` with the selected `year`/`month`
- [x] 4.3.4 Downloaded file is saved using the `Content-Disposition` file name, not a client-generated one
- [x] 4.3.5 Changing the period, then downloading again, requests the newly selected period
- [x] 4.3.6 A header-only (empty) workbook response is still saved without error
- [x] 4.3.7 Backend `400 VALIDATION_ERROR` is surfaced without attempting a file save

### 4.4 `frontend-breadcrumb-navigation` (Vitest + React Testing Library)

- [x] 4.4.1 Finance Search → expense detail trail reads `Home > Finance Search > <Expense Number>`
- [x] 4.4.2 Expense list → same expense detail trail reads `Home > Expenses > <Expense Number>`
- [x] 4.4.3 A freshly loaded page with no recorded history shows only `Home > <current page>`
- [x] 4.4.4 Expense detail page renders the breadcrumb, providing backward navigation
- [x] 4.4.5 Home crumb links to `/dashboard` for Employee/Manager/Finance
- [x] 4.4.6 Home crumb links to `/expenses` for Compliance Officer
- [x] 4.4.7 Clicking an earlier crumb navigates to that page
- [x] 4.4.8 Navigating via a breadcrumb truncates entries after it, and subsequent navigation builds a fresh trail

### 4.5 `frontend-attachment-viewer-ui` (Vitest + React Testing Library)

- [x] 4.5.1 Detail screen shows a "View Receipt" link labeled with `attachmentOriginalFileName`
- [x] 4.5.2 Clicking View Receipt calls `GET /api/attachments/{id}` with the `Authorization` header and opens a new tab on success
- [x] 4.5.3 Backend `403`/`404` is surfaced without opening a new tab
- [x] 4.5.4 Manager/Finance/Compliance Officer viewers who can see the expense detail also see the receipt link

### 4.6 End-to-End (Playwright — this ticket touches user-facing flows per AGENTS.md quality gates)

- [ ] 4.6.1 E2E: Employee logs in and sees correct dashboard metric tiles
- [ ] 4.6.2 E2E: Finance downloads a monthly report and the file is produced
- [ ] 4.6.3 E2E: Navigate Finance Search → expense detail → click breadcrumb back to Finance Search
- [ ] 4.6.4 E2E: Viewing a receipt opens a new tab with the uploaded file

> **Deviation, deliberately not resolved silently (AGENTS.md §13):** no Playwright config or
> `e2e/`/`tests/` directory exists anywhere in the repo yet, and `docs/TICKETS.md` scopes standing
> up Playwright E2E infrastructure to **ET020** ("Unit tests, integration tests, Playwright E2E,
> FRS traceability matrix, CI quality gates, release readiness"), still `Planned`. Building that
> harness now would (a) build ahead of the fixed ticket order (AGENTS.md §11) and (b) add a new
> pnpm dependency without asking first (CLAUDE.md permission model). These 4 scenarios are left
> unchecked and carried forward as an explicit follow-up for ET020 rather than skipped silently or
> built ahead of schedule.

**Checkpoint 4** (full quality gate, all green):
```bash
# Backend
dotnet build
dotnet format --verify-no-changes
dotnet test

# Frontend
pnpm --filter frontend lint
pnpm --filter frontend exec tsc --noEmit
pnpm --filter frontend build
pnpm --filter frontend test

# E2E (repo root)
npx playwright test
```

## 5. Archive & Ticket Status

- [ ] 5.1 Run `openspec archive et019-dashboard-reports`
- [ ] 5.2 Confirm `docs/TICKETS.md` ET019 row keeps `Change Name = et019-dashboard-reports` and
      `Status = In progress` — archiving happens before the PR is raised (by design, per
      `/implement`) and does not itself mean the ticket is `Done`; `/pr` sets `PR open (#N)`, and
      merge sets `Done`
