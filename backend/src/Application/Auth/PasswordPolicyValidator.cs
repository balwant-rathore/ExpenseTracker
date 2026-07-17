namespace Application.Auth;

public class PasswordPolicyValidator : IPasswordPolicyValidator
{
    private const int MinimumLength = 8;

    public bool IsSatisfiedBy(string password)
    {
        if (password.Length < MinimumLength)
        {
            return false;
        }

        return password.Any(char.IsLetter) && password.Any(char.IsDigit);
    }
}
