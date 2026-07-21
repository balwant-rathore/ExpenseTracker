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

public class ExpenseReimbursementTests : IAsyncLifetime
{
    private readonly AttachmentFunctionalWebApplicationFactory _factory = new();
    private readonly List<Guid> _employeeIdsToCleanUp = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Expenses.RemoveRange(dbContext.Expenses.Where(e => _employeeIdsToCleanUp.Contains(e.EmployeeId)));
        await dbContext.SaveChangesAsync();

        dbContext.Attachments.RemoveRange(dbContext.Attachments.Where(a => _employeeIdsToCleanUp.Contains(a.UploadedByEmployeeId)));
        await dbContext.SaveChangesAsync();

        dbContext.Users.RemoveRange(dbContext.Users.Where(u => _employeeIdsToCleanUp.Contains(u.EmployeeId)));
        await dbContext.SaveChangesAsync();

        dbContext.Employees.RemoveRange(dbContext.Employees.Where(e => _employeeIdsToCleanUp.Contains(e.EmployeeId)));
        await dbContext.SaveChangesAsync();

        _factory.Dispose();
    }

    [Fact]
    public async Task Reimburse_ApprovedNonClientEntertainment_Returns200Reimbursed()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "Travel");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await financeClient.PostAsync($"/api/expenses/{expenseId}/reimburse", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Reimbursed", json.RootElement.GetProperty("expense").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Reimburse_ComplianceApprovedClientEntertainment_Returns200Reimbursed()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.ComplianceApproved);

        var response = await financeClient.PostAsync($"/api/expenses/{expenseId}/reimburse", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Reimbursed", json.RootElement.GetProperty("expense").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Reimburse_ApprovedClientEntertainmentNotYetComplianceApproved_Returns422StatusUnchanged()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await financeClient.PostAsync($"/api/expenses/{expenseId}/reimburse", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Approved);
    }

    [Fact]
    public async Task Reimburse_ComplianceApprovedNonClientEntertainment_Returns422()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "Meals");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.ComplianceApproved);

        var response = await financeClient.PostAsync($"/api/expenses/{expenseId}/reimburse", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.ComplianceApproved);
    }

    [Theory]
    [InlineData(ExpenseStatus.Draft)]
    [InlineData(ExpenseStatus.Submitted)]
    [InlineData(ExpenseStatus.Rejected)]
    [InlineData(ExpenseStatus.Cancelled)]
    [InlineData(ExpenseStatus.Reimbursed)]
    public async Task Reimburse_NonEligibleStatus_Returns422StatusUnchanged(ExpenseStatus status)
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "Travel");
        await SetExpenseStatusAsync(expenseId, status);

        var response = await financeClient.PostAsync($"/api/expenses/{expenseId}/reimburse", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, status);
    }

    [Theory]
    [InlineData(EmployeeRole.Employee)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.ComplianceOfficer)]
    public async Task Reimburse_NonFinanceRole_Returns403(EmployeeRole role)
    {
        var (callerClient, _) = await CreateAuthorizedClientAsync(role);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "Travel");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await callerClient.PostAsync($"/api/expenses/{expenseId}/reimburse", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reimburse_NonexistentId_Returns404()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);

        var response = await financeClient.PostAsync($"/api/expenses/{Guid.NewGuid()}/reimburse", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reimburse_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/expenses/{Guid.NewGuid()}/reimburse", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reimburse_Success_PopulatesAuditFields()
    {
        var (financeClient, financeId) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "Travel");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        await financeClient.PostAsync($"/api/expenses/{expenseId}/reimburse", null);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        Assert.Equal(financeId, expense!.ReimbursedByEmployeeId);
        Assert.NotNull(expense.ReimbursedAt);
        Assert.True(expense.ReimbursedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Reimburse_ClientSuppliedAuditFields_HaveNoEffect()
    {
        var (financeClient, financeId) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "Travel");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await financeClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reimburse", new
        {
            ReimbursedAt = "2020-01-01T00:00:00Z",
            ReimbursedByEmployeeId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        Assert.Equal(financeId, expense!.ReimbursedByEmployeeId);
        Assert.True(expense.ReimbursedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    // ---- Helpers ----

    private static ExpenseRequestBody CreateExpenseBody(Guid attachmentId, string action, string category) => new(
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        category,
        100.00m,
        "INR",
        "Integration test expense",
        attachmentId,
        action);

    private async Task<Guid> CreateExpenseAsync(HttpClient client, Guid attachmentId, string action, string category)
    {
        var response = await client.PostAsJsonAsync("/api/expenses", CreateExpenseBody(attachmentId, action, category));
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("expense").GetProperty("id").GetGuid();
    }

    private async Task SetExpenseStatusAsync(Guid expenseId, ExpenseStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        expense!.Status = status;
        await dbContext.SaveChangesAsync();
    }

    private async Task AssertStatusUnchangedAsync(Guid expenseId, ExpenseStatus expectedStatus)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        Assert.Equal(expectedStatus, expense!.Status);
    }

    private static async Task<Guid> UploadAttachmentAsync(HttpClient client)
    {
        var fileContent = new ByteArrayContent(new byte[1024]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var multipart = new MultipartFormDataContent { { fileContent, "file", "et012-test-receipt.pdf" } };

        var response = await client.PostAsync("/api/attachments", multipart);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("attachmentId").GetGuid();
    }

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
            EmployeeNumber = $"IT-{uniqueSuffix}",
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

    private sealed record ExpenseRequestBody(
        string ExpenseDate,
        string Category,
        decimal Amount,
        string Currency,
        string Description,
        Guid ReceiptAttachmentId,
        string Action);
}
