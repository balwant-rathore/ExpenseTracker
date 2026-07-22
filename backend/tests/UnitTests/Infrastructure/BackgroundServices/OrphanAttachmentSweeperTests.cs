using Domain.Entities;
using Domain.Repositories;
using Domain.Storage;
using Infrastructure.BackgroundServices;
using Microsoft.Extensions.Options;

namespace UnitTests.Infrastructure.BackgroundServices;

public class OrphanAttachmentSweeperTests
{
    private static OrphanAttachmentSweeper CreateSweeper(
        FakeAttachmentRepository repository,
        FakeFileStorageService fileStorage,
        FakeUnitOfWork unitOfWork,
        int orphanThresholdHours = 24) =>
        new(repository, fileStorage, unitOfWork, Options.Create(new AttachmentCleanupOptions { OrphanThresholdHours = orphanThresholdHours }));

    private static Attachment CreateAttachment(DateTime uploadedAt, Expense? expense = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            FileName = "generated.pdf",
            OriginalFileName = "receipt.pdf",
            ContentType = "application/pdf",
            FileExtension = ".pdf",
            FileSize = 1024,
            StoragePath = "2026/06/generated.pdf",
            UploadedAt = uploadedAt,
            Expense = expense,
        };

    [Fact]
    public async Task SweepAsync_OrphanOlderThanThreshold_IsDeleted()
    {
        var repository = new FakeAttachmentRepository();
        var attachment = CreateAttachment(DateTime.UtcNow.AddHours(-25));
        repository.Seed(attachment);
        var fileStorage = new FakeFileStorageService();
        var unitOfWork = new FakeUnitOfWork();
        var sweeper = CreateSweeper(repository, fileStorage, unitOfWork);

        await sweeper.SweepAsync(CancellationToken.None);

        Assert.Empty(repository.Attachments);
        Assert.Contains(attachment.StoragePath, fileStorage.DeletedRelativePaths);
        Assert.True(unitOfWork.SaveChangesCalled);
    }

    [Fact]
    public async Task SweepAsync_OrphanYoungerThanThreshold_IsRetained()
    {
        var repository = new FakeAttachmentRepository();
        var attachment = CreateAttachment(DateTime.UtcNow.AddHours(-1));
        repository.Seed(attachment);
        var fileStorage = new FakeFileStorageService();
        var unitOfWork = new FakeUnitOfWork();
        var sweeper = CreateSweeper(repository, fileStorage, unitOfWork);

        await sweeper.SweepAsync(CancellationToken.None);

        Assert.Single(repository.Attachments);
        Assert.Empty(fileStorage.DeletedRelativePaths);
        Assert.False(unitOfWork.SaveChangesCalled);
    }

    [Fact]
    public async Task SweepAsync_LinkedAttachment_IsNeverDeletedRegardlessOfAge()
    {
        var repository = new FakeAttachmentRepository();
        var expense = new Expense { Id = Guid.NewGuid(), ExpenseNumber = "EXP-20260101-0001", Currency = "INR", Description = "d" };
        var attachment = CreateAttachment(DateTime.UtcNow.AddDays(-30), expense);
        repository.Seed(attachment);
        var fileStorage = new FakeFileStorageService();
        var unitOfWork = new FakeUnitOfWork();
        var sweeper = CreateSweeper(repository, fileStorage, unitOfWork);

        await sweeper.SweepAsync(CancellationToken.None);

        Assert.Single(repository.Attachments);
        Assert.Empty(fileStorage.DeletedRelativePaths);
    }
}

internal sealed class FakeAttachmentRepository : IAttachmentRepository
{
    private readonly List<Attachment> _attachments = [];

    public IReadOnlyList<Attachment> Attachments => _attachments;

    public void Seed(Attachment attachment) => _attachments.Add(attachment);

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
    public List<string> DeletedRelativePaths { get; } = [];

    public Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken) =>
        Task.FromResult($"2026/07/{Guid.NewGuid()}{fileExtension}");

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken)
    {
        DeletedRelativePaths.Add(relativePath);
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not exercised by OrphanAttachmentSweeper.");
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public bool SaveChangesCalled { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCalled = true;
        return Task.FromResult(0);
    }

    public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken) => operation();
}
