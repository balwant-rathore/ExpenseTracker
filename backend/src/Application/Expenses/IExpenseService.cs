namespace Application.Expenses;

public interface IExpenseService
{
    Task<ExpenseResult> CreateAsync(Guid employeeId, CreateExpenseRequest request, CancellationToken cancellationToken);

    Task<ExpenseResult> SubmitAsync(Guid employeeId, Guid expenseId, CancellationToken cancellationToken);

    Task<ExpenseResult> GetByIdAsync(Guid employeeId, Guid expenseId, CancellationToken cancellationToken);

    Task<ExpenseResult> UpdateAsync(Guid employeeId, Guid expenseId, UpdateExpenseRequest request, CancellationToken cancellationToken);

    Task<ExpenseResult> CancelAsync(Guid employeeId, Guid expenseId, CancellationToken cancellationToken);
}
