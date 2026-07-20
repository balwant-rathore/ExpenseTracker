namespace Application.Auth;

public enum AuthFailureReason
{
    None,
    EmployeeNumberInvalid,
    EmailAlreadyRegistered,
    PasswordPolicyViolation,
    InvalidCredentials,
    RefreshTokenInvalid,
    OtpExpired,
    OtpInvalid,
    NewPasswordPolicyViolation
}

public record AuthResult(
    bool Succeeded,
    UserDto? User,
    string? AccessToken,
    string? RefreshToken,
    AuthFailureReason FailureReason)
{
    public static AuthResult Success(UserDto user, string accessToken, string refreshToken) =>
        new(true, user, accessToken, refreshToken, AuthFailureReason.None);

    public static AuthResult Failure(AuthFailureReason reason) =>
        new(false, null, null, null, reason);
}
