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
/// the "AuthRegister" policy - see the factory's doc comment for why. No single test here issues
/// more than 2 register calls, comfortably under the production default limit of 5 per 300s.
/// </summary>
public class AuthRegisterTests : IAsyncLifetime
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
    public async Task UnknownEmployeeNumberIsRejected()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"UNKNOWN-{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@test.local", "Passw0rd1"));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);

        var (code, message) = await ReadErrorAsync(response);
        Assert.NotEmpty(code);
        Assert.NotEmpty(message);
    }

    [Fact]
    public async Task InactiveEmployeesNumberIsRejectedWithTheSameGenericError()
    {
        var client = _factory.CreateClient();

        var unknownResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"UNKNOWN-{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@test.local", "Passw0rd1"));
        var (unknownCode, unknownMessage) = await ReadErrorAsync(unknownResponse);

        var employeeNumber = await CreateEmployeeAsync(isActive: false);
        var inactiveResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(employeeNumber, $"{Guid.NewGuid():N}@test.local", "Passw0rd1"));
        var (inactiveCode, inactiveMessage) = await ReadErrorAsync(inactiveResponse);

        Assert.Equal(unknownResponse.StatusCode, inactiveResponse.StatusCode);
        Assert.Equal(unknownCode, inactiveCode);
        Assert.Equal(unknownMessage, inactiveMessage);
    }

    [Fact]
    public async Task AlreadyRegisteredEmployeeNumberIsRejectedWithTheSameGenericError()
    {
        var client = _factory.CreateClient();

        var unknownResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"UNKNOWN-{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@test.local", "Passw0rd1"));
        var (unknownCode, unknownMessage) = await ReadErrorAsync(unknownResponse);

        var (employeeNumber, employeeId) = await CreateRegisteredEmployeeAsync();
        var newEmail = $"{Guid.NewGuid():N}@test.local";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(employeeNumber, newEmail, "Passw0rd1"));
        var (code, message) = await ReadErrorAsync(response);

        Assert.Equal(unknownResponse.StatusCode, response.StatusCode);
        Assert.Equal(unknownCode, code);
        Assert.Equal(unknownMessage, message);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var usersForEmployee = await dbContext.Users.CountAsync(u => u.EmployeeId == employeeId);
        Assert.Equal(1, usersForEmployee);

        var newEmailWasNeverPersisted = await dbContext.Users.AnyAsync(u => u.NormalizedEmail == newEmail.ToUpperInvariant());
        Assert.False(newEmailWasNeverPersisted);
    }

    [Fact]
    public async Task ActiveNotYetRegisteredEmployeesNumberAllowsRegistrationToProceed()
    {
        var employeeNumber = await CreateEmployeeAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(employeeNumber, $"{Guid.NewGuid():N}@test.local", "Passw0rd1"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateCaseInsensitiveEmailIsRejected()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@test.local";

        var employeeNumberA = await CreateEmployeeAsync();
        var firstResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(employeeNumberA, email, "Passw0rd1"));
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var employeeNumberB = await CreateEmployeeAsync();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(employeeNumberB, email.ToUpperInvariant(), "Passw0rd1"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("error");
        Assert.Equal("RESOURCE_CONFLICT", error.GetProperty("code").GetString());
        Assert.Contains("already registered", error.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(error.GetProperty("fields").EnumerateArray());
    }

    [Fact]
    public async Task UniqueEmailIsAccepted()
    {
        var employeeNumber = await CreateEmployeeAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(employeeNumber, $"{Guid.NewGuid():N}@test.local", "Passw0rd1"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task MalformedEmail_ReturnsValidationErrorOnEmailField()
    {
        var employeeNumber = await CreateEmployeeAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(employeeNumber, "not-an-email", "Passw0rd1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("error");
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
        var fields = error.GetProperty("fields").EnumerateArray().Select(f => f.GetString()).ToList();
        Assert.Contains("Email", fields);
    }

    [Fact]
    public async Task NonCompliantPasswordIsRejectedWithAFieldLevelError()
    {
        var employeeNumber = await CreateEmployeeAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(employeeNumber, $"{Guid.NewGuid():N}@test.local", "allletters"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("error");
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
        var fields = error.GetProperty("fields").EnumerateArray().Select(f => f.GetString()).ToList();
        Assert.Contains("password", fields);
    }

    [Fact]
    public async Task SuccessfulRegistrationReturnsTokensAndAUserWithoutThePasswordHash()
    {
        var employeeNumber = await CreateEmployeeAsync();
        var client = _factory.CreateClient();
        const string password = "Passw0rd1";

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(employeeNumber, $"{Guid.NewGuid():N}@test.local", password));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hash", rawBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(password, rawBody, StringComparison.Ordinal);

        var auth = JsonSerializer.Deserialize<AuthResponse>(rawBody, JsonOptions);
        Assert.NotNull(auth);
        Assert.NotEqual(Guid.Empty, auth!.User.Id);
        Assert.Equal(employeeNumber, auth.User.EmployeeNumber);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
    }

    [Fact]
    public async Task PasswordIsPersistedOnlyAsAHash()
    {
        var employeeNumber = await CreateEmployeeAsync();
        var client = _factory.CreateClient();
        const string password = "Passw0rd1";
        var email = $"{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(employeeNumber, email, password));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await dbContext.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());

        Assert.NotEqual(password, user.PasswordHash);
        Assert.StartsWith("$2", user.PasswordHash);
    }

    private static async Task<(string Code, string Message)> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
    }

    private async Task<string> CreateEmployeeAsync(bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..9];
        var employee = new Employee
        {
            EmployeeId = Guid.NewGuid(),
            EmployeeNumber = $"REG-{suffix}",
            FirstName = "Test",
            LastName = "User",
            Email = $"{suffix}@test.local",
            Role = EmployeeRole.Employee,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        _employeeIdsToCleanUp.Add(employee.EmployeeId);
        return employee.EmployeeNumber;
    }

    private async Task<(string EmployeeNumber, Guid EmployeeId)> CreateRegisteredEmployeeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..9];
        var employee = new Employee
        {
            EmployeeId = Guid.NewGuid(),
            EmployeeNumber = $"REG-{suffix}",
            FirstName = "Test",
            LastName = "User",
            Email = $"{suffix}@test.local",
            Role = EmployeeRole.Employee,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.EmployeeId,
            Email = employee.Email,
            NormalizedEmail = employee.Email.ToUpperInvariant(),
            PasswordHash = "unused-hash",
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Employees.Add(employee);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        _employeeIdsToCleanUp.Add(employee.EmployeeId);
        return (employee.EmployeeNumber, employee.EmployeeId);
    }
}
