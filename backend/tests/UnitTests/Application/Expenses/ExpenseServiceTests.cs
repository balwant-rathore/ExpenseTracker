using Application.Expenses;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;

namespace UnitTests.Application.Expenses;

public class ExpenseServiceTests
{
    private static readonly Guid EmployeeId = Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_RetriesOnExpenseNumberConflict_ThenSucceeds()
    {
        var expenseRepository = new FakeExpenseRepository
        {
            OutcomeQueue = new Queue<ExpenseInsertOutcome>([ExpenseInsertOutcome.ExpenseNumberConflict, ExpenseInsertOutcome.Success]),
        };
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var attachment = CreateAttachment(EmployeeId);
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.CreateAsync(EmployeeId, CreateRequest(attachment.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Single(expenseRepository.Expenses);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Submit")]
    public async Task CreateAsync_AmountNotPositive_Fails(string action)
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var attachment = CreateAttachment(EmployeeId);
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.CreateAsync(EmployeeId, CreateRequest(attachment.Id, amount: 0m, action: action), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.AmountNotPositive, result.FailureReason);
        Assert.Empty(expenseRepository.Expenses);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Submit")]
    public async Task CreateAsync_FutureExpenseDate_Fails(string action)
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var attachment = CreateAttachment(EmployeeId);
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var result = await service.CreateAsync(EmployeeId, CreateRequest(attachment.Id, expenseDate: futureDate, action: action), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.ExpenseDateInFuture, result.FailureReason);
        Assert.Empty(expenseRepository.Expenses);
    }

    [Fact]
    public async Task CreateAsync_AttachmentNotFound_Fails()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.CreateAsync(EmployeeId, CreateRequest(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.AttachmentNotFound, result.FailureReason);
    }

    [Fact]
    public async Task CreateAsync_AttachmentAlreadyLinked_Fails()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var attachment = CreateAttachment(EmployeeId);
        attachmentRepository.Attachments.Add(attachment);
        expenseRepository.Expenses.Add(new Expense { Id = Guid.NewGuid(), AttachmentId = attachment.Id, ExpenseNumber = "EXP-EXISTING-0001" });
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.CreateAsync(EmployeeId, CreateRequest(attachment.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.AttachmentAlreadyLinked, result.FailureReason);
    }

    [Fact]
    public async Task CreateAsync_AttachmentUploadedByDifferentEmployee_Fails()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var attachment = CreateAttachment(Guid.NewGuid());
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.CreateAsync(EmployeeId, CreateRequest(attachment.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.AttachmentNotOwned, result.FailureReason);
    }

    [Fact]
    public async Task CreateAsync_Draft_HasNoSubmittedAt()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var attachment = CreateAttachment(EmployeeId);
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.CreateAsync(EmployeeId, CreateRequest(attachment.Id, action: "Draft"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Draft", result.Expense!.Status);
        Assert.Null(result.Expense.SubmittedAt);
    }

    [Fact]
    public async Task CreateAsync_Submit_HasSubmittedAtPopulated()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var attachment = CreateAttachment(EmployeeId);
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.CreateAsync(EmployeeId, CreateRequest(attachment.Id, action: "Submit"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Submitted", result.Expense!.Status);
        Assert.NotNull(result.Expense.SubmittedAt);
    }

    [Fact]
    public async Task SubmitAsync_OwnerDraft_TransitionsToSubmitted()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.SubmitAsync(EmployeeId, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Submitted", result.Expense!.Status);
        Assert.NotNull(result.Expense.SubmittedAt);
        Assert.Equal(ExpenseStatus.Submitted, expense.Status);
    }

    [Fact]
    public async Task SubmitAsync_NonOwner_ReturnsNotOwner()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.SubmitAsync(Guid.NewGuid(), expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotOwner, result.FailureReason);
        Assert.Equal(ExpenseStatus.Draft, expense.Status);
    }

    [Fact]
    public async Task SubmitAsync_NonDraftStatus_ReturnsNotDraft()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        expense.Status = ExpenseStatus.Submitted;
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.SubmitAsync(EmployeeId, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotDraft, result.FailureReason);
    }

    [Fact]
    public async Task SubmitAsync_AttachmentNoLongerValid_ReturnsFailure_StatusRemainsDraft()
    {
        var expense = CreateDraftExpense(EmployeeId, Guid.NewGuid());
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.SubmitAsync(EmployeeId, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.AttachmentNotFound, result.FailureReason);
        Assert.Equal(ExpenseStatus.Draft, expense.Status);
    }

    [Fact]
    public async Task SubmitAsync_StoredCurrencyNoLongerInr_ReturnsFailure_StatusRemainsDraft()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        expense.Currency = "USD";
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.SubmitAsync(EmployeeId, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.CurrencyInvalid, result.FailureReason);
        Assert.Equal(ExpenseStatus.Draft, expense.Status);
    }

    [Fact]
    public async Task SubmitAsync_StoredDescriptionOverFiveHundredCharacters_ReturnsFailure_StatusRemainsDraft()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        expense.Description = new string('a', 501);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.SubmitAsync(EmployeeId, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.DescriptionTooLong, result.FailureReason);
        Assert.Equal(ExpenseStatus.Draft, expense.Status);
    }

    [Fact]
    public async Task CreateAsync_UsesCompanyClock_NotRawUtcNow_ForFutureDateCheck()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var attachment = CreateAttachment(EmployeeId);
        attachmentRepository.Attachments.Add(attachment);
        var companyClock = new FakeCompanyClock { FixedToday = new DateOnly(2026, 3, 20) };
        var service = new ExpenseService(expenseRepository, attachmentRepository, new FakeExpenseNumberGenerator(), companyClock, new FakeExpensesUnitOfWork());

        var sameDayAsCompanyToday = CreateRequest(attachment.Id, expenseDate: new DateOnly(2026, 3, 20));
        var oneDayAfterCompanyToday = CreateRequest(attachment.Id, expenseDate: new DateOnly(2026, 3, 21));

        var successResult = await service.CreateAsync(EmployeeId, sameDayAsCompanyToday, CancellationToken.None);
        var failureResult = await service.CreateAsync(EmployeeId, oneDayAfterCompanyToday, CancellationToken.None);

        Assert.True(successResult.Succeeded);
        Assert.False(failureResult.Succeeded);
        Assert.Equal(ExpenseFailureReason.ExpenseDateInFuture, failureResult.FailureReason);
    }

    private static ExpenseService CreateService(FakeExpenseRepository expenseRepository, FakeExpensesAttachmentRepository attachmentRepository) =>
        new(expenseRepository, attachmentRepository, new FakeExpenseNumberGenerator(), new FakeCompanyClock(), new FakeExpensesUnitOfWork());

    private static Attachment CreateAttachment(Guid uploaderId) => new()
    {
        Id = Guid.NewGuid(),
        FileName = "test.pdf",
        OriginalFileName = "test.pdf",
        ContentType = "application/pdf",
        FileExtension = ".pdf",
        StoragePath = "2026/07/test.pdf",
        UploadedAt = DateTime.UtcNow,
        UploadedByEmployeeId = uploaderId,
    };

    private static Expense CreateDraftExpense(Guid employeeId, Guid attachmentId) => new()
    {
        Id = Guid.NewGuid(),
        ExpenseNumber = "EXP-TEST-0001",
        EmployeeId = employeeId,
        AttachmentId = attachmentId,
        ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
        Category = ExpenseCategory.Travel,
        Amount = 100m,
        Currency = "INR",
        Description = "Existing draft",
        Status = ExpenseStatus.Draft,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static CreateExpenseRequest CreateRequest(
        Guid attachmentId, decimal amount = 100m, DateOnly? expenseDate = null, string action = "Draft") => new()
        {
            ExpenseDate = expenseDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Category = "Travel",
            Amount = amount,
            Currency = "INR",
            Description = "Test expense",
            ReceiptAttachmentId = attachmentId,
            Action = action,
        };
}

internal sealed class FakeExpensesAttachmentRepository : IAttachmentRepository
{
    public List<Attachment> Attachments { get; } = [];

    public Task<Attachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Attachments.FirstOrDefault(a => a.Id == id));

    public Task AddAsync(Attachment entity, CancellationToken cancellationToken)
    {
        Attachments.Add(entity);
        return Task.CompletedTask;
    }

    public IQueryable<Attachment> Query() => Attachments.AsQueryable();

    public void Remove(Attachment attachment) => Attachments.Remove(attachment);
}

internal sealed class FakeExpenseNumberGenerator : IExpenseNumberGenerator
{
    private int _count;

    public Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        _count++;
        return Task.FromResult($"EXP-TEST-{_count:D4}");
    }
}

internal sealed class FakeExpensesUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);

    public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken) => operation();
}
