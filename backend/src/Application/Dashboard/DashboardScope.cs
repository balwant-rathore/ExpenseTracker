using System.Linq.Expressions;
using Domain.Entities;
using Domain.Enums;

namespace Application.Dashboard;

// Deliberately separate from Application.Expenses.ExpenseVisibility (design.md D2): the
// Manager scope here excludes the manager's own expenses entirely, whereas
// ExpenseVisibility's Manager predicate includes them - these are two different, confirmed
// rules, not a divergence to reconcile.
public static class DashboardScope
{
    public static Expression<Func<Expense, bool>> BuildPredicate(EmployeeRole role, Guid employeeId) => role switch
    {
        EmployeeRole.Employee => e => e.EmployeeId == employeeId,
        EmployeeRole.Manager => e => e.Employee.ManagerId == employeeId,
        EmployeeRole.Finance => e => true,
        _ => e => false,
    };
}
