using System.Net;
using System.Net.Http.Headers;
using Application.Auth;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

public class RoleBasedAuthorizationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly List<Guid> _employeeIdsToCleanUp = [];
    private readonly List<Guid> _userIdsToCleanUp = [];

    public RoleBasedAuthorizationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Users.RemoveRange(dbContext.Users.Where(u => _userIdsToCleanUp.Contains(u.Id)));
        dbContext.Employees.RemoveRange(dbContext.Employees.Where(e => _employeeIdsToCleanUp.Contains(e.EmployeeId)));

        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Whoami_RoleReflectsCurrentEmployeeRole_NotCachedAcrossRequests()
    {
        var (userId, employeeId) = await CreateEmployeeAndUserAsync(EmployeeRole.Employee);
        var client = CreateAuthorizedClient(GenerateToken(userId));

        var firstBody = await (await client.GetAsync("/__test/whoami")).Content.ReadAsStringAsync();
        Assert.Contains(nameof(EmployeeRole.Employee), firstBody);

        await UpdateEmployeeRoleAsync(employeeId, EmployeeRole.Manager);

        var secondBody = await (await client.GetAsync("/__test/whoami")).Content.ReadAsStringAsync();
        Assert.Contains(nameof(EmployeeRole.Manager), secondBody);
    }

    [Fact]
    public async Task Whoami_AttachesRoleClaimMatchingEmployeeRole()
    {
        var (userId, _) = await CreateEmployeeAndUserAsync(EmployeeRole.Manager);
        var client = CreateAuthorizedClient(GenerateToken(userId));

        var response = await client.GetAsync("/__test/whoami");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(nameof(EmployeeRole.Manager), body);
    }

    [Fact]
    public async Task Whoami_UserWithoutEmployeeRecord_Returns401()
    {
        var userIdWithNoUserRow = Guid.NewGuid();
        var client = CreateAuthorizedClient(GenerateToken(userIdWithNoUserRow));

        var response = await client.GetAsync("/__test/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Whoami_InactiveEmployee_Returns401()
    {
        var (userId, _) = await CreateEmployeeAndUserAsync(EmployeeRole.Employee, isActive: false);
        var client = CreateAuthorizedClient(GenerateToken(userId));

        var response = await client.GetAsync("/__test/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ManagerOnly_MatchingRole_IsAuthorized()
    {
        var (userId, _) = await CreateEmployeeAndUserAsync(EmployeeRole.Manager);
        var client = CreateAuthorizedClient(GenerateToken(userId));

        var response = await client.GetAsync("/__test/manager-only");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ManagerOnly_NonMatchingRole_Returns403WithEnvelope()
    {
        var (userId, _) = await CreateEmployeeAndUserAsync(EmployeeRole.Employee);
        var client = CreateAuthorizedClient(GenerateToken(userId));

        var response = await client.GetAsync("/__test/manager-only");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("AUTHORIZATION_FAILED", body);
    }

    [Theory]
    [InlineData(EmployeeRole.Employee)]
    [InlineData(EmployeeRole.Manager)]
    public async Task EmployeeOrManagerOnly_MatchingRole_IsAuthorized(EmployeeRole role)
    {
        var (userId, _) = await CreateEmployeeAndUserAsync(role);
        var client = CreateAuthorizedClient(GenerateToken(userId));

        var response = await client.GetAsync("/__test/employee-or-manager-only");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(EmployeeRole.Finance)]
    [InlineData(EmployeeRole.ComplianceOfficer)]
    public async Task EmployeeOrManagerOnly_NonMatchingRole_Returns403WithEnvelope(EmployeeRole role)
    {
        var (userId, _) = await CreateEmployeeAndUserAsync(role);
        var client = CreateAuthorizedClient(GenerateToken(userId));

        var response = await client.GetAsync("/__test/employee-or-manager-only");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("AUTHORIZATION_FAILED", body);
    }

    private HttpClient CreateAuthorizedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string GenerateToken(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        return jwtTokenService.GenerateAccessToken(userId);
    }

    private async Task<(Guid UserId, Guid EmployeeId)> CreateEmployeeAndUserAsync(EmployeeRole role, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..9];
        var employee = new Employee
        {
            EmployeeId = Guid.NewGuid(),
            EmployeeNumber = $"IT-{uniqueSuffix}",
            FirstName = "Test",
            LastName = "User",
            Email = $"{uniqueSuffix}@test.local",
            Role = role,
            IsActive = isActive,
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
        _userIdsToCleanUp.Add(user.Id);

        return (user.Id, employee.EmployeeId);
    }

    private async Task UpdateEmployeeRoleAsync(Guid employeeId, EmployeeRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var employee = await dbContext.Employees.FindAsync(employeeId);
        employee!.Role = role;
        await dbContext.SaveChangesAsync();
    }
}
