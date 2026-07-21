namespace Application.Expenses;

public record ExpenseResponse(
    Guid Id,
    string ExpenseNumber,
    DateOnly ExpenseDate,
    string Category,
    decimal Amount,
    string Currency,
    string Description,
    string Status,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt,
    string? RejectionComment,
    DateTime CreatedAt,
    string? EmployeeName);
