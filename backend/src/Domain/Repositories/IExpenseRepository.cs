using Domain.Entities;

namespace Domain.Repositories;

public interface IExpenseRepository : IRepository<Expense>
{
    IQueryable<Expense> Query();
    Task<int> CountByExpenseNumberPrefixAsync(string prefix, CancellationToken cancellationToken);
    Task<bool> ExistsByAttachmentIdAsync(Guid attachmentId, CancellationToken cancellationToken);
    Task<ExpenseInsertOutcome> TryAddAsync(Expense expense, CancellationToken cancellationToken);
    Task<bool> TryUpdateAsync(Expense expense, CancellationToken cancellationToken);
}
