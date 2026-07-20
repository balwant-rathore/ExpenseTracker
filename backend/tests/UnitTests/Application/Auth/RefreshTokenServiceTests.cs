using Application.Auth;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.Extensions.Options;

namespace UnitTests.Application.Auth;

public class RefreshTokenServiceTests
{
    private static RefreshTokenService CreateService(FakeRefreshTokenRepository repository, int lifetimeDays = 7) =>
        new(repository, new FakeUnitOfWork(), Options.Create(new RefreshTokenOptions { LifetimeDays = lifetimeDays }));

    [Fact]
    public async Task IssueAsync_PersistsOnlyTokenHash_NeverRawValue()
    {
        var repository = new FakeRefreshTokenRepository();
        var service = CreateService(repository);
        var userId = Guid.NewGuid();

        var rawToken = await service.IssueAsync(userId, CancellationToken.None);

        var stored = Assert.Single(repository.Tokens);
        Assert.NotEqual(rawToken, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
    }

    [Fact]
    public async Task IssueAsync_ExpiresInSevenDays()
    {
        var repository = new FakeRefreshTokenRepository();
        var service = CreateService(repository);

        var before = DateTime.UtcNow;
        await service.IssueAsync(Guid.NewGuid(), CancellationToken.None);
        var after = DateTime.UtcNow;

        var stored = Assert.Single(repository.Tokens);
        Assert.InRange(stored.ExpiresAt, before.AddDays(7).AddSeconds(-5), after.AddDays(7).AddSeconds(5));
    }

    [Fact]
    public async Task RedeemAsync_ValidToken_RotatesAndRevokesOld()
    {
        var repository = new FakeRefreshTokenRepository();
        var service = CreateService(repository);
        var userId = Guid.NewGuid();
        var rawToken = await service.IssueAsync(userId, CancellationToken.None);
        var original = repository.Tokens[0];

        var result = await service.RedeemAsync(rawToken, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(userId, result.UserId);
        Assert.NotEqual(rawToken, result.NewRawToken);
        Assert.NotNull(original.RevokedAt);
        Assert.Equal(2, repository.Tokens.Count);
    }

    [Fact]
    public async Task RedeemAsync_AlreadyRevokedToken_Fails()
    {
        var repository = new FakeRefreshTokenRepository();
        var service = CreateService(repository);
        var rawToken = await service.IssueAsync(Guid.NewGuid(), CancellationToken.None);
        await service.RedeemAsync(rawToken, CancellationToken.None);

        var result = await service.RedeemAsync(rawToken, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RedeemAsync_ReuseOfRevokedToken_RevokesAllActiveTokensForUser()
    {
        var repository = new FakeRefreshTokenRepository();
        var service = CreateService(repository);
        var userId = Guid.NewGuid();

        var rawTokenA = await service.IssueAsync(userId, CancellationToken.None);
        var rawTokenB = await service.IssueAsync(userId, CancellationToken.None);
        await service.RedeemAsync(rawTokenA, CancellationToken.None); // rotates A -> C, A now revoked

        var result = await service.RedeemAsync(rawTokenA, CancellationToken.None); // reuse of revoked A

        Assert.False(result.Succeeded);
        Assert.All(repository.Tokens.Where(t => t.UserId == userId), t => Assert.NotNull(t.RevokedAt));
        _ = rawTokenB;
    }

    [Fact]
    public async Task RevokeAllAsync_RevokesActiveTokens_LeavesRevokedAndExpiredUnchanged()
    {
        var repository = new FakeRefreshTokenRepository();
        var service = CreateService(repository);
        var userId = Guid.NewGuid();

        await service.IssueAsync(userId, CancellationToken.None);
        await service.IssueAsync(userId, CancellationToken.None);

        var alreadyRevokedAt = DateTime.UtcNow.AddDays(-1);
        var alreadyRevoked = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "already-revoked-hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = alreadyRevokedAt,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
        };
        var expired = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "expired-hash",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-8),
        };
        await repository.AddAsync(alreadyRevoked, CancellationToken.None);
        await repository.AddAsync(expired, CancellationToken.None);

        await service.RevokeAllAsync(userId, CancellationToken.None);

        Assert.All(repository.Tokens.Where(t => t.Id != alreadyRevoked.Id && t.Id != expired.Id), t => Assert.NotNull(t.RevokedAt));
        Assert.Equal(alreadyRevokedAt, alreadyRevoked.RevokedAt);
        Assert.Null(expired.RevokedAt);
    }

    [Fact]
    public async Task RevokingASingleTokenSetsOnlyThatTokensRevokedAt()
    {
        var repository = new FakeRefreshTokenRepository();
        var service = CreateService(repository);
        var userId = Guid.NewGuid();

        var rawTokenA = await service.IssueAsync(userId, CancellationToken.None);
        await service.IssueAsync(userId, CancellationToken.None);
        var tokenA = repository.Tokens[0];
        var tokenB = repository.Tokens[1];

        var result = await service.RevokeAsync(userId, rawTokenA, CancellationToken.None);

        Assert.True(result);
        Assert.NotNull(tokenA.RevokedAt);
        Assert.Null(tokenB.RevokedAt);
    }

    [Fact]
    public async Task RevokingATokenThatDoesNotBelongToTheCallerFails()
    {
        var repository = new FakeRefreshTokenRepository();
        var service = CreateService(repository);
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var rawToken = await service.IssueAsync(ownerUserId, CancellationToken.None);
        var token = repository.Tokens[0];

        var result = await service.RevokeAsync(otherUserId, rawToken, CancellationToken.None);

        Assert.False(result);
        Assert.Null(token.RevokedAt);
    }
}

internal sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly List<RefreshToken> _tokens = [];

    public IReadOnlyList<RefreshToken> Tokens => _tokens;

    public Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_tokens.FirstOrDefault(t => t.Id == id));

    public Task AddAsync(RefreshToken entity, CancellationToken cancellationToken)
    {
        _tokens.Add(entity);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        IReadOnlyList<RefreshToken> active = _tokens
            .Where(t => t.UserId == userId && t.RevokedAt is null && t.ExpiresAt > DateTime.UtcNow)
            .ToList();

        return Task.FromResult(active);
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);

    public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken) => operation();
}
