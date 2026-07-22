# ADR-0020: `ExpenseResponse` Gains `EmployeeNumber`, `ReceiptAttachmentId`, and `AttachmentOriginalFileName`

## Status

Accepted

## Context

ET017 (`docs/FRS.md` §4, `docs/SDS.md` §5.2) builds the Employee/Manager-facing expense
list, detail, edit, and cancel screens. Two requirements in the approved
`openspec/changes/et017-expense-ui` spec delta depend on data the current
`ExpenseResponse` record (`backend/src/Application/Expenses/ExpenseResponse.cs`, shipped by
ET007/ET008/ET009) does not expose:

- **`frontend-expense-visibility-ui`**'s "Action Visibility on Detail Reflects Ownership and
  Status" requirement — the detail screen must show Edit/Submit/Cancel only when the
  authenticated caller is the expense's owner. `ExpenseResponse` currently exposes only
  `EmployeeName` (a display string), not an owner identifier the frontend can compare
  itself against.
- **`frontend-attachment-upload-ui`**'s "Existing Attachment Preview on Edit" requirement —
  the edit form must show the current receipt attachment's file name. `ExpenseResponse`
  exposes no attachment reference or filename at all, even though `Expense.AttachmentId`
  and `Expense.Attachment.OriginalFileName` already exist on the domain entity
  (`docs/SDS.md` §3.6–3.7).

This gap was identified during `/plan ET017` and raised with the ticket owner before any
design was written (per `AGENTS.md` §13 — gaps must be flagged, not silently resolved). The
alternative (deriving ownership by comparing `EmployeeName` strings client-side, and
dropping the attachment-preview requirement) was rejected as fragile — name collisions are
possible, and it would leave the already-approved edit-form spec requirement unimplementable.

## Decision

Extend `ExpenseResponse` with three additive fields, all backed by data the `Expense`
entity already stores:

```csharp
public record ExpenseResponse(
    Guid Id,
    string ExpenseNumber,
    DateOnly ExpenseDate,
    string Category,
    decimal Amount,
    string Currency,
    string Description,
    string Status,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? ComplianceApprovedAt,
    DateTime? RejectedAt,
    string? RejectionComment,
    DateTime? ReimbursedAt,
    DateTime CreatedAt,
    string? EmployeeName,
    string? EmployeeNumber,             // new
    Guid ReceiptAttachmentId,           // new
    string? AttachmentOriginalFileName); // new
```

- `EmployeeNumber` is `expense.Employee?.EmployeeNumber` — requires the `Employee`
  navigation, which `GetByIdWithEmployeeAsync`/`GetPagedAsync` already `.Include()` (same
  condition as the existing `EmployeeName` field: null on paths that don't include
  `Employee`, e.g. `CreateAsync`/`UpdateAsync`/`SubmitAsync`/`CancelAsync`'s responses).
- `ReceiptAttachmentId` is `expense.AttachmentId` — a plain column, always populated
  regardless of `Include`s.
- `AttachmentOriginalFileName` is `expense.Attachment?.OriginalFileName`, which requires
  adding `.Include(e => e.Attachment)` to `ExpenseRepository.GetByIdWithEmployeeAsync` and
  `GetPagedAsync` (both currently `.Include(e => e.Employee)` only).

This is purely additive to an existing response shape — no field is removed or retyped, no
existing consumer (`GET /api/expenses`, `GET /api/expenses/{id}`) breaks, and no EF Core
migration is needed since no schema changes. `docs/SDS.md` §5 does not specify
`ExpenseResponse`'s exact field list (only "each mapped to `ExpenseResponse`"), so this is
not a contradiction of the SDS, but it does extend an established API contract beyond
ET007/ET008/ET009's original implementation, which is why it is logged here per this
repository's OpenSpec proposal rule (`openspec/config.yaml` `rules.proposal`).

## Addendum: `EmployeeId` (GUID) replaced with `EmployeeNumber` (string)

The first implementation of this ADR added `Guid EmployeeId` (`expense.EmployeeId`, the
`Expense` row's owning-`Employee` foreign key) instead of `EmployeeNumber`, on the
assumption the frontend could compare it against the authenticated user's own id. That
assumption was wrong, and was only caught during Phase 3's mandatory real-browser E2E
verification (`AGENTS.md`'s "start the dev server and use the feature in a browser" rule) —
every unit/component test up to that point mocked *both* sides of the comparison by hand,
so none of them could have caught a wrong-field bug like this.

The actual bug: the frontend's authenticated `User` object (`frontend/src/types/auth.ts`,
returned by `/api/auth/register`, `/login`, and `/me` — see `UserDto` in
`backend/src/Application/Auth/AuthService.cs`) exposes `id`, which is the **`User` row's own
GUID**, not `Employee.EmployeeId`. There is no endpoint that returns the authenticated
caller's `Employee.EmployeeId` to the frontend at all. Comparing
`expense.employeeId === user.id` therefore compared two unrelated GUIDs from two different
entities and could never match — every expense, including the owner's own, rendered as a
non-owner, read-only view (Edit/Submit/Cancel silently never appeared for anyone).

`EmployeeNumber` was substituted instead because it is the one owner-identifying field
already present on both sides without any further change: `ExpenseResponse` can expose the
expense owner's `Employee.EmployeeNumber` (same `Include(Employee)` this ADR already adds),
and the frontend `User` type already carries `employeeNumber` (added by ET004/ET016's
registration flow, which takes an `employeeNumber` as input). The alternative — adding
`EmployeeId` (GUID) to the shared `UserDto`/`User` type instead — was rejected as a larger,
riskier change: `UserDto` is constructed in three places across the auth module
(`RegisterAsync`, `LoginAsync`, `GetCurrentUserAsync`), shared by ET004/ET005/ET016, and
already has its own established test coverage; swapping in `EmployeeNumber` on
`ExpenseResponse` (a DTO this ADR is already modifying) is strictly smaller and keeps the
fix scoped to the capability that actually needs it.

## Consequences

- `backend/src/Application/Expenses/ExpenseResponse.cs`: `EmployeeNumber` (string?),
  `ReceiptAttachmentId`, `AttachmentOriginalFileName` — three new fields.
- `backend/src/Application/Expenses/ExpenseService.cs` (`Map`): populate the three new
  fields from the already-loaded `Expense`/`Attachment` entities.
- `backend/src/Infrastructure/Persistence/Repositories/ExpenseRepository.cs`:
  `GetByIdWithEmployeeAsync` and `GetPagedAsync` add `.Include(e => e.Attachment)`.
- Existing backend integration tests asserting on `ExpenseResponse`'s JSON shape
  (`ExpenseSubmissionTests`, `ExpenseMaintenanceTests`, `ExpenseVisibilityTests`) gain new
  field assertions rather than needing structural changes — additive fields don't break
  existing deserialization-based assertions unless a test asserts an exact/closed JSON
  shape. `EmployeeNumber` assertions are only meaningful on `GetById`/`GetAll` responses
  (where `Employee` is `Include`'d); `Create`/`Update` response assertions don't check it,
  since it's null there just like `EmployeeName` always has been.
- Frontend `types/expense.ts` (`ExpenseResponse` TS type, ET017) includes `employeeNumber`,
  `receiptAttachmentId`, and `attachmentOriginalFileName` from the start.
- `ExpenseDetail.tsx`/`ExpenseList.tsx` (ET017) compare `expense.employeeNumber` against
  `useAuthStore(state => state.user?.employeeNumber)` — never a GUID — to determine
  ownership. Prop name is `currentUserEmployeeNumber`, not `currentUserId`, specifically to
  make this distinction impossible to miss on future reads of the code.
- No change to any workflow rule, authorization rule, or visibility predicate — this is a
  read-model projection change only.
