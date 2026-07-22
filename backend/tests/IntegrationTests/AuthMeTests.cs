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
/// the "AuthRegister" policy exercised during setup - see the factory's doc comment for why.
/// GET /api/auth/me itself carries no rate-limit policy.
/// </summary>
public class AuthMeTests : IAsyncLifetime
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
    public async Task AuthenticatedRequestReturnsTheCurrentUser()
    {
        var client = _factory.CreateClient();
        const string password = "Passw0rd1";
        var employeeNumber = await CreateEmployeeAsync();
        var email = $"{Guid.NewGuid():N}@test.local";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, email, password));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registerAuth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        var authorizedClient = _factory.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registerAuth!.AccessToken);
        var meResponse = await authorizedClient.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var user = await meResponse.Content.ReadFromJsonAsync<UserDto>(JsonOptions);
        Assert.NotNull(user);
        Assert.Equal(registerAuth.User.Id, user!.Id);
        Assert.Equal(email, user.Email);
        Assert.Equal(employeeNumber, user.EmployeeNumber);
        Assert.Equal(registerAuth.User.FirstName, user.FirstName);
        Assert.Equal(registerAuth.User.LastName, user.LastName);
        Assert.Equal(registerAuth.User.Role, user.Role);
    }

    [Fact]
    public async Task MissingAuthorizationHeaderIsRejected()
    {
        // Matches AuthLogoutTests.MissingOrInvalidAccessTokenIsRejected: a request with no bearer
        // token never reaches the [Authorize] action, so it is rejected by the JWT-bearer
        // authentication challenge itself (bare 401, no body) rather than by AuthController's own
        // FailureResult envelope - identical, pre-existing behavior for every [Authorize] endpoint.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidBearerTokenIsRejected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
            EmployeeNumber = $"ME-{suffix}",
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
