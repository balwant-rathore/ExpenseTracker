namespace Application.Auth;

public record ResetPasswordRequest(string Email, string Otp, string NewPassword);
