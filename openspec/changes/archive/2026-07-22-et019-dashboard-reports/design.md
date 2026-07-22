## Context

ET019's remaining frontend scope is the Dashboard UI and Monthly Report download UI (finance
search was already delivered under ET018 — see proposal.md). Both backend endpoints already exist
and are fully specced (`dashboard-summary`, `monthly-reimbursement-report`); `DashboardPage` is an
explicit stub today and no page calls the reports endpoint. The ticket owner also folded in two
UX gaps found during ET017/ET018 usage: no breadcrumb/back-navigation anywhere in the app, and no
way to actually view an uploaded receipt (only its file name is shown).

Existing frontend conventions this design must follow (confirmed by reading the current code):
- Each feature has `api/<verb><Noun>.ts` (TanStack Query hook) + `<noun>Api.ts` (raw
  `apiRequest` call) + `components/*.tsx` + a route in `AppRouter.tsx`, guarded by
  `ProtectedRoute`/`RequireRole`.
- `frontend/src/lib/apiClient.ts`'s `apiRequest<T>()` is `fetch`-based, assumes a JSON body, and
  always calls `response.json()` — it cannot be reused as-is for the two binary downloads this
  change needs (`.xlsx` report, receipt file).
- Zustand holds only ephemeral UI state (`authStore` precedent) — never server data.
- Backend: thin controller → `Application` service → DTO, `[Authorize(Policy = ...)]` at the
  class level with per-action overrides, `IRepository<T>` + capability-specific repository
  interfaces, `IUnitOfWork` only for state-changing paths.

## Goals / Non-Goals

**Goals:**
- Real Dashboard UI consuming `GET /api/dashboard` for Employee/Manager/Finance; Compliance
  Officer redirected away from it.
- Monthly Report screen with month/year pickers that downloads the `.xlsx` from
  `GET /api/reports/monthly-reimbursement`.
- A session-scoped breadcrumb trail reflecting actual navigation history, fixing
  `ExpenseDetailPage`'s lack of backward navigation.
- A "View Receipt" link on the expense detail screen, backed by a new
  `GET /api/attachments/{id}` endpoint that reuses `ExpenseVisibility.BuildPredicate` verbatim.

**Non-Goals:**
- Finance search UI (already built, ET018).
- Any change to workflow, business-rule, or existing authorization behavior.
- Any DB schema/migration change.
- Persisting breadcrumb history beyond the current browser session/tab (in-memory Zustand store;
  a hard reload legitimately resets it, per the approved spec's "freshly loaded page" scenario).
- In-app file preview/thumbnailing — the browser's native PDF/image viewer in a new tab is the
  entire UX, no custom viewer component.

## Decisions

### D1 — New `apiRequestBlob` helper in `frontend/src/lib/apiClient.ts`, not a new HTTP library
`apiRequest<T>()` stays JSON-only (every other endpoint in the app is JSON). Add a sibling
function in the same file:

```ts
export interface BlobResponse {
  blob: Blob
  fileName: string | null
}

function parseFileName(contentDisposition: string | null): string | null {
  const match = contentDisposition?.match(/filename="?([^";]+)"?/)
  return match ? match[1] : null
}

export async function apiRequestBlob(
  path: string,
  options: { accessToken?: string | null } = {},
): Promise<BlobResponse> {
  const headers: Record<string, string> = {}
  if (options.accessToken) headers.Authorization = `Bearer ${options.accessToken}`

  const response = await fetch(`/api${path}`, { headers })

  if (!response.ok) {
    const data: unknown = await response.json().catch(() => null)
    throw isErrorEnvelope(data) ? data.error : FALLBACK_ERROR
  }

  return { blob: await response.blob(), fileName: parseFileName(response.headers.get('Content-Disposition')) }
}
```
(`isErrorEnvelope`/`FALLBACK_ERROR` already exist in this file — reused, not duplicated.)

Both `frontend-monthly-report-ui` and `frontend-attachment-viewer-ui` consume this one helper.

**Alternative considered**: pull in `axios` for `responseType: 'blob'` support — rejected; no HTTP
client dependency exists in `frontend/package.json` today, and AGENTS.md/CLAUDE.md require asking
before adding a pnpm dependency. Extending the existing `fetch` client avoids that entirely and
keeps one HTTP code path in the app.

### D2 — File-save and inline-view both go through a shared "open a blob" utility
New `frontend/src/lib/blobDownload.ts`:
```ts
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}

export function openBlobInNewTab(blob: Blob): Window | null {
  const url = URL.createObjectURL(blob)
  return window.open(url, '_blank')
  // Intentionally not revoked immediately — the new tab needs the object URL to remain valid
  // while it renders; it is released when that tab/window is closed or navigated away (garbage
  // collected with the document), matching browser object-URL lifetime semantics.
}
```
`frontend-monthly-report-ui`'s download button calls `saveBlob`; `frontend-attachment-viewer-ui`'s
"View Receipt" link calls `openBlobInNewTab`. No new dependency (`file-saver` etc.) — both are
~10-line browser-API wrappers, consistent with "don't add a dependency for something this small."

### D3 — New `GET /api/attachments/{id}` endpoint, and why the controller's class-level policy must move
`AttachmentsController` currently declares
`[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]` at the **class** level. ASP.NET
Core combines (ANDs) class-level and action-level `[Authorize]` attributes — it does not let a
method-level attribute replace a class-level one. Adding the new `Download` action with only
`[Authorize]` (no policy) would still be gated by the class's `EmployeeOrManager` policy, silently
blocking Finance and Compliance Officer, which the approved spec explicitly requires to work.

Fix: move `[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]` from the class down
to the existing `Upload` action only, leaving the class with a bare `[Authorize]` (any
authenticated user) — this is exactly the pattern `ExpensesController` already uses (bare
`[Authorize]` on the class, per-action policies where an action needs one). `Upload`'s actual
authorization behavior is unchanged (still Employee/Manager only); this is an internal attribute
relocation, not a requirement change to the `attachment-upload` capability — no delta spec needed
for it, but it is called out here since it's easy to regress.

```csharp
[ApiController]
[Route("api/attachments")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManager)]
    public async Task<IActionResult> Upload(...) { ... }   // unchanged behavior

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var result = await _attachmentService.DownloadAsync(
            User.GetEmployeeId(), User.GetRole(), id, cancellationToken);

        if (!result.Succeeded)
        {
            return result.FailureReason == AttachmentDownloadFailureReason.AttachmentNotFound
                ? NotFound(new ErrorResponse(new ErrorDetail("RESOURCE_NOT_FOUND", "Attachment not found.", [], HttpContext.TraceIdentifier)))
                : StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse(new ErrorDetail("AUTHORIZATION_FAILED", "You do not have permission to perform this action.", [], HttpContext.TraceIdentifier)));
        }

        Response.Headers.ContentDisposition = $"inline; filename=\"{result.OriginalFileName}\"";
        return File(result.Content!, result.ContentType!);
    }
}
```
(No `fileDownloadName` argument to `File(...)` — that ASP.NET Core overload forces
`Content-Disposition: attachment`. Setting the header manually as `inline` is what makes the
browser render the file instead of prompting a save, per the approved spec.)

New Application-layer types (`Application/Attachments/`), mirroring `ExpenseResult`'s shape:
```csharp
public enum AttachmentDownloadFailureReason { None, AttachmentNotFound, NotVisible }

public record AttachmentDownloadResult(
    bool Succeeded, Stream? Content, string? ContentType, string? OriginalFileName,
    AttachmentDownloadFailureReason FailureReason)
{
    public static AttachmentDownloadResult Success(Stream content, string contentType, string originalFileName) =>
        new(true, content, contentType, originalFileName, AttachmentDownloadFailureReason.None);
    public static AttachmentDownloadResult Failure(AttachmentDownloadFailureReason reason) =>
        new(false, null, null, null, reason);
}
```
`IAttachmentService` gains `Task<AttachmentDownloadResult> DownloadAsync(Guid employeeId,
EmployeeRole role, Guid attachmentId, CancellationToken ct)`.

`AttachmentService.DownloadAsync` implementation:
1. `_attachmentRepository.GetByIdAsync(attachmentId, ct)` → null → `Failure(AttachmentNotFound)`.
2. New `IExpenseRepository.GetByAttachmentIdWithEmployeeAsync(attachmentId, ct)` (mirrors the
   existing `GetByIdWithEmployeeAsync`'s `Include(e => e.Employee).Include(e => e.Attachment)`
   pattern, filtering `e.AttachmentId == attachmentId` instead of `e.Id == id`) → null (attachment
   not yet linked to any expense, or its expense create failed and the orphan sweeper hasn't run
   yet) → `Failure(AttachmentNotFound)` — an unlinked attachment has no visibility to authorize
   against, so it is indistinguishable from "not found" to every caller, including its uploader.
3. `ExpenseVisibility.BuildPredicate(role, employeeId).Compile().Invoke(owningExpense)` → false →
   `Failure(NotVisible)`.
4. New `IFileStorageService.OpenReadAsync(string relativePath, CancellationToken ct)` returning
   `Stream` (mirrors `FileStorageService`'s existing `ToAbsolutePath` used by `DeleteAsync`) →
   `Success(stream, attachment.ContentType, attachment.OriginalFileName)`.

`FileStorageService.OpenReadAsync`:
```csharp
public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
{
    var absolutePath = ToAbsolutePath(relativePath);
    return Task.FromResult<Stream>(new FileStream(absolutePath, FileMode.Open, FileAccess.Read));
}
```

No new authorization policy is registered — the endpoint relies on the same
`ExpenseVisibility.BuildPredicate` runtime check `ExpenseService.GetByIdAsync` already uses, so
there is exactly one place per-expense visibility rules live, per AGENTS.md's anti-pattern
guidance against duplicating authorization logic.

**Requires**: `docs/decisions/ADR-0022-attachment-download-endpoint.md` (new ADR — this endpoint
does not exist in `docs/SDS.md` §5.3 today) and a `docs/SDS.md` §5.3 row added for
`GET /api/attachments/{id}`, both in this same change per AGENTS.md §13 ("a design doc that goes
stale during coding is itself a defect").

### D4 — Dashboard UI: new `frontend/src/features/dashboard/` feature folder
- `types/dashboard.ts` (or inline in `api/`): TS interfaces mirroring the three backend response
  records verbatim:
  ```ts
  export interface EmployeeDashboard { totalSubmitted: number; approved: number; reimbursed: number }
  export interface ManagerDashboard extends EmployeeDashboard { pendingApprovals: number }
  export interface FinanceDashboard extends ManagerDashboard { pendingReimbursements: number }
  export type DashboardResponse = EmployeeDashboard | ManagerDashboard | FinanceDashboard
  ```
  (Discriminate at render time by the authenticated user's `role` from `authStore`, not by
  inspecting which fields are present — same principle `RequireRole` already uses: role comes
  from the auth store, never inferred from response shape.)
- `api/dashboardApi.ts` → `getDashboard(accessToken)` calling `apiRequest<DashboardResponse>('/dashboard', { accessToken })`.
- `api/useDashboard.ts` → `useQuery` hook, same shape as `useExpense.ts`.
- `components/DashboardMetrics.tsx` → renders the role-appropriate tile set (reads `role` prop,
  shows 3/4/5 tiles per `frontend-dashboard-ui` spec).
- `pages/DashboardPage.tsx` replaces the current stub in `frontend/src/pages/DashboardPage.tsx`
  (same file path, real implementation).

### D5 — Monthly Report UI: new `frontend/src/features/reports/` feature folder
- `api/reportsApi.ts` → `downloadMonthlyReimbursementReport(year, month, accessToken)` calling
  `apiRequestBlob('/reports/monthly-reimbursement?year=...&month=...', { accessToken })`.
- `components/MonthYearPicker.tsx` → two shadcn `Select` controls (already used elsewhere, e.g.
  `FinanceSearchFilters`), year range = current year and the prior 4 years (5 options — no FRS/SDS
  constraint on how far back; matches the existing search screen's precedent of a small bounded
  dropdown rather than free text).
- `pages/MonthlyReportPage.tsx`, routed at `/reports/monthly-reimbursement`, `RequireRole
  allowedRoles={['Finance']}` (identical guard pattern to `/finance/search`).
- Download button calls `reportsApi` directly inside an `onClick` (a `useMutation` wrapping the
  blob fetch, mirroring how `useReimburseExpense` etc. wrap a `POST`), then `saveBlob(blob,
  fileName ?? fallbackName)` from D2, where `fallbackName` is computed client-side
  (`Monthly-Reimbursement-{year}-{month:02}.xlsx`) only if the header parse somehow returns
  `null` (defense-in-depth; the backend always sets it per `monthly-reimbursement-report`'s file
  naming requirement).

### D6 — Breadcrumb navigation: new Zustand store, not React Router state
New `frontend/src/store/breadcrumbStore.ts`:
```ts
interface BreadcrumbEntry { label: string; path: string }
interface BreadcrumbState {
  trail: BreadcrumbEntry[]
  push: (entry: BreadcrumbEntry) => void
  truncateTo: (index: number) => void
  reset: () => void
}
```
- `push` is called by each page on mount (via a small `useBreadcrumb(label)` hook) with its own
  label; it appends unless the trail's current last entry is the same `path` (avoids duplicate
  entries on re-render/refetch).
- `truncateTo(index)` is called when the user clicks an earlier crumb — slices `trail` to
  `index + 1` before navigating, per the approved spec's truncation requirement.
- `reset()` is called once at session bootstrap (`useSessionBootstrap`, which already runs once
  per app load) so a hard reload starts a fresh trail (per the approved spec's "freshly loaded
  page" scenario) — Zustand state is in-memory only, so a reload already clears it implicitly;
  `reset()` exists mainly for the logout path, so a new login doesn't inherit the previous
  session's trail.
- **Why Zustand and not deriving the trail purely from React Router's location/history**: React
  Router v7 (`createBrowserRouter`) does not expose a navigation-history stack to components (only
  the current location) and the trail must depend on *how* a route was reached (Finance Search vs.
  expense list), which router state alone can't encode without app-level bookkeeping — a small
  ephemeral UI-state store is exactly Zustand's documented role here (AGENTS.md "Zustand owns
  local UI state only"), not server state, so no TanStack Query overlap.
- New `frontend/src/components/Breadcrumbs.tsx`, rendered once inside `AppLayout` (replacing
  nothing — additive above `<Outlet/>`), reading `trail` from the store and a computed `Home`
  crumb (`/dashboard` or `/expenses` for `ComplianceOfficer`, per D7).
- Each page that should appear in the trail (`ExpenseListPage`, `FinanceSearchPage`,
  `ExpenseDetailPage`, `DashboardPage`, `MonthlyReportPage`) calls `useBreadcrumb('<Label>')` once
  on mount — a small hook, not per-page boilerplate logic.

### D7 — Compliance Officer dashboard redirect
`DashboardPage` checks `role === 'ComplianceOfficer'` and renders `<Navigate to="/expenses"
replace />` before calling `useDashboard()` at all (so no wasted 403 round-trip). `AppLayout`'s nav
`<Link to="/dashboard">` is conditionally omitted the same way the existing `Finance Search` link
already is conditional on role (`role === 'Finance'` today; add `role !== 'ComplianceOfficer'`
for the Dashboard link, `role === 'ComplianceOfficer'` for a `Link to="/expenses"` in its place).
`AppRouter.tsx`'s root redirect (`{ path: '/', element: <Navigate to="/dashboard" replace /> }`)
is left as-is — it still lands on `/dashboard`, which then immediately re-redirects
`ComplianceOfficer` to `/expenses`; this two-hop redirect is simpler than teaching the root route
about role, and is invisible to the user (both hops happen before paint).

### D8 — No backend DI/policy additions beyond what D3 already covers
`AddAttachmentFoundation` (`Api/Extensions/AttachmentServiceCollectionExtensions.cs`) needs no new
policy registration (D3 uses the existing bare-`[Authorize]` + service-layer visibility pattern,
not a new named policy). No changes to `AddDashboardFoundation`/`AddReportFoundation` — both
services are consumed as-is.

## File Manifest

**Backend (new)**:
- `Application/Attachments/AttachmentDownloadResult.cs`, `AttachmentDownloadFailureReason.cs`
- `docs/decisions/ADR-0022-attachment-download-endpoint.md`

**Backend (modified)**:
- `Api/Controllers/AttachmentsController.cs` — add `Download` action, move class-level policy to
  `Upload` (D3)
- `Application/Attachments/IAttachmentService.cs`, `AttachmentService.cs` — add `DownloadAsync`
- `Domain/Repositories/IExpenseRepository.cs`,
  `Infrastructure/Persistence/Repositories/ExpenseRepository.cs` — add
  `GetByAttachmentIdWithEmployeeAsync`
- `Domain/Storage/IFileStorageService.cs`, `Infrastructure/Storage/FileStorageService.cs` — add
  `OpenReadAsync`
- `docs/SDS.md` §5.3 — add the new endpoint row

**Frontend (new)**:
- `lib/blobDownload.ts` (D2)
- `features/dashboard/api/dashboardApi.ts`, `useDashboard.ts`, `components/DashboardMetrics.tsx`
- `features/reports/api/reportsApi.ts`, `components/MonthYearPicker.tsx`,
  `pages/MonthlyReportPage.tsx`
- `store/breadcrumbStore.ts`, `components/Breadcrumbs.tsx`, a `useBreadcrumb` hook (colocated with
  the store or in `hooks/`)
- `features/expenses/api/attachmentDownloadApi.ts` (or added to existing `attachmentApi.ts`) —
  `viewAttachment(id, accessToken)` calling `apiRequestBlob`
- `features/expenses/components/ViewReceiptLink.tsx`

**Frontend (modified)**:
- `lib/apiClient.ts` — add `apiRequestBlob`, export `BlobResponse` (D1)
- `pages/DashboardPage.tsx` — replaced with real implementation (D4, D7)
- `layouts/AppLayout.tsx` — conditional Dashboard link, render `<Breadcrumbs/>` (D6, D7)
- `routes/AppRouter.tsx` — add `/reports/monthly-reimbursement` route
- `pages/ExpenseDetailPage.tsx`, `features/expenses/components/ExpenseDetail.tsx` — render
  `<ViewReceiptLink/>`
- `types/expense.ts` — no field changes needed (`attachmentOriginalFileName`/
  `receiptAttachmentId` already present)

## DB Changes

None. `Attachment`/`Expense` schema is untouched — the download endpoint only reads existing
`StoragePath`/`ContentType`/`OriginalFileName` columns. No EF Core migration required.

## Risks / Trade-offs

- **[Risk] `window.open` after an `await` may be blocked as a popup by some browsers**, since the
  call is not perfectly synchronous with the click event once a network round-trip separates them.
  → **Mitigation**: check the returned `Window | null` from `openBlobInNewTab`; if `null`, show an
  inline fallback ("Your browser blocked the receipt from opening — click here to try again",
  retrying the same already-resolved blob synchronously on the retry click, which browsers do not
  block). Covered by the `frontend-attachment-viewer-ui` spec's error-surfacing requirement in
  spirit; add this specific popup-blocked case as an implementation-level test, not a new spec
  requirement (it's a browser-environment edge case, not a backend-driven error state).
- **[Risk] An `IFileStorageService.OpenReadAsync` `FileStream` left un-disposed on an early
  controller return** (e.g. if a future change adds an early-exit path) would leak a file handle.
  → **Mitigation**: `return File(result.Content!, result.ContentType!)` hands the stream to
  ASP.NET Core's `FileStreamResult`, which disposes it after writing the response — matches how
  `ReportsController` already returns a `MemoryStream` via the same `File()` helper; no manual
  `using`/dispose needed in the controller.
- **[Risk] Breadcrumb trail grows unbounded across a very long session** (many page visits without
  a reload). → **Mitigation**: out of scope for this change's correctness (the approved spec only
  requires accurate trails, not a length cap); note as a follow-up if it proves to be a real
  problem, not a blocking risk for this ticket.
- **[Trade-off] The breadcrumb "Home" crumb computation is duplicated in two places** (`AppLayout`
  for the nav link and `Breadcrumbs` for the Home crumb) rather than centralized in one hook. →
  Accepted: both need the identical `role === 'ComplianceOfficer' ? '/expenses' : '/dashboard'`
  one-liner; a shared `useHomeRoute()` hook is cheap enough to add during implementation if it
  turns out to drift, but isn't designed as a hard requirement here.

## Migration Plan

No data migration. Deployment order: backend (new endpoint + SDS/ADR docs) can ship independently
of frontend (it's purely additive, no existing contract changes) — but for a coherent PR, ship
both together per this ticket. Rollback is a plain revert; no schema or data to roll back.

## Open Questions

None outstanding — all ambiguities raised during `/spec` were resolved with the ticket owner
(finance-search exclusion, breadcrumb model, attachment authorization scope, inline vs. download,
Compliance Officer landing route, report picker UX) and are recorded in proposal.md and the spec
deltas.

## Verification Commands

Backend (`backend/`):
```bash
dotnet build
dotnet test --filter FullyQualifiedName~UnitTests
dotnet test --filter FullyQualifiedName~IntegrationTests
```

Frontend (`frontend/`):
```bash
pnpm lint
pnpm exec tsc --noEmit
pnpm build
pnpm test
```

E2E (repo root, only if the ticket's user-facing flows warrant it per AGENTS.md's quality gates):
```bash
npx playwright test
```
