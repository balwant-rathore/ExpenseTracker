using System.Security.Claims;
using Api.Authorization;
using Api.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace IntegrationTests;

public class TestEndpointsStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            next(app);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/__test/whoami", (HttpContext context) =>
                        Results.Ok(new { role = context.User.FindFirst(ClaimTypes.Role)?.Value }))
                    .RequireAuthorization();

                endpoints.MapGet("/__test/manager-only", () => Results.Ok())
                    .RequireAuthorization(AuthorizationPolicyNames.Manager);

                endpoints.MapPost("/__test/rate-limited", () => Results.Ok())
                    .RequireRateLimiting(AuthRateLimitPolicyNames.Login);
            });
        };
    }
}
