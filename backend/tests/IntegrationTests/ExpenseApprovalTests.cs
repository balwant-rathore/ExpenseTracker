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

public class ExpenseApprovalTests : IAsyncLifetime
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

    // ---- Approve ----

    [Fact]
    public async Task Approve_DirectManagerOfReport_Returns200Approved()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");

        var response = await managerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Approved", json.RootElement.GetProperty("expense").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Approve_UnrelatedManager_Returns403()
    {
        var (managerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");

        var response = await managerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Submitted);
    }

    [Theory]
    [InlineData(EmployeeRole.Employee)]
    [InlineData(EmployeeRole.Finance)]
    [InlineData(EmployeeRole.ComplianceOfficer)]
    public async Task Approve_NonManagerRole_Returns403(EmployeeRole role)
    {
        var (callerClient, _) = await CreateAuthorizedClientAsync(role);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");

        var response = await callerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approve_NonexistentId_Returns404()
    {
        var (managerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);

        var response = await managerClient.PostAsync($"/api/expenses/{Guid.NewGuid()}/approve", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Approve_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/expenses/{Guid.NewGuid()}/approve", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Reject ----

    [Fact]
    public async Task Reject_DirectManagerOfReportWithComment_Returns200Rejected()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");

        var response = await managerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reject", RejectBody("Missing itemized receipt"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Rejected", json.RootElement.GetProperty("expense").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Reject_UnrelatedManager_Returns403()
    {
        var (managerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");

        var response = await managerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reject", RejectBody("Comment"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Submitted);
    }

    [Theory]
    [InlineData(EmployeeRole.Employee)]
    [InlineData(EmployeeRole.Finance)]
    [InlineData(EmployeeRole.ComplianceOfficer)]
    public async Task Reject_NonManagerRole_Returns403(EmployeeRole role)
    {
        var (callerClient, _) = await CreateAuthorizedClientAsync(role);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");

        var response = await callerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reject", RejectBody("Comment"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reject_NonexistentId_Returns404()
    {
        var (managerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);

        var response = await managerClient.PostAsJsonAsync($"/api/expenses/{Guid.NewGuid()}/reject", RejectBody("Comment"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reject_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/expenses/{Guid.NewGuid()}/reject", RejectBody("Comment"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Mandatory rejection comment validation ----

    [Fact]
    public async Task Reject_MissingComment_Returns400WithFieldsEntry()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");

        var response = await managerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reject", new { });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        var fields = json.RootElement.GetProperty("error").GetProperty("fields").EnumerateArray().Select(f => f.GetString()).ToList();
        Assert.Contains("RejectionComment", fields);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Submitted);
    }

    [Fact]
    public async Task Reject_WhitespaceOnlyComment_Returns400()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");

        var response = await managerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reject", RejectBody("   "));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VALIDATION_ERROR", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Submitted);
    }

    [Fact]
    public async Task Reject_CommentOverFiveHundredCharacters_Returns400()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");

        var response = await managerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reject", RejectBody(new string('a', 501)));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VALIDATION_ERROR", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Submitted);
    }

    // ---- BR-06 self-review restriction ----

    [Fact]
    public async Task Approve_OwnExpense_Returns403()
    {
        var (managerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var attachmentId = await UploadAttachmentAsync(managerClient);
        var expenseId = await CreateExpenseAsync(managerClient, attachmentId, "Submit");

        var response = await managerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Submitted);
    }

    [Fact]
    public async Task Reject_OwnExpense_Returns403()
    {
        var (managerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var attachmentId = await UploadAttachmentAsync(managerClient);
        var expenseId = await CreateExpenseAsync(managerClient, attachmentId, "Submit");

        var response = await managerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reject", RejectBody("Comment"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Submitted);
    }

    // ---- Skip-level escalation ----

    [Fact]
    public async Task Approve_SkipLevelManagerOfManagerOwnedExpense_Returns200Approved()
    {
        var (skipLevelClient, skipLevelManagerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (ownerManagerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager, skipLevelManagerId);
        var attachmentId = await UploadAttachmentAsync(ownerManagerClient);
        var expenseId = await CreateExpenseAsync(ownerManagerClient, attachmentId, "Submit");

        var response = await skipLevelClient.PostAsync($"/api/expenses/{expenseId}/approve", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Approved", json.RootElement.GetProperty("expense").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Reject_SkipLevelManagerOfManagerOwnedExpense_Returns200Rejected()
    {
        var (skipLevelClient, skipLevelManagerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (ownerManagerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager, skipLevelManagerId);
        var attachmentId = await UploadAttachmentAsync(ownerManagerClient);
        var expenseId = await CreateExpenseAsync(ownerManagerClient, attachmentId, "Submit");

        var response = await skipLevelClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reject", RejectBody("Comment"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Rejected", json.RootElement.GetProperty("expense").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Approve_ManagerOwnedExpenseWithNoSkipLevelManager_OtherManagerAttempt_Returns403StatusRemainsSubmitted()
    {
        // The expense owner is a Manager with no ManagerId (top of the hierarchy) - a known,
        // accepted gap (design.md Open Questions): no caller can approve this expense.
        var (ownerManagerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var attachmentId = await UploadAttachmentAsync(ownerManagerClient);
        var expenseId = await CreateExpenseAsync(ownerManagerClient, attachmentId, "Submit");

        var (otherManagerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var response = await otherManagerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Submitted);
    }

    // ---- Non-Submitted expenses rejected on approve/reject ----

    [Fact]
    public async Task Approve_DraftExpense_Returns422()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Draft");

        var response = await managerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Draft);
    }

    [Fact]
    public async Task Approve_AlreadyApprovedExpense_Returns422()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await managerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Approved);
    }

    [Fact]
    public async Task Approve_CancelledExpense_Returns422()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Cancelled);

        var response = await managerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Cancelled);
    }

    [Fact]
    public async Task Reject_DraftExpense_Returns422()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Draft");

        var response = await managerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reject", RejectBody("Comment"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Draft);
    }

    [Fact]
    public async Task Reject_ReimbursedExpense_Returns422()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Reimbursed);

        var response = await managerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reject", RejectBody("Comment"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Reimbursed);
    }

    // ---- Rejected expenses are terminal ----

    [Fact]
    public async Task Approve_RejectedExpense_Returns422StatusRemainsRejected()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Rejected);

        var response = await managerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Rejected);
    }

    // ---- Audit field population ----

    [Fact]
    public async Task Approve_Success_SetsApprovedAtAndApprovedByEmployeeId()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");

        await managerClient.PostAsync($"/api/expenses/{expenseId}/approve", null);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        Assert.Equal(managerId, expense!.ApprovedByEmployeeId);
        Assert.NotNull(expense.ApprovedAt);
        Assert.True(expense.ApprovedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Reject_Success_SetsRejectedAtRejectedByEmployeeIdAndRejectionComment()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");

        await managerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reject", RejectBody("Missing itemized receipt"));

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        Assert.Equal(managerId, expense!.RejectedByEmployeeId);
        Assert.NotNull(expense.RejectedAt);
        Assert.True(expense.RejectedAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal("Missing itemized receipt", expense.RejectionComment);
    }

    [Fact]
    public async Task Approve_ClientSuppliedAuditFields_HaveNoEffect()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");

        var response = await managerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/approve", new
        {
            ApprovedAt = "2020-01-01T00:00:00Z",
            ApprovedByEmployeeId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        Assert.Equal(managerId, expense!.ApprovedByEmployeeId);
        Assert.True(expense.ApprovedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Reject_ClientSuppliedAuditFields_HaveNoEffect()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var expenseId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");

        var response = await managerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/reject", new
        {
            RejectionComment = "Genuine comment",
            RejectedAt = "2020-01-01T00:00:00Z",
            RejectedByEmployeeId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        Assert.Equal(managerId, expense!.RejectedByEmployeeId);
        Assert.True(expense.RejectedAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal("Genuine comment", expense.RejectionComment);
    }

    // ---- Helpers ----

    private static object RejectBody(string rejectionComment) => new { RejectionComment = rejectionComment };

    private static ExpenseRequestBody CreateExpenseBody(Guid attachmentId, string action) => new(
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        "Travel",
        100.00m,
        "INR",
        "Integration test expense",
        attachmentId,
        action);

    private async Task<Guid> CreateExpenseAsync(HttpClient client, Guid attachmentId, string action)
    {
        var response = await client.PostAsJsonAsync("/api/expenses", CreateExpenseBody(attachmentId, action));
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
        var multipart = new MultipartFormDataContent { { fileContent, "file", "et010-test-receipt.pdf" } };

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
