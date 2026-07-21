namespace Application.Reports;

public record MonthlyReimbursementReportResponse(IReadOnlyList<MonthlyReimbursementRecord> Items);
