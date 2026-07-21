using Application.Expenses;

namespace UnitTests.Application.Expenses;

public class RejectExpenseRequestValidatorTests
{
    private static readonly RejectExpenseRequestValidator Validator = new();

    [Fact]
    public void ValidComment_Passes()
    {
        var result = Validator.Validate(new RejectExpenseRequest { RejectionComment = "Missing itemized receipt" });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void MissingComment_FailsWithoutThrowing()
    {
        var result = Validator.Validate(new RejectExpenseRequest { RejectionComment = null });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RejectExpenseRequest.RejectionComment));
    }

    [Fact]
    public void EmptyComment_Fails()
    {
        var result = Validator.Validate(new RejectExpenseRequest { RejectionComment = string.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RejectExpenseRequest.RejectionComment));
    }

    [Fact]
    public void WhitespaceOnlyComment_Fails()
    {
        var result = Validator.Validate(new RejectExpenseRequest { RejectionComment = "   " });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RejectExpenseRequest.RejectionComment));
    }

    [Fact]
    public void CommentOverFiveHundredCharacters_Fails()
    {
        var result = Validator.Validate(new RejectExpenseRequest { RejectionComment = new string('a', 501) });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RejectExpenseRequest.RejectionComment));
    }

    [Fact]
    public void CommentAtFiveHundredCharacters_Passes()
    {
        var result = Validator.Validate(new RejectExpenseRequest { RejectionComment = new string('a', 500) });

        Assert.True(result.IsValid);
    }
}
