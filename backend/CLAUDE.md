@../AGENTS.md

Backend-specific rules for `backend/`. Stack, schema, and workflow details are in the root
`AGENTS.md` — this file covers only how to work inside this project.

## Commands (run from `backend/`)

```bash
dotnet build                          # compile, surfaces analyzer warnings
dotnet run --project src/Api          # run API locally
dotnet test                           # all tests
dotnet test --filter FullyQualifiedName~UnitTests        # unit only
dotnet test --filter FullyQualifiedName~IntegrationTests # integration only
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api
dotnet ef database update --project src/Infrastructure --startup-project src/Api
dotnet format                         # apply EditorConfig formatting
```

## Local Database Setup

The `Api` project has user secrets initialized (`UserSecretsId` in `Api.csproj`). No connection
string is ever committed to `appsettings.*.json`. One-time setup per developer machine, against
your own existing SQL Server 2022 instance:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=ExpenseTrackerDb;Trusted_Connection=True;TrustServerCertificate=True;" --project src/Api
```

## Framework Patterns

- New endpoint = thin controller action → `Application` service method → returns a DTO, never an
  EF entity directly.
- Business rules and workflow transitions live in `Application` services, not in controllers or
  EF entity classes.
- Register services in DI as interfaces (`IExpenseService` → `ExpenseService`); controllers and
  services depend on the interface, never the concrete type.
- Validation: FluentValidation (or DataAnnotations, whichever the project adopts first — stay
  consistent) runs before any service method touches the database.
- All EF Core queries for list/search endpoints apply filtering, sorting, and paging in the query
  itself (`IQueryable` composition) — never materialize a full table then filter in memory.
- Wrap multi-step state changes (e.g. approve → set audit fields → persist) in a single
  `DbContext` SaveChanges transaction; enqueue the notification only after that commit succeeds.

## Auth Approach

(Moved from `AGENTS.md` §7 — backend-only concern; frontend only calls the `/api/auth/*`
endpoints, per `docs/SDS.md` §5.1.)

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

## DB Schema Summary

(Moved from `AGENTS.md` §9 — backend-only concern. Full definitions: `docs/SDS.md` §3.
Enum values and workflow rules stay in the root `AGENTS.md` since frontend needs them too.)

- **Employee** — CSV-seeded, read-only after setup, no CRUD API, self-references `ManagerId`.
- **User** — 1:1 Employee, unique case-insensitive `Email`, `PasswordHash`.
- **RefreshToken** / **PasswordResetOtp** — 1:many from User, hashes only, expiry/used tracking.
- **Expense** — 1:many from Employee, 1:1 Attachment. `ExpenseNumber` = `EXP-yyyyMMdd-XXXX`
  (backend-generated, immutable). Full audit trail (`SubmittedAt/ApprovedAt/...By...Id/
  RejectionComment`) — system-managed only, never client-editable.
- **Attachment** — 1:1 Expense, metadata + `StoragePath` only (file on disk), max 10 MB,
  PDF/JPG/PNG only.

## Anti-Patterns to Avoid

- Don't return `Domain` entities from controllers — always map to an `Application`-layer DTO.
- Don't put `if (role == ...)` authorization checks inline in controllers — use policy-based
  authorization (`[Authorize(Policy = ...)]`) so rules live in one place.
- Don't call `SaveChanges` multiple times across one logical workflow transition.
- Don't inject `DbContext` directly into controllers — go through a service/repository. Exception:
  `HealthController`'s `GET /api/health` — a pure infra connectivity probe with no business logic
  — injects `ApplicationDbContext` directly to call `CanConnectAsync()` (per ET001 design.md D3).
  Any controller that touches business data still must go through an `Application` service.
- Don't use `.Result`/`.Wait()` on async calls; don't leave a method `async` without actually
  awaiting anything inside it.
- Don't hand-write SQL migrations — always generate via `dotnet ef migrations add`.
- Don't catch exceptions in a controller/service just to swallow or rethrow generically — let the
  global exception handler do its job.
