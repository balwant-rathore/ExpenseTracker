using Domain.Enums;

namespace Application.Expenses;

public interface IExpenseService
{
    Task<ExpenseResult> CreateAsync(Guid employeeId, CreateExpenseRequest request, CancellationToken cancellationToken);

    Task<ExpenseResult> SubmitAsync(Guid employeeId, Guid expenseId, CancellationToken cancellationToken);

    Task<ExpenseResult> GetByIdAsync(Guid employeeId, EmployeeRole role, Guid expenseId, CancellationToken cancellationToken);

    Task<ExpenseResult> UpdateAsync(Guid employeeId, Guid expenseId, UpdateExpenseRequest request, CancellationToken cancellationToken);

    Task<ExpenseResult> CancelAsync(Guid employeeId, Guid expenseId, CancellationToken cancellationToken);

    Task<ExpenseResult> ApproveAsync(Guid managerId, Guid expenseId, CancellationToken cancellationToken);

    Task<ExpenseResult> RejectAsync(Guid managerId, Guid expenseId, RejectExpenseRequest request, CancellationToken cancellationToken);

    Task<PagedExpenseResponse> GetVisibleAsync(Guid employeeId, EmployeeRole role, ExpenseListRequest request, CancellationToken cancellationToken);
}
