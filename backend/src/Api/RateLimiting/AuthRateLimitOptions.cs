namespace Api.RateLimiting;

public class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:AuthEndpoints";

    public int PermitLimit { get; set; } = 5;
    public int WindowSeconds { get; set; } = 300;
}
