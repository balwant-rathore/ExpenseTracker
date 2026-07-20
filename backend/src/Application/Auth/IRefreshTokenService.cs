namespace Application.Auth;

public interface IRefreshTokenService
{
    Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken);
    Task<RefreshTokenRedemptionResult> RedeemAsync(string rawToken, CancellationToken cancellationToken);
    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> RevokeAsync(Guid userId, string rawToken, CancellationToken cancellationToken);
}
