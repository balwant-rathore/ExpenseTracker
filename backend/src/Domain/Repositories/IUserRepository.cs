using Domain.Entities;

namespace Domain.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<User?> GetByIdWithEmployeeAsync(Guid id, CancellationToken cancellationToken);
}
