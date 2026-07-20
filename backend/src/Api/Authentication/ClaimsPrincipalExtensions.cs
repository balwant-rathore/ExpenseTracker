using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var subClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub);
        return subClaim is not null && Guid.TryParse(subClaim.Value, out var userId)
            ? userId
            : throw new InvalidOperationException("No valid sub claim present on the authenticated principal.");
    }
}
