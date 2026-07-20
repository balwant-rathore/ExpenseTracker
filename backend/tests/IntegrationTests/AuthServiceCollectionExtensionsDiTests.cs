using Api.Authentication;
using Api.Extensions;
using Application.Auth;
using Domain.Repositories;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

public class AuthServiceCollectionExtensionsDiTests
{
    [Fact]
    public void AddAuthFoundation_RegistersAllAuthServices_ResolvableFromDi()
    {
        var services = new ServiceCollection();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer("Server=unused;Database=unused;"));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "di-test-signing-key-at-least-32-bytes-long!!",
                ["Jwt:Issuer"] = "ExpenseTracker",
                ["Jwt:Audience"] = "ExpenseTracker",
            })
            .Build();

        services.AddAuthFoundation(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IJwtTokenService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRefreshTokenService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPasswordPolicyValidator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<EmployeeRoleResolutionMiddleware>());
        Assert.NotNull(provider.GetRequiredService<IAuthorizationMiddlewareResultHandler>());
    }
}
