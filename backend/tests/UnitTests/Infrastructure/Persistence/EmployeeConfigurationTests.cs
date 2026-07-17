using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace UnitTests.Infrastructure.Persistence;

public class EmployeeConfigurationTests
{
    [Fact]
    public void Employee_EmployeeNumberAndEmail_AreUniqueIndexes()
    {
        using var dbContext = TestDbContextFactory.Create();
        var entityType = dbContext.Model.FindEntityType(typeof(Employee))!;

        Assert.True(SingleIndex(entityType, nameof(Employee.EmployeeNumber)).IsUnique);
        Assert.True(SingleIndex(entityType, nameof(Employee.Email)).IsUnique);
    }

    [Fact]
    public void Employee_ManagerId_SelfReferencesEmployeeId()
    {
        using var dbContext = TestDbContextFactory.Create();
        var entityType = dbContext.Model.FindEntityType(typeof(Employee))!;

        var foreignKey = entityType.GetForeignKeys()
            .Single(fk => fk.Properties.Any(p => p.Name == nameof(Employee.ManagerId)));

        Assert.Equal(typeof(Employee), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    internal static IIndex SingleIndex(IEntityType entityType, string propertyName)
    {
        return entityType.GetIndexes()
            .Single(i => i.Properties.Count == 1 && i.Properties[0].Name == propertyName);
    }
}
