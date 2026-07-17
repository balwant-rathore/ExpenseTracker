using Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly ApplicationDbContext DbContext;

    public Repository(ApplicationDbContext dbContext)
    {
        DbContext = dbContext;
    }

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await DbContext.Set<TEntity>().FindAsync([id], cancellationToken);
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        await DbContext.Set<TEntity>().AddAsync(entity, cancellationToken);
    }
}
