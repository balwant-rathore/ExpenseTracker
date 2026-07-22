## Why

**ET019** (`docs/TICKETS.md`) covers the Dashboard, Finance Search, and Monthly Report UI (FRS §7,
8, 10). Finance search (filters, results, pagination, `/finance/search` route) was already fully
built and archived under ET018 (`frontend-finance-search-ui` capability), so it is excluded from
this change per the ticket owner's confirmation during `/spec` — ET019 covers only the Dashboard
UI and Monthly Report download UI that remain unbuilt. Both backend endpoints
(`GET /api/dashboard`, `GET /api/reports/monthly-reimbursement`) already exist and are fully
specced (`dashboard-summary`, `monthly-reimbursement-report` capabilities); today `DashboardPage`
is an explicit stub ("ET019 replaces this with the real role-specific dashboard") and no frontend
page/route calls the reports endpoint at all.

The ticket owner has also expanded this ticket's scope (per `/spec` command args) with two UX
additions surfaced during ET017/ET018 usage: (1) no page in the app currently offers backward/
breadcrumb navigation — `ExpenseDetailPage` has no back-link, and users reaching it from Finance
Search or the expense list have no way to retrace their path except the top-level nav bar; (2) an
expense's receipt attachment is stored and its filename is displayed on the detail screen, but
there is no way to actually open/view the uploaded file — `POST /api/attachments` is the only
attachment endpoint that exists.

## What Changes

- Build the Dashboard UI: role-specific metric tiles for Employee/Manager/Finance consuming the
  existing `GET /api/dashboard` (FRS §8.1, SDS §8.1–8.2, `dashboard-summary` capability).
  Compliance Officer has no dashboard (backend returns `403`); the frontend hides the Dashboard
  nav link for that role and redirects it to `/expenses` instead of `/dashboard` — a deviation
  from SDS §5.1's "All users land to dashboard after successful login" note, called out below.
- Build the Monthly Report download UI: a Finance-only screen with month/year dropdown pickers
  (current month pre-selected) that calls `GET /api/reports/monthly-reimbursement` and downloads
  the returned `.xlsx` file (FRS §7.1.4, §10.1, SDS §5.6, §8.4, `monthly-reimbursement-report`
  capability).
- Add breadcrumb navigation across authenticated pages, reflecting the actual page-visit history
  for the session (a dynamic stack, e.g. reaching an expense detail from Finance Search shows
  `Home > Finance Search > Expense`, from the expense list shows `Home > Expenses > Expense`),
  with a `Home` crumb always resolving to the dashboard (or `/expenses` for Compliance Officer,
  consistent with its redirect above). This directly fixes the ET017/ET018 gap where
  `ExpenseDetailPage` has no backward navigation.
- Add a way to view an expense's receipt attachment from the expense detail screen: a "View
  Receipt" link that opens the file inline in a new browser tab. Since the new download endpoint
  is a protected route requiring `Authorization: Bearer <access-token>` (AGENTS.md §8, SDS §5), a
  plain `<a href>` cannot authenticate the request — the frontend fetches the file via an
  authenticated request, then opens the result in a new tab as an object URL (not a direct link to
  the API route).
- Add a new backend endpoint, `GET /api/attachments/{id}`, to serve the stored file (purely
  additive — no existing contract changes). This does not exist
  today — `AttachmentsController` currently only exposes upload — and is not documented in
  `docs/SDS.md` §5.3, so it is an explicit deviation from the current SDS API surface requiring
  an ADR (`docs/decisions/ADR-0022-attachment-download-endpoint.md`, to be authored during
  `/plan`) and an update to `docs/SDS.md` §5.3 in this same change, per AGENTS.md §13. Design,
  confirmed with the ticket owner during `/spec`:
  - Authorization reuses the existing `ExpenseVisibility.BuildPredicate(role, employeeId)` rule
    (`Application/Expenses/ExpenseVisibility.cs`) — the same rule already enforced on
    `GET /api/expenses/{id}` — applied to the attachment's owning expense, so no new
    authorization logic is designed or tested; whoever can view the expense can view its
    attachment.
  - Response sets `Content-Disposition: inline` (not `attachment`) with the attachment's stored
    `ContentType`, so the browser renders the PDF/JPG/PNG directly instead of forcing a download.

## Capabilities

### New Capabilities
- `frontend-dashboard-ui`: role-specific dashboard screen (Employee/Manager/Finance metric tiles)
  consuming `GET /api/dashboard`; Compliance Officer nav-link hiding and redirect behavior.
- `frontend-monthly-report-ui`: Finance-only monthly reimbursement report screen with month/year
  pickers that downloads the `.xlsx` file from `GET /api/reports/monthly-reimbursement`.
- `frontend-breadcrumb-navigation`: dynamic, visit-history-based breadcrumb trail rendered across
  authenticated pages, with a `Home` crumb resolving per-role.
- `attachment-download`: backend `GET /api/attachments/{id}` endpoint reusing expense-visibility
  authorization, serving the file inline with its stored content type.
- `frontend-attachment-viewer-ui`: "View Receipt" link on the expense detail screen that opens the
  new download endpoint's response in a new browser tab.

### Modified Capabilities
- (none — the attachment view link is delivered as its own new capability rather than a
  requirement change to `frontend-expense-visibility-ui`, to mirror the existing
  `frontend-attachment-upload-ui` / `frontend-expense-submission-ui` split.)

## Impact

- **Backend**: new `AttachmentsController.Download` action (or new `GET` route on the existing
  controller), new `IAttachmentService.DownloadAsync` / `IFileStorageService` read-back method,
  new authorization test coverage reusing `ExpenseVisibility`. Requires
  `docs/SDS.md` §5.3 update (add the new endpoint row) and `docs/decisions/ADR-0022-*.md` in the
  same change, per AGENTS.md §13.
- **Frontend**: new `features/dashboard/` (page, API hook, metric-tile components), new
  `features/reports/` (page, API hook, month/year picker), new shared `Breadcrumbs` component in
  `components/` plus a navigation-history store (Zustand — UI-only state, no server data) wired
  into `AppLayout`, and a new `ViewReceiptLink`/attachment-viewer component consumed by
  `ExpenseDetailPage`.
- **No database schema changes** — attachment download reads existing `Attachment` metadata and
  filesystem storage; no new migration required.
- **No changes to any existing workflow, authorization, or business-rule enforcement** — dashboard
  and report endpoints are consumed exactly as already specced; attachment download reuses
  existing visibility rules verbatim.
