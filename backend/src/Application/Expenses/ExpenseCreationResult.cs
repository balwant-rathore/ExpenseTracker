namespace Application.Expenses;

public record ExpenseCreationResult(bool Succeeded, ExpenseResponse? Expense, ExpenseFailureReason FailureReason)
{
    public static ExpenseCreationResult Success(ExpenseResponse expense) => new(true, expense, ExpenseFailureReason.None);

    public static ExpenseCreationResult Failure(ExpenseFailureReason reason) => new(false, null, reason);
}
