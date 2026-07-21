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

public class ExpenseMaintenanceTests : IAsyncLifetime
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

    // ---- GetById ----

    [Fact]
    public async Task GetById_Owner_Returns200WithExpenseData()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var expenseId = await CreateExpenseAsync(client, attachmentId, "Draft");

        var response = await client.GetAsync($"/api/expenses/{expenseId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(expenseId, json.RootElement.GetProperty("expense").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task GetById_NonOwner_Returns403()
    {
        var (ownerClient, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(ownerClient);
        var expenseId = await CreateExpenseAsync(ownerClient, attachmentId, "Draft");

        var (otherClient, _) = await CreateAuthorizedEmployeeClientAsync();
        var response = await otherClient.GetAsync($"/api/expenses/{expenseId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonexistentId_Returns404()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();

        var response = await client.GetAsync($"/api/expenses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/expenses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_OwnerOnDraft_Returns200WithUpdatedFields()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var expenseId = await CreateExpenseAsync(client, attachmentId, "Draft");

        var response = await client.PutAsJsonAsync($"/api/expenses/{expenseId}", UpdateExpenseBody(attachmentId, description: "Updated description", amount: 250.00m));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        var expenseElement = json.RootElement.GetProperty("expense");
        Assert.Equal("Updated description", expenseElement.GetProperty("description").GetString());
        Assert.Equal(250.00m, expenseElement.GetProperty("amount").GetDecimal());
        Assert.Equal("Draft", expenseElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Update_OwnerOnSubmitted_Returns200_StatusRemainsSubmitted()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var expenseId = await CreateExpenseAsync(client, attachmentId, "Submit");

        var response = await client.PutAsJsonAsync($"/api/expenses/{expenseId}", UpdateExpenseBody(attachmentId, description: "Updated submitted expense"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Submitted", json.RootElement.GetProperty("expense").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Update_NonOwner_Returns403()
    {
        var (ownerClient, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(ownerClient);
        var expenseId = await CreateExpenseAsync(ownerClient, attachmentId, "Draft");

        var (otherClient, _) = await CreateAuthorizedEmployeeClientAsync();
        var response = await otherClient.PutAsJsonAsync($"/api/expenses/{expenseId}", UpdateExpenseBody(attachmentId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_NonexistentId_Returns404()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);

        var response = await client.PutAsJsonAsync($"/api/expenses/{Guid.NewGuid()}", UpdateExpenseBody(attachmentId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/expenses/{Guid.NewGuid()}", UpdateExpenseBody(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_ClientSuppliedStatusAndAuditFields_HaveNoEffect()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var expenseId = await CreateExpenseAsync(client, attachmentId, "Draft");
        var updateBody = UpdateExpenseBody(attachmentId);

        var response = await client.PutAsJsonAsync($"/api/expenses/{expenseId}", new
        {
            updateBody.ExpenseDate,
            updateBody.Category,
            updateBody.Amount,
            updateBody.Currency,
            updateBody.Description,
            updateBody.ReceiptAttachmentId,
            Status = "Approved",
            SubmittedAt = "2020-01-01T00:00:00Z",
            ApprovedAt = "2020-01-01T00:00:00Z",
        });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Draft", json.RootElement.GetProperty("expense").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Update_ApprovedExpense_Returns422()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var expenseId = await CreateExpenseAsync(client, attachmentId, "Submit");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var response = await client.PutAsJsonAsync($"/api/expenses/{expenseId}", UpdateExpenseBody(attachmentId));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
    }

    [Fact]
    public async Task Update_InvalidCategory_Returns400WithFieldsEntry()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var expenseId = await CreateExpenseAsync(client, attachmentId, "Draft");
        var updateBody = UpdateExpenseBody(attachmentId);

        var response = await client.PutAsJsonAsync($"/api/expenses/{expenseId}", new
        {
            updateBody.ExpenseDate,
            Category = "NotARealCategory",
            updateBody.Amount,
            updateBody.Currency,
            updateBody.Description,
            updateBody.ReceiptAttachmentId,
        });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        var fields = json.RootElement.GetProperty("error").GetProperty("fields").EnumerateArray().Select(f => f.GetString()).ToList();
        Assert.Contains("Category", fields);
    }

    [Fact]
    public async Task Update_AttachmentSwap_Returns200WithNewAttachment()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var originalAttachmentId = await UploadAttachmentAsync(client);
        var expenseId = await CreateExpenseAsync(client, originalAttachmentId, "Draft");
        var newAttachmentId = await UploadAttachmentAsync(client);

        var response = await client.PutAsJsonAsync($"/api/expenses/{expenseId}", UpdateExpenseBody(newAttachmentId));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        Assert.Equal(newAttachmentId, expense!.AttachmentId);
    }

    [Fact]
    public async Task Update_AttachmentAlreadyLinkedToDifferentExpense_Returns422()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var expenseId = await CreateExpenseAsync(client, attachmentId, "Draft");

        var otherAttachmentId = await UploadAttachmentAsync(client);
        await CreateExpenseAsync(client, otherAttachmentId, "Draft");

        var response = await client.PutAsJsonAsync($"/api/expenses/{expenseId}", UpdateExpenseBody(otherAttachmentId));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);
    }

    // ---- Cancel ----

    [Fact]
    public async Task Cancel_OwnerOnDraft_Returns200StatusCancelled()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var expenseId = await CreateExpenseAsync(client, attachmentId, "Draft");

        var response = await client.PostAsync($"/api/expenses/{expenseId}/cancel", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Cancelled", json.RootElement.GetProperty("expense").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cancel_OwnerOnSubmitted_Returns200StatusCancelled()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var expenseId = await CreateExpenseAsync(client, attachmentId, "Submit");

        var response = await client.PostAsync($"/api/expenses/{expenseId}/cancel", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Cancelled", json.RootElement.GetProperty("expense").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cancel_NonOwner_Returns403()
    {
        var (ownerClient, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(ownerClient);
        var expenseId = await CreateExpenseAsync(ownerClient, attachmentId, "Draft");

        var (otherClient, _) = await CreateAuthorizedEmployeeClientAsync();
        var response = await otherClient.PostAsync($"/api/expenses/{expenseId}/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_NonexistentId_Returns404()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();

        var response = await client.PostAsync($"/api/expenses/{Guid.NewGuid()}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/expenses/{Guid.NewGuid()}/cancel", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelledExpense_Returns422StatusUnchanged()
    {
        var (client, _) = await CreateAuthorizedEmployeeClientAsync();
        var attachmentId = await UploadAttachmentAsync(client);
        var expenseId = await CreateExpenseAsync(client, attachmentId, "Draft");
        await client.PostAsync($"/api/expenses/{expenseId}/cancel", null);

        var response = await client.PostAsync($"/api/expenses/{expenseId}/cancel", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", body);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        Assert.Equal(ExpenseStatus.Cancelled, expense!.Status);
    }

    // ---- Helpers ----

    private static UpdateExpenseRequestBody UpdateExpenseBody(
        Guid attachmentId, decimal amount = 100.00m, string? description = null) => new(
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        "Travel",
        amount,
        "INR",
        description ?? "Integration test expense",
        attachmentId);

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

    private static async Task<Guid> UploadAttachmentAsync(HttpClient client)
    {
        var fileContent = new ByteArrayContent(new byte[1024]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var multipart = new MultipartFormDataContent { { fileContent, "file", "et008-test-receipt.pdf" } };

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

    private sealed record UpdateExpenseRequestBody(
        string ExpenseDate,
        string Category,
        decimal Amount,
        string Currency,
        string Description,
        Guid ReceiptAttachmentId);
}
