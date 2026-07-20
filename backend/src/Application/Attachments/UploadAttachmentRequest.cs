namespace Application.Attachments;

public record UploadAttachmentRequest
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }
    public required Stream Content { get; init; }
}
