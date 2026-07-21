using FluentValidation;

namespace Application.Attachments;

public class UploadAttachmentRequestValidator : AbstractValidator<UploadAttachmentRequest>
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png",
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/jpeg", "image/png",
    };

    public UploadAttachmentRequestValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .Must(fileName => AllowedExtensions.Contains(Path.GetExtension(fileName)))
            .WithMessage("File type must be PDF, JPG, or PNG.")
            .OverridePropertyName("file");

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(contentType => AllowedContentTypes.Contains(contentType))
            .WithMessage("File type must be PDF, JPG, or PNG.")
            .OverridePropertyName("file");

        RuleFor(x => x.FileSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage("File size must be between 1 byte and 10 MB.")
            .OverridePropertyName("file");
    }
}
