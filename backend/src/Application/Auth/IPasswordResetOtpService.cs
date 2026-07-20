namespace Application.Auth;

public interface IPasswordResetOtpService
{
    Task RequestResetAsync(Guid userId, string email, CancellationToken cancellationToken);
    Task<OtpVerificationResult> VerifyAndConsumeAsync(Guid userId, string plaintextOtp, CancellationToken cancellationToken);
}
