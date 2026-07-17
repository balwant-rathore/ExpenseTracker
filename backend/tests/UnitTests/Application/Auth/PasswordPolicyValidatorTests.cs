using Application.Auth;

namespace UnitTests.Application.Auth;

public class PasswordPolicyValidatorTests
{
    private readonly PasswordPolicyValidator _validator = new();

    [Fact]
    public void IsSatisfiedBy_ShorterThan8Characters_ReturnsFalse()
    {
        Assert.False(_validator.IsSatisfiedBy("ab1"));
    }

    [Fact]
    public void IsSatisfiedBy_NoDigit_ReturnsFalse()
    {
        Assert.False(_validator.IsSatisfiedBy("abcdefgh"));
    }

    [Fact]
    public void IsSatisfiedBy_NoLetter_ReturnsFalse()
    {
        Assert.False(_validator.IsSatisfiedBy("12345678"));
    }

    [Fact]
    public void IsSatisfiedBy_CompliantPassword_ReturnsTrue()
    {
        Assert.True(_validator.IsSatisfiedBy("abcdefg1"));
    }
}
