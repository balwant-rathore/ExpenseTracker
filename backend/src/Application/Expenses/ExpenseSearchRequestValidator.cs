using Domain.Enums;
using FluentValidation;

namespace Application.Expenses;

public class ExpenseSearchRequestValidator : AbstractValidator<ExpenseSearchRequest>
{
    public static readonly int[] AllowedPageSizes = [20, 50, 100, 500];

    public ExpenseSearchRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .Must(pageSize => AllowedPageSizes.Contains(pageSize))
            .WithMessage("PageSize must be one of 20, 50, 100, 500.");

        RuleFor(x => x.SortBy)
            .Must(sortBy => ExpenseListRequestValidator.AllowedSortFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortBy must be one of the allowed sort fields.");

        RuleFor(x => x.SortDirection)
            .Must(sortDirection => ExpenseListRequestValidator.AllowedSortDirections.Contains(sortDirection, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortDirection must be 'asc' or 'desc'.");

        RuleFor(x => x.Category)
            .Must(category => category is null || Enum.TryParse<ExpenseCategory>(category, out _))
            .WithMessage("Category must be one of the defined expense categories.");

        RuleFor(x => x.Status)
            .Must(status => status is null || Enum.TryParse<ExpenseStatus>(status, out _))
            .WithMessage("Status must be one of the defined expense statuses.");
    }
}
