using Application.Expenses;
using Domain.Reporting;
using Domain.Repositories;

namespace Application.Reports;

public class ReportService : IReportService
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IMonthlyReimbursementReportGenerator _reportGenerator;
    private readonly ICompanyClock _companyClock;

    public ReportService(IExpenseRepository expenseRepository, IMonthlyReimbursementReportGenerator reportGenerator, ICompanyClock companyClock)
    {
        _expenseRepository = expenseRepository;
        _reportGenerator = reportGenerator;
        _companyClock = companyClock;
    }

    public async Task<IReadOnlyList<MonthlyReimbursementRecord>> GetMonthlyReimbursementAsync(int year, int month, CancellationToken cancellationToken)
    {
        var localMonthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var rangeStart = _companyClock.ConvertLocalToUtc(localMonthStart);
        var rangeEnd = _companyClock.ConvertLocalToUtc(localMonthStart.AddMonths(1));

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

    public async Task<byte[]> GenerateMonthlyReimbursementExcelAsync(int year, int month, CancellationToken cancellationToken)
    {
        var records = await GetMonthlyReimbursementAsync(year, month, cancellationToken);
        return _reportGenerator.Generate(records);
    }
}
