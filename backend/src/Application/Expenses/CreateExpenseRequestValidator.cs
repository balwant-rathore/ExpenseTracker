using Domain.Enums;
using FluentValidation;

namespace Application.Expenses;

public class CreateExpenseRequestValidator : AbstractValidator<CreateExpenseRequest>
{
    public const int MaxDescriptionLength = 500;
    public const string RequiredCurrency = "INR";

    public CreateExpenseRequestValidator()
    {
        RuleFor(x => x.ExpenseDate)
            .NotEmpty()
            .WithMessage("Expense date is required.");

        RuleFor(x => x.Category)
            .Must(category => Enum.TryParse<ExpenseCategory>(category, out _))
            .WithMessage("Category must be one of the defined expense categories.");

        RuleFor(x => x.Action)
            .Must(action => Enum.TryParse<ExpenseAction>(action, out _))
            .WithMessage("Action must be either Draft or Submit.");

        RuleFor(x => x.Currency)
            .Equal(RequiredCurrency)
            .When(x => x.Currency is not null)
            .WithMessage("Currency must be INR.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required.")
            .MaximumLength(MaxDescriptionLength)
            .WithMessage("Description must not exceed 500 characters.");
    }
}
