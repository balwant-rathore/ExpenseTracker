using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Auth;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>
/// Each test owns its own <see cref="AuthFunctionalWebApplicationFactory"/> instance for the same
/// reason <see cref="AuthLoginTests"/> does - a pristine per-host rate-limiter counter per test.
/// Since <see cref="AuthFunctionalWebApplicationFactory"/> hosts the API in-process, the plaintext
/// OTP written via <c>Console.WriteLine</c> during a forgot-password call can be captured by
/// redirecting <see cref="Console.Out"/> around that call.
/// </summary>
public class AuthResetPasswordTests : IAsyncLifetime
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

            dbContext.PasswordResetOtps.RemoveRange(dbContext.PasswordResetOtps.Where(o => userIds.Contains(o.UserId)));
            await dbContext.SaveChangesAsync();

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
    public async Task ValidOtpAndCompliantPassword_ResetsPassword()
    {
        var client = _factory.CreateClient();
        var employeeNumber = await CreateEmployeeAsync();
        var email = $"{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, email, "Passw0rd1"));
        var otp = await RequestOtpAsync(client, email);

        var resetResponse = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(email, otp, "NewPassw0rd2"));

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var oldPasswordLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Passw0rd1"));
        var newPasswordLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "NewPassw0rd2"));

        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task ExpiredOtp_ReturnsResourceExpired()
    {
        var client = _factory.CreateClient();
        var employeeNumber = await CreateEmployeeAsync();
        var email = $"{Guid.NewGuid():N}@test.local";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, email, "Passw0rd1"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        await InsertOtpDirectlyAsync(auth!.User.Id, "123456", DateTime.UtcNow.AddMinutes(-1));

        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(email, "123456", "NewPassw0rd2"));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        var (code, _) = await ReadErrorAsync(response);
        Assert.Equal("RESOURCE_EXPIRED", code);
    }

    [Fact]
    public async Task WrongOtp_ReturnsAuthenticationFailed()
    {
        var client = _factory.CreateClient();
        var employeeNumber = await CreateEmployeeAsync();
        var email = $"{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, email, "Passw0rd1"));
        var otp = await RequestOtpAsync(client, email);
        var wrongOtp = otp == "111111" ? "222222" : "111111";

        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(email, wrongOtp, "NewPassw0rd2"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var (code, _) = await ReadErrorAsync(response);
        Assert.Equal("AUTHENTICATION_FAILED", code);
    }

    [Fact]
    public async Task AlreadyUsedOtp_IsRejectedAndCannotBeReusedASecondTime()
    {
        var client = _factory.CreateClient();
        var employeeNumber = await CreateEmployeeAsync();
        var email = $"{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, email, "Passw0rd1"));
        var otp = await RequestOtpAsync(client, email);

        var firstReset = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(email, otp, "NewPassw0rd2"));
        var secondReset = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(email, otp, "AnotherPass3"));

        Assert.Equal(HttpStatusCode.OK, firstReset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, secondReset.StatusCode);
        var (code, _) = await ReadErrorAsync(secondReset);
        Assert.Equal("AUTHENTICATION_FAILED", code);
    }

    [Fact]
    public async Task WeakNewPassword_ReturnsValidationErrorOnNewPasswordField()
    {
        var client = _factory.CreateClient();
        var employeeNumber = await CreateEmployeeAsync();
        var email = $"{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, email, "Passw0rd1"));

        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(email, "000000", "weak"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fields = body.GetProperty("error").GetProperty("fields").EnumerateArray().Select(f => f.GetString()).ToList();
        Assert.Contains("newPassword", fields);
    }

    [Fact]
    public async Task SuccessfulReset_RevokesAllActiveRefreshTokensForUser()
    {
        var client = _factory.CreateClient();
        var employeeNumber = await CreateEmployeeAsync();
        var email = $"{Guid.NewGuid():N}@test.local";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, email, "Passw0rd1"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var otp = await RequestOtpAsync(client, email);

        await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(email, otp, "NewPassw0rd2"));
        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth!.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    private static async Task<string> RequestOtpAsync(HttpClient client, string email)
    {
        var originalOut = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);
        string consoleOutput;
        try
        {
            await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        }
        finally
        {
            Console.SetOut(originalOut);
            consoleOutput = captured.ToString();
        }

        var match = Regex.Match(consoleOutput, @"\b\d{6}\b");
        Assert.True(match.Success, $"Expected a 6-digit OTP in console output, got: {consoleOutput}");
        return match.Value;
    }

    private async Task InsertOtpDirectlyAsync(Guid userId, string plaintextOtp, DateTime expiresAt)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.PasswordResetOtps.Add(new PasswordResetOtp
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OtpHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintextOtp))),
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow.AddMinutes(-11),
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<(string Code, string Message)> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
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
            EmployeeNumber = $"RST-{suffix}",
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
