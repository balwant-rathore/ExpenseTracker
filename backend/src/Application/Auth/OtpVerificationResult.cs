namespace Application.Auth;

public enum OtpVerificationFailureReason
{
    None,
    Expired,
    Invalid
}

public record OtpVerificationResult(bool Succeeded, OtpVerificationFailureReason FailureReason)
{
    public static OtpVerificationResult Success() => new(true, OtpVerificationFailureReason.None);

    public static OtpVerificationResult Failure(OtpVerificationFailureReason reason) => new(false, reason);
}
