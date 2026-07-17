using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Infrastructure.Persistence;

public class CascadeDeleteTests
{
    [Theory]
    [InlineData(typeof(Employee))]
    [InlineData(typeof(User))]
    [InlineData(typeof(RefreshToken))]
    [InlineData(typeof(PasswordResetOtp))]
    [InlineData(typeof(Expense))]
    public void ForeignKeys_AreConfiguredWithRestrictDeleteBehavior(Type entityClrType)
    {
        using var dbContext = TestDbContextFactory.Create();
        var entityType = dbContext.Model.FindEntityType(entityClrType)!;

        var foreignKeys = entityType.GetForeignKeys().ToList();

        Assert.NotEmpty(foreignKeys);
        Assert.All(foreignKeys, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
}
