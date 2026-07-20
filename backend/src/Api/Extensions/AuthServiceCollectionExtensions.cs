using System.Text;
using System.Threading.RateLimiting;
using Api.Authentication;
using Api.Authorization;
using Api.RateLimiting;
using Application.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Shared.ErrorHandling;

namespace Api.Extensions;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddAuthFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<RefreshTokenOptions>(configuration.GetSection(RefreshTokenOptions.SectionName));
        services.Configure<AuthRateLimitOptions>(configuration.GetSection(AuthRateLimitOptions.SectionName));

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IPasswordPolicyValidator, PasswordPolicyValidator>();

        services.AddScoped<EmployeeRoleResolutionMiddleware>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, EnvelopeAuthorizationMiddlewareResultHandler>();

        AddJwtBearerAuthentication(services, configuration);
        AddRolePolicies(services);
        AddAuthRateLimiting(services, configuration);

        return services;
    }

    private static void AddJwtBearerAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var issuer = jwtSection[nameof(JwtOptions.Issuer)] ?? "ExpenseTracker";
        var audience = jwtSection[nameof(JwtOptions.Audience)] ?? "ExpenseTracker";
        var signingKey = jwtSection[nameof(JwtOptions.SigningKey)]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };
            });
    }

    private static void AddRolePolicies(IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicyNames.Employee, p => p.RequireRole(AuthorizationPolicyNames.Employee));
            options.AddPolicy(AuthorizationPolicyNames.Manager, p => p.RequireRole(AuthorizationPolicyNames.Manager));
            options.AddPolicy(AuthorizationPolicyNames.Finance, p => p.RequireRole(AuthorizationPolicyNames.Finance));
            options.AddPolicy(
                AuthorizationPolicyNames.ComplianceOfficer,
                p => p.RequireRole(AuthorizationPolicyNames.ComplianceOfficer));
        });
    }

    private static void AddAuthRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        var rateLimitOptions = configuration.GetSection(AuthRateLimitOptions.SectionName).Get<AuthRateLimitOptions>()
            ?? new AuthRateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var response = new ErrorResponse(new ErrorDetail(
                    "RATE_LIMIT_EXCEEDED",
                    "Too many requests. Please try again later.",
                    [],
                    context.HttpContext.TraceIdentifier));

                await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            };

            foreach (var policyName in AuthRateLimitPolicyNames.All)
            {
                options.AddPolicy(policyName, httpContext => RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                        SegmentsPerWindow = 5,
                        QueueLimit = 0,
                    }));
            }
        });
    }
}
