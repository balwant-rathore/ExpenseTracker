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

public class MonthlyReimbursementReportTests : IAsyncLifetime
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
    public async Task MonthlyReimbursement_Finance_Returns200WithMatchingRecords()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, employeeId) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Travel");
        await SetWorkflowFieldsAsync(expenseId, ExpenseStatus.Reimbursed, new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc));

        var response = await financeClient.GetAsync("/api/reports/monthly-reimbursement?year=2026&month=7");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var expenseNumber = await GetExpenseNumberAsync(expenseId);
        using var json = JsonDocument.Parse(body);
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("expenseNumber").GetString() == expenseNumber);
        _ = employeeId;
    }

    [Theory]
    [InlineData(EmployeeRole.Employee)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.ComplianceOfficer)]
    public async Task MonthlyReimbursement_NonFinanceRole_Returns403(EmployeeRole role)
    {
        var (callerClient, _) = await CreateAuthorizedClientAsync(role);

        var response = await callerClient.GetAsync("/api/reports/monthly-reimbursement?year=2026&month=7");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MonthlyReimbursement_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reports/monthly-reimbursement?year=2026&month=7");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MonthlyReimbursement_OnlyIncludesExpensesReimbursedWithinRequestedMonth()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);

        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var julyExpenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Travel");
        await SetWorkflowFieldsAsync(julyExpenseId, ExpenseStatus.Reimbursed, new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc));

        var augustAttachmentId = await UploadAttachmentAsync(employeeClient);
        var augustExpenseId = await CreateExpenseAsync(employeeClient, augustAttachmentId, "Travel");
        await SetWorkflowFieldsAsync(augustExpenseId, ExpenseStatus.Reimbursed, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc));

        var response = await financeClient.GetAsync("/api/reports/monthly-reimbursement?year=2026&month=7");
        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);
        var expenseNumbers = json.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("expenseNumber").GetString()).ToList();
        Assert.Contains(await GetExpenseNumberAsync(julyExpenseId), expenseNumbers);
        Assert.DoesNotContain(await GetExpenseNumberAsync(augustExpenseId), expenseNumbers);
    }

    [Fact]
    public async Task MonthlyReimbursement_ExcludesNonReimbursedExpensesEvenWithinMonth()
    {
        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Travel");
        await SetWorkflowFieldsAsync(expenseId, ExpenseStatus.Approved, new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc), null, null);

        var response = await financeClient.GetAsync("/api/reports/monthly-reimbursement?year=2026&month=7");
        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);
        var expenseNumbers = json.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("expenseNumber").GetString()).ToList();
        Assert.DoesNotContain(await GetExpenseNumberAsync(expenseId), expenseNumbers);
    }

    // ---- Helpers ----

    private static ExpenseRequestBody CreateExpenseBody(Guid attachmentId, string category) => new(
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        category,
        100.00m,
        "INR",
        "Integration test expense",
        attachmentId,
        "Submit");

    private async Task<Guid> CreateExpenseAsync(HttpClient client, Guid attachmentId, string category)
    {
        var response = await client.PostAsJsonAsync("/api/expenses", CreateExpenseBody(attachmentId, category));
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

    private async Task SetWorkflowFieldsAsync(Guid expenseId, ExpenseStatus status, DateTime? approvedAt, DateTime? complianceApprovedAt, DateTime? reimbursedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expense = await dbContext.Expenses.FindAsync(expenseId);
        expense!.Status = status;
        expense.ApprovedAt = approvedAt;
        expense.ComplianceApprovedAt = complianceApprovedAt;
        expense.ReimbursedAt = reimbursedAt;
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
