## Context

ET012 built `GET /api/reports/monthly-reimbursement` returning JSON
(`MonthlyReimbursementReportResponse` wrapping `MonthlyReimbursementRecord[]`), with the
Finance-only authorization, `year`/`month` validation, and Reimbursed-only/month-filtered query
already in place (`ReportService.GetMonthlyReimbursementAsync` → `IExpenseRepository
.GetReimbursedForReportAsync`). The ET012/ET014 boundary was recorded explicitly at that time:
ET014 replaces the JSON body with an `.xlsx` file on the same endpoint, reusing the same query.
`docs/SDS.md` §2 names `ClosedXML` as the report-generation library and §8.4 specifies the
`.xlsx` format and `Monthly-Reimbursement-YYYY-MM.xlsx` file name.

Backend project references confirm the dependency graph: `Domain` has no references; `Application`
and `Infrastructure` both reference only `Domain` + `Shared` (siblings); `Api` references
`Application` + `Infrastructure` + `Shared`. `Infrastructure` cannot reference `Application`
types. This matters because the existing `MonthlyReimbursementRecord` DTO lives in
`Application.Reports`, but the new ClosedXML-based generator must live in `Infrastructure` (per
the `/spec` decision to keep the ClosedXML dependency out of `Application`) and therefore cannot
take an `Application`-namespaced type as a parameter.

## Goals / Non-Goals

**Goals:**
- Replace the JSON response of `GET /api/reports/monthly-reimbursement` with a generated
  `.xlsx` workbook, per `docs/FRS.md` §7.1.4/§10.1.2 and `docs/SDS.md` §5.6/§8.4.
- Keep the ClosedXML library dependency confined to `Infrastructure`, consistent with the
  `Domain`-defines-interface / `Infrastructure`-implements pattern already used for
  `IFileStorageService` (`Domain.Storage` → `Infrastructure.Storage.FileStorageService`).
- Preserve all currently-specified behavior that isn't the response representation: Finance-only
  authorization, `year`/`month` validation, Reimbursed-only/month-filtered data scope, and the
  Compliance-Approved-vs-Approved Approval Date rule.

**Non-Goals:**
- No change to the data query, business rules, or authorization logic already implemented in
  ET012 — only the output representation changes.
- No new API endpoint, no content-negotiation/format switch (confirmed during `/spec`: full
  replacement, not additive).
- No frontend work (ET019 consumes this endpoint later).
- No DB schema changes.

## Decisions

### D1. Move `MonthlyReimbursementRecord` from `Application.Reports` to `Domain.Reporting`
The record is a plain data shape (7 primitive/`DateTime?` fields) with no business logic
attached to it — it is produced by `ReportService` and now must also be consumed by an
`Infrastructure` component. Since `Infrastructure` cannot depend on `Application`, and `Domain`
is the shared base both `Application` and `Infrastructure` already depend on, moving the record
into a new `Domain.Reporting` namespace (alongside a new `IMonthlyReimbursementReportGenerator`
interface) resolves the layering conflict without introducing a duplicate near-identical DTO.
This mirrors the existing `IFileStorageService`/`IExpenseRepository` precedent: cross-layer
contracts and their data shapes live in `Domain`, implementations live in `Infrastructure`,
business logic that produces the data lives in `Application`.
- **Alternative considered — duplicate a `Domain`-level record and map to it in `ReportService`**:
  rejected as pure duplication (two identical 7-field records) for no behavioral benefit.
- **Alternative considered — add an `Infrastructure` → `Application` project reference** so the
  generator could depend on `Application.Reports.MonthlyReimbursementRecord` directly: rejected
  because it inverts the established dependency direction for no other component in this
  codebase and would need to be justified as a broader architectural change, not a side effect
  of one ticket.

### D2. `IMonthlyReimbursementReportGenerator` lives in `Domain`, implementation in `Infrastructure`
```csharp
// src/Domain/Reporting/IMonthlyReimbursementReportGenerator.cs
namespace Domain.Reporting;

public interface IMonthlyReimbursementReportGenerator
{
    byte[] Generate(IReadOnlyList<MonthlyReimbursementRecord> records);
}
```
Synchronous, not `async` — the work is in-memory workbook construction (CPU-bound), not I/O;
per `backend/CLAUDE.md` Anti-Patterns, an `async` method that never awaits anything is exactly
the pattern to avoid. `ReportService.GenerateMonthlyReimbursementExcelAsync` (see D3) stays
`async` because *it* awaits the real I/O (`GetMonthlyReimbursementAsync` → EF Core).

### D3. `ReportService` gains a second method rather than replacing the first
```csharp
// src/Application/Reports/IReportService.cs
public interface IReportService
{
    Task<IReadOnlyList<MonthlyReimbursementRecord>> GetMonthlyReimbursementAsync(
        int year, int month, CancellationToken cancellationToken);

    Task<byte[]> GenerateMonthlyReimbursementExcelAsync(
        int year, int month, CancellationToken cancellationToken);
}
```
`GetMonthlyReimbursementAsync` (the query/mapping method, including the Compliance-Approved-vs-
Approved Approval Date rule) is kept as-is and stays independently unit-testable — the existing
`ReportServiceTests.cs` coverage for that business rule remains valuable and is preserved
(only its `using` for the record's new `Domain.Reporting` namespace changes).
`GenerateMonthlyReimbursementExcelAsync` is new: it calls the existing method, then delegates to
`IMonthlyReimbursementReportGenerator.Generate(...)`. `ReportService`'s constructor gains an
`IMonthlyReimbursementReportGenerator` dependency alongside the existing `IExpenseRepository`.

### D4. Controller returns a file result; file naming is a controller-level concern
```csharp
[HttpGet("monthly-reimbursement")]
public async Task<IActionResult> MonthlyReimbursement(
    [FromQuery] MonthlyReimbursementQuery query, CancellationToken cancellationToken)
{
    // ...existing validation, unchanged...

    var year = query.Year!.Value;
    var month = query.Month!.Value;
    var content = await _reportService.GenerateMonthlyReimbursementExcelAsync(year, month, cancellationToken);
    var fileName = $"Monthly-Reimbursement-{year:D4}-{month:D2}.xlsx";
    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
}
```
`ControllerBase.File(bytes, contentType, fileDownloadName)` sets `Content-Disposition:
attachment; filename="..."` automatically — no manual header construction needed. Building the
zero-padded file name string from already-validated `year`/`month` is response formatting, not
business logic, so it stays in the controller (same class of decision as
`AttachmentsController` mapping `IFormFile` fields into a request DTO). `year:D4` / `month:D2`
zero-pad per the `/spec`-confirmed naming rule.

`MonthlyReimbursementReportResponse` (the JSON wrapper record) is deleted — it has no remaining
caller.

### D5. ClosedXML usage — manual cell writes, not `InsertData`
```csharp
// src/Infrastructure/Reporting/ClosedXmlMonthlyReimbursementReportGenerator.cs
namespace Infrastructure.Reporting;

public class ClosedXmlMonthlyReimbursementReportGenerator : IMonthlyReimbursementReportGenerator
{
    private const string DateFormat = "yyyy-MM-dd";
    private static readonly string[] Headers =
    [
        "Employee", "Expense Number", "Category", "Amount", "Currency",
        "Approval Date", "Reimbursement Date",
    ];

    public byte[] Generate(IReadOnlyList<MonthlyReimbursementRecord> records)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Monthly Reimbursement");

        for (var col = 0; col < Headers.Length; col++)
        {
            worksheet.Cell(1, col + 1).Value = Headers[col];
        }

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var row = i + 2;
            worksheet.Cell(row, 1).Value = record.EmployeeName;
            worksheet.Cell(row, 2).Value = record.ExpenseNumber;
            worksheet.Cell(row, 3).Value = record.Category;
            worksheet.Cell(row, 4).Value = record.Amount;
            worksheet.Cell(row, 5).Value = record.Currency;
            SetDateCell(worksheet.Cell(row, 6), record.ApprovalDate);
            SetDateCell(worksheet.Cell(row, 7), record.ReimbursementDate);
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetDateCell(IXLCell cell, DateTime? value)
    {
        if (value is null)
        {
            return;
        }

        cell.Value = value.Value;
        cell.Style.DateFormat.Format = DateFormat;
    }
}
```
`InsertData`/`InsertTable` (ClosedXML's bulk-insert helpers, confirmed via Context7) were
considered and rejected: they don't apply an explicit per-column date format, so `DateTime`
values would fall back to ClosedXML's default date style rather than the `yyyy-MM-dd` format
the spec delta requires. Manual cell writes give exact control over the two date columns and
keep the mapping obviously 1:1 with `MonthlyReimbursementRecord`'s field order (which already
matches `docs/FRS.md` §10.1.1's field order). A month with zero records produces a workbook with
only the header row — the data loop simply doesn't execute — satisfying the "header-only, not
`404`" requirement with no special-case branch.

### D6. `ClosedXML` package version
NuGet's index for `ClosedXML` (queried at plan time) lists `0.105.0` as the latest stable
release (`0.105.0-rc` precedes it; no newer stable exists). Pin
`<PackageReference Include="ClosedXML" Version="0.105.0" />` in `Infrastructure.csproj`, per
`docs/SDS.md` §2.2 ("dependency versions shall be explicitly specified").

### D7. DI registration
Add to the existing `ReportServiceCollectionExtensions.AddReportFoundation`:
```csharp
services.AddScoped<IMonthlyReimbursementReportGenerator, ClosedXmlMonthlyReimbursementReportGenerator>();
```
`AddScoped` matches the existing convention for every comparable registration in this codebase
(`IFileStorageService`, `IAttachmentService`, `IReportService` itself) — introducing a different
lifetime for one component with no stated reason would be an unexplained inconsistency.

## File-Level Change List

**New files**
- `backend/src/Domain/Reporting/MonthlyReimbursementRecord.cs` — moved from
  `Application.Reports`, namespace `Domain.Reporting`, fields unchanged.
- `backend/src/Domain/Reporting/IMonthlyReimbursementReportGenerator.cs` — new interface (D2).
- `backend/src/Infrastructure/Reporting/ClosedXmlMonthlyReimbursementReportGenerator.cs` — new
  implementation (D5).

**Modified files**
- `backend/src/Application/Reports/IReportService.cs` — add
  `GenerateMonthlyReimbursementExcelAsync`; `using Domain.Reporting;` replaces the local record.
- `backend/src/Application/Reports/ReportService.cs` — new constructor dependency
  (`IMonthlyReimbursementReportGenerator`), new method (D3); existing method body unchanged
  besides the `using`.
- `backend/src/Api/Controllers/ReportsController.cs` — return a file result (D4).
- `backend/src/Api/Extensions/ReportServiceCollectionExtensions.cs` — register the new
  generator (D7).
- `backend/src/Infrastructure/Infrastructure.csproj` — add `ClosedXML` package reference (D6).
- `backend/tests/UnitTests/Application/Reports/ReportServiceTests.cs` — update `using` to
  `Domain.Reporting`; add coverage for `GenerateMonthlyReimbursementExcelAsync` delegating to a
  fake `IMonthlyReimbursementReportGenerator` (verifies `ReportService` passes through the
  records it fetched, without depending on real ClosedXML output).
- `backend/tests/IntegrationTests/MonthlyReimbursementReportTests.cs` — replace JSON body
  assertions with: status `200`, `Content-Type`, `Content-Disposition` file name, and parsing
  the returned bytes with `new XLWorkbook(new MemoryStream(bytes))` (ClosedXML is already
  available transitively via the `Api` → `Infrastructure` project reference chain — no new test
  package reference needed) to assert header row and data row values.

**Deleted files**
- `backend/src/Application/Reports/MonthlyReimbursementReportResponse.cs` — no remaining
  caller once the controller returns a file result (D4).

**Unchanged (reused as-is)**
- `MonthlyReimbursementQuery` / `MonthlyReimbursementQueryValidator` — request-side validation
  is unaffected by the response-format change.
- `IExpenseRepository.GetReimbursedForReportAsync` and its `ExpenseRepository` implementation —
  data scope and filtering are unaffected.

## DB Changes

None. No entity, migration, or schema change of any kind — this ticket only changes how already-
queried data is rendered in the HTTP response.

## Risks / Trade-offs

- **[Risk] Breaking API change** — any existing client depending on the ET012 JSON contract
  stops working. → **Mitigation**: this was the explicitly agreed ET012/ET014 boundary from the
  start (recorded in the capability spec before ET012 was even implemented); no frontend
  currently consumes this endpoint (ET019, which builds the report-download UI, is still
  `Planned`), so there is no in-repo caller to break.
- **[Risk] Large months could produce a large in-memory workbook** (`byte[]` held fully in
  memory via `MemoryStream.ToArray()`) — acceptable for this system's scale (internal tool,
  monthly reimbursement volume); streaming the workbook directly to the response body would add
  complexity with no evidenced need at current scale.
- **[Risk] Cross-layer type move (D1) touches an ET012 file that already shipped** — small blast
  radius (one record, three call sites: `ReportService`, `IReportService`, the two test files
  already identified above); validated by re-running the full existing `ReportServiceTests`/
  `MonthlyReimbursementQueryValidatorTests`/`MonthlyReimbursementReportTests` suites after the
  move, not just checking it compiles.

## Migration Plan

No data migration. Deployment is a normal code deploy: merge → build → deploy. Rollback is a
plain revert of this change's commit(s) — no schema or data changes to unwind.

## Build/Test Checkpoints

Backend only (no frontend files touched by this ticket):
```bash
cd backend
dotnet build                                              # compile + analyzers
dotnet test --filter FullyQualifiedName~UnitTests          # ReportServiceTests, MonthlyReimbursementQueryValidatorTests
dotnet test --filter FullyQualifiedName~IntegrationTests   # MonthlyReimbursementReportTests
dotnet format                                              # EditorConfig formatting
```
No E2E (Playwright) run — this ticket has no user-facing frontend flow (ET019 will exercise this
endpoint from the UI later).

## Open Questions

None outstanding — the ambiguities identified at `/spec` time (response replacement scope,
generator layer placement, file naming, empty-workbook/date-format behavior) were resolved with
the ticket owner before this design was written, and are reflected in the spec delta at
`openspec/changes/et014-excel-reports/specs/monthly-reimbursement-report/spec.md`.
