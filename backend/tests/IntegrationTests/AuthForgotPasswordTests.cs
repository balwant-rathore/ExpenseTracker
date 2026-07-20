using System.Net;
using System.Net.Http.Json;
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
/// </summary>
public class AuthForgotPasswordTests : IAsyncLifetime
{
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
    public async Task ExistingAccount_ReturnsGenericSuccess()
    {
        var client = _factory.CreateClient();
        var employeeNumber = await CreateEmployeeAsync();
        var email = $"{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, email, "Passw0rd1"));

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NonExistingAccount_ReturnsIdenticalGenericSuccess()
    {
        var client = _factory.CreateClient();
        var employeeNumber = await CreateEmployeeAsync();
        var existingEmail = $"{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(employeeNumber, existingEmail, "Passw0rd1"));

        var existingResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(existingEmail));
        var nonExistingResponse = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest($"{Guid.NewGuid():N}@test.local"));

        Assert.Equal(existingResponse.StatusCode, nonExistingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, nonExistingResponse.StatusCode);
        var existingBody = await existingResponse.Content.ReadAsStringAsync();
        var nonExistingBody = await nonExistingResponse.Content.ReadAsStringAsync();
        Assert.Equal(existingBody, nonExistingBody);
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
            EmployeeNumber = $"FGT-{suffix}",
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
