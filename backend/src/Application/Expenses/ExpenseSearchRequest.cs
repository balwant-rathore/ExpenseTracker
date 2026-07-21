namespace Application.Expenses;

public record ExpenseSearchRequest
{
    public string? ExpenseNumber { get; init; }
    public string? EmployeeName { get; init; }
    public string? Category { get; init; }
    public string? Status { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string SortBy { get; init; } = "expenseDate";
    public string SortDirection { get; init; } = "desc";
}
