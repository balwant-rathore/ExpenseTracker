namespace Application.Expenses;

public record RejectExpenseRequest
{
    public string? RejectionComment { get; init; }
}
