namespace Application.Expenses;

public record ExpenseResult(bool Succeeded, ExpenseResponse? Expense, ExpenseFailureReason FailureReason)
{
    public static ExpenseResult Success(ExpenseResponse expense) => new(true, expense, ExpenseFailureReason.None);

    public static ExpenseResult Failure(ExpenseFailureReason reason) => new(false, null, reason);
}
