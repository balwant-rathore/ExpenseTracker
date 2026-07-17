using Domain.Entities;

namespace Domain.Repositories;

public interface IExpenseRepository : IRepository<Expense>
{
    IQueryable<Expense> Query();
}
