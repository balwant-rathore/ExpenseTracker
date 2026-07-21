namespace Application.Reports;

public interface IReportService
{
    Task<IReadOnlyList<MonthlyReimbursementRecord>> GetMonthlyReimbursementAsync(int year, int month, CancellationToken cancellationToken);
}
