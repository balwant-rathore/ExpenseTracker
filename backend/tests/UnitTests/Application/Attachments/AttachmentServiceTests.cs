using System.Linq.Expressions;
using Application.Attachments;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Domain.Storage;

namespace UnitTests.Application.Attachments;

public class AttachmentServiceTests
{
    [Fact]
    public async Task UploadAsync_PreservesOriginalFileName_UsesGeneratedOnDiskName()
    {
        var repository = new FakeAttachmentRepository();
        var expenseRepository = new FakeExpenseRepository();
        var fileStorage = new FakeFileStorageService();
        var unitOfWork = new FakeUnitOfWork();
        var service = new AttachmentService(repository, expenseRepository, fileStorage, unitOfWork);

        var request = new UploadAttachmentRequest
        {
            FileName = "My Receipt (final).PDF",
            ContentType = "application/pdf",
            FileSize = 1024,
            Content = Stream.Null,
            UploadedByEmployeeId = Guid.NewGuid(),
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
        var expenseRepository = new FakeExpenseRepository();
        var fileStorage = new FakeFileStorageService();
        var unitOfWork = new FakeUnitOfWork { ThrowOnSaveChanges = true };
        var service = new AttachmentService(repository, expenseRepository, fileStorage, unitOfWork);

        var request = new UploadAttachmentRequest
        {
            FileName = "receipt.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            Content = Stream.Null,
            UploadedByEmployeeId = Guid.NewGuid(),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(request, CancellationToken.None));

        Assert.Contains(fileStorage.LastSavedRelativePath!, fileStorage.DeletedRelativePaths);
    }

    [Fact]
    public async Task DownloadAsync_FileMissingOnDisk_ReturnsAttachmentNotFoundInsteadOfThrowing()
    {
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            FileName = "stored.pdf",
            OriginalFileName = "receipt.pdf",
            ContentType = "application/pdf",
            FileExtension = ".pdf",
            FileSize = 1024,
            StoragePath = "2026/07/missing.pdf",
            UploadedAt = DateTime.UtcNow,
            UploadedByEmployeeId = Guid.NewGuid(),
        };
        var employeeId = Guid.NewGuid();
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            AttachmentId = attachment.Id,
            Status = ExpenseStatus.Draft,
        };
        var repository = new FakeAttachmentRepository();
        await repository.AddAsync(attachment, CancellationToken.None);
        var expenseRepository = new FakeExpenseRepository();
        await expenseRepository.AddAsync(expense, CancellationToken.None);
        var fileStorage = new FakeFileStorageService { ThrowFileNotFoundOnOpenRead = true };
        var service = new AttachmentService(repository, expenseRepository, fileStorage, new FakeUnitOfWork());

        var result = await service.DownloadAsync(employeeId, EmployeeRole.Employee, attachment.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AttachmentDownloadFailureReason.AttachmentNotFound, result.FailureReason);
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

    public bool ThrowFileNotFoundOnOpenRead { get; set; }

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

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        if (ThrowFileNotFoundOnOpenRead)
        {
            throw new FileNotFoundException("Simulated missing file.", relativePath);
        }

        return Task.FromResult<Stream>(Stream.Null);
    }
}

internal sealed class FakeExpenseRepository : IExpenseRepository
{
    private readonly List<Expense> _expenses = [];

    public Task<Expense?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_expenses.FirstOrDefault(e => e.Id == id));

    public Task AddAsync(Expense entity, CancellationToken cancellationToken)
    {
        _expenses.Add(entity);
        return Task.CompletedTask;
    }

    public IQueryable<Expense> Query() => _expenses.AsQueryable();

    public Task<int> CountByExpenseNumberPrefixAsync(string prefix, CancellationToken cancellationToken) =>
        Task.FromResult(0);

    public Task<bool> ExistsByAttachmentIdAsync(Guid attachmentId, CancellationToken cancellationToken) =>
        Task.FromResult(_expenses.Any(e => e.AttachmentId == attachmentId));

    public Task<ExpenseInsertOutcome> TryAddAsync(Expense expense, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not exercised by these Upload-focused tests.");

    public Task<bool> TryUpdateAsync(Expense expense, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not exercised by these Upload-focused tests.");

    public Task<Expense?> GetByIdWithEmployeeAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_expenses.FirstOrDefault(e => e.Id == id));

    public Task<Expense?> GetByAttachmentIdWithEmployeeAsync(Guid attachmentId, CancellationToken cancellationToken) =>
        Task.FromResult(_expenses.FirstOrDefault(e => e.AttachmentId == attachmentId));

    public Task<(IReadOnlyList<Expense> Items, int TotalRecords)> GetPagedAsync(
        Expression<Func<Expense, bool>> visibilityPredicate,
        ExpenseSortField sortBy,
        bool descending,
        int page,
        int pageSize,
        ExpenseStatus? statusFilter,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not exercised by these Upload-focused tests.");

    public Task<(IReadOnlyList<Expense> Items, int TotalRecords)> SearchPagedAsync(
        string? expenseNumber,
        string? employeeName,
        ExpenseCategory? category,
        ExpenseStatus? status,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        ExpenseSortField sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not exercised by these Upload-focused tests.");

    public Task<IReadOnlyList<Expense>> GetReimbursedForReportAsync(
        DateTime rangeStartUtcInclusive, DateTime rangeEndUtcExclusive, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not exercised by these Upload-focused tests.");

    public Task<IReadOnlyList<StatusCategoryCount>> GetStatusCategoryCountsAsync(
        Expression<Func<Expense, bool>> scopePredicate, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not exercised by these Upload-focused tests.");
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
