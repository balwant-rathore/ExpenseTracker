using Domain.Entities;

namespace UnitTests.Infrastructure.Persistence;

public class ExpenseConfigurationTests
{
    [Fact]
    public void Expense_ExpenseNumber_IsUniqueIndex()
    {
        using var dbContext = TestDbContextFactory.Create();
        var entityType = dbContext.Model.FindEntityType(typeof(Expense))!;

        Assert.True(EmployeeConfigurationTests.SingleIndex(entityType, nameof(Expense.ExpenseNumber)).IsUnique);
    }

    [Fact]
    public void Expense_Description_HasFiveHundredCharacterLimit()
    {
        using var dbContext = TestDbContextFactory.Create();
        var entityType = dbContext.Model.FindEntityType(typeof(Expense))!;

        var property = entityType.FindProperty(nameof(Expense.Description))!;

        Assert.Equal(500, property.GetMaxLength());
    }

    [Fact]
    public void Expense_AttachmentId_IsUniqueIndex()
    {
        using var dbContext = TestDbContextFactory.Create();
        var entityType = dbContext.Model.FindEntityType(typeof(Expense))!;

        Assert.True(EmployeeConfigurationTests.SingleIndex(entityType, nameof(Expense.AttachmentId)).IsUnique);
    }
}
