using System.Net;
using System.Net.Http.Headers;
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
/// the "AuthRegister"/"AuthLogin" policies exercised during setup - see the factory's doc comment
/// for why. Logout itself carries no rate-limit policy.
/// </summary>
public class AuthLogoutTests : IAsyncLifetime
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
    public async Task MissingOrInvalidAccessTokenIsRejected()
    {
        var registerClient = _factory.CreateClient();
        var auth = await RegisterAsync(registerClient);

        var noAuthClient = _factory.CreateClient();
        var missingResponse = await noAuthClient.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, missingResponse.StatusCode);

        var invalidAuthClient = _factory.CreateClient();
        invalidAuthClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");
        var invalidResponse = await invalidAuthClient.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);

        // Neither rejected attempt should have revoked the refresh token.
        var refreshResponse = await registerClient.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task LogoutRevokesTheSuppliedTokenAndLeavesOtherSessionsActive()
    {
        var client = _factory.CreateClient();
        const string password = "Passw0rd1";
        var employeeNumber = await CreateEmployeeAsync();
        var email = $"{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, email, password));

        var loginAResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var loginA = await loginAResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var loginBResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var loginB = await loginBResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        var authorizedClient = _factory.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginA!.AccessToken);
        var logoutResponse = await authorizedClient.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(loginA.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        // Note: intentionally NOT also asserting that loginA's now-revoked token is rejected by
        // /api/auth/refresh here - redeeming an already-revoked token triggers reuse-detection,
        // which (by design, see refresh-token-lifecycle) revokes ALL of the user's active
        // tokens, including loginB's. That specific assertion belongs to
        // RevokedTokenCannotBeUsedToObtainANewAccessToken; this test only needs to prove the
        // *other* session survives logout.
        var otherSessionRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(loginB!.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, otherSessionRefresh.StatusCode);
    }

    [Fact]
    public async Task RevokedTokenCannotBeUsedToObtainANewAccessToken()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAsync(client);

        var authorizedClient = _factory.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var logoutResponse = await authorizedClient.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task SupplyingARefreshTokenBelongingToADifferentUserIsRejected()
    {
        var client = _factory.CreateClient();
        var authUserA = await RegisterAsync(client);
        var authUserB = await RegisterAsync(client);

        var authorizedAsB = _factory.CreateClient();
        authorizedAsB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authUserB.AccessToken);

        var response = await authorizedAsB.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(authUserA.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(authUserA.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task SuccessfulLogoutReturns204()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAsync(client);

        var authorizedClient = _factory.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var response = await authorizedClient.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Empty(body);
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
            EmployeeNumber = $"OUT-{suffix}",
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
