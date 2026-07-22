using Application.Expenses;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Domain.Storage;

namespace Application.Attachments;

public class AttachmentService : IAttachmentService
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _unitOfWork;

    public AttachmentService(
        IAttachmentRepository attachmentRepository,
        IExpenseRepository expenseRepository,
        IFileStorageService fileStorage,
        IUnitOfWork unitOfWork)
    {
        _attachmentRepository = attachmentRepository;
        _expenseRepository = expenseRepository;
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
            UploadedByEmployeeId = request.UploadedByEmployeeId,
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

    public async Task<AttachmentDownloadResult> DownloadAsync(
        Guid employeeId, EmployeeRole role, Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetByIdAsync(attachmentId, cancellationToken);
        if (attachment is null)
        {
            return AttachmentDownloadResult.Failure(AttachmentDownloadFailureReason.AttachmentNotFound);
        }

        var owningExpense = await _expenseRepository.GetByAttachmentIdWithEmployeeAsync(attachmentId, cancellationToken);
        if (owningExpense is null)
        {
            return AttachmentDownloadResult.Failure(AttachmentDownloadFailureReason.AttachmentNotFound);
        }

        var isVisible = ExpenseVisibility.BuildPredicate(role, employeeId).Compile().Invoke(owningExpense);
        if (!isVisible)
        {
            return AttachmentDownloadResult.Failure(AttachmentDownloadFailureReason.NotVisible);
        }

        Stream stream;
        try
        {
            stream = await _fileStorage.OpenReadAsync(attachment.StoragePath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return AttachmentDownloadResult.Failure(AttachmentDownloadFailureReason.AttachmentNotFound);
        }

        return AttachmentDownloadResult.Success(stream, attachment.ContentType, attachment.OriginalFileName);
    }
}
