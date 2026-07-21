using Domain.Entities;
using Domain.Repositories;
using Domain.Storage;

namespace Application.Attachments;

public class AttachmentService : IAttachmentService
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _unitOfWork;

    public AttachmentService(
        IAttachmentRepository attachmentRepository,
        IFileStorageService fileStorage,
        IUnitOfWork unitOfWork)
    {
        _attachmentRepository = attachmentRepository;
        _fileStorage = fileStorage;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> UploadAsync(UploadAttachmentRequest request, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        var relativePath = await _fileStorage.SaveAsync(request.Content, extension, cancellationToken);

        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            FileName = Path.GetFileName(relativePath),
            OriginalFileName = request.FileName,
            ContentType = request.ContentType,
            FileExtension = extension,
            FileSize = (int)request.FileSize,
            StoragePath = relativePath,
            UploadedAt = DateTime.UtcNow,
        };

        try
        {
            await _attachmentRepository.AddAsync(attachment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _fileStorage.DeleteAsync(relativePath, CancellationToken.None);
            throw;
        }

        return attachment.Id;
    }
}
