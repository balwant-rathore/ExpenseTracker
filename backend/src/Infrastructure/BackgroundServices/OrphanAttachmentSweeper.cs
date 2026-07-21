using Domain.Repositories;
using Domain.Storage;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundServices;

public class OrphanAttachmentSweeper : IOrphanAttachmentSweeper
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AttachmentCleanupOptions _options;

    public OrphanAttachmentSweeper(
        IAttachmentRepository attachmentRepository,
        IFileStorageService fileStorage,
        IUnitOfWork unitOfWork,
        IOptions<AttachmentCleanupOptions> options)
    {
        _attachmentRepository = attachmentRepository;
        _fileStorage = fileStorage;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddHours(-_options.OrphanThresholdHours);
        var orphans = _attachmentRepository.Query()
            .Where(a => a.Expense == null && a.UploadedAt < cutoff)
            .ToList();

        foreach (var orphan in orphans)
        {
            await _fileStorage.DeleteAsync(orphan.StoragePath, cancellationToken);
            _attachmentRepository.Remove(orphan);
        }

        if (orphans.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
