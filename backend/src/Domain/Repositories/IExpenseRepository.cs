using System.Linq.Expressions;
using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories;

public interface IExpenseRepository : IRepository<Expense>
{
    IQueryable<Expense> Query();
    Task<int> CountByExpenseNumberPrefixAsync(string prefix, CancellationToken cancellationToken);
    Task<bool> ExistsByAttachmentIdAsync(Guid attachmentId, CancellationToken cancellationToken);
    Task<ExpenseInsertOutcome> TryAddAsync(Expense expense, CancellationToken cancellationToken);
    Task<bool> TryUpdateAsync(Expense expense, CancellationToken cancellationToken);
    Task<Expense?> GetByIdWithEmployeeAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Expense> Items, int TotalRecords)> GetPagedAsync(
        Expression<Func<Expense, bool>> visibilityPredicate,
        ExpenseSortField sortBy,
        bool descending,
        int page,
        int pageSize,
        ExpenseStatus? statusFilter,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<Expense> Items, int TotalRecords)> SearchPagedAsync(
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
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Expense>> GetReimbursedForReportAsync(
        DateTime rangeStartUtcInclusive,
        DateTime rangeEndUtcExclusive,
        CancellationToken cancellationToken);
}
