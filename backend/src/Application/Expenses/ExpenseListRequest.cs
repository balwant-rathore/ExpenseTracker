namespace Application.Expenses;

public record ExpenseListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string SortBy { get; init; } = "expenseDate";
    public string SortDirection { get; init; } = "desc";
}
