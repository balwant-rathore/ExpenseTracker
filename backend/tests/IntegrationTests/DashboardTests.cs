using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.Auth;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

public class DashboardTests : IAsyncLifetime
{
    private readonly AttachmentFunctionalWebApplicationFactory _factory = new();
    private readonly List<Guid> _employeeIdsToCleanUp = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Users.RemoveRange(dbContext.Users.Where(u => _employeeIdsToCleanUp.Contains(u.EmployeeId)));
        await dbContext.SaveChangesAsync();

        dbContext.Employees.RemoveRange(dbContext.Employees.Where(e => _employeeIdsToCleanUp.Contains(e.EmployeeId)));
        await dbContext.SaveChangesAsync();

        _factory.Dispose();
    }

    // Scenario: Authenticated Employee/Manager/Finance caller receives a dashboard
    [Theory]
    [InlineData(EmployeeRole.Employee)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.Finance)]
    public async Task Get_AuthenticatedEmployeeManagerOrFinance_Returns200WithRoleMetrics(EmployeeRole role)
    {
        var (client, _) = await CreateAuthorizedClientAsync(role);

        var response = await client.GetAsync("/api/dashboard");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("totalSubmitted", out _));
        Assert.True(json.RootElement.TryGetProperty("approved", out _));
        Assert.True(json.RootElement.TryGetProperty("reimbursed", out _));
    }

    // Scenario: Unauthenticated request is rejected
    [Fact]
    public async Task Get_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Scenario: Compliance Officer is rejected
    [Fact]
    public async Task Get_ComplianceOfficer_Returns403()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);

        var response = await client.GetAsync("/api/dashboard");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("AUTHORIZATION_FAILED", body);
    }

    // Scenario: Employee response omits Manager/Finance-only fields
    [Fact]
    public async Task Get_Employee_ResponseOmitsPendingApprovalsAndPendingReimbursements()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);

        var response = await client.GetAsync("/api/dashboard");
        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);
        Assert.False(json.RootElement.TryGetProperty("pendingApprovals", out _));
        Assert.False(json.RootElement.TryGetProperty("pendingReimbursements", out _));
    }

    // Scenario: Manager response omits the Finance-only field
    [Fact]
    public async Task Get_Manager_ResponseOmitsPendingReimbursements()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);

        var response = await client.GetAsync("/api/dashboard");
        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("pendingApprovals", out _));
        Assert.False(json.RootElement.TryGetProperty("pendingReimbursements", out _));
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, Guid EmployeeId)> CreateAuthorizedClientAsync(EmployeeRole role, Guid? managerId = null)
    {
        var (userId, employeeId) = await CreateEmployeeAndUserAsync(role, managerId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateToken(userId));
        return (client, employeeId);
    }

    private string GenerateToken(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        return jwtTokenService.GenerateAccessToken(userId);
    }

    private async Task<(Guid UserId, Guid EmployeeId)> CreateEmployeeAndUserAsync(EmployeeRole role, Guid? managerId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..9];
        var employee = new Employee
        {
            EmployeeId = Guid.NewGuid(),
            EmployeeNumber = $"ET013-{uniqueSuffix}",
            FirstName = "Test",
            LastName = "User",
            Email = $"{uniqueSuffix}@test.local",
            Role = role,
            ManagerId = managerId,
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
            PasswordHash = "unused",
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Employees.Add(employee);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        _employeeIdsToCleanUp.Add(employee.EmployeeId);

        return (user.Id, employee.EmployeeId);
    }
}
