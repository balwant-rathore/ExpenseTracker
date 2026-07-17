using Domain.Entities;

namespace Domain.Repositories;

public interface IPasswordResetOtpRepository : IRepository<PasswordResetOtp>
{
    Task<PasswordResetOtp?> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken);
}
