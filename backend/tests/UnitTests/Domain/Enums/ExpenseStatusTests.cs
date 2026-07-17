using Domain.Enums;

namespace UnitTests.Domain.Enums;

public class ExpenseStatusTests
{
    [Fact]
    public void ExpenseStatus_ContainsExactlySevenValues()
    {
        var values = Enum.GetNames<ExpenseStatus>();

        Assert.Equal(7, values.Length);
        Assert.Equal(
            new[] { "Draft", "Submitted", "Approved", "ComplianceApproved", "Cancelled", "Reimbursed", "Rejected" },
            values);
    }
}
