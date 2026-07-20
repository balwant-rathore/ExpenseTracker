namespace Api.RateLimiting;

public static class AuthRateLimitPolicyNames
{
    public const string Register = "AuthRegister";
    public const string Login = "AuthLogin";
    public const string ForgotPassword = "AuthForgotPassword";
    public const string ResetPassword = "AuthResetPassword";

    public static readonly IReadOnlyList<string> All = [Register, Login, ForgotPassword, ResetPassword];
}
