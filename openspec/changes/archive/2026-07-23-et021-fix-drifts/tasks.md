## Phase 1: Foundation

No new API contract, DTO, or EF Core migration is needed — `CreateExpenseRequest`/
`UpdateExpenseRequest` already have a nullable `string? Currency`, and `Expense.Currency` is
unchanged (`docs/SDS.md` §3.6). This phase only confirms that starting point.

- [x] 1.1 Confirm no schema/DTO/migration change is required (per `design.md` Non-Goals): `git
      diff` should show zero changes under `backend/src/Domain/`,
      `backend/src/Infrastructure/Persistence/Migrations/`, and no new/changed public properties
      on `CreateExpenseRequest`/`UpdateExpenseRequest`.

**Checkpoint:** `dotnet build` (backend) → 0 errors. No frontend changes yet, so no frontend
checkpoint at this phase.

## Phase 2: Core Implementation

Backend (currency defaulting) and frontend (New Expense button visibility) touch disjoint files
in different workspaces and share no dependency — safe to run as `[PARALLEL]` in separate
worktrees per `CLAUDE.md`'s "Frontend and backend work must run in separate worktrees."

- [x] 2.1 [PARALLEL] `backend/src/Application/Expenses/CreateExpenseRequestValidator.cs`: change
      the `Currency` rule to `.Equal(RequiredCurrency).When(x => x.Currency is not null)` so an
      omitted `currency` passes validation (design.md D1).
- [x] 2.2 [PARALLEL] `backend/src/Application/Expenses/UpdateExpenseRequestValidator.cs`: apply
      the identical `.When(x => x.Currency is not null)` change, still referencing
      `CreateExpenseRequestValidator.RequiredCurrency`.
- [x] 2.3 [PARALLEL] `backend/src/Application/Expenses/ExpenseService.cs` `CreateAsync`: replace
      `Currency = request.Currency!,` with
      `Currency = request.Currency ?? CreateExpenseRequestValidator.RequiredCurrency,`; correct
      the adjacent "Category/Currency/Description/Action are non-null by this point" comment to
      note Currency is now defaulted here, not guaranteed non-null (design.md D2).
- [x] 2.4 [PARALLEL] `backend/src/Application/Expenses/ExpenseService.cs` `UpdateAsync`: apply the
      identical `request.Currency ?? CreateExpenseRequestValidator.RequiredCurrency` change and
      comment correction (same root cause as 2.3 — `AGENTS.md` §13 checklist rule).
- [x] 2.5 [PARALLEL] `frontend/src/pages/ExpenseListPage.tsx`: wrap the "New expense" `<Button>`
      in a condition on the already-destructured `user`'s role
      (`user?.role === 'Employee' || user?.role === 'Manager'`), matching the pattern
      `AppLayout.tsx` uses for its Finance-only nav links (design.md D3).

**Checkpoint:**
- Backend: `dotnet build` → 0 errors; `dotnet format --verify-no-changes`.
- Frontend: `pnpm build` (or `npm run build`) → 0 errors; `pnpm lint -- --max-warnings 0`.

## Phase 3: Integration

- [x] 3.1 Search `backend/tests/IntegrationTests/ExpenseSubmissionTests.cs` and
      `backend/tests/IntegrationTests/ExpenseMaintenanceTests.cs` for any existing test asserting
      `400`/rejection on an omitted `currency`; if found, update it to assert success (`201`/`200`)
      with `Currency: "INR"` in the response body instead of deleting it. (None found — no
      integration test exercised the missing-currency case; nothing to update.)
- [x] 3.2 `docs/TRACEABILITY.md`: replace the Currency ambiguity flag (the "Ambiguity flagged, not
      silently resolved (Currency 'defaults to INR')" note under §4.1) with a resolution note
      pointing at `et021-fix-drifts`'s spec delta and the new defaulting behavior.
- [x] 3.3 `docs/TRACEABILITY.md`: replace the BR-10 gap note (under §11 Business Rules Glossary)
      with a resolution note re-affirming BR-10 is satisfied per `ADR-0009`/`ADR-0016` — explicitly
      state no code change was made for this item and why (per `proposal.md`'s Why section).

**Checkpoint:** `dotnet build` (backend) → 0 errors; `pnpm build` (frontend) → 0 errors. Re-run
`openspec validate et021-fix-drifts --strict` → must still pass.

## Phase 4: Tests

One task per spec-delta scenario. Unchanged scenarios (carried over verbatim into the MODIFIED
requirement blocks) are grouped into single verification tasks rather than rewritten; changed/
added scenarios each get their own task.

- [x] 4.1 `expense-submission` — verify unchanged scenarios still pass with no code change:
      *Invalid category is rejected*, *Explicit non-INR currency is rejected*, *Description over
      500 characters is rejected* (existing `CategoryOutsideDefinedValues_Fails`,
      `CurrencyOtherThanInr_Fails`, `DescriptionOverFiveHundredCharacters_Fails` in
      `CreateExpenseRequestValidatorTests.cs`).
- [x] 4.2 `expense-submission` — scenario *Omitted currency defaults to INR*: in
      `CreateExpenseRequestValidatorTests.cs`, rename/rewrite `MissingCurrency_FailsWithoutThrowing`
      to `MissingCurrency_Passes` (asserts `result.IsValid` true); in `ExpenseServiceTests.cs`, add
      `CreateAsync_MissingCurrency_DefaultsToInr` asserting the created `Expense.Currency == "INR"`
      when `CreateExpenseRequest.Currency` is `null`.
- [x] 4.3 `expense-maintenance` — verify unchanged scenarios still pass with no code change:
      *Invalid category is rejected*, *Explicit non-INR currency is rejected*, *Description over
      500 characters is rejected*, *Zero or negative amount is rejected*, *Future expense date is
      rejected* (existing tests in `UpdateExpenseRequestValidatorTests.cs`/`ExpenseServiceTests.cs`).
- [x] 4.4 `expense-maintenance` — scenario *Omitted currency defaults to INR*: in
      `UpdateExpenseRequestValidatorTests.cs`, rename/rewrite `MissingCurrency_FailsWithoutThrowing`
      to `MissingCurrency_Passes`; in `ExpenseServiceTests.cs`, add
      `UpdateAsync_MissingCurrency_DefaultsToInr` asserting the updated `Expense.Currency == "INR"`.
- [x] 4.5 `frontend-expense-submission-ui` — scenario *Employee sees the New expense link*: add a
      test in `ExpenseListPage.test.tsx` setting `role: 'Employee'` and asserting the link renders.
- [x] 4.6 `frontend-expense-submission-ui` — scenario *Manager sees the New expense link*: add an
      explicit assertion for the existing `role: 'Manager'` `beforeEach` fixture (implicitly
      exercised already by every existing test in the file, but not yet asserted directly).
- [x] 4.7 `frontend-expense-submission-ui` — scenario *Finance does not see the New expense link*:
      add a test setting `role: 'Finance'` and asserting the link does not render.
- [x] 4.8 `frontend-expense-submission-ui` — scenario *Compliance Officer does not see the New
      expense link*: add a test setting `role: 'ComplianceOfficer'` and asserting the link does not
      render.

**Checkpoint:**
- Backend: `dotnet test` (all — unit + integration) → all green.
- Frontend: `pnpm test` (or `npm run test`) → all green.

## Phase 5: Archive

- [x] 5.1 Run full quality gates in order per `AGENTS.md`/`CLAUDE.md`: lint → build → unit tests →
      integration tests. E2E (`npx playwright test`) is not required for this change — the New
      Expense button visibility is covered by a Vitest/RTL test, matching the existing precedent
      for nav-link role-visibility (`AppLayout.test.tsx`), and no new user-facing workflow is
      introduced. (Backend: `dotnet format --verify-no-changes` clean, `dotnet build` 0 errors,
      `dotnet test` 293 unit + 267 integration passed. Frontend: `pnpm lint` clean, `tsc -b`
      clean, `pnpm build` 0 errors, `pnpm test` 243 passed.)
- [x] 5.2 Run `openspec archive et021-fix-drifts` (moves the change into
      `openspec/changes/archive/`, updates `openspec/specs/expense-submission`,
      `openspec/specs/expense-maintenance`, and `openspec/specs/frontend-expense-submission-ui`).
- [x] 5.3 Update `docs/TICKETS.md` ET021 row: `Status` → `PR open (#N)` once the PR is opened via
      `/pr` (per the Status-column convention in `docs/TICKETS.md`'s Notes section — archive
      happening before the PR is raised does not mean `Done`). Status kept at `In progress` for
      now per `/implement`'s instruction — `/pr` sets `PR open (#N)`.
