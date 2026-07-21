namespace Application.Attachments;

public interface IAttachmentService
{
    Task<Guid> UploadAsync(UploadAttachmentRequest request, CancellationToken cancellationToken);
}
