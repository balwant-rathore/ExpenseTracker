using System.Linq.Expressions;
using Domain.Entities;
using Domain.Enums;

namespace Application.Expenses;

public static class ExpenseVisibility
{
    public static Expression<Func<Expense, bool>> BuildPredicate(EmployeeRole role, Guid employeeId) => role switch
    {
        EmployeeRole.Employee => e => e.EmployeeId == employeeId,
        EmployeeRole.Manager => e => e.EmployeeId == employeeId
            || (e.Employee.ManagerId == employeeId && e.Status != ExpenseStatus.Draft),
        EmployeeRole.Finance => e => e.Status != ExpenseStatus.Draft,
        EmployeeRole.ComplianceOfficer => e => e.Category == ExpenseCategory.ClientEntertainment
            && (e.Status == ExpenseStatus.Approved || e.Status == ExpenseStatus.ComplianceApproved),
        _ => e => false,
    };
}
