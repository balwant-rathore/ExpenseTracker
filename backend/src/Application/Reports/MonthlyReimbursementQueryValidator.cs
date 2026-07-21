using FluentValidation;

namespace Application.Reports;

public class MonthlyReimbursementQueryValidator : AbstractValidator<MonthlyReimbursementQuery>
{
    public MonthlyReimbursementQueryValidator()
    {
        RuleFor(x => x.Year)
            .NotNull()
            .WithMessage("Year is required.")
            .InclusiveBetween(1, 9999)
            .WithMessage("Year must be between 1 and 9999.")
            .When(x => x.Year.HasValue, ApplyConditionTo.CurrentValidator);

        RuleFor(x => x.Month)
            .NotNull()
            .WithMessage("Month is required.")
            .InclusiveBetween(1, 12)
            .WithMessage("Month must be between 1 and 12.")
            .When(x => x.Month.HasValue, ApplyConditionTo.CurrentValidator);
    }
}
