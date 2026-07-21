using Application.Expenses;

namespace UnitTests.Application.Expenses;

public class ExpenseSearchRequestValidatorTests
{
    private static readonly ExpenseSearchRequestValidator Validator = new();

    [Fact]
    public void ValidDefaultRequest_Passes()
    {
        var result = Validator.Validate(new ExpenseSearchRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void PageSizeNotInAllowedSet_Fails()
    {
        var result = Validator.Validate(new ExpenseSearchRequest { PageSize = 15 });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ExpenseSearchRequest.PageSize));
    }

    [Fact]
    public void SortByOutsideAllowedValues_Fails()
    {
        var result = Validator.Validate(new ExpenseSearchRequest { SortBy = "bogus" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ExpenseSearchRequest.SortBy));
    }

    [Fact]
    public void CategoryOutsideDefinedValues_Fails()
    {
        var result = Validator.Validate(new ExpenseSearchRequest { Category = "NotARealCategory" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ExpenseSearchRequest.Category));
    }

    [Fact]
    public void StatusOutsideDefinedValues_Fails()
    {
        var result = Validator.Validate(new ExpenseSearchRequest { Status = "NotARealStatus" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ExpenseSearchRequest.Status));
    }
}
