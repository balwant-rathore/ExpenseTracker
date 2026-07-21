using Application.Expenses;
using Domain.Entities;
using Domain.Enums;

namespace UnitTests.Application.Expenses;

public class ExpenseVisibilityTests
{
    // ---- Employee ----

    [Fact]
    public void Employee_OwnExpense_IsVisible()
    {
        var employeeId = Guid.NewGuid();
        var expense = new Expense { EmployeeId = employeeId, Status = ExpenseStatus.Submitted };

        var isVisible = Invoke(EmployeeRole.Employee, employeeId, expense);

        Assert.True(isVisible);
    }

    [Fact]
    public void Employee_OthersExpense_IsNotVisible()
    {
        var employeeId = Guid.NewGuid();
        var expense = new Expense { EmployeeId = Guid.NewGuid(), Status = ExpenseStatus.Submitted };

        var isVisible = Invoke(EmployeeRole.Employee, employeeId, expense);

        Assert.False(isVisible);
    }

    [Fact]
    public void Employee_OwnDraftExpense_IsVisible()
    {
        var employeeId = Guid.NewGuid();
        var expense = new Expense { EmployeeId = employeeId, Status = ExpenseStatus.Draft };

        var isVisible = Invoke(EmployeeRole.Employee, employeeId, expense);

        Assert.True(isVisible);
    }

    // ---- Manager ----

    [Fact]
    public void Manager_OwnExpense_AnyStatus_IsVisible()
    {
        var managerId = Guid.NewGuid();
        var expense = new Expense { EmployeeId = managerId, Status = ExpenseStatus.Draft };

        var isVisible = Invoke(EmployeeRole.Manager, managerId, expense);

        Assert.True(isVisible);
    }

    [Fact]
    public void Manager_DirectReportsNonDraftExpense_IsVisible()
    {
        var managerId = Guid.NewGuid();
        var expense = new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Status = ExpenseStatus.Submitted,
            Employee = new Employee { ManagerId = managerId },
        };

        var isVisible = Invoke(EmployeeRole.Manager, managerId, expense);

        Assert.True(isVisible);
    }

    [Fact]
    public void Manager_DirectReportsDraftExpense_IsNotVisible()
    {
        var managerId = Guid.NewGuid();
        var expense = new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Status = ExpenseStatus.Draft,
            Employee = new Employee { ManagerId = managerId },
        };

        var isVisible = Invoke(EmployeeRole.Manager, managerId, expense);

        Assert.False(isVisible);
    }

    [Fact]
    public void Manager_IndirectReportsExpense_IsNotVisible()
    {
        var managerId = Guid.NewGuid();
        var directReportId = Guid.NewGuid();
        var expense = new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Status = ExpenseStatus.Submitted,
            // Two levels down: this employee's manager is a direct report of the caller, not the
            // caller themself — visibility is direct reports only, not recursive.
            Employee = new Employee { ManagerId = directReportId },
        };

        var isVisible = Invoke(EmployeeRole.Manager, managerId, expense);

        Assert.False(isVisible);
    }

    [Fact]
    public void Manager_UnrelatedEmployeesExpense_IsNotVisible()
    {
        var managerId = Guid.NewGuid();
        var expense = new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Status = ExpenseStatus.Submitted,
            Employee = new Employee { ManagerId = Guid.NewGuid() },
        };

        var isVisible = Invoke(EmployeeRole.Manager, managerId, expense);

        Assert.False(isVisible);
    }

    // ---- Finance ----

    [Fact]
    public void Finance_AnyEmployeesNonDraftExpense_IsVisible()
    {
        var expense = new Expense { EmployeeId = Guid.NewGuid(), Status = ExpenseStatus.Submitted };

        var isVisible = Invoke(EmployeeRole.Finance, Guid.NewGuid(), expense);

        Assert.True(isVisible);
    }

    [Fact]
    public void Finance_DraftExpense_IsNotVisible()
    {
        var expense = new Expense { EmployeeId = Guid.NewGuid(), Status = ExpenseStatus.Draft };

        var isVisible = Invoke(EmployeeRole.Finance, Guid.NewGuid(), expense);

        Assert.False(isVisible);
    }

    // ---- ComplianceOfficer ----

    [Fact]
    public void Compliance_ClientEntertainmentApproved_IsVisible()
    {
        var expense = new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Category = ExpenseCategory.ClientEntertainment,
            Status = ExpenseStatus.Approved,
        };

        var isVisible = Invoke(EmployeeRole.ComplianceOfficer, Guid.NewGuid(), expense);

        Assert.True(isVisible);
    }

    [Fact]
    public void Compliance_ClientEntertainmentComplianceApproved_IsVisible()
    {
        var expense = new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Category = ExpenseCategory.ClientEntertainment,
            Status = ExpenseStatus.ComplianceApproved,
        };

        var isVisible = Invoke(EmployeeRole.ComplianceOfficer, Guid.NewGuid(), expense);

        Assert.True(isVisible);
    }

    [Fact]
    public void Compliance_ClientEntertainmentReimbursed_IsNotVisible()
    {
        var expense = new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Category = ExpenseCategory.ClientEntertainment,
            Status = ExpenseStatus.Reimbursed,
        };

        var isVisible = Invoke(EmployeeRole.ComplianceOfficer, Guid.NewGuid(), expense);

        Assert.False(isVisible);
    }

    [Fact]
    public void Compliance_NonClientEntertainmentCategoryApproved_IsNotVisible()
    {
        var expense = new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Category = ExpenseCategory.Travel,
            Status = ExpenseStatus.Approved,
        };

        var isVisible = Invoke(EmployeeRole.ComplianceOfficer, Guid.NewGuid(), expense);

        Assert.False(isVisible);
    }

    [Theory]
    [InlineData(ExpenseStatus.Draft)]
    [InlineData(ExpenseStatus.Submitted)]
    public void Compliance_ClientEntertainmentDraftOrSubmitted_IsNotVisible(ExpenseStatus status)
    {
        var expense = new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Category = ExpenseCategory.ClientEntertainment,
            Status = status,
        };

        var isVisible = Invoke(EmployeeRole.ComplianceOfficer, Guid.NewGuid(), expense);

        Assert.False(isVisible);
    }

    private static bool Invoke(EmployeeRole role, Guid employeeId, Expense expense) =>
        ExpenseVisibility.BuildPredicate(role, employeeId).Compile().Invoke(expense);
}
