using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class PasswordResetOtpRepository : Repository<PasswordResetOtp>, IPasswordResetOtpRepository
{
    public PasswordResetOtpRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<PasswordResetOtp?> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await DbContext.PasswordResetOtps
            .Where(o => o.UserId == userId && o.UsedAt == null && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
