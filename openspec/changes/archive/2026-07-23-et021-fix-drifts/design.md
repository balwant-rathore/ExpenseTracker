## Context

ET021 closes three drifts the ET020 traceability audit flagged and deliberately deferred
(`docs/TRACEABILITY.md`). Per `proposal.md`, re-investigation during `/spec` resolved one of the
three (BR-10 audit-timestamp storage) as a documentation-only correction — `ADR-0009` and
`ADR-0016` (both `Accepted`) already twice decided that `Expense` audit timestamps stay UTC
instants, with BR-10 satisfied by `ICompanyClock`-based conversion at the point calendar-day
comparisons actually happen. This design covers only the two items with an actual code change:

1. **Currency defaulting** — `docs/FRS.md` §4.1.1 says Currency should "Default to Rupees - INR,"
   but both `CreateExpenseRequestValidator` and `UpdateExpenseRequestValidator` reject an omitted
   `currency` today (`.Equal(RequiredCurrency)` fails on `null`), and `ExpenseService.CreateAsync`/
   `UpdateAsync` use the null-forgiving operator (`request.Currency!`) on the assumption the
   validator already guaranteed non-null.
2. **New Expense button visibility** — `ExpenseListPage.tsx` renders the "New expense" link
   unconditionally; `/expenses/new` is `RequireRole`-guarded to `Employee`/`Manager` only
   (`routes/AppRouter.tsx`), so Finance/ComplianceOfficer see a link that bounces them to
   `/dashboard`.

Both are small, contained fixes with no schema, no new dependency, and (per the proposal's
Currency decision) a deliberate behavior change from "reject" to "default" — the kind of change
this repo's `AGENTS.md` §13 requires enumerating fully (both `Create` **and** `Update` call
sites) rather than fixing only the instance a bug report names.

## Goals / Non-Goals

**Goals:**
- `POST /api/expenses` and `PUT /api/expenses/{id}` accept a request that omits `currency`
  entirely and persist `Currency = "INR"`, while an explicitly-supplied non-`INR` value is still
  rejected with `400 VALIDATION_ERROR`.
- `ExpenseListPage` shows "New expense" only for `Employee`/`Manager` roles.
- `docs/TRACEABILITY.md`'s Currency and BR-10 notes are updated to reflect the resolutions
  (Currency: fixed; BR-10: reaffirmed as satisfied, no code change).

**Non-Goals:**
- No change to `Expense` audit-timestamp storage, `ICompanyClock`, `ReportService`, or
  `ExpenseService.SearchAsync`'s date-range comparison — BR-10 is out of this change's code scope
  per the proposal's resolution.
- No change to the frontend `ExpenseFormInput`/Zod schema — the create/edit forms already always
  send `currency: 'INR'` explicitly (`frontend/src/features/expenses/api/expenseApi.ts`); the
  defaulting behavior only matters to a caller that omits the field (e.g. a direct API client, or
  a test), which the frontend never does.
- No new ADR — the Currency change is a straightforward FRS-compliance fix (not an architecture
  deviation), and BR-10 is reaffirmed, not reversed.

## Decisions

### D1 — Default at the validator (via `.When`), not by rewriting the request before validation
`CreateExpenseRequestValidator`/`UpdateExpenseRequestValidator`'s `Currency` rule becomes
conditional: `.Equal(RequiredCurrency).When(x => x.Currency is not null)`. A `null` `Currency`
now passes validation (no error); an explicitly-supplied non-`INR` value still fails. The actual
default-to-`"INR"` assignment happens where every other field is finalized —
`ExpenseService.CreateAsync`/`UpdateAsync` — via `request.Currency ?? CreateExpenseRequestValidator.RequiredCurrency`
(reusing the existing `public const string RequiredCurrency = "INR"` rather than a second
literal). `UpdateExpenseRequestValidator` already references this same constant for its
`.Equal(...)` call, so no new constant/duplication is introduced.

Rejected alternative: default in a FluentValidation `.Custom(...)` rule that mutates
`context.InstanceToValidate.Currency`. Rejected because FluentValidation rules are meant to
validate, not mutate the model as a side effect — the existing `ExpenseService` is already the
place non-null-by-construction assumptions get finalized (see the pre-existing "Category/
Currency/Description/Action are non-null by this point" comment this change updates), so
defaulting there keeps validation and data-shaping concerns separate.

### D2 — `ExpenseService` computes the default explicitly, replacing the `!` null-forgiving usage
`CreateAsync` line `Currency = request.Currency!,` becomes
`Currency = request.Currency ?? CreateExpenseRequestValidator.RequiredCurrency,` (same pattern for
`UpdateAsync`'s `expense.Currency = request.Currency!;`). The adjacent comment ("Category/
Currency/Description/Action are non-null by this point") is corrected to say Currency is no
longer guaranteed non-null and is defaulted here — per `backend/CLAUDE.md`'s ET007 gotcha
("a missing/default value on a bound field must land somewhere that already rejects it — verify,
don't assume"), this is the one field of the four the comment covers that changes.

Rejected alternative: default in the `Api` layer (controller) before calling the validator.
Rejected because `docs/SDS.md` §1.3 "Controllers contain no business logic" — deciding what a
missing Currency becomes is a business rule (BR from FRS §4.1.1), not request binding.

### D3 — Frontend role check reuses the existing inline `useAuthStore` + role-conditional pattern
`ExpenseListPage.tsx` already reads `const user = useAuthStore((state) => state.user)` (for
`currentUserEmployeeNumber`). The "New expense" `<Link>` gets wrapped in
`{(user?.role === 'Employee' || user?.role === 'Manager') && (...)}`, matching the exact pattern
`AppLayout.tsx` already uses for its Finance-only nav links (`role === 'Finance' && (...)`) —
no new component, no new hook. `frontend/CLAUDE.md`'s "centralize role checks in one place" note
is about avoiding *scattered, inconsistent* role logic; this reuses the one pattern already
established for nav-visibility gating rather than introducing a second mechanism.

Rejected alternative: derive the allowed-roles list from `RequireRole`'s `allowedRoles` prop on
the `/expenses/new` route (a single source of truth). Rejected as unnecessary indirection for two
hardcoded roles that mirror an existing, working nav-link pattern; `AppRouter.tsx` and
`ExpenseListPage.tsx` both hardcode `['Employee', 'Manager']`/`Employee`/`Manager` today with no
prior drift between them, so there's no evidence this needs a shared constant to stay in sync.

## File Plan

**Backend — modify:**
- `backend/src/Application/Expenses/CreateExpenseRequestValidator.cs` — `Currency` rule gains
  `.When(x => x.Currency is not null)`.
- `backend/src/Application/Expenses/UpdateExpenseRequestValidator.cs` — same change, referencing
  `CreateExpenseRequestValidator.RequiredCurrency` as it already does.
- `backend/src/Application/Expenses/ExpenseService.cs` — `CreateAsync` (~line 82) and `UpdateAsync`
  (~line 320): `Currency = request.Currency ?? CreateExpenseRequestValidator.RequiredCurrency`;
  update the adjacent "non-null by this point" comments in both methods.

**Backend — tests to update:**
- `backend/tests/UnitTests/Application/Expenses/CreateExpenseRequestValidatorTests.cs` —
  `MissingCurrency_FailsWithoutThrowing` renamed/rewritten to
  `MissingCurrency_Passes` (asserts `result.IsValid` true), since a missing currency is no longer
  a validator-level failure. `CurrencyOtherThanInr_Fails` is unchanged (explicit bad value still
  rejected).
- `backend/tests/UnitTests/Application/Expenses/UpdateExpenseRequestValidatorTests.cs` — identical
  rename/rewrite of `MissingCurrency_FailsWithoutThrowing`.
- `backend/tests/UnitTests/Application/Expenses/ExpenseServiceTests.cs` — two new tests:
  `CreateAsync_MissingCurrency_DefaultsToInr` (build a `CreateExpenseRequest` with
  `Currency = null`, assert the created `Expense.Currency == "INR"`) and
  `UpdateAsync_MissingCurrency_DefaultsToInr` (same, via the update path). Existing
  `CreateRequest`/update-request test helpers already default `currency: "INR"` — these two new
  tests pass `Currency = null` explicitly rather than changing the shared helper's default (every
  other existing test keeps relying on the helper's `"INR"` default).
- `backend/tests/IntegrationTests/ExpenseSubmissionTests.cs` /
  `backend/tests/IntegrationTests/ExpenseMaintenanceTests.cs` — check for an existing
  missing-currency `400` assertion; if one exists it must be updated to expect `201`/`200` with
  `Currency: "INR"` in the response body instead (same "same as X is a checklist" scan for Create
  and Update).

**Frontend — modify:**
- `frontend/src/pages/ExpenseListPage.tsx` — wrap the existing `<Button render={<Link
  to="/expenses/new" />}>New expense</Button>` in a role check against the already-destructured
  `user`.
- `frontend/src/pages/ExpenseListPage.test.tsx` — two new tests: New Expense link renders for
  `Manager`/`Employee` (the existing `beforeEach` already seeds `role: 'Manager'`, so the current
  suite implicitly covers the Manager case once the conditional is added — add an explicit
  assertion), and does not render for `Finance`/`ComplianceOfficer` (set `useAuthStore.setState`
  role per case, matching `AppLayout.test.tsx`'s existing per-role nav-link test pattern).

**Docs — modify:**
- `docs/TRACEABILITY.md` — replace the Currency ambiguity flag (lines ~120-129) and the BR-10 gap
  note (lines ~304-317) with resolution notes per `proposal.md`'s "What Changes" (Docs) section.

**No DB migration, no new NuGet/pnpm package, no ADR.**

## Risks / Trade-offs

- **[Risk]** Widening acceptance (missing `currency` now succeeds instead of `400`) is a
  backward-compatible relaxation for any existing caller that always sent `"INR"` explicitly
  (the frontend does) — no behavior change for them. A caller that previously relied on the
  omitted-currency-rejected behavior (none identified in this codebase; only the two now-updated
  unit tests exercised it) would see a different outcome. **Mitigation**: this is exactly the
  FRS-compliance fix requested; the only currency value the system ever accepts is `INR` either
  way, so no invalid data can result from the relaxation.
- **[Trade-off]** `UpdateExpenseRequestValidator`'s `.When(x => x.Currency is not null)` mirrors
  `CreateExpenseRequestValidator`'s change by hand rather than the two validators sharing one
  currency-rule helper. **Mitigation**: they already independently duplicate the `.Equal(...)`
  call today (both reference the same `RequiredCurrency` constant but write their own `RuleFor`),
  so this preserves the existing duplication pattern rather than introducing a refactor
  unrelated to this ticket's scope.

## Build/Test/Lint Checkpoints

Backend (from `backend/`):
```
dotnet build
dotnet test --filter FullyQualifiedName~UnitTests
dotnet test --filter FullyQualifiedName~IntegrationTests
```

Frontend (from `frontend/`):
```
pnpm lint
pnpm exec tsc --noEmit
pnpm build
pnpm test
```
