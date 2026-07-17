using Domain.Enums;

namespace UnitTests.Domain.Enums;

public class ExpenseCategoryTests
{
    [Fact]
    public void ExpenseCategory_ContainsExactlySevenValues()
    {
        var values = Enum.GetNames<ExpenseCategory>();

        Assert.Equal(7, values.Length);
        Assert.Equal(
            new[] { "Travel", "Hotel", "Meals", "OfficeSupplies", "ClientEntertainment", "Training", "Other" },
            values);
    }
}
