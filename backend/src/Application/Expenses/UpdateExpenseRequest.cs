namespace Application.Expenses;

public record UpdateExpenseRequest
{
    public DateOnly ExpenseDate { get; init; }
    public string? Category { get; init; }
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
    public string? Description { get; init; }
    public Guid ReceiptAttachmentId { get; init; }
}
