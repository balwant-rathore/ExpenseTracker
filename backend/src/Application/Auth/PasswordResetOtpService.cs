using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Application.Auth;

public class PasswordResetOtpService : IPasswordResetOtpService
{
    private readonly IPasswordResetOtpRepository _passwordResetOtpRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PasswordResetOtpOptions _options;

    public PasswordResetOtpService(
        IPasswordResetOtpRepository passwordResetOtpRepository,
        IUnitOfWork unitOfWork,
        IOptions<PasswordResetOtpOptions> options)
    {
        _passwordResetOtpRepository = passwordResetOtpRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task RequestResetAsync(Guid userId, string email, CancellationToken cancellationToken)
    {
        var activeOtp = await _passwordResetOtpRepository.GetActiveForUserAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;

        if (activeOtp is not null)
        {
            activeOtp.ExpiresAt = now;
        }

        var plaintextOtp = GenerateOtp();
        var otp = new PasswordResetOtp
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OtpHash = HashOtp(plaintextOtp),
            ExpiresAt = now.AddMinutes(_options.OtpExpiryMinutes),
            CreatedAt = now,
        };

        await _passwordResetOtpRepository.AddAsync(otp, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        Console.WriteLine($"Password reset OTP for {email}: {plaintextOtp}");
    }

    public async Task<OtpVerificationResult> VerifyAndConsumeAsync(Guid userId, string plaintextOtp, CancellationToken cancellationToken)
    {
        var otp = await _passwordResetOtpRepository.GetMostRecentForUserAsync(userId, cancellationToken);
        if (otp is null || otp.UsedAt is not null)
        {
            return OtpVerificationResult.Failure(OtpVerificationFailureReason.Invalid);
        }

        if (otp.ExpiresAt <= DateTime.UtcNow)
        {
            return OtpVerificationResult.Failure(OtpVerificationFailureReason.Expired);
        }

        if (otp.OtpHash != HashOtp(plaintextOtp))
        {
            return OtpVerificationResult.Failure(OtpVerificationFailureReason.Invalid);
        }

        // Intentionally no SaveChangesAsync here: this mutation must commit atomically with the
        // password update and refresh-token revocation the caller performs afterward, inside one
        // transaction, so a partial failure can't consume the OTP without actually resetting the password.
        otp.UsedAt = DateTime.UtcNow;

        return OtpVerificationResult.Success();
    }

    private static string GenerateOtp() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string HashOtp(string plaintextOtp) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintextOtp)));
}
