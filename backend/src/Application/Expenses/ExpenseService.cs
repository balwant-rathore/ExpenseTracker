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

    public async Task<ExpenseCreationResult> CreateAsync(Guid employeeId, CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return ExpenseCreationResult.Failure(ExpenseFailureReason.AmountNotPositive);
        }

        if (request.ExpenseDate > _companyClock.Today())
        {
            return ExpenseCreationResult.Failure(ExpenseFailureReason.ExpenseDateInFuture);
        }

        var attachment = await _attachmentRepository.GetByIdAsync(request.ReceiptAttachmentId, cancellationToken);
        if (attachment is null)
        {
            return ExpenseCreationResult.Failure(ExpenseFailureReason.AttachmentNotFound);
        }

        if (attachment.UploadedByEmployeeId != employeeId)
        {
            return ExpenseCreationResult.Failure(ExpenseFailureReason.AttachmentNotOwned);
        }

        if (await _expenseRepository.ExistsByAttachmentIdAsync(attachment.Id, cancellationToken))
        {
            return ExpenseCreationResult.Failure(ExpenseFailureReason.AttachmentAlreadyLinked);
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
                    return ExpenseCreationResult.Success(Map(expense));
                case ExpenseInsertOutcome.AttachmentAlreadyLinked:
                    return ExpenseCreationResult.Failure(ExpenseFailureReason.AttachmentAlreadyLinked);
                case ExpenseInsertOutcome.ExpenseNumberConflict:
                default:
                    continue;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique expense number after multiple attempts.");
    }

    public async Task<ExpenseSubmitResult> SubmitAsync(Guid employeeId, Guid expenseId, CancellationToken cancellationToken)
    {
        var expense = await _expenseRepository.GetByIdAsync(expenseId, cancellationToken);
        if (expense is null)
        {
            return ExpenseSubmitResult.Failure(ExpenseFailureReason.ExpenseNotFound);
        }

        if (expense.EmployeeId != employeeId)
        {
            return ExpenseSubmitResult.Failure(ExpenseFailureReason.NotOwner);
        }

        if (expense.Status != ExpenseStatus.Draft)
        {
            return ExpenseSubmitResult.Failure(ExpenseFailureReason.NotDraft);
        }

        if (expense.Amount <= 0)
        {
            return ExpenseSubmitResult.Failure(ExpenseFailureReason.AmountNotPositive);
        }

        if (expense.ExpenseDate > _companyClock.Today())
        {
            return ExpenseSubmitResult.Failure(ExpenseFailureReason.ExpenseDateInFuture);
        }

        if (expense.Currency != CreateExpenseRequestValidator.RequiredCurrency)
        {
            return ExpenseSubmitResult.Failure(ExpenseFailureReason.CurrencyInvalid);
        }

        if (expense.Description.Length > CreateExpenseRequestValidator.MaxDescriptionLength)
        {
            return ExpenseSubmitResult.Failure(ExpenseFailureReason.DescriptionTooLong);
        }

        var attachment = await _attachmentRepository.GetByIdAsync(expense.AttachmentId, cancellationToken);
        if (attachment is null)
        {
            return ExpenseSubmitResult.Failure(ExpenseFailureReason.AttachmentNotFound);
        }

        if (attachment.UploadedByEmployeeId != employeeId)
        {
            return ExpenseSubmitResult.Failure(ExpenseFailureReason.AttachmentNotOwned);
        }

        var now = DateTime.UtcNow;
        expense.Status = ExpenseStatus.Submitted;
        expense.SubmittedAt = now;
        expense.UpdatedAt = now;

        await _unitOfWork.ExecuteInTransactionAsync(
            () => _unitOfWork.SaveChangesAsync(cancellationToken),
            cancellationToken);

        return ExpenseSubmitResult.Success(Map(expense));
    }

    private static ExpenseResponse Map(Expense expense)
    {
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
            expense.CreatedAt);
    }
}
