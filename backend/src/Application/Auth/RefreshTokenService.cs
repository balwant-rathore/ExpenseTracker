using System.Security.Cryptography;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Application.Auth;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RefreshTokenOptions _options;

    public RefreshTokenService(
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IOptions<RefreshTokenOptions> options)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rawToken = GenerateRawToken();
        var now = DateTime.UtcNow;

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(rawToken),
            ExpiresAt = now.AddDays(_options.LifetimeDays),
            CreatedAt = now,
        };

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return rawToken;
    }

    public async Task<RefreshTokenRedemptionResult> RedeemAsync(string rawToken, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(rawToken);
        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (existingToken is null)
        {
            return RefreshTokenRedemptionResult.Failure(RefreshTokenRedemptionFailureReason.NotFound);
        }

        if (existingToken.RevokedAt is not null)
        {
            await RevokeAllActiveAsync(existingToken.UserId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return RefreshTokenRedemptionResult.Failure(RefreshTokenRedemptionFailureReason.ReuseDetected);
        }

        var now = DateTime.UtcNow;
        if (existingToken.ExpiresAt <= now)
        {
            return RefreshTokenRedemptionResult.Failure(RefreshTokenRedemptionFailureReason.Expired);
        }

        var newRawToken = GenerateRawToken();
        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existingToken.UserId,
            TokenHash = HashToken(newRawToken),
            ExpiresAt = now.AddDays(_options.LifetimeDays),
            CreatedAt = now,
        };

        existingToken.RevokedAt = now;
        await _refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return RefreshTokenRedemptionResult.Success(existingToken.UserId, newRawToken);
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        await RevokeAllActiveAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(Guid userId, string rawToken, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(rawToken);
        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (existingToken is null || existingToken.UserId != userId || existingToken.RevokedAt is not null)
        {
            return false;
        }

        existingToken.RevokedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task RevokeAllActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeTokens = await _refreshTokenRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }
    }

    private static string GenerateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
}
