using Domain.Entities;

namespace UnitTests.Infrastructure.Persistence;

public class RefreshTokenConfigurationTests
{
    [Fact]
    public void RefreshToken_OnlyStoresTokenHash_NoPlaintextTokenProperty()
    {
        using var dbContext = TestDbContextFactory.Create();
        var entityType = dbContext.Model.FindEntityType(typeof(RefreshToken))!;

        Assert.NotNull(entityType.FindProperty(nameof(RefreshToken.TokenHash)));
        Assert.Null(entityType.FindProperty("Token"));
        Assert.True(EmployeeConfigurationTests.SingleIndex(entityType, nameof(RefreshToken.TokenHash)).IsUnique);
    }
}
