namespace Application.Expenses;

public interface IExpenseService
{
    Task<ExpenseCreationResult> CreateAsync(Guid employeeId, CreateExpenseRequest request, CancellationToken cancellationToken);

    Task<ExpenseSubmitResult> SubmitAsync(Guid employeeId, Guid expenseId, CancellationToken cancellationToken);
}
