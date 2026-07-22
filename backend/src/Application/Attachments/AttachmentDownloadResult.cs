namespace Application.Attachments;

public record AttachmentDownloadResult(
    bool Succeeded,
    Stream? Content,
    string? ContentType,
    string? OriginalFileName,
    AttachmentDownloadFailureReason FailureReason)
{
    public static AttachmentDownloadResult Success(Stream content, string contentType, string originalFileName) =>
        new(true, content, contentType, originalFileName, AttachmentDownloadFailureReason.None);

    public static AttachmentDownloadResult Failure(AttachmentDownloadFailureReason reason) =>
        new(false, null, null, null, reason);
}
