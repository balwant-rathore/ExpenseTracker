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
/// the "AuthLogin" policy - see the factory's doc comment for why. No single test here issues
/// more than 2 login calls, comfortably under the production default limit of 5 per 300s.
/// </summary>
public class AuthLoginTests : IAsyncLifetime
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
    public async Task CorrectCredentialsIssueATokenPair()
    {
        var client = _factory.CreateClient();
        const string password = "Passw0rd1";
        var employeeNumber = await CreateEmployeeAsync();
        var email = $"{Guid.NewGuid():N}@test.local";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, email, password));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        Assert.Equal(email, auth!.User.Email);
        Assert.Equal(employeeNumber, auth.User.EmployeeNumber);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
    }

    [Fact]
    public async Task UnknownEmailReturnsTheGenericError()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest($"{Guid.NewGuid():N}@test.local", "Passw0rd1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var (code, _) = await ReadErrorAsync(response);
        Assert.Equal("AUTHENTICATION_FAILED", code);
    }

    [Fact]
    public async Task WrongPasswordReturnsTheIdenticalGenericError()
    {
        var client = _factory.CreateClient();
        var employeeNumber = await CreateEmployeeAsync();
        var email = $"{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, email, "Passw0rd1"));

        var unknownResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest($"{Guid.NewGuid():N}@test.local", "Passw0rd1"));
        var wrongPasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "WrongPass1"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        var (unknownCode, unknownMessage) = await ReadErrorAsync(unknownResponse);
        var (wrongCode, wrongMessage) = await ReadErrorAsync(wrongPasswordResponse);
        Assert.Equal(unknownResponse.StatusCode, wrongPasswordResponse.StatusCode);
        Assert.Equal(unknownCode, wrongCode);
        Assert.Equal(unknownMessage, wrongMessage);
    }

    [Fact]
    public async Task MissingRequiredFieldsReturnAValidationError()
    {
        var client = _factory.CreateClient();

        var missingEmailResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("", "Passw0rd1"));
        var missingPasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest($"{Guid.NewGuid():N}@test.local", ""));

        Assert.Equal(HttpStatusCode.BadRequest, missingEmailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingPasswordResponse.StatusCode);

        var emailFields = await ReadFieldsAsync(missingEmailResponse);
        var passwordFields = await ReadFieldsAsync(missingPasswordResponse);

        Assert.Contains("Email", emailFields);
        Assert.Contains("Password", passwordFields);
    }

    [Fact]
    public async Task SuccessfulLoginPersistsARedeemableRefreshToken()
    {
        var client = _factory.CreateClient();
        const string password = "Passw0rd1";
        var employeeNumber = await CreateEmployeeAsync();
        var email = $"{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, email, password));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        var firstRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth!.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        var secondRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, secondRefresh.StatusCode);
    }

    private static async Task<(string Code, string Message)> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
    }

    private static async Task<List<string?>> ReadFieldsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("error").GetProperty("fields").EnumerateArray().Select(f => f.GetString()).ToList();
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
            EmployeeNumber = $"LOG-{suffix}",
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
