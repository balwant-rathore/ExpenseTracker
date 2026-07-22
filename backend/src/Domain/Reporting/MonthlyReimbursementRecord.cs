namespace Domain.Reporting;

public record MonthlyReimbursementRecord(
    string EmployeeName,
    string ExpenseNumber,
    string Category,
    decimal Amount,
    string Currency,
    DateTime? ApprovalDate,
    DateTime? ReimbursementDate);
