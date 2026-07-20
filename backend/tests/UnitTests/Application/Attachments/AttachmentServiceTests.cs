using Application.Attachments;
using Domain.Entities;
using Domain.Repositories;
using Domain.Storage;

namespace UnitTests.Application.Attachments;

public class AttachmentServiceTests
{
    [Fact]
    public async Task UploadAsync_PreservesOriginalFileName_UsesGeneratedOnDiskName()
    {
        var repository = new FakeAttachmentRepository();
        var fileStorage = new FakeFileStorageService();
        var unitOfWork = new FakeUnitOfWork();
        var service = new AttachmentService(repository, fileStorage, unitOfWork);

        var request = new UploadAttachmentRequest
        {
            FileName = "My Receipt (final).PDF",
            ContentType = "application/pdf",
            FileSize = 1024,
            Content = Stream.Null,
        };

        var attachmentId = await service.UploadAsync(request, CancellationToken.None);

        var stored = Assert.Single(repository.Attachments);
        Assert.Equal(attachmentId, stored.Id);
        Assert.Equal("My Receipt (final).PDF", stored.OriginalFileName);
        Assert.NotEqual("My Receipt (final).PDF", stored.FileName);
        Assert.Equal(fileStorage.LastSavedRelativePath, stored.StoragePath);
    }

    [Fact]
    public async Task UploadAsync_SaveChangesFails_DeletesTheJustWrittenFile()
    {
        var repository = new FakeAttachmentRepository();
        var fileStorage = new FakeFileStorageService();
        var unitOfWork = new FakeUnitOfWork { ThrowOnSaveChanges = true };
        var service = new AttachmentService(repository, fileStorage, unitOfWork);

        var request = new UploadAttachmentRequest
        {
            FileName = "receipt.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            Content = Stream.Null,
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(request, CancellationToken.None));

        Assert.Contains(fileStorage.LastSavedRelativePath!, fileStorage.DeletedRelativePaths);
    }
}

internal sealed class FakeAttachmentRepository : IAttachmentRepository
{
    private readonly List<Attachment> _attachments = [];

    public IReadOnlyList<Attachment> Attachments => _attachments;

    public Task<Attachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_attachments.FirstOrDefault(a => a.Id == id));

    public Task AddAsync(Attachment entity, CancellationToken cancellationToken)
    {
        _attachments.Add(entity);
        return Task.CompletedTask;
    }

    public IQueryable<Attachment> Query() => _attachments.AsQueryable();

    public void Remove(Attachment attachment) => _attachments.Remove(attachment);
}

internal sealed class FakeFileStorageService : IFileStorageService
{
    public string? LastSavedRelativePath { get; private set; }

    public List<string> DeletedRelativePaths { get; } = [];

    public Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken)
    {
        LastSavedRelativePath = $"2026/07/{Guid.NewGuid()}{fileExtension}";
        return Task.FromResult(LastSavedRelativePath);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken)
    {
        DeletedRelativePaths.Add(relativePath);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public bool ThrowOnSaveChanges { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (ThrowOnSaveChanges)
        {
            throw new InvalidOperationException("Simulated SaveChanges failure.");
        }

        return Task.FromResult(0);
    }

    public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken) => operation();
}
