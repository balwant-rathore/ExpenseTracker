using Application.Auth;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.Extensions.Options;

namespace UnitTests.Application.Auth;

public class PasswordResetOtpServiceTests
{
    private static PasswordResetOtpService CreateService(FakePasswordResetOtpRepository repository, int otpExpiryMinutes = 10) =>
        new(repository, new FakeUnitOfWork(), Options.Create(new PasswordResetOtpOptions { OtpExpiryMinutes = otpExpiryMinutes }));

    [Fact]
    public async Task RequestResetAsync_PersistsOtpAsHash_NeverPlaintext()
    {
        var repository = new FakePasswordResetOtpRepository();
        var service = CreateService(repository);
        var userId = Guid.NewGuid();

        var originalOut = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);
        string plaintextOtp;
        try
        {
            await service.RequestResetAsync(userId, "user@test.local", CancellationToken.None);
            plaintextOtp = System.Text.RegularExpressions.Regex.Match(captured.ToString(), @"\d{6}").Value;
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var stored = Assert.Single(repository.Otps);
        Assert.Equal(64, stored.OtpHash.Length);
        Assert.False(string.IsNullOrEmpty(plaintextOtp));
        Assert.NotEqual(plaintextOtp, stored.OtpHash);
        Assert.DoesNotContain(plaintextOtp, stored.OtpHash);
    }

    [Fact]
    public async Task RequestResetAsync_ExpiresAfterConfiguredMinutes()
    {
        var repository = new FakePasswordResetOtpRepository();
        var service = CreateService(repository, otpExpiryMinutes: 10);
        var userId = Guid.NewGuid();

        var originalOut = Console.Out;
        Console.SetOut(TextWriter.Null);
        DateTime before, after;
        try
        {
            before = DateTime.UtcNow;
            await service.RequestResetAsync(userId, "user@test.local", CancellationToken.None);
            after = DateTime.UtcNow;
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var stored = Assert.Single(repository.Otps);
        Assert.InRange(stored.ExpiresAt, before.AddMinutes(10).AddSeconds(-5), after.AddMinutes(10).AddSeconds(5));
    }

    [Fact]
    public async Task RequestResetAsync_WritesPlaintextOtpAndEmailToConsole()
    {
        var repository = new FakePasswordResetOtpRepository();
        var service = CreateService(repository);
        var userId = Guid.NewGuid();

        var originalOut = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);
        string consoleOutput;
        try
        {
            await service.RequestResetAsync(userId, "target@test.local", CancellationToken.None);
        }
        finally
        {
            Console.SetOut(originalOut);
            consoleOutput = captured.ToString();
        }

        Assert.Contains("target@test.local", consoleOutput);
        Assert.Matches(@"\b\d{6}\b", consoleOutput);
    }

    [Fact]
    public async Task RequestResetAsync_SecondRequest_InvalidatesFirstOtp()
    {
        var repository = new FakePasswordResetOtpRepository();
        var service = CreateService(repository);
        var userId = Guid.NewGuid();

        var originalOut = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            await service.RequestResetAsync(userId, "user@test.local", CancellationToken.None);
            var first = repository.Otps[0];

            await service.RequestResetAsync(userId, "user@test.local", CancellationToken.None);

            Assert.True(first.ExpiresAt <= DateTime.UtcNow);
            Assert.Equal(2, repository.Otps.Count);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task VerifyAndConsumeAsync_NeverRequested_ReturnsInvalid()
    {
        var repository = new FakePasswordResetOtpRepository();
        var service = CreateService(repository);

        var result = await service.VerifyAndConsumeAsync(Guid.NewGuid(), "123456", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(OtpVerificationFailureReason.Invalid, result.FailureReason);
    }

    [Fact]
    public async Task VerifyAndConsumeAsync_ExpiredOtp_ReturnsExpired()
    {
        var repository = new FakePasswordResetOtpRepository();
        var userId = Guid.NewGuid();
        var expired = new PasswordResetOtp
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OtpHash = Sha256("123456"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-11),
        };
        await repository.AddAsync(expired, CancellationToken.None);
        var service = CreateService(repository);

        var result = await service.VerifyAndConsumeAsync(userId, "123456", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(OtpVerificationFailureReason.Expired, result.FailureReason);
    }

    [Fact]
    public async Task VerifyAndConsumeAsync_WrongOtp_ReturnsInvalid()
    {
        var repository = new FakePasswordResetOtpRepository();
        var userId = Guid.NewGuid();
        var active = new PasswordResetOtp
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OtpHash = Sha256("123456"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow,
        };
        await repository.AddAsync(active, CancellationToken.None);
        var service = CreateService(repository);

        var result = await service.VerifyAndConsumeAsync(userId, "999999", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(OtpVerificationFailureReason.Invalid, result.FailureReason);
        Assert.Null(active.UsedAt);
    }

    [Fact]
    public async Task VerifyAndConsumeAsync_AlreadyUsedOtp_ReturnsInvalid()
    {
        var repository = new FakePasswordResetOtpRepository();
        var userId = Guid.NewGuid();
        var used = new PasswordResetOtp
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OtpHash = Sha256("123456"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            UsedAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
        };
        await repository.AddAsync(used, CancellationToken.None);
        var service = CreateService(repository);

        var result = await service.VerifyAndConsumeAsync(userId, "123456", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(OtpVerificationFailureReason.Invalid, result.FailureReason);
    }

    [Fact]
    public async Task VerifyAndConsumeAsync_ValidOtp_SucceedsAndMarksUsed()
    {
        var repository = new FakePasswordResetOtpRepository();
        var userId = Guid.NewGuid();
        var active = new PasswordResetOtp
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OtpHash = Sha256("123456"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow,
        };
        await repository.AddAsync(active, CancellationToken.None);
        var service = CreateService(repository);

        var result = await service.VerifyAndConsumeAsync(userId, "123456", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(active.UsedAt);
    }

    private static string Sha256(string plaintext) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plaintext)));
}

internal sealed class FakePasswordResetOtpRepository : IPasswordResetOtpRepository
{
    private readonly List<PasswordResetOtp> _otps = [];

    public IReadOnlyList<PasswordResetOtp> Otps => _otps;

    public Task<PasswordResetOtp?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_otps.FirstOrDefault(o => o.Id == id));

    public Task AddAsync(PasswordResetOtp entity, CancellationToken cancellationToken)
    {
        _otps.Add(entity);
        return Task.CompletedTask;
    }

    public Task<PasswordResetOtp?> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var active = _otps
            .Where(o => o.UserId == userId && o.UsedAt == null && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        return Task.FromResult(active);
    }

    public Task<PasswordResetOtp?> GetMostRecentForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var mostRecent = _otps
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        return Task.FromResult(mostRecent);
    }
}
