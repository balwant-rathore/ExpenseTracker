using Application.Dashboard;
using Domain.Entities;
using Domain.Enums;
using UnitTests.Application.Expenses;

namespace UnitTests.Application.Dashboard;

public class DashboardServiceTests
{
    // ---- Employee ----

    // Scenario: Employee sees only their own counts
    [Fact]
    public async Task GetEmployeeDashboardAsync_CountsOnlyOwnSubmittedExpenses()
    {
        var employeeId = Guid.NewGuid();
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense { EmployeeId = employeeId, Status = ExpenseStatus.Submitted });
        repository.Expenses.Add(new Expense { EmployeeId = employeeId, Status = ExpenseStatus.Submitted });
        repository.Expenses.Add(new Expense { EmployeeId = Guid.NewGuid(), Status = ExpenseStatus.Submitted });
        repository.Expenses.Add(new Expense { EmployeeId = Guid.NewGuid(), Status = ExpenseStatus.Submitted });
        repository.Expenses.Add(new Expense { EmployeeId = Guid.NewGuid(), Status = ExpenseStatus.Submitted });
        var service = new DashboardService(repository);

        var result = await service.GetEmployeeDashboardAsync(employeeId, CancellationToken.None);

        Assert.Equal(2, result.TotalSubmitted);
    }

    // Scenario: Employee approved count includes Compliance Approved
    [Fact]
    public async Task GetEmployeeDashboardAsync_ApprovedIncludesComplianceApproved()
    {
        var employeeId = Guid.NewGuid();
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense { EmployeeId = employeeId, Status = ExpenseStatus.Approved });
        repository.Expenses.Add(new Expense { EmployeeId = employeeId, Status = ExpenseStatus.ComplianceApproved });
        var service = new DashboardService(repository);

        var result = await service.GetEmployeeDashboardAsync(employeeId, CancellationToken.None);

        Assert.Equal(2, result.Approved);
    }

    // Scenario: Employee Draft and Cancelled expenses are never counted
    [Fact]
    public async Task GetEmployeeDashboardAsync_DraftAndCancelledAreNeverCounted()
    {
        var employeeId = Guid.NewGuid();
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense { EmployeeId = employeeId, Status = ExpenseStatus.Draft });
        repository.Expenses.Add(new Expense { EmployeeId = employeeId, Status = ExpenseStatus.Cancelled });
        var service = new DashboardService(repository);

        var result = await service.GetEmployeeDashboardAsync(employeeId, CancellationToken.None);

        Assert.Equal(0, result.TotalSubmitted);
        Assert.Equal(0, result.Approved);
        Assert.Equal(0, result.Reimbursed);
    }

    // ---- Manager ----

    // Scenario: Manager's own expenses are excluded from every metric
    [Fact]
    public async Task GetManagerDashboardAsync_OwnExpensesExcludedFromEveryMetric()
    {
        var managerId = Guid.NewGuid();
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense
        {
            EmployeeId = managerId,
            Status = ExpenseStatus.Submitted,
            Employee = new Employee { ManagerId = Guid.NewGuid() },
        });
        var service = new DashboardService(repository);

        var result = await service.GetManagerDashboardAsync(managerId, CancellationToken.None);

        Assert.Equal(0, result.TotalSubmitted);
        Assert.Equal(0, result.PendingApprovals);
    }

    // Scenario: Manager sees only direct reports' counts
    [Fact]
    public async Task GetManagerDashboardAsync_CountsDirectReportsSubmittedExpense()
    {
        var managerId = Guid.NewGuid();
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Status = ExpenseStatus.Submitted,
            Employee = new Employee { ManagerId = managerId },
        });
        var service = new DashboardService(repository);

        var result = await service.GetManagerDashboardAsync(managerId, CancellationToken.None);

        Assert.Equal(1, result.TotalSubmitted);
        Assert.Equal(1, result.PendingApprovals);
    }

    // Scenario: Manager does not see an indirect report's expense
    [Fact]
    public async Task GetManagerDashboardAsync_IndirectReportsExpenseIsNotCounted()
    {
        var managerId = Guid.NewGuid();
        var directReportId = Guid.NewGuid();
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Status = ExpenseStatus.Submitted,
            Employee = new Employee { ManagerId = directReportId },
        });
        var service = new DashboardService(repository);

        var result = await service.GetManagerDashboardAsync(managerId, CancellationToken.None);

        Assert.Equal(0, result.TotalSubmitted);
    }

    // Scenario: Manager approved count includes Compliance Approved
    [Fact]
    public async Task GetManagerDashboardAsync_ApprovedIncludesComplianceApproved()
    {
        var managerId = Guid.NewGuid();
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Status = ExpenseStatus.Approved,
            Employee = new Employee { ManagerId = managerId },
        });
        repository.Expenses.Add(new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Status = ExpenseStatus.ComplianceApproved,
            Employee = new Employee { ManagerId = managerId },
        });
        var service = new DashboardService(repository);

        var result = await service.GetManagerDashboardAsync(managerId, CancellationToken.None);

        Assert.Equal(2, result.Approved);
    }

    // Scenario: Manager Draft and Cancelled expenses are never counted
    [Fact]
    public async Task GetManagerDashboardAsync_DraftAndCancelledAreNeverCounted()
    {
        var managerId = Guid.NewGuid();
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Status = ExpenseStatus.Draft,
            Employee = new Employee { ManagerId = managerId },
        });
        repository.Expenses.Add(new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Status = ExpenseStatus.Cancelled,
            Employee = new Employee { ManagerId = managerId },
        });
        var service = new DashboardService(repository);

        var result = await service.GetManagerDashboardAsync(managerId, CancellationToken.None);

        Assert.Equal(0, result.TotalSubmitted);
        Assert.Equal(0, result.Approved);
        Assert.Equal(0, result.Reimbursed);
        Assert.Equal(0, result.PendingApprovals);
    }

    // ---- Finance ----

    // Scenario: Finance sees organization-wide counts
    [Fact]
    public async Task GetFinanceDashboardAsync_CountsAcrossAllEmployees()
    {
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Status = ExpenseStatus.Submitted,
            Employee = new Employee { ManagerId = Guid.NewGuid() },
        });
        repository.Expenses.Add(new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Status = ExpenseStatus.Submitted,
            Employee = new Employee { ManagerId = Guid.NewGuid() },
        });
        var service = new DashboardService(repository);

        var result = await service.GetFinanceDashboardAsync(CancellationToken.None);

        Assert.Equal(2, result.TotalSubmitted);
    }

    // Scenario: Pending reimbursements excludes Approved Client Entertainment awaiting compliance
    [Fact]
    public async Task GetFinanceDashboardAsync_PendingReimbursementsExcludesApprovedClientEntertainment()
    {
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Category = ExpenseCategory.ClientEntertainment,
            Status = ExpenseStatus.Approved,
        });
        var service = new DashboardService(repository);

        var result = await service.GetFinanceDashboardAsync(CancellationToken.None);

        Assert.Equal(0, result.PendingReimbursements);
    }

    // Scenario: Pending reimbursements includes Compliance Approved Client Entertainment
    [Fact]
    public async Task GetFinanceDashboardAsync_PendingReimbursementsIncludesComplianceApprovedClientEntertainment()
    {
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Category = ExpenseCategory.ClientEntertainment,
            Status = ExpenseStatus.ComplianceApproved,
        });
        var service = new DashboardService(repository);

        var result = await service.GetFinanceDashboardAsync(CancellationToken.None);

        Assert.Equal(1, result.PendingReimbursements);
    }

    // Scenario: Pending reimbursements includes Approved non-Client-Entertainment expenses
    [Fact]
    public async Task GetFinanceDashboardAsync_PendingReimbursementsIncludesApprovedNonClientEntertainment()
    {
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Category = ExpenseCategory.Travel,
            Status = ExpenseStatus.Approved,
        });
        var service = new DashboardService(repository);

        var result = await service.GetFinanceDashboardAsync(CancellationToken.None);

        Assert.Equal(1, result.PendingReimbursements);
    }

    // Scenario: Pending approvals excludes Approved Client Entertainment awaiting compliance
    [Fact]
    public async Task GetFinanceDashboardAsync_PendingApprovalsExcludesApprovedClientEntertainment()
    {
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Category = ExpenseCategory.ClientEntertainment,
            Status = ExpenseStatus.Approved,
        });
        var service = new DashboardService(repository);

        var result = await service.GetFinanceDashboardAsync(CancellationToken.None);

        Assert.Equal(0, result.PendingApprovals);
    }

    // Scenario: Finance approved count includes Compliance Approved
    [Fact]
    public async Task GetFinanceDashboardAsync_ApprovedIncludesComplianceApproved()
    {
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense { EmployeeId = Guid.NewGuid(), Status = ExpenseStatus.Approved });
        repository.Expenses.Add(new Expense { EmployeeId = Guid.NewGuid(), Status = ExpenseStatus.ComplianceApproved });
        var service = new DashboardService(repository);

        var result = await service.GetFinanceDashboardAsync(CancellationToken.None);

        Assert.Equal(2, result.Approved);
    }

    // Scenario: Finance Draft and Cancelled expenses are never counted
    [Fact]
    public async Task GetFinanceDashboardAsync_DraftAndCancelledAreNeverCounted()
    {
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(new Expense { EmployeeId = Guid.NewGuid(), Status = ExpenseStatus.Draft });
        repository.Expenses.Add(new Expense { EmployeeId = Guid.NewGuid(), Status = ExpenseStatus.Cancelled });
        var service = new DashboardService(repository);

        var result = await service.GetFinanceDashboardAsync(CancellationToken.None);

        Assert.Equal(0, result.TotalSubmitted);
        Assert.Equal(0, result.Approved);
        Assert.Equal(0, result.Reimbursed);
        Assert.Equal(0, result.PendingApprovals);
        Assert.Equal(0, result.PendingReimbursements);
    }
}
