using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;

namespace Application.Expenses;

public class ExpenseService : IExpenseService
{
    private const int MaxExpenseNumberAttempts = 5;

    private readonly IExpenseRepository _expenseRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IExpenseNumberGenerator _expenseNumberGenerator;
    private readonly ICompanyClock _companyClock;
    private readonly IUnitOfWork _unitOfWork;

    public ExpenseService(
        IExpenseRepository expenseRepository,
        IAttachmentRepository attachmentRepository,
        IExpenseNumberGenerator expenseNumberGenerator,
        ICompanyClock companyClock,
        IUnitOfWork unitOfWork)
    {
        _expenseRepository = expenseRepository;
        _attachmentRepository = attachmentRepository;
        _expenseNumberGenerator = expenseNumberGenerator;
        _companyClock = companyClock;
        _unitOfWork = unitOfWork;
    }

    public async Task<ExpenseResult> CreateAsync(Guid employeeId, CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.AmountNotPositive);
        }

        if (request.ExpenseDate > _companyClock.Today())
        {
            return ExpenseResult.Failure(ExpenseFailureReason.ExpenseDateInFuture);
        }

        var attachment = await _attachmentRepository.GetByIdAsync(request.ReceiptAttachmentId, cancellationToken);
        if (attachment is null)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.AttachmentNotFound);
        }

        if (attachment.UploadedByEmployeeId != employeeId)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.AttachmentNotOwned);
        }

        if (await _expenseRepository.ExistsByAttachmentIdAsync(attachment.Id, cancellationToken))
        {
            return ExpenseResult.Failure(ExpenseFailureReason.AttachmentAlreadyLinked);
        }

        // Category/Currency/Description/Action are non-null by this point: the controller
        // always runs CreateExpenseRequestValidator (which rejects null/invalid values for all
        // four) before calling CreateAsync.
        var status = Enum.Parse<ExpenseAction>(request.Action!) == ExpenseAction.Submit
            ? ExpenseStatus.Submitted
            : ExpenseStatus.Draft;

        for (var attempt = 1; attempt <= MaxExpenseNumberAttempts; attempt++)
        {
            var now = DateTime.UtcNow;
            var expense = new Expense
            {
                Id = Guid.NewGuid(),
                ExpenseNumber = await _expenseNumberGenerator.GenerateAsync(cancellationToken),
                EmployeeId = employeeId,
                AttachmentId = attachment.Id,
                ExpenseDate = request.ExpenseDate,
                Category = Enum.Parse<ExpenseCategory>(request.Category!),
                Amount = request.Amount,
                Currency = request.Currency!,
                Description = request.Description!,
                Status = status,
                SubmittedAt = status == ExpenseStatus.Submitted ? now : null,
                CreatedAt = now,
                UpdatedAt = now,
            };

            var outcome = ExpenseInsertOutcome.Success;
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                outcome = await _expenseRepository.TryAddAsync(expense, cancellationToken);
            }, cancellationToken);

            switch (outcome)
            {
                case ExpenseInsertOutcome.Success:
                    return ExpenseResult.Success(Map(expense));
                case ExpenseInsertOutcome.AttachmentAlreadyLinked:
                    return ExpenseResult.Failure(ExpenseFailureReason.AttachmentAlreadyLinked);
                case ExpenseInsertOutcome.ExpenseNumberConflict:
                default:
                    continue;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique expense number after multiple attempts.");
    }

    public async Task<ExpenseResult> SubmitAsync(Guid employeeId, Guid expenseId, CancellationToken cancellationToken)
    {
        var expense = await _expenseRepository.GetByIdAsync(expenseId, cancellationToken);
        if (expense is null)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.ExpenseNotFound);
        }

        if (expense.EmployeeId != employeeId)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.NotOwner);
        }

        if (expense.Status != ExpenseStatus.Draft)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.NotDraft);
        }

        if (expense.Amount <= 0)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.AmountNotPositive);
        }

        if (expense.ExpenseDate > _companyClock.Today())
        {
            return ExpenseResult.Failure(ExpenseFailureReason.ExpenseDateInFuture);
        }

        if (expense.Currency != CreateExpenseRequestValidator.RequiredCurrency)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.CurrencyInvalid);
        }

        if (expense.Description.Length > CreateExpenseRequestValidator.MaxDescriptionLength)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.DescriptionTooLong);
        }

        var attachment = await _attachmentRepository.GetByIdAsync(expense.AttachmentId, cancellationToken);
        if (attachment is null)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.AttachmentNotFound);
        }

        if (attachment.UploadedByEmployeeId != employeeId)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.AttachmentNotOwned);
        }

        var now = DateTime.UtcNow;
        expense.Status = ExpenseStatus.Submitted;
        expense.SubmittedAt = now;
        expense.UpdatedAt = now;

        await _unitOfWork.ExecuteInTransactionAsync(
            () => _unitOfWork.SaveChangesAsync(cancellationToken),
            cancellationToken);

        return ExpenseResult.Success(Map(expense));
    }

    public async Task<ExpenseResult> GetByIdAsync(Guid employeeId, EmployeeRole role, Guid expenseId, CancellationToken cancellationToken)
    {
        var expense = await _expenseRepository.GetByIdWithEmployeeAsync(expenseId, cancellationToken);
        if (expense is null)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.ExpenseNotFound);
        }

        var isVisible = ExpenseVisibility.BuildPredicate(role, employeeId).Compile().Invoke(expense);
        if (!isVisible)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.NotVisible);
        }

        return ExpenseResult.Success(Map(expense));
    }

    public async Task<PagedExpenseResponse> GetVisibleAsync(Guid employeeId, EmployeeRole role, ExpenseListRequest request, CancellationToken cancellationToken)
    {
        var predicate = ExpenseVisibility.BuildPredicate(role, employeeId);

        // SortBy/SortDirection are non-null, valid values by this point: the controller
        // always runs ExpenseListRequestValidator before calling GetVisibleAsync.
        var sortField = Enum.Parse<ExpenseSortField>(request.SortBy, ignoreCase: true);
        var descending = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        var (items, totalRecords) = await _expenseRepository.GetPagedAsync(
            predicate, sortField, descending, request.Page, request.PageSize, cancellationToken);

        return new PagedExpenseResponse(items.Select(Map).ToList(), request.Page, request.PageSize, totalRecords);
    }

    public async Task<ExpenseResult> UpdateAsync(Guid employeeId, Guid expenseId, UpdateExpenseRequest request, CancellationToken cancellationToken)
    {
        var expense = await _expenseRepository.GetByIdAsync(expenseId, cancellationToken);
        if (expense is null)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.ExpenseNotFound);
        }

        if (expense.EmployeeId != employeeId)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.NotOwner);
        }

        if (expense.Status is not (ExpenseStatus.Draft or ExpenseStatus.Submitted))
        {
            return ExpenseResult.Failure(ExpenseFailureReason.NotEditable);
        }

        if (request.Amount <= 0)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.AmountNotPositive);
        }

        if (request.ExpenseDate > _companyClock.Today())
        {
            return ExpenseResult.Failure(ExpenseFailureReason.ExpenseDateInFuture);
        }

        var attachment = await _attachmentRepository.GetByIdAsync(request.ReceiptAttachmentId, cancellationToken);
        if (attachment is null)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.AttachmentNotFound);
        }

        if (attachment.UploadedByEmployeeId != employeeId)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.AttachmentNotOwned);
        }

        // Category/Currency/Description are non-null by this point: the controller always
        // runs UpdateExpenseRequestValidator (which rejects null/invalid values for all
        // three) before calling UpdateAsync.
        expense.ExpenseDate = request.ExpenseDate;
        expense.Category = Enum.Parse<ExpenseCategory>(request.Category!);
        expense.Amount = request.Amount;
        expense.Currency = request.Currency!;
        expense.Description = request.Description!;
        expense.AttachmentId = attachment.Id;
        expense.UpdatedAt = DateTime.UtcNow;

        var updated = false;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            updated = await _expenseRepository.TryUpdateAsync(expense, cancellationToken);
        }, cancellationToken);

        if (!updated)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.AttachmentAlreadyLinked);
        }

        return ExpenseResult.Success(Map(expense));
    }

    public async Task<ExpenseResult> CancelAsync(Guid employeeId, Guid expenseId, CancellationToken cancellationToken)
    {
        var expense = await _expenseRepository.GetByIdAsync(expenseId, cancellationToken);
        if (expense is null)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.ExpenseNotFound);
        }

        if (expense.EmployeeId != employeeId)
        {
            return ExpenseResult.Failure(ExpenseFailureReason.NotOwner);
        }

        if (expense.Status is not (ExpenseStatus.Draft or ExpenseStatus.Submitted))
        {
            return ExpenseResult.Failure(ExpenseFailureReason.NotCancellable);
        }

        expense.Status = ExpenseStatus.Cancelled;
        expense.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(
            () => _unitOfWork.SaveChangesAsync(cancellationToken),
            cancellationToken);

        return ExpenseResult.Success(Map(expense));
    }

    private static ExpenseResponse Map(Expense expense)
    {
        var employeeName = expense.Employee is not null
            ? $"{expense.Employee.FirstName} {expense.Employee.LastName}"
            : null;

        return new ExpenseResponse(
            expense.Id,
            expense.ExpenseNumber,
            expense.ExpenseDate,
            expense.Category.ToString(),
            expense.Amount,
            expense.Currency,
            expense.Description,
            expense.Status.ToString(),
            expense.SubmittedAt,
            expense.CreatedAt,
            employeeName);
    }
}
