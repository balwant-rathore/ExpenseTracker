using Application.Auth;

namespace UnitTests.Application.Auth;

public class BCryptPasswordHasherTests
{
    [Fact]
    public void HashAndVerify_SamePassword_Succeeds()
    {
        var hasher = new BCryptPasswordHasher();
        var hash = hasher.Hash("correct-horse-1");

        var isValid = hasher.Verify("correct-horse-1", hash);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_NonMatchingPassword_Fails()
    {
        var hasher = new BCryptPasswordHasher();
        var hash = hasher.Hash("correct-horse-1");

        var isValid = hasher.Verify("wrong-password-1", hash);

        Assert.False(isValid);
    }
}
