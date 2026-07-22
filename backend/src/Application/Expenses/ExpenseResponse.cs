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
    DateTime? ComplianceApprovedAt,
    DateTime? RejectedAt,
    string? RejectionComment,
    DateTime? ReimbursedAt,
    DateTime CreatedAt,
    string? EmployeeName,
    string? EmployeeNumber,
    Guid ReceiptAttachmentId,
    string? AttachmentOriginalFileName);
