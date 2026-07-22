## Why

The backend expense APIs (expense creation/submission, edit/cancel, list/detail visibility,
attachment upload — ET006–ET009) are complete, but there is no UI for an Employee or Manager to
actually create, edit, cancel, or view their expenses. ET017 (`docs/TICKETS.md`) closes this gap
by building the Employee/Manager-facing expense screens defined in `docs/FRS.md` §4 (Expense
Create, Edit, Cancel, View), reusing the authentication/session/route-guard foundation delivered
in ET016.

## What Changes

- Add an expense creation form (`react-hook-form` + `zod`) collecting `expenseDate`, `category`,
  `amount`, `currency` (fixed `INR`), `description`, and a receipt attachment, with a `Draft` /
  `Submit` action choice (`docs/FRS.md` §4.1, `docs/SDS.md` §5.2 `POST /api/expenses`).
- Add a receipt-attachment picker shared by the create and edit forms: client-side validates file
  type (PDF/JPG/PNG) and size (≤10 MB) for immediate UX feedback, but the actual
  `POST /api/attachments` upload is deferred until the expense form is saved (as `Draft` or
  `Submit`) — not fired on file selection (`docs/FRS.md` §4.1.3–4.1.4, `docs/SDS.md` §5.3). The
  returned `attachmentId` is then passed as `receiptAttachmentId` on the same create/edit call.
- Add an expense edit form reusing the same fields/validation for `Draft` or `Submitted` expenses
  the owner may still edit, including replacing the receipt attachment (`docs/FRS.md` §4.2,
  `docs/SDS.md` §5.2 `PUT /api/expenses/{id}`).
- Add a "Submit" action on an existing `Draft` expense's detail/edit view, calling
  `POST /api/expenses/{id}/submit` (`docs/FRS.md` §4.1.2).
- Add a "Cancel" action on the owner's `Draft` or `Submitted` expense, calling
  `POST /api/expenses/{id}/cancel` (`docs/FRS.md` §4.3).
- Add a paginated, sortable expense list screen backed by `GET /api/expenses`, reflecting each
  authenticated role's default backend visibility (Employee: own only; Manager: own + direct
  reports' non-Draft) with no manager approve/reject actions in this ticket — those belong to
  ET018 (`docs/FRS.md` §4.4, `docs/SDS.md` §5.2). The list includes status, category, and
  date-range filter controls, using `GET /api/expenses`'s optional `status` query parameter for
  status and client-side filtering for category/date range within the already-fetched page.
- Add an expense detail screen showing full expense data (`GET /api/expenses/{id}`), rendering
  edit/submit/cancel actions only when the backend response indicates the caller is the owner and
  the expense is in an actionable status — a manager viewing a direct report's expense sees a
  read-only detail view.
- All client-side field/format checks mirror the backend's rules for UX only; every rule remains
  authoritative and re-enforced server-side (`docs/SDS.md` §1.3, `AGENTS.md` §11).

## Capabilities

### New Capabilities
- `frontend-expense-submission-ui`: Expense creation form (Draft/Submit action), and the
  Submit-existing-Draft action on the detail/edit view.
- `frontend-expense-maintenance-ui`: Expense edit form (Draft/Submitted expenses) and the Cancel
  action.
- `frontend-expense-visibility-ui`: Paginated/sortable/filterable expense list and the expense
  detail screen, honoring per-role backend visibility and read-only rendering for non-owned or
  non-actionable expenses.
- `frontend-attachment-upload-ui`: Shared receipt-attachment picker component (client-side type/
  size validation, deferred upload-on-save behavior) used by both the creation and edit forms.

### Modified Capabilities
- None. `frontend-route-guards` and `frontend-session-management` (ET016) are reused as-is to
  gate the new expense routes — no change to their requirements.

## Impact

- **Frontend**: New `frontend/src/features/expenses/` module (`api/`, `components/`, `schemas/`,
  `pages/` per `docs/SDS.md` §2.1), new routes registered under the existing authenticated route
  tree from ET016, new TanStack Query hooks for the expense/attachment endpoints, new Zod schemas
  mirroring (UX-only) the backend's expense field/business-rule validation.
- **Backend**: One small additive change identified during `/plan` and logged as
  **ADR-0020** (`docs/decisions/ADR-0020-expense-response-owner-and-attachment-fields.md`):
  `ExpenseResponse` gains `EmployeeNumber`, `ReceiptAttachmentId`, and
  `AttachmentOriginalFileName` fields, needed for ownership-based action visibility and
  attachment preview-on-edit. No new endpoint, no migration. Otherwise this ticket consumes
  existing APIs from ET006 (`attachment-upload`), ET007 (`expense-submission`), ET008
  (`expense-maintenance`), and ET009 (`expense-visibility`) as-is.
- **Dependencies**: No new pnpm packages expected beyond what ET016 already introduced
  (`react-hook-form`, `zod`, `@tanstack/react-query`); confirm during `/plan` before adding
  anything new.
