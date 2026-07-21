namespace Application.Expenses;

public record PagedExpenseResponse(
    IReadOnlyList<ExpenseResponse> Items,
    int Page,
    int PageSize,
    int TotalRecords);
