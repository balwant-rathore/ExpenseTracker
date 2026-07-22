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

public class AttachmentDownloadTests : IAsyncLifetime
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

    // Scenario: Authorized caller downloads an existing attachment
    [Fact]
    public async Task Download_Owner_Returns200WithFileContent()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(client);
        await CreateExpenseAsync(client, attachmentId, "Draft");

        var response = await client.GetAsync($"/api/attachments/{attachmentId}");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1024, bytes.Length);
    }

    // Scenario: Unauthenticated request is rejected
    [Fact]
    public async Task Download_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/attachments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Scenario: Nonexistent attachment id
    [Fact]
    public async Task Download_NonexistentAttachment_Returns404()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);

        var response = await client.GetAsync($"/api/attachments/{Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("RESOURCE_NOT_FOUND", body);
    }

    // Scenario: Owner can download their own expense's attachment regardless of status
    [Fact]
    public async Task Download_OwnerDraftExpense_Returns200()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(client);
        await CreateExpenseAsync(client, attachmentId, "Draft");

        var response = await client.GetAsync($"/api/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Scenario: Manager can download a direct report's non-Draft expense's attachment
    [Fact]
    public async Task Download_ManagerDirectReportsNonDraftExpense_Returns200()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        await CreateExpenseAsync(reportClient, attachmentId, "Submit");

        var response = await managerClient.GetAsync($"/api/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Scenario: Manager cannot download an indirect report's attachment
    [Fact]
    public async Task Download_ManagerIndirectReportsExpense_Returns403()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (_, directReportId) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var (indirectReportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, directReportId);
        var attachmentId = await UploadAttachmentAsync(indirectReportClient);
        await CreateExpenseAsync(indirectReportClient, attachmentId, "Submit");

        var response = await managerClient.GetAsync($"/api/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Scenario: Finance can download any non-Draft expense's attachment
    [Fact]
    public async Task Download_FinanceAnyNonDraftExpense_Returns200()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        await CreateExpenseAsync(employeeClient, attachmentId, "Submit");

        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var response = await financeClient.GetAsync($"/api/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Scenario: Compliance Officer can download an Approved ClientEntertainment expense's attachment
    [Fact]
    public async Task Download_ComplianceApprovedClientEntertainmentExpense_Returns200()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");
        await SetExpenseCategoryAsync(expenseId, ExpenseCategory.ClientEntertainment);
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var response = await complianceClient.GetAsync($"/api/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Scenario: Compliance Officer can download a ComplianceApproved ClientEntertainment expense's attachment
    [Fact]
    public async Task Download_ComplianceApprovedStatusClientEntertainmentExpense_Returns200()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");
        await SetExpenseCategoryAsync(expenseId, ExpenseCategory.ClientEntertainment);
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.ComplianceApproved);

        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var response = await complianceClient.GetAsync($"/api/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Scenario: Compliance Officer is rejected for a non-ClientEntertainment expense's attachment
    [Fact]
    public async Task Download_ComplianceNonEligibleExpense_Returns403()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var response = await complianceClient.GetAsync($"/api/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Scenario: Compliance Officer is rejected for a Reimbursed ClientEntertainment expense's attachment
    [Fact]
    public async Task Download_ComplianceReimbursedClientEntertainmentExpense_Returns403()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");
        await SetExpenseCategoryAsync(expenseId, ExpenseCategory.ClientEntertainment);
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Reimbursed);

        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var response = await complianceClient.GetAsync($"/api/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Scenario: Draft expense's attachment is not visible to a non-owner Manager
    [Fact]
    public async Task Download_ManagerDirectReportsDraftExpense_Returns403()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        await CreateExpenseAsync(reportClient, attachmentId, "Draft");

        var response = await managerClient.GetAsync($"/api/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Scenario: PDF attachment downloads with its stored Content-Type and inline disposition
    [Fact]
    public async Task Download_PdfAttachment_ReturnsStoredContentTypeAndInlineDisposition()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(client, contentType: "application/pdf", fileName: "receipt.pdf");
        await CreateExpenseAsync(client, attachmentId, "Draft");

        var response = await client.GetAsync($"/api/attachments/{attachmentId}");

        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", response.Content.Headers.ContentDisposition?.DispositionType);
    }

    // Scenario: Image attachment preserves its OriginalFileName in Content-Disposition
    [Fact]
    public async Task Download_ImageAttachment_PreservesOriginalFileNameInContentDisposition()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(client, contentType: "image/jpeg", fileName: "my receipt.jpg");
        await CreateExpenseAsync(client, attachmentId, "Draft");

        var response = await client.GetAsync($"/api/attachments/{attachmentId}");

        Assert.Equal("my receipt.jpg", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    // ---- Helpers ----

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

    private async Task SetExpenseCategoryAsync(Guid expenseId, ExpenseCategory category)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        expense!.Category = category;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> UploadAttachmentAsync(
        HttpClient client, string contentType = "application/pdf", string fileName = "et019-test-receipt.pdf")
    {
        var fileContent = new ByteArrayContent(new byte[1024]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var multipart = new MultipartFormDataContent { { fileContent, "file", fileName } };

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
