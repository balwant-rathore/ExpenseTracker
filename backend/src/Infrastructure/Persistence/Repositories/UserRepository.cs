using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return await DbContext.Users
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public async Task<User?> GetByIdWithEmployeeAsync(Guid id, CancellationToken cancellationToken)
    {
        return await DbContext.Users
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }
}
