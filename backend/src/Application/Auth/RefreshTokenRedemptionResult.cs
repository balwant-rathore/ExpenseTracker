namespace Application.Auth;

public enum RefreshTokenRedemptionFailureReason
{
    NotFound,
    Expired,
    ReuseDetected
}

public record RefreshTokenRedemptionResult(
    bool Succeeded,
    Guid? UserId,
    string? NewRawToken,
    RefreshTokenRedemptionFailureReason? FailureReason)
{
    public static RefreshTokenRedemptionResult Success(Guid userId, string newRawToken) =>
        new(true, userId, newRawToken, null);

    public static RefreshTokenRedemptionResult Failure(RefreshTokenRedemptionFailureReason reason) =>
        new(false, null, null, reason);
}
