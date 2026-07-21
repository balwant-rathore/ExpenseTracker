# AGENTS.md

Single source of truth for AI tools and contributors in this repo. Full specs live in
`docs/FRS.md` (functional requirements), `docs/SDS.md` (technical design), `docs/TICKETS.md`
(build order). This file summarizes them — when in doubt, the docs win.

> **Status:** Repo currently contains only `docs/` — no application code has been bootstrapped yet
> (ticket ET001 is `Planned`). Sections below describe the target architecture per the SDS, to be
> built in the ticket order given in `docs/TICKETS.md`.

## 1. Project Overview

An internal Expense Management System where employees submit expenses with receipts, managers
approve/reject them, a Compliance Officer reviews Client Entertainment expenses specifically, and
Finance reimburses approved expenses. Role-based workflow, auditing, and reporting are enforced
end-to-end on the backend.

## 2. Repository Structure

Frontend and backend both organize the same four feature areas: **Authentication, Expenses,
Dashboard, Reports**. See `docs/SDS.md` §2.1 for the full directory layout, or `ls`/`find`
the current tree — it's kept in sync as tickets land.

## 3. Tech Stack

See `frontend/package.json` and the backend `.csproj` files for exact versions in use;
full rationale for each choice is in `docs/SDS.md` §2.

## 5. Architecture Patterns

- Layered backend: `Api` → `Application` (business logic/DTOs/validators) → `Domain`
  (entities/enums) → `Infrastructure` (EF Core, storage, notifications). `Shared` = cross-cutting.
- Controllers contain **no business logic** — bind + delegate to `Application` services only.
- Backend is the single source of truth for business rules; frontend validation is UX-only.
- Frontend/backend talk **only** via REST/JSON over HTTPS.
- TanStack Query owns server state; Zustand owns local UI state only — never mix the two.
- State-changing operations run in DB transactions; side effects (notifications) fire **after
  commit** and never roll back the transaction on failure.
- Schema changes go exclusively through EF Core migrations. Cascade delete disabled on business
  entities.

## 6. Coding Standards

| Area | Convention |
|------|------------|
| C# | One public class/file, PascalCase types/members, `I*` interfaces, `_camelCase` private fields |
| C# services | Single responsibility, constructor injection only, `async/await` + `CancellationToken` |
| Database | PascalCase entities, `<EntityName>Id` keys |
| TypeScript | PascalCase components/types, `camelCase` vars/functions, `use*` hooks, `UPPER_SNAKE_CASE` constants |
| Nullable/implicit usings | Enabled; formatting via EditorConfig |

**Error contract** — one global exception handler; controllers/services do not duplicate it:

```json
{ "error": { "code": "ERROR_CODE", "message": "...", "fields": [], "traceId": "..." } }
```

`fields` populates only for validation errors. Never leak internals; never log passwords, hashes,
JWTs, refresh tokens, or OTPs.

| HTTP | Code | | HTTP | Code |
|------|------|---|------|------|
| 400 | `VALIDATION_ERROR` | | 409 | `RESOURCE_CONFLICT` |
| 401 | `AUTHENTICATION_FAILED` | | 410 | `RESOURCE_EXPIRED` |
| 403 | `AUTHORIZATION_FAILED` | | 422 | `BUSINESS_RULE_VIOLATION` |
| 404 | `RESOURCE_NOT_FOUND` | | 429 | `RATE_LIMIT_EXCEEDED` |
| | | | 500 | `INTERNAL_SERVER_ERROR` |

## 7. Auth Approach

Full JWT/refresh-token/OTP design detail moved to `backend/CLAUDE.md` §Auth Approach
(backend-only concern; frontend only calls the `/api/auth/*` endpoints below).

## 8. API Design Conventions

- REST/JSON, base path `/api`, stateless, HTTPS. Protected routes require `Authorization: Bearer
  <access-token>`. Timestamps ISO 8601; dates stored in company local timezone.
- Pagination/sorting are server-side always (per-endpoint defaults in `docs/SDS.md` §5.2/§5.4).
- Key endpoints (full contracts in `docs/SDS.md` §5):
  - `POST /api/auth/{register,login,refresh,logout,forgot-password,reset-password}`
  - `POST /api/expenses`, `GET /api/expenses`, `GET/PUT /api/expenses/{id}`
  - `POST /api/expenses/{id}/{submit,cancel,approve,reject,compliance-approve,compliance-reject,reimburse}`
  - `POST /api/attachments` (multipart, returns `attachmentId` used at expense create)
  - `POST /api/expenses/search` (Finance only), `GET /api/dashboard`,
    `GET /api/reports/monthly-reimbursement`
- Status transitions happen only via dedicated action endpoints — never a generic `PUT status=`.
- All public APIs documented via OpenAPI/Swagger.

## 9. DB Schema Summary

(Full definitions: `docs/SDS.md` §3. Entity/table-level detail moved to `backend/CLAUDE.md`
§DB Schema Summary — backend-only concern; frontend never touches EF Core entities directly.)

Enums: `EmployeeRole` (Employee, Manager, Finance, ComplianceOfficer) · `ExpenseCategory` (Travel,
Hotel, Meals, OfficeSupplies, ClientEntertainment, Training, Other — exactly these 7) ·
`ExpenseStatus` (Draft, Submitted, Approved, ComplianceApproved, Cancelled, Reimbursed, Rejected).

Workflow: `Draft → Submitted → Approved → Reimbursed`, except `ClientEntertainment` which routes
`Approved → ComplianceApproved → Reimbursed`. No backward transitions; rejected expenses are
terminal (employee must submit a new expense). Cancellation flow: `Draft` or `Submitted` → `Cancelled`.

## 10. Testing Approach

Run: `dotnet test` (backend), `pnpm --filter frontend test` (component), `npx playwright test`
(E2E). Tests are deterministic/independent and verify observable behavior, not implementation.
Business rules are tested once on the backend — frontend tests don't re-verify them. Every FRS
requirement must trace to a test; every defect fix needs a regression test. Test DBs are recreated
via EF Core migrations with Employee seed data loaded first.

## 11. Do NOT Do

- No business logic in controllers or the frontend.
- Never trust roles from the JWT — always re-resolve from the `Employee` record per request.
- Never embed anything beyond `sub` in the access token.
- Never let the frontend set `Expense.Status` directly — only dedicated workflow endpoints.
- No backward status transitions; no re-approving a `Rejected` expense.
- Managers cannot approve/reject their own expenses (BR-06).
- No more than the 7 fixed `ExpenseCategory` values.
- No Employee CRUD APIs — seed-only, read-only after setup.
- No real email/SMTP, live OTP delivery, multiple receipts, payment processing, or extra expense
  categories — see `docs/FRS.md` §12 (Out of Scope).
- Never reveal which field failed on login/registration/reset (anti-enumeration).
- Never store or log OTPs, passwords, or refresh tokens in plaintext.
- Notification failures must never roll back or block a workflow transaction.
- Never store server/business state in Zustand.
- Never bypass EF Core migrations for schema changes.
- Never skip rate limiting on the four auth endpoints.
- Don't build ahead of the fixed order in `docs/TICKETS.md`.

## 12. Shared Packages

No `/packages/shared` exists or is planned. Backend cross-cutting code lives in
`backend/src/Shared/`; frontend cross-cutting code lives in `frontend/src/types/` and
`frontend/src/utils/`. Frontend and backend share no code — only the REST API contract in
`docs/SDS.md` §5.

## 13. Implementation Completeness Guardrails

Added after ET007 needed four `/review` passes to reach compliance — each pass fixed the
specific thing it found, but missed that the same root cause applied more broadly. These rules
exist to catch that broader case on the *first* pass, before `/review` runs at all.

- **A rule that says "same as X" or "identical to X" is a checklist, not a phrase.** When a spec/
  design says two things are validated/handled "the same way" (e.g., "Submit re-runs the same
  checks as Create"; "all seven fields are mandatory"), enumerate every item that phrase covers
  and verify each one individually against the code — don't verify one instance and assume the
  rest follow. A fix that only handles the instance that triggered a bug report, without asking
  "does this same root cause apply to the other N-1 cases," is not done.
- **A design/proposal doc that goes stale during coding is itself a defect**, not acceptable
  documentation debt to leave for later. If implementation deviates from an already-approved
  `design.md`/`proposal.md` while coding (a type changes, a mechanism changes, a new file is
  added), update that doc in the *same* change — before calling the ticket ready for `/review`,
  not after a review catches the mismatch.
- **Diff generated artifacts against your actual intent, not just against "did it run."**
  Scaffolders (EF Core migrations, codegen, etc.) optimize for "won't break on apply," which is a
  different goal than "matches what you designed." Read the generated output line-by-line against
  what the design doc/ADR says before considering the task done.
- **Before marking a ticket ready for `/review`, self-check every "handled identically" claim by
  re-tracing both code paths side by side** — don't rely on one being a copy-paste of the other
  staying in sync as either evolves.