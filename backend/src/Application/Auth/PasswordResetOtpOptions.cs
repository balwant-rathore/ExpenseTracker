namespace Application.Auth;

public class PasswordResetOtpOptions
{
    public const string SectionName = "PasswordReset";

    public int OtpExpiryMinutes { get; set; } = 10;
}
