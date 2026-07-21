using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    public const string EmployeeIdClaimType = "employee_id";

    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var subClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub);
        return subClaim is not null && Guid.TryParse(subClaim.Value, out var userId)
            ? userId
            : throw new InvalidOperationException("No valid sub claim present on the authenticated principal.");
    }

    public static Guid GetEmployeeId(this ClaimsPrincipal principal)
    {
        var employeeIdClaim = principal.FindFirst(EmployeeIdClaimType);
        return employeeIdClaim is not null && Guid.TryParse(employeeIdClaim.Value, out var employeeId)
            ? employeeId
            : throw new InvalidOperationException("No valid employee_id claim present on the authenticated principal.");
    }
}
