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

public class ExpenseNotificationTests : IAsyncLifetime
{
    private readonly NotificationFunctionalWebApplicationFactory _factory = new();
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
    public async Task CreateWithSubmitAction_AppendsSubmittedEntry()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);

        await CreateExpenseAsync(reportClient, attachmentId, "Submit", "Travel");

        var entry = await ReadSingleEntryAsync();
        AssertWellFormedEntry(entry, "Submitted");
    }

    [Fact]
    public async Task SubmitExistingDraft_AppendsSubmittedEntry()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Draft", "Travel");
        Assert.False(File.Exists(_factory.NotificationLogFilePath));

        await reportClient.PostAsync($"/api/expenses/{expenseId}/submit", null);

        var entry = await ReadSingleEntryAsync();
        AssertWellFormedEntry(entry, "Submitted");
    }

    [Fact]
    public async Task Approve_AppendsApprovedEntry()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit", "Travel");
        await ClearLogAsync();

        await managerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);

        var entry = await ReadSingleEntryAsync();
        AssertWellFormedEntry(entry, "Approved");
        Assert.DoesNotContain(expenseId.ToString(), entry);
    }

    [Fact]
    public async Task Reject_AppendsRejectedEntry()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit", "Travel");
        await ClearLogAsync();

        await managerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reject", RejectBody("Missing itemized receipt"));

        var entry = await ReadSingleEntryAsync();
        AssertWellFormedEntry(entry, "Rejected");
    }

    [Fact]
    public async Task ComplianceApprove_AppendsComplianceApprovedEntry()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit", "ClientEntertainment");
        await managerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);
        await ClearLogAsync();

        await complianceClient.PostAsync($"/api/expenses/{expenseId}/compliance-approve", null);

        var entry = await ReadSingleEntryAsync();
        AssertWellFormedEntry(entry, "ComplianceApproved");
    }

    [Fact]
    public async Task ComplianceReject_AppendsComplianceRejectedEntry()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit", "ClientEntertainment");
        await managerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);
        await ClearLogAsync();

        await complianceClient.PostAsJsonAsync($"/api/expenses/{expenseId}/compliance-reject", RejectBody("Exceeds per-person entertainment limit"));

        var entry = await ReadSingleEntryAsync();
        AssertWellFormedEntry(entry, "ComplianceRejected");
    }

    [Fact]
    public async Task Reimburse_AppendsReimbursedEntry()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit", "Travel");
        await managerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);
        await ClearLogAsync();

        await financeClient.PostAsync($"/api/expenses/{expenseId}/reimburse", null);

        var entry = await ReadSingleEntryAsync();
        AssertWellFormedEntry(entry, "Reimbursed");
        Assert.DoesNotContain(expenseId.ToString(), entry);
    }

    [Fact]
    public async Task FailedTransition_LeavesLogUnchanged()
    {
        var (managerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var attachmentId = await UploadAttachmentAsync(managerClient);
        var expenseId = await CreateExpenseAsync(managerClient, attachmentId, "Submit", "Travel");
        await ClearLogAsync();

        // BR-06: a Manager cannot approve their own expense - fails authorization, no transaction commits.
        await managerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);

        Assert.False(File.Exists(_factory.NotificationLogFilePath));
    }

    // ---- Helpers ----

    private async Task ClearLogAsync()
    {
        if (File.Exists(_factory.NotificationLogFilePath))
        {
            File.Delete(_factory.NotificationLogFilePath);
        }

        await Task.CompletedTask;
    }

    private async Task<string> ReadSingleEntryAsync()
    {
        Assert.True(File.Exists(_factory.NotificationLogFilePath));
        var content = await File.ReadAllTextAsync(_factory.NotificationLogFilePath);
        Assert.NotEmpty(content);
        return content;
    }

    private static void AssertWellFormedEntry(string entry, string expectedEvent)
    {
        Assert.Contains($"data-event=\"{expectedEvent}\"", entry);
        Assert.Contains("<strong>Timestamp:</strong>", entry);
        Assert.Contains($"<strong>Event:</strong> {expectedEvent}", entry);
        Assert.Contains("<strong>To:</strong>", entry);
        Assert.Contains("<strong>CC:</strong>", entry);
        Assert.Contains("<strong>Subject:</strong>", entry);
        Assert.Contains("<strong>Body:</strong>", entry);
    }

    private static object RejectBody(string rejectionComment) => new { RejectionComment = rejectionComment };

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

    private static async Task<Guid> UploadAttachmentAsync(HttpClient client)
    {
        var fileContent = new ByteArrayContent(new byte[1024]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var multipart = new MultipartFormDataContent { { fileContent, "file", "et015-test-receipt.pdf" } };

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
