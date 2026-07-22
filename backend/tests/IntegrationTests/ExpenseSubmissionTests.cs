using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Auth;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

public class ExpenseSubmissionTests : IAsyncLifetime
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
    public async Task Create_DraftAction_Returns201WithDraftStatus()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);

        var response = await client.PostAsJsonAsync("/api/expenses", CreateExpenseBody(attachmentId, "Draft"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        var expenseElement = json.RootElement.GetProperty("expense");
        Assert.Equal("Draft", expenseElement.GetProperty("status").GetString());
        // ADR-0020: receiptAttachmentId is a plain scalar on Expense, always populated
        // regardless of whether the Employee navigation was Include()'d.
        Assert.Equal(attachmentId, expenseElement.GetProperty("receiptAttachmentId").GetGuid());
    }

    [Fact]
    public async Task Create_SubmitAction_Returns201WithSubmittedStatus()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);

        var response = await client.PostAsJsonAsync("/api/expenses", CreateExpenseBody(attachmentId, "Submit"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        var expenseElement = json.RootElement.GetProperty("expense");
        Assert.Equal("Submitted", expenseElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, expenseElement.GetProperty("submittedAt").ValueKind);
        Assert.Equal(attachmentId, expenseElement.GetProperty("receiptAttachmentId").GetGuid());
    }

    [Fact]
    public async Task Create_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/expenses", CreateExpenseBody(Guid.NewGuid(), "Draft"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(EmployeeRole.Finance)]
    [InlineData(EmployeeRole.ComplianceOfficer)]
    public async Task Create_RoleOutsideEmployeeOrManager_Returns403(EmployeeRole role)
    {
        var (client, _) = await CreateAuthorizedClientAsync(role);

        var response = await client.PostAsJsonAsync("/api/expenses", CreateExpenseBody(Guid.NewGuid(), "Draft"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_ClientSuppliedExpenseNumber_IsIgnored()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var body = CreateExpenseBody(attachmentId, "Draft");

        var response = await client.PostAsJsonAsync("/api/expenses", new
        {
            body.ExpenseDate,
            body.Category,
            body.Amount,
            body.Currency,
            body.Description,
            body.ReceiptAttachmentId,
            body.Action,
            ExpenseNumber = "EXP-HACKED-9999",
        });
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(responseBody);
        Assert.NotEqual("EXP-HACKED-9999", json.RootElement.GetProperty("expense").GetProperty("expenseNumber").GetString());
    }

    [Fact]
    public async Task Create_RequestBodyGenuinelyOmitsDescriptionKey_Returns400StandardEnvelope()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);

        var rawJson = $$"""
            {
                "expenseDate": "{{DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}}",
                "category": "Travel",
                "amount": 100.00,
                "currency": "INR",
                "receiptAttachmentId": "{{attachmentId}}",
                "action": "Draft"
            }
            """;
        var response = await client.PostAsync("/api/expenses", new StringContent(rawJson, Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("VALIDATION_ERROR", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Create_ClientSuppliedAuditFields_HaveNoEffect()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var body = CreateExpenseBody(attachmentId, "Draft");

        var response = await client.PostAsJsonAsync("/api/expenses", new
        {
            body.ExpenseDate,
            body.Category,
            body.Amount,
            body.Currency,
            body.Description,
            body.ReceiptAttachmentId,
            body.Action,
            SubmittedAt = "2020-01-01T00:00:00Z",
            ApprovedAt = "2020-01-01T00:00:00Z",
        });
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(responseBody);
        var expenseElement = json.RootElement.GetProperty("expense");
        Assert.Equal("Draft", expenseElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, expenseElement.GetProperty("submittedAt").ValueKind);
    }

    [Fact]
    public async Task Submit_OwnerOnExistingDraft_Returns200WithSubmittedStatus()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var draftId = await CreateExpenseAsync(client, attachmentId, "Draft");

        var response = await client.PostAsync($"/api/expenses/{draftId}/submit", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Submitted", json.RootElement.GetProperty("expense").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Submit_NonOwner_Returns403()
    {
        var (ownerClient, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(ownerClient);
        var draftId = await CreateExpenseAsync(ownerClient, attachmentId, "Draft");

        var (otherClient, _) = await CreateAuthorizedEmployeeClientAsync();

        var response = await otherClient.PostAsync($"/api/expenses/{draftId}/submit", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Submit_AlreadySubmittedExpense_Returns422_StatusUnchanged()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var submittedId = await CreateExpenseAsync(client, attachmentId, "Submit");

        var response = await client.PostAsync($"/api/expenses/{submittedId}/submit", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(submittedId);
        Assert.Equal(ExpenseStatus.Submitted, expense!.Status);
    }

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

    private static async Task<Guid> UploadAttachmentAsync(HttpClient client)
    {
        var fileContent = new ByteArrayContent(new byte[1024]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var multipart = new MultipartFormDataContent { { fileContent, "file", "et007-test-receipt.pdf" } };

        var response = await client.PostAsync("/api/attachments", multipart);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("attachmentId").GetGuid();
    }

    private Task<(HttpClient Client, Guid EmployeeId)> CreateAuthorizedEmployeeClientAsync() =>
        CreateAuthorizedClientAsync(EmployeeRole.Employee);

    private async Task<(HttpClient Client, Guid EmployeeId)> CreateAuthorizedClientAsync(EmployeeRole role)
    {
        var (userId, employeeId) = await CreateEmployeeAndUserAsync(role);
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

    private async Task<(Guid UserId, Guid EmployeeId)> CreateEmployeeAndUserAsync(EmployeeRole role)
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
