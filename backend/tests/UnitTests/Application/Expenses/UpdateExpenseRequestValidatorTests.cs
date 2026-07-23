using Application.Expenses;

namespace UnitTests.Application.Expenses;

public class UpdateExpenseRequestValidatorTests
{
    private static readonly UpdateExpenseRequestValidator Validator = new();

    private static UpdateExpenseRequest CreateRequest(
        DateOnly? expenseDate = null,
        string? category = "Travel",
        string? currency = "INR",
        string? description = "Test expense") =>
        new()
        {
            ExpenseDate = expenseDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Category = category,
            Amount = 100m,
            Currency = currency,
            Description = description,
            ReceiptAttachmentId = Guid.NewGuid(),
        };

    [Fact]
    public void ValidRequest_Passes()
    {
        var result = Validator.Validate(CreateRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CategoryOutsideDefinedValues_Fails()
    {
        var result = Validator.Validate(CreateRequest(category: "Bogus"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateExpenseRequest.Category));
    }

    [Fact]
    public void CurrencyOtherThanInr_Fails()
    {
        var result = Validator.Validate(CreateRequest(currency: "USD"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateExpenseRequest.Currency));
    }

    [Fact]
    public void DescriptionOverFiveHundredCharacters_Fails()
    {
        var result = Validator.Validate(CreateRequest(description: new string('a', 501)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateExpenseRequest.Description));
    }

    [Fact]
    public void DescriptionAtFiveHundredCharacters_Passes()
    {
        var result = Validator.Validate(CreateRequest(description: new string('a', 500)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void MissingExpenseDate_Fails()
    {
        var result = Validator.Validate(CreateRequest(expenseDate: default(DateOnly)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateExpenseRequest.ExpenseDate));
    }

    [Fact]
    public void MissingCategory_FailsWithoutThrowing()
    {
        var result = Validator.Validate(CreateRequest(category: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateExpenseRequest.Category));
    }

    [Fact]
    public void MissingCurrency_Passes()
    {
        var result = Validator.Validate(CreateRequest(currency: null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void MissingDescription_Fails()
    {
        var result = Validator.Validate(CreateRequest(description: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateExpenseRequest.Description));
    }
}
