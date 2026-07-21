using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Infrastructure.Persistence;

public class AttachmentConfigurationTests
{
    [Fact]
    public void Attachment_UploadedByEmployeeId_ReferencesEmployeeWithRestrictDelete()
    {
        using var dbContext = TestDbContextFactory.Create();
        var entityType = dbContext.Model.FindEntityType(typeof(Attachment))!;

        var foreignKey = entityType.GetForeignKeys()
            .Single(fk => fk.Properties.Any(p => p.Name == nameof(Attachment.UploadedByEmployeeId)));

        Assert.Equal(typeof(Employee), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.False(foreignKey.Properties.Single().IsNullable);
    }
}
