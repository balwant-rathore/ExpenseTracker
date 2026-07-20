using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Auth;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>
/// Each test owns its own <see cref="AuthFunctionalWebApplicationFactory"/> instance (instead of
/// sharing one via IClassFixture) so every test starts with a pristine rate-limiter counter for
/// the "AuthRegister" policy exercised during setup - see the factory's doc comment for why. The
/// refresh endpoint itself carries no rate-limit policy per the session-refresh spec.
/// </summary>
public class AuthRefreshTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AuthFunctionalWebApplicationFactory _factory = new();
    private readonly List<Guid> _employeeIdsToCleanUp = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var userIds = await dbContext.Users
                .Where(u => _employeeIdsToCleanUp.Contains(u.EmployeeId))
                .Select(u => u.Id)
                .ToListAsync();

            dbContext.RefreshTokens.RemoveRange(dbContext.RefreshTokens.Where(rt => userIds.Contains(rt.UserId)));
            await dbContext.SaveChangesAsync();

            dbContext.Users.RemoveRange(dbContext.Users.Where(u => _employeeIdsToCleanUp.Contains(u.EmployeeId)));
            await dbContext.SaveChangesAsync();

            dbContext.Employees.RemoveRange(dbContext.Employees.Where(e => _employeeIdsToCleanUp.Contains(e.EmployeeId)));
            await dbContext.SaveChangesAsync();
        }

        _factory.Dispose();
    }

    [Fact]
    public async Task ValidRefreshTokenReturnsANewTokenPair()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await response.Content.ReadFromJsonAsync<RefreshResponse>(JsonOptions);
        Assert.NotNull(refreshed);

        // The rotated refresh token is a fresh, cryptographically random value - always unique.
        Assert.NotEqual(auth.RefreshToken, refreshed!.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.RefreshToken));

        // The new access token is a freshly-issued, valid token for the same user. Note: it isn't
        // asserted to differ textually from the original - the access token JWT only carries a
        // `sub` claim plus second-granularity iat/exp, so two tokens minted for the same user
        // within the same clock second are byte-identical (no jti/nonce claim). See ET004 test
        // report for this observation.
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        using var scope = _factory.Services.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        Assert.True(jwtTokenService.TryValidateAccessToken(refreshed.AccessToken, out var userId));
        Assert.Equal(auth.User.Id, userId);
    }

    [Fact]
    public async Task InvalidExpiredOrReusedRefreshTokenIsRejected()
    {
        var client = _factory.CreateClient();

        var notFoundResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(Convert.ToBase64String(Guid.NewGuid().ToByteArray())));
        Assert.Equal(HttpStatusCode.Unauthorized, notFoundResponse.StatusCode);

        var auth = await RegisterAsync(client);
        var firstRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        var reusedResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reusedResponse.StatusCode);
    }

    [Fact]
    public async Task RefreshSucceedsWithoutAnAuthorizationHeader()
    {
        var client = _factory.CreateClient();
        Assert.Null(client.DefaultRequestHeaders.Authorization);
        var auth = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<AuthResponse> RegisterAsync(HttpClient client, string password = "Passw0rd1")
    {
        var employeeNumber = await CreateEmployeeAsync();
        var email = $"{Guid.NewGuid():N}@test.local";
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, email, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!;
    }

    private async Task<string> CreateEmployeeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..9];
        var employee = new Employee
        {
            EmployeeId = Guid.NewGuid(),
            EmployeeNumber = $"RFR-{suffix}",
            FirstName = "Test",
            LastName = "User",
            Email = $"{suffix}@test.local",
            Role = EmployeeRole.Employee,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        _employeeIdsToCleanUp.Add(employee.EmployeeId);
        return employee.EmployeeNumber;
    }
}
