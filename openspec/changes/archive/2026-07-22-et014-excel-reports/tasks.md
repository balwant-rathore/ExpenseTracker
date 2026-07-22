## 1. Foundation

- [x] 1.1 Add `<PackageReference Include="ClosedXML" Version="0.105.0" />` to
      `backend/src/Infrastructure/Infrastructure.csproj`.
- [x] 1.2 Create `backend/src/Domain/Reporting/MonthlyReimbursementRecord.cs` in namespace
      `Domain.Reporting` (same 7 fields as the current `Application.Reports` record).
- [x] 1.3 Create `backend/src/Domain/Reporting/IMonthlyReimbursementReportGenerator.cs`
      (`byte[] Generate(IReadOnlyList<MonthlyReimbursementRecord> records)`, synchronous per
      design D2).
- [x] 1.4 Delete `backend/src/Application/Reports/MonthlyReimbursementRecord.cs` (moved to
      Domain in 1.2) and `backend/src/Application/Reports/MonthlyReimbursementReportResponse.cs`
      (dead code once the controller stops returning JSON — see task 2.3).
- [x] 1.5 Update `backend/src/Application/Reports/IReportService.cs`: `using Domain.Reporting;`
      in place of the deleted local record; add
      `Task<byte[]> GenerateMonthlyReimbursementExcelAsync(int year, int month, CancellationToken cancellationToken)`
      to the interface.

**Checkpoint:** `cd backend && dotnet build` → 0 errors (expect it to fail until `ReportService`
is updated in Phase 2 — acceptable at this checkpoint only if the sole errors are the
not-yet-implemented interface member; otherwise stop and fix). Frontend: N/A, no frontend files
touched by ET014.

Result: build failed with exactly the 3 expected `ReportService` errors (missing ctor arg,
mismatched return type, unimplemented interface member) — confirmed transitional, resolved in
Phase 2.

## 2. Core Implementation

- [x] 2.1 Implement `backend/src/Infrastructure/Reporting/ClosedXmlMonthlyReimbursementReportGenerator.cs`
      per design D5: header row (`Employee, Expense Number, Category, Amount, Currency,
      Approval Date, Reimbursement Date`), one data row per record, `Approval Date`/
      `Reimbursement Date` cells set via `Style.DateFormat.Format = "yyyy-MM-dd"`, empty
      `records` list yields header-only workbook, `SaveAs` to a `MemoryStream` and return
      `ToArray()`.
- [x] 2.2 Update `backend/src/Application/Reports/ReportService.cs`: inject
      `IMonthlyReimbursementReportGenerator` via constructor; implement
      `GenerateMonthlyReimbursementExcelAsync` to call the existing
      `GetMonthlyReimbursementAsync` then `_reportGenerator.Generate(records)`. Leave
      `GetMonthlyReimbursementAsync`'s body untouched.
- [x] 2.3 Update `backend/src/Api/Controllers/ReportsController.cs`
      `MonthlyReimbursement` action: after existing validation, call
      `GenerateMonthlyReimbursementExcelAsync`, build
      `$"Monthly-Reimbursement-{year:D4}-{month:D2}.xlsx"`, and return
      `File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName)`
      instead of `Ok(new MonthlyReimbursementReportResponse(...))`.
- [x] 2.4 Register the new generator in
      `backend/src/Api/Extensions/ReportServiceCollectionExtensions.cs`:
      `services.AddScoped<IMonthlyReimbursementReportGenerator, ClosedXmlMonthlyReimbursementReportGenerator>();`

**Checkpoint:** `cd backend && dotnet build` → 0 errors; `dotnet format --verify-no-changes` →
clean. Frontend: N/A.

Result: source projects (`Domain`, `Infrastructure`, `Application`, `Api`) built with 0 errors;
`dotnet format --verify-no-changes` clean. `dotnet build` still reported 3 errors confined to
`ReportServiceTests.cs` (Phase 4 scope) — expected transitional state.

## 3. Integration

- [x] 3.1 Grep the full `backend/` tree for any remaining reference to
      `Application.Reports.MonthlyReimbursementRecord` or
      `MonthlyReimbursementReportResponse` and fix/remove any stragglers (there are no
      `[ProducesResponseType]`/Swagger annotations on `ReportsController` today, so no OpenAPI
      attribute updates are expected — confirm this is still true before closing this task).
- [x] 3.2 Manually verify DI resolves and the endpoint serves a real `.xlsx` download — done via
      the Phase 4 integration tests (`MonthlyReimbursementReportTests.cs`), which already run the
      full ASP.NET Core pipeline (`WebApplicationFactory`) against the real local SQL Server
      instance and parse the returned bytes with ClosedXML, so a separate manual
      `dotnet run` + browser/Excel check would have exercised the identical code path.

**Checkpoint:** `cd backend && dotnet build` → 0 errors. Frontend: N/A.

Result: grep found zero stale references; no OpenAPI attributes present to update.

## 4. Tests

One task per spec delta scenario (`openspec/changes/et014-excel-reports/specs/monthly-reimbursement-report/spec.md`),
plus updates to existing ET012 tests broken by the response-format change.

- [x] 4.1 [Scenario: Finance downloads the monthly reimbursement workbook] Updated
      `MonthlyReimbursement_Finance_Returns200WithMatchingRecords` in
      `backend/tests/IntegrationTests/MonthlyReimbursementReportTests.cs`: asserts `200`,
      `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, parses
      the response bytes with `new XLWorkbook(new MemoryStream(bytes))`, and asserts a data row
      contains the expected expense number.
- [x] 4.2 [Scenario: Non-Finance role is rejected] `MonthlyReimbursement_NonFinanceRole_Returns403`
      passes unmodified — confirmed still green.
- [x] 4.3 [Scenario: Unauthenticated request is rejected] `MonthlyReimbursement_NoBearerToken_Returns401`
      passes unmodified — confirmed still green.
- [x] 4.4 [Scenario: Single-digit month is zero-padded in the file name] Added
      `MonthlyReimbursement_SingleDigitMonth_ZeroPadsFileName`: GET with `month=7`, asserts the
      response's `Content-Disposition` header names the file `Monthly-Reimbursement-2026-07.xlsx`.
- [x] 4.5 [Scenario: Header row matches FRS field order] Added
      `MonthlyReimbursement_HeaderRow_MatchesFrsFieldOrder`: asserts row 1 of the parsed workbook
      equals, in order, `Employee, Expense Number, Category, Amount, Currency, Approval Date,
      Reimbursement Date`.
- [x] 4.6 [Scenario: Date cells use Excel date formatting] Added
      `MonthlyReimbursement_DateCells_UseExcelDateFormatting`: seeds an expense with known
      Approval/Reimbursement dates, asserts the corresponding workbook cells' date values match
      and `Style.DateFormat.Format` is `"yyyy-MM-dd"`.
- [x] 4.7 [Scenario: A month with no reimbursements returns a header-only workbook] Added
      `MonthlyReimbursement_MonthWithNoReimbursements_ReturnsHeaderOnlyWorkbook` (no equivalent
      integration test existed at ET012 time — only a unit-level empty-list test did — so this is
      a new test, not an update as originally assumed when this task list was written): asserts
      `200` with a parsed workbook containing only the header row and zero data rows.
- [x] 4.8 Updated `MonthlyReimbursement_OnlyIncludesExpensesReimbursedWithinRequestedMonth` and
      `MonthlyReimbursement_ExcludesNonReimbursedExpensesEvenWithinMonth` to parse workbook rows
      instead of JSON `items`, preserving the same expense-number-present/absent assertions
      (these scenarios are unchanged in the spec — only the response parsing changed).
- [x] 4.9 Updated `backend/tests/UnitTests/Application/Reports/ReportServiceTests.cs`: added
      `using Domain.Reporting;` for the moved record type; confirmed the three existing tests
      (Client Entertainment approval date, non-Client-Entertainment approval date, empty-month
      list) still pass unmodified in behavior (each now constructs `ReportService` with a fake
      generator, since the constructor signature changed).
- [x] 4.10 Added `ReportServiceTests.GenerateMonthlyReimbursementExcelAsync_PassesFetchedRecordsToGenerator`:
      with a fake `IMonthlyReimbursementReportGenerator` (per design D3), asserts `ReportService`
      passes through exactly the records `GetMonthlyReimbursementAsync` fetched, and returns the
      generator's byte output unchanged.
- [x] 4.11 Confirmed `MonthlyReimbursementQueryValidatorTests.cs` required no code change and is
      still green.
- [x] 4.12 Added new unit test file
      `backend/tests/UnitTests/Infrastructure/Reporting/ClosedXmlMonthlyReimbursementReportGeneratorTests.cs`
      covering: header row order/text, one data row's values (including `Amount`/`Currency`),
      date cell formatting (`yyyy-MM-dd`), and an empty `records` list producing a header-only
      workbook with no exception.

**Checkpoint:** `cd backend && dotnet build` → 0 errors; `dotnet format --verify-no-changes` →
clean; `dotnet test --filter FullyQualifiedName~UnitTests` → all green; `dotnet test --filter FullyQualifiedName~IntegrationTests` → all green. Frontend: N/A (no `npm run build`/`lint`/`test`
applicable — ET014 has no frontend scope).

Result: build 0 errors, format clean, 262/262 unit tests green, 232/232 integration tests green
(11 in `MonthlyReimbursementReportTests.cs`, up from 5 before this ticket).

## 5. Archive

- [x] 5.1 Re-ran the full backend quality gate end-to-end: `dotnet build` (0 errors) →
      `dotnet format --verify-no-changes` (clean) → `dotnet test` (all projects: 262 unit + 232
      integration, all passing).
- [x] 5.2 Ran `openspec archive et014-excel-reports` to fold the spec delta into
      `openspec/specs/monthly-reimbursement-report/spec.md`.
- [x] 5.3 `docs/TICKETS.md` ET014 row `Status` left as `In progress` per `/implement`'s explicit
      instruction (archiving happens before the PR exists; `/pr` sets `PR open (#N)`, `Done` is
      reserved for after the PR merges).
