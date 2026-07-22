using Domain.Reporting;

namespace Application.Reports;

public interface IReportService
{
    Task<IReadOnlyList<MonthlyReimbursementRecord>> GetMonthlyReimbursementAsync(int year, int month, CancellationToken cancellationToken);

    Task<byte[]> GenerateMonthlyReimbursementExcelAsync(int year, int month, CancellationToken cancellationToken);
}
