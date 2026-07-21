using Application.Expenses;

namespace UnitTests.Application.Expenses;

public class CreateExpenseRequestValidatorTests
{
    private static readonly CreateExpenseRequestValidator Validator = new();

    private static CreateExpenseRequest CreateRequest(
        DateOnly? expenseDate = null,
        string? category = "Travel",
        string? currency = "INR",
        string? description = "Test expense",
        string? action = "Draft") =>
        new()
        {
            ExpenseDate = expenseDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Category = category,
            Amount = 100m,
            Currency = currency,
            Description = description,
            ReceiptAttachmentId = Guid.NewGuid(),
            Action = action,
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
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateExpenseRequest.Category));
    }

    [Fact]
    public void CurrencyOtherThanInr_Fails()
    {
        var result = Validator.Validate(CreateRequest(currency: "USD"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateExpenseRequest.Currency));
    }

    [Fact]
    public void DescriptionOverFiveHundredCharacters_Fails()
    {
        var result = Validator.Validate(CreateRequest(description: new string('a', 501)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateExpenseRequest.Description));
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
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateExpenseRequest.ExpenseDate));
    }

    [Fact]
    public void MissingCategory_FailsWithoutThrowing()
    {
        var result = Validator.Validate(CreateRequest(category: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateExpenseRequest.Category));
    }

    [Fact]
    public void MissingCurrency_FailsWithoutThrowing()
    {
        var result = Validator.Validate(CreateRequest(currency: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateExpenseRequest.Currency));
    }

    [Fact]
    public void MissingDescription_Fails()
    {
        var result = Validator.Validate(CreateRequest(description: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateExpenseRequest.Description));
    }

    [Fact]
    public void MissingAction_FailsWithoutThrowing()
    {
        var result = Validator.Validate(CreateRequest(action: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateExpenseRequest.Action));
    }
}
