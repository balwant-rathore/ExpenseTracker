using Application.Expenses;

namespace UnitTests.Application.Expenses;

public class ExpenseListRequestValidatorTests
{
    private static readonly ExpenseListRequestValidator Validator = new();

    private static ExpenseListRequest CreateRequest(
        int page = 1,
        int pageSize = 20,
        string sortBy = "expenseDate",
        string sortDirection = "desc") =>
        new()
        {
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection,
        };

    [Fact]
    public void ValidDefaultRequest_Passes()
    {
        var result = Validator.Validate(CreateRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void PageSizeNotInAllowedSet_Fails()
    {
        var result = Validator.Validate(CreateRequest(pageSize: 15));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ExpenseListRequest.PageSize));
    }

    [Fact]
    public void SortByOutsideAllowedValues_Fails()
    {
        var result = Validator.Validate(CreateRequest(sortBy: "bogus"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ExpenseListRequest.SortBy));
    }

    [Fact]
    public void SortDirectionOutsideAllowedValues_Fails()
    {
        var result = Validator.Validate(CreateRequest(sortDirection: "sideways"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ExpenseListRequest.SortDirection));
    }

    [Fact]
    public void PageLessThanOne_Fails()
    {
        var result = Validator.Validate(CreateRequest(page: 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ExpenseListRequest.Page));
    }
}
