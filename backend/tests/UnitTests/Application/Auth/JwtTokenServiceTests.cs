using System.IdentityModel.Tokens.Jwt;
using Application.Auth;
using Microsoft.Extensions.Options;

namespace UnitTests.Application.Auth;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(int accessTokenLifetimeMinutes = 15) =>
        new(Options.Create(new JwtOptions
        {
            SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!",
            Issuer = "ExpenseTracker",
            Audience = "ExpenseTracker",
            AccessTokenLifetimeMinutes = accessTokenLifetimeMinutes,
        }));

    [Fact]
    public void GenerateAccessToken_ContainsOnlySubClaim()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();

        var token = service.GenerateAccessToken(userId);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var subClaim = Assert.Single(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.Equal(userId.ToString(), subClaim.Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type is "role" or JwtRegisteredClaimNames.Email);
    }

    [Fact]
    public void GenerateAccessToken_ExpiresAfter15Minutes()
    {
        var service = CreateService();

        var before = DateTime.UtcNow;
        var token = service.GenerateAccessToken(Guid.NewGuid());
        var after = DateTime.UtcNow;

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.InRange(jwt.ValidTo, before.AddMinutes(15).AddSeconds(-5), after.AddMinutes(15).AddSeconds(5));
    }

    [Fact]
    public void TryValidateAccessToken_ExpiredToken_IsRejected()
    {
        var service = CreateService(accessTokenLifetimeMinutes: -5);
        var token = service.GenerateAccessToken(Guid.NewGuid());

        var isValid = service.TryValidateAccessToken(token, out _);

        Assert.False(isValid);
    }

    [Fact]
    public void TryValidateAccessToken_TamperedToken_IsRejected()
    {
        var service = CreateService();
        var token = service.GenerateAccessToken(Guid.NewGuid());
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        var isValid = service.TryValidateAccessToken(tampered, out _);

        Assert.False(isValid);
    }

    [Fact]
    public void TryValidateAccessToken_ValidToken_ReturnsOriginalUserId()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();
        var token = service.GenerateAccessToken(userId);

        var isValid = service.TryValidateAccessToken(token, out var resolvedUserId);

        Assert.True(isValid);
        Assert.Equal(userId, resolvedUserId);
    }
}
