using Domain.Enums;
using Domain.Repositories;

namespace Application.Dashboard;

public class DashboardService : IDashboardService
{
    private readonly IExpenseRepository _expenseRepository;

    public DashboardService(IExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public async Task<EmployeeDashboardResponse> GetEmployeeDashboardAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var counts = await GetCountsAsync(EmployeeRole.Employee, employeeId, cancellationToken);
        return new EmployeeDashboardResponse(
            TotalSubmitted(counts),
            Approved(counts),
            Reimbursed(counts));
    }

    public async Task<ManagerDashboardResponse> GetManagerDashboardAsync(Guid managerId, CancellationToken cancellationToken)
    {
        var counts = await GetCountsAsync(EmployeeRole.Manager, managerId, cancellationToken);
        var totalSubmitted = TotalSubmitted(counts);

        return new ManagerDashboardResponse(
            totalSubmitted,
            Approved(counts),
            Reimbursed(counts),
            totalSubmitted);
    }

    public async Task<FinanceDashboardResponse> GetFinanceDashboardAsync(CancellationToken cancellationToken)
    {
        var counts = await GetCountsAsync(EmployeeRole.Finance, Guid.Empty, cancellationToken);
        var totalSubmitted = TotalSubmitted(counts);

        return new FinanceDashboardResponse(
            totalSubmitted,
            Approved(counts),
            Reimbursed(counts),
            totalSubmitted,
            PendingReimbursements(counts));
    }

    // Single GROUP BY (Status, Category) query per request (design.md D1) - at most 7x7 = 49
    // rows come back regardless of table size, and every metric below is derived from this one
    // result set instead of issuing a separate COUNT query per metric. The EF Core query itself
    // lives in ExpenseRepository (Infrastructure) - Application never references EF Core.
    private Task<IReadOnlyList<StatusCategoryCount>> GetCountsAsync(EmployeeRole role, Guid employeeId, CancellationToken cancellationToken)
    {
        var predicate = DashboardScope.BuildPredicate(role, employeeId);
        return _expenseRepository.GetStatusCategoryCountsAsync(predicate, cancellationToken);
    }

    private static int TotalSubmitted(IReadOnlyList<StatusCategoryCount> counts) =>
        CountByStatus(counts, ExpenseStatus.Submitted);

    // Approved includes ComplianceApproved (docs/SDS.md §8.1 footnote).
    private static int Approved(IReadOnlyList<StatusCategoryCount> counts) =>
        CountByStatus(counts, ExpenseStatus.Approved) + CountByStatus(counts, ExpenseStatus.ComplianceApproved);

    private static int Reimbursed(IReadOnlyList<StatusCategoryCount> counts) =>
        CountByStatus(counts, ExpenseStatus.Reimbursed);

    // Excludes Approved Client Entertainment expenses still awaiting Compliance sign-off -
    // those are not yet eligible for the /reimburse action (design.md D3, confirmed during /spec).
    private static int PendingReimbursements(IReadOnlyList<StatusCategoryCount> counts) =>
        counts.Where(c => c.Status == ExpenseStatus.Approved && c.Category != ExpenseCategory.ClientEntertainment).Sum(c => c.Count)
        + CountByStatus(counts, ExpenseStatus.ComplianceApproved);

    private static int CountByStatus(IReadOnlyList<StatusCategoryCount> counts, ExpenseStatus status) =>
        counts.Where(c => c.Status == status).Sum(c => c.Count);
}
