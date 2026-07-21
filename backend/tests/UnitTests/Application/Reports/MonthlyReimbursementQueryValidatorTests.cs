using Application.Reports;

namespace UnitTests.Application.Reports;

public class MonthlyReimbursementQueryValidatorTests
{
    private static readonly MonthlyReimbursementQueryValidator Validator = new();

    [Fact]
    public void ValidYearAndMonth_Passes()
    {
        var result = Validator.Validate(new MonthlyReimbursementQuery { Year = 2026, Month = 7 });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void MissingYear_Fails()
    {
        var result = Validator.Validate(new MonthlyReimbursementQuery { Year = null, Month = 7 });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(MonthlyReimbursementQuery.Year));
    }

    [Fact]
    public void MissingMonth_Fails()
    {
        var result = Validator.Validate(new MonthlyReimbursementQuery { Year = 2026, Month = null });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(MonthlyReimbursementQuery.Month));
    }

    [Fact]
    public void OutOfRangeMonth_Fails()
    {
        var result = Validator.Validate(new MonthlyReimbursementQuery { Year = 2026, Month = 13 });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(MonthlyReimbursementQuery.Month));
    }
}
