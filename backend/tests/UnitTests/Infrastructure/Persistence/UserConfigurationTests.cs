using Domain.Entities;

namespace UnitTests.Infrastructure.Persistence;

public class UserConfigurationTests
{
    [Fact]
    public void User_EmployeeId_IsUniqueIndex()
    {
        using var dbContext = TestDbContextFactory.Create();
        var entityType = dbContext.Model.FindEntityType(typeof(User))!;

        Assert.True(EmployeeConfigurationTests.SingleIndex(entityType, nameof(User.EmployeeId)).IsUnique);
    }

    [Fact]
    public void User_NormalizedEmail_IsUniqueIndex()
    {
        using var dbContext = TestDbContextFactory.Create();
        var entityType = dbContext.Model.FindEntityType(typeof(User))!;

        Assert.True(EmployeeConfigurationTests.SingleIndex(entityType, nameof(User.NormalizedEmail)).IsUnique);
    }
}
