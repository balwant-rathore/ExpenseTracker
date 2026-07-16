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

```
docs/                    Specs: FRS.md, SDS.md, TICKETS.md, decisions/ (ADRs), EmployeeSeedData.csv
frontend/                React 19 SPA (ET001, ET016-019)
  src/api/                 API client functions
  src/components/           Reusable UI (shadcn/ui)
  src/features/             Feature modules: auth, expenses, dashboard, reports
  src/hooks/, layouts/, pages/, routes/   Custom hooks, app shell, route pages, router config
  src/store/                Zustand stores (local UI state ONLY)
  src/types/, utils/         Shared types, shared utilities
backend/                 ASP.NET Core 9 Web API (ET001-ET015)
  src/Api/                   Controllers, middleware, auth wiring, DI, config
  src/Application/           Business logic, DTOs, validators, services
  src/Domain/                Entities, enums, interfaces
  src/Infrastructure/        EF Core, SQL Server, repositories, file storage, notifications
  src/Shared/                Common utilities, constants, extensions
```

Frontend and backend both organize the same four feature areas: **Authentication, Expenses,
Dashboard, Reports**.

## 3. Tech Stack

| Layer | Technology |
|-------|------------|
| Frontend | React 19 + TypeScript + Vite, shadcn/ui, Tailwind CSS, React Router |
| Frontend state | TanStack Query (server state), Zustand (UI state only), React Hook Form + Zod |
| Backend | ASP.NET Core 9 Web API, C# 13, Entity Framework Core 9 |
| Database | Microsoft SQL Server 2022 |
| Auth | JWT access tokens + server-persisted refresh tokens, BCrypt password hashing |
| Files / Reports | Local filesystem storage (metadata in DB); ClosedXML for `.xlsx` reports |
| Docs | OpenAPI / Swagger |
| Backend tests | xUnit; WebApplicationFactory (integration) |
| Frontend tests | Vitest + React Testing Library; Playwright (E2E) |
| Package mgmt | pnpm (JS/TS monorepo workspace), NuGet (.NET) |

## 4. Key Commands

> Once bootstrapped (ET001), expected shape — update as real scripts land.

```bash
pnpm install                                # install workspace deps
pnpm --filter frontend dev                  # frontend dev server
pnpm --filter frontend build|test|lint      # frontend build / component tests / lint
dotnet build|run --project src/Api|test     # backend build / run / test (from backend/)
dotnet ef migrations add <Name>             # add EF Core migration
dotnet ef database update                   # apply migrations
npx playwright test                          # E2E tests
```

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

- JWT access token: HS256, 15 min, claim is `sub` (UserId) only — no roles embedded. Refresh
  token: random, 7-day, stored server-side as SHA-256 hash only, **rotated on every use**; reuse
  of a revoked token invalidates all of that user's refresh tokens.
- Role is never trusted from the token — middleware resolves the user, loads their `Employee`
  record, and derives role on **every** request.
- BCrypt password hashing; policy: min 8 chars, ≥1 letter, ≥1 digit.
- Registration requires `email` + `password` + `employeeId` matched against a pre-seeded
  `Employee` record — no self-service employee creation, no social login.
- Password reset: 6-digit OTP, SHA-256 hashed, 10-min expiry, single-use, new OTP invalidates
  prior one, logged to console only (no real email). Successful reset revokes all refresh tokens.
- Auth errors are generic — never reveal which field failed or whether an account exists.
- Rate limiting on `register`, `login`, `forgot-password`, `reset-password` → `429` over the limit.

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

(Full definitions: `docs/SDS.md` §3.)

- **Employee** — CSV-seeded, read-only after setup, no CRUD API, self-references `ManagerId`.
- **User** — 1:1 Employee, unique case-insensitive `Email`, `PasswordHash`.
- **RefreshToken** / **PasswordResetOtp** — 1:many from User, hashes only, expiry/used tracking.
- **Expense** — 1:many from Employee, 1:1 Attachment. `ExpenseNumber` = `EXP-yyyyMMdd-XXXX`
  (backend-generated, immutable). Full audit trail (`SubmittedAt/ApprovedAt/...By...Id/
  RejectionComment`) — system-managed only, never client-editable.
- **Attachment** — 1:1 Expense, metadata + `StoragePath` only (file on disk), max 10 MB,
  PDF/JPG/PNG only.

Enums: `EmployeeRole` (Employee, Manager, Finance, ComplianceOfficer) · `ExpenseCategory` (Travel,
Hotel, Meals, OfficeSupplies, ClientEntertainment, Training, Other — exactly these 7) ·
`ExpenseStatus` (Draft, Submitted, Approved, ComplianceApproved, Cancelled, Reimbursed, Rejected).

Workflow: `Draft → Submitted → Approved → Reimbursed`, except `ClientEntertainment` which routes
`Approved → ComplianceApproved → Reimbursed`. No backward transitions; rejected expenses are
terminal (employee must submit a new expense). Cancellation flow: `Draft` or `Submitted` → `Cancelled`.

## 10. Testing Approach

| Level | Framework | Scope |
|-------|-----------|-------|
| Unit | xUnit | Business logic, validators, utilities |
| Integration | xUnit + WebApplicationFactory | APIs, middleware, EF Core, storage, notifications |
| Component | Vitest + React Testing Library | React components/UI |
| E2E | Playwright | Full user workflows |

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