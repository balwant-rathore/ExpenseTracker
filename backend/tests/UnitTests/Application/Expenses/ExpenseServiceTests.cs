using Application.Expenses;
using Domain.Entities;
using Domain.Enums;
using Domain.Notifications;
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
    public async Task CreateAsync_Submit_SendsSubmittedNotificationAfterCommit()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var attachment = CreateAttachment(EmployeeId);
        attachmentRepository.Attachments.Add(attachment);
        var notificationService = new FakeNotificationService();
        var service = new ExpenseService(expenseRepository, attachmentRepository, new FakeExpenseNumberGenerator(), new FakeCompanyClock(), new FakeExpensesUnitOfWork(), notificationService);

        var result = await service.CreateAsync(EmployeeId, CreateRequest(attachment.Id, action: "Submit"), CancellationToken.None);

        Assert.True(result.Succeeded);
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal(NotificationEvent.Submitted, notification.Event);
    }

    [Fact]
    public async Task CreateAsync_Draft_SendsNoNotification()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var attachment = CreateAttachment(EmployeeId);
        attachmentRepository.Attachments.Add(attachment);
        var notificationService = new FakeNotificationService();
        var service = new ExpenseService(expenseRepository, attachmentRepository, new FakeExpenseNumberGenerator(), new FakeCompanyClock(), new FakeExpensesUnitOfWork(), notificationService);

        var result = await service.CreateAsync(EmployeeId, CreateRequest(attachment.Id, action: "Draft"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(notificationService.Notifications);
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
    public async Task SubmitAsync_OwnerDraft_SendsSubmittedNotificationAfterCommit()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var notificationService = new FakeNotificationService();
        var service = new ExpenseService(expenseRepository, attachmentRepository, new FakeExpenseNumberGenerator(), new FakeCompanyClock(), new FakeExpensesUnitOfWork(), notificationService);

        var result = await service.SubmitAsync(EmployeeId, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal(NotificationEvent.Submitted, notification.Event);
    }

    [Fact]
    public async Task SubmitAsync_NonOwner_SendsNoNotification()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var notificationService = new FakeNotificationService();
        var service = new ExpenseService(expenseRepository, attachmentRepository, new FakeExpenseNumberGenerator(), new FakeCompanyClock(), new FakeExpensesUnitOfWork(), notificationService);

        var result = await service.SubmitAsync(Guid.NewGuid(), expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(notificationService.Notifications);
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
        var service = new ExpenseService(expenseRepository, attachmentRepository, new FakeExpenseNumberGenerator(), companyClock, new FakeExpensesUnitOfWork(), new FakeNotificationService());

        var sameDayAsCompanyToday = CreateRequest(attachment.Id, expenseDate: new DateOnly(2026, 3, 20));
        var oneDayAfterCompanyToday = CreateRequest(attachment.Id, expenseDate: new DateOnly(2026, 3, 21));

        var successResult = await service.CreateAsync(EmployeeId, sameDayAsCompanyToday, CancellationToken.None);
        var failureResult = await service.CreateAsync(EmployeeId, oneDayAfterCompanyToday, CancellationToken.None);

        Assert.True(successResult.Succeeded);
        Assert.False(failureResult.Succeeded);
        Assert.Equal(ExpenseFailureReason.ExpenseDateInFuture, failureResult.FailureReason);
    }

    [Fact]
    public async Task GetByIdAsync_Owner_ReturnsExpense()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.GetByIdAsync(EmployeeId, EmployeeRole.Employee, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(expense.Id, result.Expense!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonOwner_ReturnsNotVisible()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.GetByIdAsync(Guid.NewGuid(), EmployeeRole.Employee, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotVisible, result.FailureReason);
    }

    [Fact]
    public async Task GetByIdAsync_NonexistentId_ReturnsExpenseNotFound()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.GetByIdAsync(EmployeeId, EmployeeRole.Employee, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.ExpenseNotFound, result.FailureReason);
    }

    [Fact]
    public async Task GetByIdAsync_ManagerOwnExpenseAnyStatus_ReturnsExpense()
    {
        var managerId = Guid.NewGuid();
        var attachment = CreateAttachment(managerId);
        var expense = CreateExpense(managerId, attachment.Id, ExpenseStatus.Draft);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.GetByIdAsync(managerId, EmployeeRole.Manager, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(expense.Id, result.Expense!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ManagerDirectReportsNonDraftExpense_ReturnsExpense()
    {
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var attachment = CreateAttachment(reportId);
        var expense = CreateExpense(reportId, attachment.Id, ExpenseStatus.Submitted, managerId: managerId);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.GetByIdAsync(managerId, EmployeeRole.Manager, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(expense.Id, result.Expense!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ManagerDirectReportsDraftExpense_ReturnsNotVisible()
    {
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var attachment = CreateAttachment(reportId);
        var expense = CreateExpense(reportId, attachment.Id, ExpenseStatus.Draft, managerId: managerId);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.GetByIdAsync(managerId, EmployeeRole.Manager, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotVisible, result.FailureReason);
    }

    [Fact]
    public async Task GetByIdAsync_ManagerUnrelatedEmployeesExpense_ReturnsNotVisible()
    {
        var managerId = Guid.NewGuid();
        var unrelatedEmployeeId = Guid.NewGuid();
        var attachment = CreateAttachment(unrelatedEmployeeId);
        var expense = CreateExpense(unrelatedEmployeeId, attachment.Id, ExpenseStatus.Submitted, managerId: Guid.NewGuid());
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.GetByIdAsync(managerId, EmployeeRole.Manager, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotVisible, result.FailureReason);
    }

    [Fact]
    public async Task GetByIdAsync_FinanceAnyNonDraftExpense_ReturnsExpense()
    {
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Submitted);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.GetByIdAsync(Guid.NewGuid(), EmployeeRole.Finance, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(expense.Id, result.Expense!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_FinanceDraftExpense_ReturnsNotVisible()
    {
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Draft);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.GetByIdAsync(Guid.NewGuid(), EmployeeRole.Finance, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotVisible, result.FailureReason);
    }

    [Fact]
    public async Task GetByIdAsync_ComplianceApprovedClientEntertainmentExpense_ReturnsExpense()
    {
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Approved, ExpenseCategory.ClientEntertainment);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.GetByIdAsync(Guid.NewGuid(), EmployeeRole.ComplianceOfficer, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(expense.Id, result.Expense!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ComplianceComplianceApprovedClientEntertainmentExpense_ReturnsExpense()
    {
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.ComplianceApproved, ExpenseCategory.ClientEntertainment);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.GetByIdAsync(Guid.NewGuid(), EmployeeRole.ComplianceOfficer, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(expense.Id, result.Expense!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ComplianceReimbursedClientEntertainmentExpense_ReturnsNotVisible()
    {
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Reimbursed, ExpenseCategory.ClientEntertainment);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.GetByIdAsync(Guid.NewGuid(), EmployeeRole.ComplianceOfficer, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotVisible, result.FailureReason);
    }

    [Fact]
    public async Task GetByIdAsync_ComplianceNonClientEntertainmentExpense_ReturnsNotVisible()
    {
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Approved, ExpenseCategory.Travel);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.GetByIdAsync(Guid.NewGuid(), EmployeeRole.ComplianceOfficer, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotVisible, result.FailureReason);
    }

    [Fact]
    public async Task GetVisibleAsync_ReflectsRequestPagingAndRepositoryTotal()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        for (var i = 0; i < 3; i++)
        {
            var attachment = CreateAttachment(EmployeeId);
            expenseRepository.Expenses.Add(CreateExpense(EmployeeId, attachment.Id, ExpenseStatus.Submitted));
        }
        var service = CreateService(expenseRepository, attachmentRepository);
        var request = new ExpenseListRequest { Page = 1, PageSize = 10, SortBy = "expenseDate", SortDirection = "desc" };

        var result = await service.GetVisibleAsync(EmployeeId, EmployeeRole.Employee, request, CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalRecords);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task GetVisibleAsync_MapsEmployeeNameFromLoadedEmployee()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateExpense(EmployeeId, attachment.Id, ExpenseStatus.Submitted);
        expenseRepository.Expenses.Add(expense);
        var service = CreateService(expenseRepository, attachmentRepository);
        var request = new ExpenseListRequest();

        var result = await service.GetVisibleAsync(EmployeeId, EmployeeRole.Employee, request, CancellationToken.None);

        var mapped = Assert.Single(result.Items);
        Assert.Equal($"{expense.Employee.FirstName} {expense.Employee.LastName}", mapped.EmployeeName);
    }

    [Fact]
    public async Task UpdateAsync_OwnerOnDraft_UpdatesFields_StatusRemainsDraft()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.UpdateAsync(EmployeeId, expense.Id, UpdateRequest(attachment.Id, amount: 999m, description: "Updated"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Draft", result.Expense!.Status);
        Assert.Equal(999m, result.Expense.Amount);
        Assert.Equal("Updated", result.Expense.Description);
    }

    [Fact]
    public async Task UpdateAsync_OwnerOnSubmitted_UpdatesFields_StatusRemainsSubmitted()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        expense.Status = ExpenseStatus.Submitted;
        expense.SubmittedAt = DateTime.UtcNow;
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.UpdateAsync(EmployeeId, expense.Id, UpdateRequest(attachment.Id, amount: 500m), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Submitted", result.Expense!.Status);
    }

    [Fact]
    public async Task UpdateAsync_NonOwner_ReturnsNotOwner()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.UpdateAsync(Guid.NewGuid(), expense.Id, UpdateRequest(attachment.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotOwner, result.FailureReason);
    }

    [Fact]
    public async Task UpdateAsync_NonexistentId_ReturnsExpenseNotFound()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.UpdateAsync(EmployeeId, Guid.NewGuid(), UpdateRequest(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.ExpenseNotFound, result.FailureReason);
    }

    [Fact]
    public async Task UpdateAsync_SubmittedExpense_LeavesSubmittedAtUnchanged()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        expense.Status = ExpenseStatus.Submitted;
        var originalSubmittedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        expense.SubmittedAt = originalSubmittedAt;
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.UpdateAsync(EmployeeId, expense.Id, UpdateRequest(attachment.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Submitted", result.Expense!.Status);
        Assert.Equal(originalSubmittedAt, expense.SubmittedAt);
    }

    [Theory]
    [InlineData(ExpenseStatus.Approved)]
    [InlineData(ExpenseStatus.ComplianceApproved)]
    [InlineData(ExpenseStatus.Rejected)]
    [InlineData(ExpenseStatus.Cancelled)]
    [InlineData(ExpenseStatus.Reimbursed)]
    public async Task UpdateAsync_NonEditableStatus_ReturnsNotEditable(ExpenseStatus status)
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        expense.Status = status;
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.UpdateAsync(EmployeeId, expense.Id, UpdateRequest(attachment.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotEditable, result.FailureReason);
        Assert.Equal(status, expense.Status);
    }

    [Fact]
    public async Task UpdateAsync_AttachmentSwap_Succeeds_OldAttachmentUnlinked()
    {
        var originalAttachment = CreateAttachment(EmployeeId);
        var newAttachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, originalAttachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(originalAttachment);
        attachmentRepository.Attachments.Add(newAttachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.UpdateAsync(EmployeeId, expense.Id, UpdateRequest(newAttachment.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(newAttachment.Id, expense.AttachmentId);
    }

    [Fact]
    public async Task UpdateAsync_ResubmitSameAttachment_Succeeds()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.UpdateAsync(EmployeeId, expense.Id, UpdateRequest(attachment.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(attachment.Id, expense.AttachmentId);
    }

    [Fact]
    public async Task UpdateAsync_AttachmentAlreadyLinkedToDifferentExpense_Fails()
    {
        var attachment = CreateAttachment(EmployeeId);
        var otherAttachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var otherExpense = CreateDraftExpense(EmployeeId, otherAttachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        expenseRepository.Expenses.Add(otherExpense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        attachmentRepository.Attachments.Add(otherAttachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.UpdateAsync(EmployeeId, expense.Id, UpdateRequest(otherAttachment.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.AttachmentAlreadyLinked, result.FailureReason);
    }

    [Fact]
    public async Task UpdateAsync_AttachmentNotOwned_Fails()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var notOwnedAttachment = CreateAttachment(Guid.NewGuid());
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        attachmentRepository.Attachments.Add(notOwnedAttachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.UpdateAsync(EmployeeId, expense.Id, UpdateRequest(notOwnedAttachment.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.AttachmentNotOwned, result.FailureReason);
    }

    [Fact]
    public async Task UpdateAsync_AttachmentNotFound_Fails()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.UpdateAsync(EmployeeId, expense.Id, UpdateRequest(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.AttachmentNotFound, result.FailureReason);
    }

    [Fact]
    public async Task UpdateAsync_AmountNotPositive_Fails()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.UpdateAsync(EmployeeId, expense.Id, UpdateRequest(attachment.Id, amount: 0m), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.AmountNotPositive, result.FailureReason);
    }

    [Fact]
    public async Task UpdateAsync_FutureExpenseDate_Fails()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        attachmentRepository.Attachments.Add(attachment);
        var service = CreateService(expenseRepository, attachmentRepository);
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var result = await service.UpdateAsync(EmployeeId, expense.Id, UpdateRequest(attachment.Id, expenseDate: futureDate), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.ExpenseDateInFuture, result.FailureReason);
    }

    [Fact]
    public async Task CancelAsync_OwnerDraft_TransitionsToCancelled()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.CancelAsync(EmployeeId, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Cancelled", result.Expense!.Status);
        Assert.Equal(ExpenseStatus.Cancelled, expense.Status);
    }

    [Fact]
    public async Task CancelAsync_OwnerSubmitted_TransitionsToCancelled()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        expense.Status = ExpenseStatus.Submitted;
        expense.SubmittedAt = DateTime.UtcNow;
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.CancelAsync(EmployeeId, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Cancelled", result.Expense!.Status);
    }

    [Fact]
    public async Task CancelAsync_NonOwner_ReturnsNotOwner()
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.CancelAsync(Guid.NewGuid(), expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotOwner, result.FailureReason);
        Assert.Equal(ExpenseStatus.Draft, expense.Status);
    }

    [Fact]
    public async Task CancelAsync_NonexistentId_ReturnsExpenseNotFound()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.CancelAsync(EmployeeId, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.ExpenseNotFound, result.FailureReason);
    }

    [Theory]
    [InlineData(ExpenseStatus.Approved)]
    [InlineData(ExpenseStatus.ComplianceApproved)]
    [InlineData(ExpenseStatus.Rejected)]
    [InlineData(ExpenseStatus.Reimbursed)]
    [InlineData(ExpenseStatus.Cancelled)]
    public async Task CancelAsync_NonCancellableStatus_ReturnsNotCancellable(ExpenseStatus status)
    {
        var attachment = CreateAttachment(EmployeeId);
        var expense = CreateDraftExpense(EmployeeId, attachment.Id);
        expense.Status = status;
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.CancelAsync(EmployeeId, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotCancellable, result.FailureReason);
        Assert.Equal(status, expense.Status);
    }

    [Fact]
    public async Task ApproveAsync_DirectManagerOfReport_TransitionsToApproved()
    {
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var attachment = CreateAttachment(reportId);
        var expense = CreateExpense(reportId, attachment.Id, ExpenseStatus.Submitted, managerId: managerId);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ApproveAsync(managerId, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Approved", result.Expense!.Status);
        Assert.Equal(ExpenseStatus.Approved, expense.Status);
        Assert.Equal(managerId, expense.ApprovedByEmployeeId);
        Assert.NotNull(expense.ApprovedAt);
    }

    [Fact]
    public async Task ApproveAsync_OwnExpense_ReturnsNotAuthorizedReviewer()
    {
        var managerId = Guid.NewGuid();
        var attachment = CreateAttachment(managerId);
        var expense = CreateExpense(managerId, attachment.Id, ExpenseStatus.Submitted);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ApproveAsync(managerId, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotAuthorizedReviewer, result.FailureReason);
        Assert.Equal(ExpenseStatus.Submitted, expense.Status);
    }

    [Fact]
    public async Task ApproveAsync_UnrelatedManager_ReturnsNotAuthorizedReviewer()
    {
        var managerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Submitted, managerId: Guid.NewGuid());
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ApproveAsync(managerId, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotAuthorizedReviewer, result.FailureReason);
    }

    [Fact]
    public async Task ApproveAsync_SkipLevelManagerOfManagerOwnedExpense_TransitionsToApproved()
    {
        var skipLevelManagerId = Guid.NewGuid();
        var ownerManagerId = Guid.NewGuid();
        var attachment = CreateAttachment(ownerManagerId);
        var expense = CreateExpense(ownerManagerId, attachment.Id, ExpenseStatus.Submitted, managerId: skipLevelManagerId);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ApproveAsync(skipLevelManagerId, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ExpenseStatus.Approved, expense.Status);
        Assert.Equal(skipLevelManagerId, expense.ApprovedByEmployeeId);
    }

    [Fact]
    public async Task ApproveAsync_NonexistentId_ReturnsExpenseNotFound()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ApproveAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.ExpenseNotFound, result.FailureReason);
    }

    [Theory]
    [InlineData(ExpenseStatus.Draft)]
    [InlineData(ExpenseStatus.Approved)]
    [InlineData(ExpenseStatus.ComplianceApproved)]
    [InlineData(ExpenseStatus.Rejected)]
    [InlineData(ExpenseStatus.Cancelled)]
    [InlineData(ExpenseStatus.Reimbursed)]
    public async Task ApproveAsync_NonSubmittedStatus_ReturnsNotSubmitted(ExpenseStatus status)
    {
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var attachment = CreateAttachment(reportId);
        var expense = CreateExpense(reportId, attachment.Id, status, managerId: managerId);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ApproveAsync(managerId, expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotSubmitted, result.FailureReason);
        Assert.Equal(status, expense.Status);
    }

    [Fact]
    public async Task ApproveAsync_Success_SendsApprovedNotificationAfterCommit()
    {
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var attachment = CreateAttachment(reportId);
        var expense = CreateExpense(reportId, attachment.Id, ExpenseStatus.Submitted, managerId: managerId);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var notificationService = new FakeNotificationService();
        var service = new ExpenseService(expenseRepository, attachmentRepository, new FakeExpenseNumberGenerator(), new FakeCompanyClock(), new FakeExpensesUnitOfWork(), notificationService);

        var result = await service.ApproveAsync(managerId, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal(NotificationEvent.Approved, notification.Event);
    }

    [Fact]
    public async Task RejectAsync_DirectManagerOfReport_TransitionsToRejected_SetsRejectionComment()
    {
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var attachment = CreateAttachment(reportId);
        var expense = CreateExpense(reportId, attachment.Id, ExpenseStatus.Submitted, managerId: managerId);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.RejectAsync(managerId, expense.Id, new RejectExpenseRequest { RejectionComment = "Missing itemized receipt" }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Rejected", result.Expense!.Status);
        Assert.Equal(ExpenseStatus.Rejected, expense.Status);
        Assert.Equal(managerId, expense.RejectedByEmployeeId);
        Assert.NotNull(expense.RejectedAt);
        Assert.Equal("Missing itemized receipt", expense.RejectionComment);
    }

    [Fact]
    public async Task RejectAsync_Success_SendsRejectedNotificationAfterCommit()
    {
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var attachment = CreateAttachment(reportId);
        var expense = CreateExpense(reportId, attachment.Id, ExpenseStatus.Submitted, managerId: managerId);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var notificationService = new FakeNotificationService();
        var service = new ExpenseService(expenseRepository, attachmentRepository, new FakeExpenseNumberGenerator(), new FakeCompanyClock(), new FakeExpensesUnitOfWork(), notificationService);

        var result = await service.RejectAsync(managerId, expense.Id, new RejectExpenseRequest { RejectionComment = "Missing itemized receipt" }, CancellationToken.None);

        Assert.True(result.Succeeded);
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal(NotificationEvent.Rejected, notification.Event);
    }

    [Fact]
    public async Task RejectAsync_OwnExpense_ReturnsNotAuthorizedReviewer()
    {
        var managerId = Guid.NewGuid();
        var attachment = CreateAttachment(managerId);
        var expense = CreateExpense(managerId, attachment.Id, ExpenseStatus.Submitted);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.RejectAsync(managerId, expense.Id, new RejectExpenseRequest { RejectionComment = "Comment" }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotAuthorizedReviewer, result.FailureReason);
        Assert.Equal(ExpenseStatus.Submitted, expense.Status);
    }

    [Fact]
    public async Task RejectAsync_UnrelatedManager_ReturnsNotAuthorizedReviewer()
    {
        var managerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Submitted, managerId: Guid.NewGuid());
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.RejectAsync(managerId, expense.Id, new RejectExpenseRequest { RejectionComment = "Comment" }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotAuthorizedReviewer, result.FailureReason);
    }

    [Fact]
    public async Task RejectAsync_SkipLevelManagerOfManagerOwnedExpense_TransitionsToRejected()
    {
        var skipLevelManagerId = Guid.NewGuid();
        var ownerManagerId = Guid.NewGuid();
        var attachment = CreateAttachment(ownerManagerId);
        var expense = CreateExpense(ownerManagerId, attachment.Id, ExpenseStatus.Submitted, managerId: skipLevelManagerId);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.RejectAsync(skipLevelManagerId, expense.Id, new RejectExpenseRequest { RejectionComment = "Comment" }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ExpenseStatus.Rejected, expense.Status);
        Assert.Equal(skipLevelManagerId, expense.RejectedByEmployeeId);
    }

    [Fact]
    public async Task RejectAsync_NonexistentId_ReturnsExpenseNotFound()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.RejectAsync(Guid.NewGuid(), Guid.NewGuid(), new RejectExpenseRequest { RejectionComment = "Comment" }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.ExpenseNotFound, result.FailureReason);
    }

    [Theory]
    [InlineData(ExpenseStatus.Draft)]
    [InlineData(ExpenseStatus.Approved)]
    [InlineData(ExpenseStatus.ComplianceApproved)]
    [InlineData(ExpenseStatus.Rejected)]
    [InlineData(ExpenseStatus.Cancelled)]
    [InlineData(ExpenseStatus.Reimbursed)]
    public async Task RejectAsync_NonSubmittedStatus_ReturnsNotSubmitted(ExpenseStatus status)
    {
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var attachment = CreateAttachment(reportId);
        var expense = CreateExpense(reportId, attachment.Id, status, managerId: managerId);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.RejectAsync(managerId, expense.Id, new RejectExpenseRequest { RejectionComment = "Comment" }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotSubmitted, result.FailureReason);
        Assert.Equal(status, expense.Status);
    }

    [Fact]
    public async Task ComplianceApproveAsync_ApprovedClientEntertainment_TransitionsToComplianceApproved()
    {
        var complianceOfficerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Approved, ExpenseCategory.ClientEntertainment);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ComplianceApproveAsync(complianceOfficerId, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("ComplianceApproved", result.Expense!.Status);
        Assert.Equal(complianceOfficerId, expense.ComplianceApprovedByEmployeeId);
        Assert.NotNull(expense.ComplianceApprovedAt);
        Assert.Equal(expense.ComplianceApprovedAt, result.Expense.ComplianceApprovedAt);
    }

    [Fact]
    public async Task ComplianceApproveAsync_NonexistentId_ReturnsExpenseNotFound()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ComplianceApproveAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.ExpenseNotFound, result.FailureReason);
    }

    [Fact]
    public async Task ComplianceApproveAsync_NonClientEntertainmentCategory_ReturnsNotClientEntertainment()
    {
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Approved, ExpenseCategory.Travel);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ComplianceApproveAsync(Guid.NewGuid(), expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotClientEntertainment, result.FailureReason);
        Assert.Equal(ExpenseStatus.Approved, expense.Status);
    }

    [Theory]
    [InlineData(ExpenseStatus.Draft)]
    [InlineData(ExpenseStatus.Submitted)]
    [InlineData(ExpenseStatus.ComplianceApproved)]
    [InlineData(ExpenseStatus.Rejected)]
    [InlineData(ExpenseStatus.Cancelled)]
    [InlineData(ExpenseStatus.Reimbursed)]
    public async Task ComplianceApproveAsync_NonApprovedStatus_ReturnsNotApprovedForCompliance(ExpenseStatus status)
    {
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, status, ExpenseCategory.ClientEntertainment);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ComplianceApproveAsync(Guid.NewGuid(), expense.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotApprovedForCompliance, result.FailureReason);
        Assert.Equal(status, expense.Status);
    }

    [Fact]
    public async Task ComplianceApproveAsync_Success_SendsComplianceApprovedNotificationAfterCommit()
    {
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Approved, ExpenseCategory.ClientEntertainment);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var notificationService = new FakeNotificationService();
        var service = new ExpenseService(expenseRepository, attachmentRepository, new FakeExpenseNumberGenerator(), new FakeCompanyClock(), new FakeExpensesUnitOfWork(), notificationService);

        var result = await service.ComplianceApproveAsync(Guid.NewGuid(), expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal(NotificationEvent.ComplianceApproved, notification.Event);
    }

    [Fact]
    public async Task ComplianceRejectAsync_ApprovedClientEntertainment_TransitionsToRejected_SetsRejectionComment()
    {
        var complianceOfficerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Approved, ExpenseCategory.ClientEntertainment);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ComplianceRejectAsync(complianceOfficerId, expense.Id, new RejectExpenseRequest { RejectionComment = "Exceeds per-person entertainment limit" }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Rejected", result.Expense!.Status);
        Assert.Equal(ExpenseStatus.Rejected, expense.Status);
        Assert.Equal(complianceOfficerId, expense.RejectedByEmployeeId);
        Assert.NotNull(expense.RejectedAt);
        Assert.Equal("Exceeds per-person entertainment limit", expense.RejectionComment);
    }

    [Fact]
    public async Task ComplianceRejectAsync_NonexistentId_ReturnsExpenseNotFound()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ComplianceRejectAsync(Guid.NewGuid(), Guid.NewGuid(), new RejectExpenseRequest { RejectionComment = "Comment" }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.ExpenseNotFound, result.FailureReason);
    }

    [Fact]
    public async Task ComplianceRejectAsync_NonClientEntertainmentCategory_ReturnsNotClientEntertainment()
    {
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Approved, ExpenseCategory.Meals);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ComplianceRejectAsync(Guid.NewGuid(), expense.Id, new RejectExpenseRequest { RejectionComment = "Comment" }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotClientEntertainment, result.FailureReason);
        Assert.Equal(ExpenseStatus.Approved, expense.Status);
    }

    [Theory]
    [InlineData(ExpenseStatus.Draft)]
    [InlineData(ExpenseStatus.Submitted)]
    [InlineData(ExpenseStatus.ComplianceApproved)]
    [InlineData(ExpenseStatus.Rejected)]
    [InlineData(ExpenseStatus.Cancelled)]
    [InlineData(ExpenseStatus.Reimbursed)]
    public async Task ComplianceRejectAsync_NonApprovedStatus_ReturnsNotApprovedForCompliance(ExpenseStatus status)
    {
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, status, ExpenseCategory.ClientEntertainment);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ComplianceRejectAsync(Guid.NewGuid(), expense.Id, new RejectExpenseRequest { RejectionComment = "Comment" }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseFailureReason.NotApprovedForCompliance, result.FailureReason);
        Assert.Equal(status, expense.Status);
    }

    [Fact]
    public async Task ComplianceRejectAsync_Success_SendsComplianceRejectedNotificationAfterCommit()
    {
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Approved, ExpenseCategory.ClientEntertainment);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var notificationService = new FakeNotificationService();
        var service = new ExpenseService(expenseRepository, attachmentRepository, new FakeExpenseNumberGenerator(), new FakeCompanyClock(), new FakeExpensesUnitOfWork(), notificationService);

        var result = await service.ComplianceRejectAsync(Guid.NewGuid(), expense.Id, new RejectExpenseRequest { RejectionComment = "Comment" }, CancellationToken.None);

        Assert.True(result.Succeeded);
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal(NotificationEvent.ComplianceRejected, notification.Event);
    }

    [Fact]
    public async Task SearchAsync_NoParametersSupplied_UsesDefaults()
    {
        var expenseRepository = new FakeExpenseRepository();
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        for (var i = 0; i < 3; i++)
        {
            var attachment = CreateAttachment(EmployeeId);
            expenseRepository.Expenses.Add(CreateExpense(EmployeeId, attachment.Id, ExpenseStatus.Submitted));
        }
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.SearchAsync(new ExpenseSearchRequest(), CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(3, result.TotalRecords);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task ReimburseAsync_ApprovedNonClientEntertainment_TransitionsToReimbursed()
    {
        var financeId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Approved, ExpenseCategory.Travel);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ReimburseAsync(financeId, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Reimbursed", result.Expense!.Status);
        Assert.Equal(ExpenseStatus.Reimbursed, expense.Status);
    }

    [Fact]
    public async Task ReimburseAsync_ComplianceApprovedClientEntertainment_TransitionsToReimbursed()
    {
        var financeId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.ComplianceApproved, ExpenseCategory.ClientEntertainment);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ReimburseAsync(financeId, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Reimbursed", result.Expense!.Status);
        Assert.Equal(ExpenseStatus.Reimbursed, expense.Status);
    }

    [Fact]
    public async Task ReimburseAsync_Success_SendsReimbursedNotificationAfterCommit()
    {
        var financeId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Approved, ExpenseCategory.Travel);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var notificationService = new FakeNotificationService();
        var service = new ExpenseService(expenseRepository, attachmentRepository, new FakeExpenseNumberGenerator(), new FakeCompanyClock(), new FakeExpensesUnitOfWork(), notificationService);

        var result = await service.ReimburseAsync(financeId, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal(NotificationEvent.Reimbursed, notification.Event);
    }

    [Fact]
    public async Task ReimburseAsync_Success_PopulatesAuditFields()
    {
        var financeId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var attachment = CreateAttachment(employeeId);
        var expense = CreateExpense(employeeId, attachment.Id, ExpenseStatus.Approved, ExpenseCategory.Travel);
        var expenseRepository = new FakeExpenseRepository();
        expenseRepository.Expenses.Add(expense);
        var attachmentRepository = new FakeExpensesAttachmentRepository();
        var service = CreateService(expenseRepository, attachmentRepository);

        var result = await service.ReimburseAsync(financeId, expense.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(financeId, expense.ReimbursedByEmployeeId);
        Assert.NotNull(expense.ReimbursedAt);
        Assert.Equal(expense.ReimbursedAt, result.Expense!.ReimbursedAt);
    }

    private static UpdateExpenseRequest UpdateRequest(
        Guid attachmentId, decimal amount = 100m, DateOnly? expenseDate = null, string description = "Updated expense") => new()
        {
            ExpenseDate = expenseDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Category = "Travel",
            Amount = amount,
            Currency = "INR",
            Description = description,
            ReceiptAttachmentId = attachmentId,
        };

    private static ExpenseService CreateService(FakeExpenseRepository expenseRepository, FakeExpensesAttachmentRepository attachmentRepository) =>
        new(expenseRepository, attachmentRepository, new FakeExpenseNumberGenerator(), new FakeCompanyClock(), new FakeExpensesUnitOfWork(), new FakeNotificationService());

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

    private static Expense CreateExpense(
        Guid employeeId,
        Guid attachmentId,
        ExpenseStatus status,
        ExpenseCategory category = ExpenseCategory.Travel,
        Guid? managerId = null) => new()
        {
            Id = Guid.NewGuid(),
            ExpenseNumber = "EXP-TEST-0001",
            EmployeeId = employeeId,
            AttachmentId = attachmentId,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Category = category,
            Amount = 100m,
            Currency = "INR",
            Description = "Existing expense",
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Employee = new Employee
            {
                EmployeeId = employeeId,
                EmployeeNumber = "EMP-TEST",
                FirstName = "Test",
                LastName = "Employee",
                Email = "test-employee@example.com",
                Role = EmployeeRole.Employee,
                ManagerId = managerId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
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

internal sealed class FakeNotificationService : INotificationService
{
    public List<(NotificationEvent Event, Expense Expense)> Notifications { get; } = [];

    public Task NotifyAsync(NotificationEvent notificationEvent, Expense expense, CancellationToken cancellationToken)
    {
        Notifications.Add((notificationEvent, expense));
        return Task.CompletedTask;
    }
}
