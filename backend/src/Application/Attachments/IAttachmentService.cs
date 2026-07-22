using Domain.Enums;

namespace Application.Attachments;

public interface IAttachmentService
{
    Task<Guid> UploadAsync(UploadAttachmentRequest request, CancellationToken cancellationToken);

    Task<AttachmentDownloadResult> DownloadAsync(
        Guid employeeId, EmployeeRole role, Guid attachmentId, CancellationToken cancellationToken);
}
