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

public class ExpenseComplianceReviewTests : IAsyncLifetime
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

    // ---- Compliance Approve ----

    [Fact]
    public async Task ComplianceApprove_ApprovedClientEntertainment_Returns200ComplianceApproved()
    {
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await complianceClient.PostAsync($"/api/expenses/{expenseId}/compliance-approve", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("ComplianceApproved", json.RootElement.GetProperty("expense").GetProperty("status").GetString());
    }

    [Theory]
    [InlineData(EmployeeRole.Employee)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.Finance)]
    public async Task ComplianceApprove_NonComplianceOfficerRole_Returns403(EmployeeRole role)
    {
        var (callerClient, _) = await CreateAuthorizedClientAsync(role);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await callerClient.PostAsync($"/api/expenses/{expenseId}/compliance-approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ComplianceApprove_NonexistentId_Returns404()
    {
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);

        var response = await complianceClient.PostAsync($"/api/expenses/{Guid.NewGuid()}/compliance-approve", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ComplianceApprove_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/expenses/{Guid.NewGuid()}/compliance-approve", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Compliance Reject ----

    [Fact]
    public async Task ComplianceReject_ApprovedClientEntertainmentWithComment_Returns200Rejected()
    {
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await complianceClient.PostAsJsonAsync($"/api/expenses/{expenseId}/compliance-reject", RejectBody("Exceeds per-person entertainment limit"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Rejected", json.RootElement.GetProperty("expense").GetProperty("status").GetString());
    }

    [Theory]
    [InlineData(EmployeeRole.Employee)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.Finance)]
    public async Task ComplianceReject_NonComplianceOfficerRole_Returns403(EmployeeRole role)
    {
        var (callerClient, _) = await CreateAuthorizedClientAsync(role);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await callerClient.PostAsJsonAsync($"/api/expenses/{expenseId}/compliance-reject", RejectBody("Comment"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ComplianceReject_NonexistentId_Returns404()
    {
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);

        var response = await complianceClient.PostAsJsonAsync($"/api/expenses/{Guid.NewGuid()}/compliance-reject", RejectBody("Comment"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ComplianceReject_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/expenses/{Guid.NewGuid()}/compliance-reject", RejectBody("Comment"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Mandatory rejection comment validation ----

    [Fact]
    public async Task ComplianceReject_MissingComment_Returns400WithFieldsEntry()
    {
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await complianceClient.PostAsJsonAsync($"/api/expenses/{expenseId}/compliance-reject", new { });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        var fields = json.RootElement.GetProperty("error").GetProperty("fields").EnumerateArray().Select(f => f.GetString()).ToList();
        Assert.Contains("RejectionComment", fields);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Approved);
    }

    [Fact]
    public async Task ComplianceReject_WhitespaceOnlyComment_Returns400()
    {
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await complianceClient.PostAsJsonAsync($"/api/expenses/{expenseId}/compliance-reject", RejectBody("   "));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VALIDATION_ERROR", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Approved);
    }

    [Fact]
    public async Task ComplianceReject_CommentOverFiveHundredCharacters_Returns400()
    {
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await complianceClient.PostAsJsonAsync($"/api/expenses/{expenseId}/compliance-reject", RejectBody(new string('a', 501)));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VALIDATION_ERROR", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Approved);
    }

    // ---- Client Entertainment category restriction ----

    [Fact]
    public async Task ComplianceApprove_TravelCategoryApprovedExpense_Returns422NotClientEntertainment()
    {
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "Travel");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await complianceClient.PostAsync($"/api/expenses/{expenseId}/compliance-approve", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Approved);
    }

    [Fact]
    public async Task ComplianceReject_MealsCategoryApprovedExpense_Returns422NotClientEntertainment()
    {
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "Meals");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await complianceClient.PostAsJsonAsync($"/api/expenses/{expenseId}/compliance-reject", RejectBody("Comment"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Approved);
    }

    // ---- Approved-status precondition ----

    [Theory]
    [InlineData(ExpenseStatus.Draft)]
    [InlineData(ExpenseStatus.Submitted)]
    [InlineData(ExpenseStatus.ComplianceApproved)]
    [InlineData(ExpenseStatus.Rejected)]
    [InlineData(ExpenseStatus.Cancelled)]
    [InlineData(ExpenseStatus.Reimbursed)]
    public async Task ComplianceApprove_NonApprovedStatus_Returns422NotApprovedForCompliance(ExpenseStatus status)
    {
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, status);

        var response = await complianceClient.PostAsync($"/api/expenses/{expenseId}/compliance-approve", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, status);
    }

    [Theory]
    [InlineData(ExpenseStatus.Draft)]
    [InlineData(ExpenseStatus.Submitted)]
    [InlineData(ExpenseStatus.ComplianceApproved)]
    [InlineData(ExpenseStatus.Rejected)]
    [InlineData(ExpenseStatus.Cancelled)]
    [InlineData(ExpenseStatus.Reimbursed)]
    public async Task ComplianceReject_NonApprovedStatus_Returns422NotApprovedForCompliance(ExpenseStatus status)
    {
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, status);

        var response = await complianceClient.PostAsJsonAsync($"/api/expenses/{expenseId}/compliance-reject", RejectBody("Comment"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, status);
    }

    // ---- Rejected expenses are terminal ----

    [Fact]
    public async Task ComplianceApprove_RejectedExpense_Returns422StatusRemainsRejected()
    {
        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Rejected);

        var response = await complianceClient.PostAsync($"/api/expenses/{expenseId}/compliance-approve", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
        await AssertStatusUnchangedAsync(expenseId, ExpenseStatus.Rejected);
    }

    // ---- Audit field population ----

    [Fact]
    public async Task ComplianceApprove_Success_SetsComplianceApprovedAtAndComplianceApprovedByEmployeeId()
    {
        var (complianceClient, complianceOfficerId) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await complianceClient.PostAsync($"/api/expenses/{expenseId}/compliance-approve", null);
        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("expense").TryGetProperty("complianceApprovedAt", out var complianceApprovedAtProperty));
        Assert.False(string.IsNullOrEmpty(complianceApprovedAtProperty.GetString()));

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        Assert.Equal(complianceOfficerId, expense!.ComplianceApprovedByEmployeeId);
        Assert.NotNull(expense.ComplianceApprovedAt);
        Assert.True(expense.ComplianceApprovedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task ComplianceReject_Success_SetsRejectedAtRejectedByEmployeeIdAndRejectionComment()
    {
        var (complianceClient, complianceOfficerId) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        await complianceClient.PostAsJsonAsync($"/api/expenses/{expenseId}/compliance-reject", RejectBody("Exceeds per-person entertainment limit"));

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        Assert.Equal(complianceOfficerId, expense!.RejectedByEmployeeId);
        Assert.NotNull(expense.RejectedAt);
        Assert.True(expense.RejectedAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal("Exceeds per-person entertainment limit", expense.RejectionComment);
    }

    [Fact]
    public async Task ComplianceApprove_ClientSuppliedAuditFields_HaveNoEffect()
    {
        var (complianceClient, complianceOfficerId) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await complianceClient.PostAsJsonAsync($"/api/expenses/{expenseId}/compliance-approve", new
        {
            ComplianceApprovedAt = "2020-01-01T00:00:00Z",
            ComplianceApprovedByEmployeeId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        Assert.Equal(complianceOfficerId, expense!.ComplianceApprovedByEmployeeId);
        Assert.True(expense.ComplianceApprovedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task ComplianceReject_ClientSuppliedAuditFields_HaveNoEffect()
    {
        var (complianceClient, complianceOfficerId) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "ClientEntertainment");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await complianceClient.PostAsJsonAsync($"/api/expenses/{expenseId}/compliance-reject", new
        {
            RejectionComment = "Genuine comment",
            RejectedAt = "2020-01-01T00:00:00Z",
            RejectedByEmployeeId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        Assert.Equal(complianceOfficerId, expense!.RejectedByEmployeeId);
        Assert.True(expense.RejectedAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal("Genuine comment", expense.RejectionComment);
    }

    // ---- Helpers ----

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
        var multipart = new MultipartFormDataContent { { fileContent, "file", "et011-test-receipt.pdf" } };

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
