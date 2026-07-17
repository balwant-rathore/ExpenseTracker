namespace Application.Auth;

public interface IPasswordPolicyValidator
{
    bool IsSatisfiedBy(string password);
}
