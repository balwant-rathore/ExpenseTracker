using Application.Attachments;

namespace UnitTests.Application.Attachments;

public class UploadAttachmentRequestValidatorTests
{
    private static readonly UploadAttachmentRequestValidator Validator = new();

    private static UploadAttachmentRequest CreateRequest(string fileName, string contentType, long fileSize) =>
        new()
        {
            FileName = fileName,
            ContentType = contentType,
            FileSize = fileSize,
            Content = Stream.Null,
            UploadedByEmployeeId = Guid.NewGuid(),
        };

    [Theory]
    [InlineData("receipt.pdf", "application/pdf")]
    [InlineData("receipt.jpg", "image/jpeg")]
    [InlineData("receipt.jpeg", "image/jpeg")]
    [InlineData("receipt.png", "image/png")]
    public void ValidRequest_Passes(string fileName, string contentType)
    {
        var result = Validator.Validate(CreateRequest(fileName, contentType, 1024));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void DisallowedExtension_Fails()
    {
        var result = Validator.Validate(CreateRequest("receipt.docx", "application/msword", 1024));

        Assert.False(result.IsValid);
        Assert.All(result.Errors, e => Assert.Equal("file", e.PropertyName));
    }

    [Fact]
    public void DisallowedContentType_WithAllowedExtension_Fails()
    {
        var result = Validator.Validate(CreateRequest("receipt.pdf", "application/octet-stream", 1024));

        Assert.False(result.IsValid);
        Assert.All(result.Errors, e => Assert.Equal("file", e.PropertyName));
    }

    [Fact]
    public void OversizedFile_Fails()
    {
        var result = Validator.Validate(CreateRequest("receipt.pdf", "application/pdf", UploadAttachmentRequestValidator.MaxFileSizeBytes + 1));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void FileExactlyAtTenMegabyteBoundary_Passes()
    {
        var result = Validator.Validate(CreateRequest("receipt.pdf", "application/pdf", UploadAttachmentRequestValidator.MaxFileSizeBytes));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyFile_Fails()
    {
        var result = Validator.Validate(CreateRequest("receipt.pdf", "application/pdf", 0));

        Assert.False(result.IsValid);
    }
}
