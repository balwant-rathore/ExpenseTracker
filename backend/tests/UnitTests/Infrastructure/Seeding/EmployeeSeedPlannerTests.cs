using Domain.Enums;
using Infrastructure.Seeding;

namespace UnitTests.Infrastructure.Seeding;

public class EmployeeSeedPlannerTests
{
    [Fact]
    public void BuildPlan_SkipsExistingEmployeeNumbers_InsertsNewOnes()
    {
        var planner = new EmployeeSeedPlanner();
        var existingId = Guid.NewGuid();

        var csvRows = new[]
        {
            new EmployeeCsvRow("EMP001", "Sarah", "Johnson", "sarah.johnson@company.com", EmployeeRole.ComplianceOfficer, null),
            new EmployeeCsvRow("EMP002", "Michael", "Brown", "michael.brown@company.com", EmployeeRole.Finance, null)
        };
        var existing = new Dictionary<string, Guid> { ["EMP001"] = existingId };

        var plan = planner.BuildPlan(csvRows, existing);

        Assert.Single(plan.EmployeesToInsert);
        Assert.Equal("EMP002", plan.EmployeesToInsert[0].EmployeeNumber);
    }

    [Fact]
    public void BuildPlan_ResolvesManagerReference_RegardlessOfCsvRowOrder()
    {
        var planner = new EmployeeSeedPlanner();

        var csvRows = new[]
        {
            new EmployeeCsvRow("EMP015", "Noah", "Walker", "noah.walker@company.com", EmployeeRole.Employee, "EMP005"),
            new EmployeeCsvRow("EMP005", "James", "Miller", "james.miller@company.com", EmployeeRole.Manager, null)
        };

        var plan = planner.BuildPlan(csvRows, new Dictionary<string, Guid>());

        var manager = plan.EmployeesToInsert.Single(e => e.EmployeeNumber == "EMP005");
        var employee = plan.EmployeesToInsert.Single(e => e.EmployeeNumber == "EMP015");

        Assert.Equal(manager.EmployeeId, employee.ManagerId);
        Assert.Empty(plan.UnresolvedManagerWarnings);
    }

    [Fact]
    public void BuildPlan_UnresolvableManager_LeavesManagerIdNull_AndWarnsWithoutBlocking()
    {
        var planner = new EmployeeSeedPlanner();

        var csvRows = new[]
        {
            new EmployeeCsvRow("EMP099", "Ghost", "Manager", "ghost.manager@company.com", EmployeeRole.Employee, "EMP999"),
            new EmployeeCsvRow("EMP002", "Michael", "Brown", "michael.brown@company.com", EmployeeRole.Finance, null)
        };

        var plan = planner.BuildPlan(csvRows, new Dictionary<string, Guid>());

        Assert.Equal(2, plan.EmployeesToInsert.Count);

        var unresolved = plan.EmployeesToInsert.Single(e => e.EmployeeNumber == "EMP099");
        Assert.Null(unresolved.ManagerId);
        Assert.Single(plan.UnresolvedManagerWarnings);
        Assert.Contains("EMP099", plan.UnresolvedManagerWarnings[0]);
        Assert.Contains("EMP999", plan.UnresolvedManagerWarnings[0]);
    }
}
