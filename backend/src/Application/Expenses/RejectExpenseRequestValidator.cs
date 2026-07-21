using FluentValidation;

namespace Application.Expenses;

public class RejectExpenseRequestValidator : AbstractValidator<RejectExpenseRequest>
{
    public const int MaxRejectionCommentLength = 500;

    public RejectExpenseRequestValidator()
    {
        RuleFor(x => x.RejectionComment)
            .NotEmpty()
            .WithMessage("Rejection comment is required.")
            .MaximumLength(MaxRejectionCommentLength)
            .WithMessage("Rejection comment must not exceed 500 characters.");
    }
}
