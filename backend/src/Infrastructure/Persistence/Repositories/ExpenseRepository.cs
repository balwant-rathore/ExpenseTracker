using Domain.Entities;
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

    private static bool IsUniqueViolation(DbUpdateException exception, string indexName)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Number is 2601 or 2627
            && sqlException.Message.Contains(indexName, StringComparison.Ordinal);
    }
}
