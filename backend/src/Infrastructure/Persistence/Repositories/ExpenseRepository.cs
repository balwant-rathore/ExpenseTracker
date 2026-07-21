using System.Linq.Expressions;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class ExpenseRepository : Repository<Expense>, IExpenseRepository
{
    public ExpenseRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public IQueryable<Expense> Query()
    {
        return DbContext.Expenses.AsQueryable();
    }

    public Task<int> CountByExpenseNumberPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        return DbContext.Expenses.CountAsync(e => e.ExpenseNumber.StartsWith(prefix), cancellationToken);
    }

    public Task<bool> ExistsByAttachmentIdAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        return DbContext.Expenses.AnyAsync(e => e.AttachmentId == attachmentId, cancellationToken);
    }

    public async Task<ExpenseInsertOutcome> TryAddAsync(Expense expense, CancellationToken cancellationToken)
    {
        await DbContext.Expenses.AddAsync(expense, cancellationToken);

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
            return ExpenseInsertOutcome.Success;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "IX_Expenses_ExpenseNumber"))
        {
            DbContext.Entry(expense).State = EntityState.Detached;
            return ExpenseInsertOutcome.ExpenseNumberConflict;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "IX_Expenses_AttachmentId"))
        {
            DbContext.Entry(expense).State = EntityState.Detached;
            return ExpenseInsertOutcome.AttachmentAlreadyLinked;
        }
    }

    public async Task<bool> TryUpdateAsync(Expense expense, CancellationToken cancellationToken)
    {
        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "IX_Expenses_AttachmentId"))
        {
            DbContext.Entry(expense).State = EntityState.Unchanged;
            return false;
        }
    }

    public Task<Expense?> GetByIdWithEmployeeAsync(Guid id, CancellationToken cancellationToken)
    {
        return DbContext.Expenses
            .Include(e => e.Employee)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Expense> Items, int TotalRecords)> GetPagedAsync(
        Expression<Func<Expense, bool>> visibilityPredicate,
        ExpenseSortField sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var filtered = DbContext.Expenses.Where(visibilityPredicate);

        var totalRecords = await filtered.CountAsync(cancellationToken);

        var sorted = sortBy switch
        {
            ExpenseSortField.ExpenseNumber => OrderBy(filtered, e => e.ExpenseNumber, descending),
            ExpenseSortField.CreatedAt => OrderBy(filtered, e => e.CreatedAt, descending),
            ExpenseSortField.Amount => OrderBy(filtered, e => e.Amount, descending),
            ExpenseSortField.SubmittedAt => OrderBy(filtered, e => e.SubmittedAt, descending),
            ExpenseSortField.ApprovedAt => OrderBy(filtered, e => e.ApprovedAt, descending),
            ExpenseSortField.ReimbursedAt => OrderBy(filtered, e => e.ReimbursedAt, descending),
            ExpenseSortField.RejectedAt => OrderBy(filtered, e => e.RejectedAt, descending),
            _ => OrderBy(filtered, e => e.ExpenseDate, descending),
        };

        var items = await sorted
            .Include(e => e.Employee)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalRecords);
    }

    private static IOrderedQueryable<Expense> OrderBy<TKey>(
        IQueryable<Expense> query,
        Expression<Func<Expense, TKey>> keySelector,
        bool descending)
    {
        return descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);
    }

    private static bool IsUniqueViolation(DbUpdateException exception, string indexName)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Number is 2601 or 2627
            && sqlException.Message.Contains(indexName, StringComparison.Ordinal);
    }
}
