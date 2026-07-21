namespace Application.Expenses;

public record CreateExpenseRequest
{
    public DateOnly ExpenseDate { get; init; }
    public string? Category { get; init; }
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
    public string? Description { get; init; }
    public Guid ReceiptAttachmentId { get; init; }
    public string? Action { get; init; }
}
