namespace Application.Reports;

public record MonthlyReimbursementQuery
{
    public int? Year { get; init; }
    public int? Month { get; init; }
}
