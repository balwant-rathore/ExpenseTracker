using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Domain.Repositories;
using Shared.ErrorHandling;

namespace Api.Authentication;

public class EmployeeRoleResolutionMiddleware : IMiddleware
{
    private readonly IUserRepository _userRepository;

    public EmployeeRoleResolutionMiddleware(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub);
            if (subClaim is null || !Guid.TryParse(subClaim.Value, out var userId))
            {
                await WriteAuthenticationFailedAsync(context);
                return;
            }

            var user = await _userRepository.GetByIdWithEmployeeAsync(userId, context.RequestAborted);
            if (user?.Employee is null || !user.Employee.IsActive)
            {
                await WriteAuthenticationFailedAsync(context);
                return;
            }

            ((ClaimsIdentity)context.User.Identity).AddClaim(new Claim(ClaimTypes.Role, user.Employee.Role.ToString()));
        }

        await next(context);
    }

    private static Task WriteAuthenticationFailedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;

        var response = new ErrorResponse(new ErrorDetail(
            "AUTHENTICATION_FAILED",
            "Authentication failed.",
            [],
            context.TraceIdentifier));

        return context.Response.WriteAsJsonAsync(response);
    }
}
