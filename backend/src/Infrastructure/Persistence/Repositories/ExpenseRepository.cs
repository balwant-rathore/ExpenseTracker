using Domain.Entities;
using Domain.Repositories;

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
}
