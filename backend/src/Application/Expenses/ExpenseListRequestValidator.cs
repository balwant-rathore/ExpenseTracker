using Domain.Enums;
using FluentValidation;

namespace Application.Expenses;

public class ExpenseListRequestValidator : AbstractValidator<ExpenseListRequest>
{
    public static readonly int[] AllowedPageSizes = [10, 20, 50, 100];

    public static readonly string[] AllowedSortFields =
    [
        "expenseDate",
        "expenseNumber",
        "createdAt",
        "amount",
        "submittedAt",
        "approvedAt",
        "reimbursedAt",
        "rejectedAt",
    ];

    public static readonly string[] AllowedSortDirections = ["asc", "desc"];

    public ExpenseListRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .Must(pageSize => AllowedPageSizes.Contains(pageSize))
            .WithMessage("PageSize must be one of 10, 20, 50, 100.");

        RuleFor(x => x.SortBy)
            .Must(sortBy => AllowedSortFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortBy must be one of the allowed sort fields.");

        RuleFor(x => x.SortDirection)
            .Must(sortDirection => AllowedSortDirections.Contains(sortDirection, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortDirection must be 'asc' or 'desc'.");

        RuleFor(x => x.Status)
            .Must(status => status is null || Enum.TryParse<ExpenseStatus>(status, out _))
            .WithMessage("Status must be one of the defined expense statuses.");
    }
}
