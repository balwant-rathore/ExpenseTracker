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

public class ExpenseSearchTests : IAsyncLifetime
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
    public async Task Search_FinanceNoFilters_Returns200PagedResult()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "Travel");

        var response = await financeClient.PostAsJsonAsync("/api/expenses/search", new { });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("items", out _));
        Assert.True(json.RootElement.TryGetProperty("page", out _));
        Assert.True(json.RootElement.TryGetProperty("pageSize", out _));
        Assert.True(json.RootElement.TryGetProperty("totalRecords", out _));
    }

    [Theory]
    [InlineData(EmployeeRole.Employee)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.ComplianceOfficer)]
    public async Task Search_NonFinanceRole_Returns403(EmployeeRole role)
    {
        var (callerClient, _) = await CreateAuthorizedClientAsync(role);

        var response = await callerClient.PostAsJsonAsync("/api/expenses/search", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Search_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/expenses/search", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_NoStatusFilter_ExcludesDraftExpenses()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var draftExpenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Draft", "Travel");

        var response = await financeClient.PostAsJsonAsync("/api/expenses/search", new { });
        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);
        var ids = json.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.DoesNotContain(draftExpenseId, ids);
    }

    [Fact]
    public async Task Search_ExplicitStatusDraft_ReturnsEmptyResult()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        await CreateExpenseAsync(employeeClient, attachmentId, "Draft", "Travel");

        var response = await financeClient.PostAsJsonAsync("/api/expenses/search", new { Status = "Draft" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(0, json.RootElement.GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task Search_ExpenseNumberFilter_MatchesExactly()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "Travel");
        var expenseNumber = await GetExpenseNumberAsync(expenseId);

        var response = await financeClient.PostAsJsonAsync("/api/expenses/search", new { ExpenseNumber = expenseNumber });
        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal(expenseId, items[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Search_EmployeeNameFilter_MatchesSubstringCaseInsensitively()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, employeeId) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, firstName: "Raj", lastName: "Malhotra");
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit", "Travel");

        var response = await financeClient.PostAsJsonAsync("/api/expenses/search", new { EmployeeName = "raj" });
        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);
        var ids = json.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(expenseId, ids);
        _ = employeeId;
    }

    [Fact]
    public async Task Search_CategoryAndDateRangeFilters_CombineWithAnd()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);

        var inRangeTravelAttachment = await UploadAttachmentAsync(employeeClient);
        var inRangeTravelId = await CreateExpenseAsync(employeeClient, inRangeTravelAttachment, "Submit", "Travel");
        await SetExpenseCreatedAtAsync(inRangeTravelId, new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc));

        var outOfRangeTravelAttachment = await UploadAttachmentAsync(employeeClient);
        var outOfRangeTravelId = await CreateExpenseAsync(employeeClient, outOfRangeTravelAttachment, "Submit", "Travel");
        await SetExpenseCreatedAtAsync(outOfRangeTravelId, new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc));

        var inRangeMealsAttachment = await UploadAttachmentAsync(employeeClient);
        var inRangeMealsId = await CreateExpenseAsync(employeeClient, inRangeMealsAttachment, "Submit", "Meals");
        await SetExpenseCreatedAtAsync(inRangeMealsId, new DateTime(2026, 7, 16, 0, 0, 0, DateTimeKind.Utc));

        var response = await financeClient.PostAsJsonAsync("/api/expenses/search", new
        {
            Category = "Travel",
            FromDate = "2026-07-01",
            ToDate = "2026-07-31",
        });
        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);
        var ids = json.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(inRangeTravelId, ids);
        Assert.DoesNotContain(outOfRangeTravelId, ids);
        Assert.DoesNotContain(inRangeMealsId, ids);
    }

    [Fact]
    public async Task Search_ValidCustomPagingAndSorting_IsHonored()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var lowAttachment = await UploadAttachmentAsync(employeeClient);
        var lowId = await CreateExpenseAsync(employeeClient, lowAttachment, "Submit", "Travel", amount: 50m);
        var highAttachment = await UploadAttachmentAsync(employeeClient);
        var highId = await CreateExpenseAsync(employeeClient, highAttachment, "Submit", "Travel", amount: 5000m);

        var response = await financeClient.PostAsJsonAsync("/api/expenses/search", new
        {
            Page = 1,
            PageSize = 100,
            SortBy = "amount",
            SortDirection = "asc",
        });
        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);
        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(100, json.RootElement.GetProperty("pageSize").GetInt32());
        var ids = json.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.True(ids.IndexOf(lowId) < ids.IndexOf(highId));
    }

    // ---- Helpers ----

    private static ExpenseRequestBody CreateExpenseBody(Guid attachmentId, string action, string category, decimal amount) => new(
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        category,
        amount,
        "INR",
        "Integration test expense",
        attachmentId,
        action);

    private async Task<Guid> CreateExpenseAsync(HttpClient client, Guid attachmentId, string action, string category, decimal amount = 100.00m)
    {
        var response = await client.PostAsJsonAsync("/api/expenses", CreateExpenseBody(attachmentId, action, category, amount));
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("expense").GetProperty("id").GetGuid();
    }

    private async Task<string> GetExpenseNumberAsync(Guid expenseId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        return expense!.ExpenseNumber;
    }

    private async Task SetExpenseCreatedAtAsync(Guid expenseId, DateTime createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        expense!.CreatedAt = createdAt;
        await dbContext.SaveChangesAsync();
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

    private async Task<(HttpClient Client, Guid EmployeeId)> CreateAuthorizedClientAsync(
        EmployeeRole role, Guid? managerId = null, string firstName = "Test", string lastName = "User")
    {
        var (userId, employeeId) = await CreateEmployeeAndUserAsync(role, managerId, firstName, lastName);
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

    private async Task<(Guid UserId, Guid EmployeeId)> CreateEmployeeAndUserAsync(
        EmployeeRole role, Guid? managerId, string firstName, string lastName)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..9];
        var employee = new Employee
        {
            EmployeeId = Guid.NewGuid(),
            EmployeeNumber = $"IT-{uniqueSuffix}",
            FirstName = firstName,
            LastName = lastName,
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
