using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Diagnostics;
using Shared.ErrorHandling;

namespace Api.ErrorHandling;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        _logger.LogError(
            exception,
            "Unhandled exception. TraceId: {TraceId}, Method: {Method}, Path: {Path}, UserId: {UserId}",
            httpContext.TraceIdentifier,
            httpContext.Request.Method,
            httpContext.Request.Path,
            userId ?? "anonymous");

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var response = new ErrorResponse(new ErrorDetail(
            "INTERNAL_SERVER_ERROR",
            "An unexpected error occurred.",
            [],
            httpContext.TraceIdentifier));

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}
