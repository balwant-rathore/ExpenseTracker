using Domain.Repositories;

namespace Application.Reports;

public class ReportService : IReportService
{
    private readonly IExpenseRepository _expenseRepository;

    public ReportService(IExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public async Task<IReadOnlyList<MonthlyReimbursementRecord>> GetMonthlyReimbursementAsync(int year, int month, CancellationToken cancellationToken)
    {
        var rangeStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var rangeEnd = rangeStart.AddMonths(1);

        var expenses = await _expenseRepository.GetReimbursedForReportAsync(rangeStart, rangeEnd, cancellationToken);

        return expenses
            .Select(e => new MonthlyReimbursementRecord(
                $"{e.Employee.FirstName} {e.Employee.LastName}",
                e.ExpenseNumber,
                e.Category.ToString(),
                e.Amount,
                e.Currency,
                e.ComplianceApprovedAt ?? e.ApprovedAt,
                e.ReimbursedAt))
            .ToList();
    }
}
