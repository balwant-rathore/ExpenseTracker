namespace Application.Expenses;

public record ExpenseSubmitResult(bool Succeeded, ExpenseResponse? Expense, ExpenseFailureReason FailureReason)
{
    public static ExpenseSubmitResult Success(ExpenseResponse expense) => new(true, expense, ExpenseFailureReason.None);

    public static ExpenseSubmitResult Failure(ExpenseFailureReason reason) => new(false, null, reason);
}
