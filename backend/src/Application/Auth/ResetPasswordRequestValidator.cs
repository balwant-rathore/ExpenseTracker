using FluentValidation;

namespace Application.Auth;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Otp).NotEmpty().Matches(@"^\d{6}$");
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}
