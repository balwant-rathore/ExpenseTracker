## Why

The ET020 traceability audit (`docs/TRACEABILITY.md`) flagged three drifts, deferred to ET021:
(1) `docs/FRS.md` §4.1.1 says Currency should "default to INR," but the validator rejects an
omitted `currency` outright instead of defaulting it; (2) a BR-10 storage gap was flagged against
`Expense` audit timestamps; (3) `ExpenseListPage` shows "New expense" for every role even though
`/expenses/new` is route-guarded to `Employee`/`Manager` only.

Re-investigating (2) surfaced it isn't a gap: `ADR-0009`/`ADR-0016` (both `Accepted`) already
twice decided `Expense` audit timestamps stay UTC instants — citing DST risk — with BR-10
satisfied by `ICompanyClock` conversion at comparison points, not by changing storage. Per user
decision, item (2) resolves as a `docs/TRACEABILITY.md` correction, not code, and is out of this
change's implementation scope.

## What Changes

- Backend: `CreateExpenseRequestValidator` no longer rejects an omitted `currency` on
  `POST /api/expenses` / `PUT /api/expenses/{id}`; `ExpenseService.CreateAsync`/`UpdateAsync`
  default a missing `currency` to `"INR"`. An explicitly-supplied non-`INR` currency is still
  rejected (`docs/FRS.md` §4.1.1 "Default to Rupees - INR"; no multi-currency support).
- Frontend: `ExpenseListPage` shows the "New expense" link only when the authenticated user's
  role is `Employee` or `Manager` (matching the `/expenses/new` route guard in
  `routes/AppRouter.tsx`), instead of unconditionally for every role.
- Docs: `docs/TRACEABILITY.md`'s BR-10 gap note is replaced with a resolution note re-affirming
  BR-10 is satisfied per `ADR-0009`/`ADR-0016` — no backend code changes for this item. The
  Currency ambiguity note is replaced with a resolution note pointing at this change's spec delta.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `expense-submission`: "Field-Level Validation" requirement changes from rejecting an omitted
  `currency` to defaulting it to `INR`; an explicitly-supplied non-`INR` value is still rejected.
- `expense-maintenance`: "Edit Field-Level and Business-Rule Validation" requirement's currency
  check changes the same way on `PUT /api/expenses/{id}` — `UpdateExpenseRequestValidator` reuses
  `CreateExpenseRequestValidator.RequiredCurrency` and has the identical omitted-currency-rejected
  behavior today (`docs/AGENTS.md` §13 "same as X is a checklist" — both call sites share the
  root cause and must both be fixed).
- `frontend-expense-submission-ui`: adds a requirement that the list screen's "New expense" entry
  point is visible only to roles permitted to reach `/expenses/new` (`Employee`, `Manager`).

## Impact

- **Backend**: `backend/src/Application/Expenses/CreateExpenseRequestValidator.cs`,
  `backend/src/Application/Expenses/UpdateExpenseRequestValidator.cs`,
  `backend/src/Application/Expenses/ExpenseService.cs` (`CreateAsync`, `UpdateAsync`),
  `backend/tests/UnitTests/Application/Expenses/CreateExpenseRequestValidatorTests.cs`,
  `backend/tests/UnitTests/Application/Expenses/UpdateExpenseRequestValidatorTests.cs`,
  `backend/tests/UnitTests/Application/Expenses/ExpenseServiceTests.cs`,
  `backend/tests/IntegrationTests/ExpenseSubmissionTests.cs` (if it exercises the missing-currency
  case).
- **Frontend**: `frontend/src/pages/ExpenseListPage.tsx`,
  `frontend/src/pages/ExpenseListPage.test.tsx`.
- **Docs**: `docs/TRACEABILITY.md` (BR-10 and Currency notes), `docs/TICKETS.md` (ET021 status/
  change-name column, handled outside this artifact per `/spec` step 13).
- No database schema change, no new migration, no ADR needed (BR-10 reaffirmed per existing
  ADR-0009/ADR-0016, not reversed).
- Ticket: `ET021` (`docs/TICKETS.md`), FRS §4 (Expenses), §8 (Dashboard — N/A, no dashboard change
  here despite the ticket's FRS-section list), §11 (Business Rules Glossary, BR-10).
