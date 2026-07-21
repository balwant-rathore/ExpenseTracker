using Domain.Enums;
using FluentValidation;

namespace Application.Expenses;

public class UpdateExpenseRequestValidator : AbstractValidator<UpdateExpenseRequest>
{
    public UpdateExpenseRequestValidator()
    {
        RuleFor(x => x.ExpenseDate)
            .NotEmpty()
            .WithMessage("Expense date is required.");

        RuleFor(x => x.Category)
            .Must(category => Enum.TryParse<ExpenseCategory>(category, out _))
            .WithMessage("Category must be one of the defined expense categories.");

        RuleFor(x => x.Currency)
            .Equal(CreateExpenseRequestValidator.RequiredCurrency)
            .WithMessage("Currency must be INR.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required.")
            .MaximumLength(CreateExpenseRequestValidator.MaxDescriptionLength)
            .WithMessage("Description must not exceed 500 characters.");
    }
}
