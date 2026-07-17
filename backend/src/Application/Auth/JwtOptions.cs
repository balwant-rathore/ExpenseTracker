namespace Application.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = null!;
    public string Issuer { get; set; } = "ExpenseTracker";
    public string Audience { get; set; } = "ExpenseTracker";
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
}
