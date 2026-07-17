using Domain.Entities;

namespace UnitTests.Infrastructure.Persistence;

public class PasswordResetOtpConfigurationTests
{
    [Fact]
    public void PasswordResetOtp_OnlyStoresOtpHash_NoPlaintextOtpProperty()
    {
        using var dbContext = TestDbContextFactory.Create();
        var entityType = dbContext.Model.FindEntityType(typeof(PasswordResetOtp))!;

        Assert.NotNull(entityType.FindProperty(nameof(PasswordResetOtp.OtpHash)));
        Assert.Null(entityType.FindProperty("Otp"));
    }
}
