using Application.Dashboard;
using Domain.Entities;
using Domain.Enums;

namespace UnitTests.Application.Dashboard;

public class DashboardScopeTests
{
    // ---- Employee ----

    [Fact]
    public void Employee_MatchesOwnExpense_ExcludesAnotherEmployeesExpense()
    {
        var employeeId = Guid.NewGuid();
        var ownExpense = new Expense { EmployeeId = employeeId };
        var othersExpense = new Expense { EmployeeId = Guid.NewGuid() };

        Assert.True(Invoke(EmployeeRole.Employee, employeeId, ownExpense));
        Assert.False(Invoke(EmployeeRole.Employee, employeeId, othersExpense));
    }

    // ---- Manager ----

    [Fact]
    public void Manager_MatchesDirectReportsExpense()
    {
        var managerId = Guid.NewGuid();
        var expense = new Expense
        {
            EmployeeId = Guid.NewGuid(),
            Employee = new Employee { ManagerId = managerId },
        };

        Assert.True(Invoke(EmployeeRole.Manager, managerId, expense));
    }

    [Fact]
    public void Manager_ExcludesOwnExpense()
    {
        var managerId = Guid.NewGuid();
        var expense = new Expense
        {
            EmployeeId = managerId,
            Employee = new Employee { ManagerId = Guid.NewGuid() },
        };

        Assert.False(Invoke(EmployeeRole.Manager, managerId, expense));
    }

    [Fact]
    public void Manager_ExcludesIndirectReportsExpense()
    {
        var managerId = Guid.NewGuid();
        var directReportId = Guid.NewGuid();
        var expense = new Expense
        {
            EmployeeId = Guid.NewGuid(),
            // Two levels down: this employee's manager is a direct report of the caller, not
            // the caller themself - dashboard scope is direct reports only, not recursive.
            Employee = new Employee { ManagerId = directReportId },
        };

        Assert.False(Invoke(EmployeeRole.Manager, managerId, expense));
    }

    // ---- Finance ----

    [Fact]
    public void Finance_MatchesRegardlessOfOwner()
    {
        var expense = new Expense { EmployeeId = Guid.NewGuid() };

        Assert.True(Invoke(EmployeeRole.Finance, Guid.NewGuid(), expense));
    }

    private static bool Invoke(EmployeeRole role, Guid employeeId, Expense expense) =>
        DashboardScope.BuildPredicate(role, employeeId).Compile().Invoke(expense);
}
