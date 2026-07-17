using Domain.Enums;

namespace Domain.Entities;

public class Expense
{
    public Guid Id { get; set; }

    public string ExpenseNumber { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public Guid AttachmentId { get; set; }

    public DateOnly ExpenseDate { get; set; }

    public ExpenseCategory Category { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public string Description { get; set; } = null!;

    public ExpenseStatus Status { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ComplianceApprovedAt { get; set; }
    public DateTime? ReimbursedAt { get; set; }
    public DateTime? RejectedAt { get; set; }

    public Guid? ApprovedByEmployeeId { get; set; }
    public Guid? ComplianceApprovedByEmployeeId { get; set; }
    public Guid? ReimbursedByEmployeeId { get; set; }
    public Guid? RejectedByEmployeeId { get; set; }

    public string? RejectionComment { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Employee Employee { get; set; } = null!;
    public Attachment Attachment { get; set; } = null!;
}
