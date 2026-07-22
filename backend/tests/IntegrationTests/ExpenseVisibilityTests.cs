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

public class ExpenseVisibilityTests : IAsyncLifetime
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

    // ---- GET /api/expenses ----

    // Scenario: Authenticated caller receives a paged result
    [Fact]
    public async Task GetAll_AuthenticatedCaller_Returns200WithPagedShape()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);

        var response = await client.GetAsync("/api/expenses");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("items").ValueKind);
        Assert.True(json.RootElement.TryGetProperty("page", out _));
        Assert.True(json.RootElement.TryGetProperty("pageSize", out _));
        Assert.True(json.RootElement.TryGetProperty("totalRecords", out _));
    }

    // Scenario: Unauthenticated request is rejected
    [Fact]
    public async Task GetAll_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/expenses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Scenario: Employee sees only their own expenses
    [Fact]
    public async Task GetAll_Employee_SeesOnlyOwnExpenses()
    {
        var (clientA, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentA = await UploadAttachmentAsync(clientA);
        var expenseAId = await CreateExpenseAsync(clientA, attachmentA, "Draft");

        var (clientB, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentB = await UploadAttachmentAsync(clientB);
        await CreateExpenseAsync(clientB, attachmentB, "Draft");

        var ids = await GetItemIdsAsync(clientA, "/api/expenses");

        Assert.Contains(expenseAId, ids);
        Assert.Single(ids);
    }

    // ADR-0020: list items include employeeNumber, receiptAttachmentId, and
    // attachmentOriginalFileName - needed by the frontend for ownership-based action
    // visibility and attachment display. employeeNumber (not a raw Employee GUID) is what
    // the frontend compares against the authenticated User.employeeNumber, since
    // /api/auth/me never exposes the Employee GUID.
    [Fact]
    public async Task GetAll_ListItem_IncludesOwnerAndAttachmentFields()
    {
        var (client, employeeId) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(client);
        var expenseId = await CreateExpenseAsync(client, attachmentId, "Draft");

        var response = await client.GetAsync("/api/expenses");
        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);
        var item = json.RootElement.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetGuid() == expenseId);

        Assert.Equal(await GetEmployeeNumberAsync(employeeId), item.GetProperty("employeeNumber").GetString());
        Assert.Equal(attachmentId, item.GetProperty("receiptAttachmentId").GetGuid());
        Assert.Equal("et009-test-receipt.pdf", item.GetProperty("attachmentOriginalFileName").GetString());
    }

    // Scenario: Employee's own Draft expense is included
    [Fact]
    public async Task GetAll_Employee_OwnDraftExpense_IsIncluded()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(client);
        var draftId = await CreateExpenseAsync(client, attachmentId, "Draft");

        var ids = await GetItemIdsAsync(client, "/api/expenses");

        Assert.Contains(draftId, ids);
    }

    // Scenario: Manager sees their own expenses at any status
    [Fact]
    public async Task GetAll_Manager_OwnDraftExpense_IsIncluded()
    {
        var (managerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var attachmentId = await UploadAttachmentAsync(managerClient);
        var draftId = await CreateExpenseAsync(managerClient, attachmentId, "Draft");

        var ids = await GetItemIdsAsync(managerClient, "/api/expenses");

        Assert.Contains(draftId, ids);
    }

    // Scenario: Manager sees a direct report's non-Draft expense
    [Fact]
    public async Task GetAll_Manager_DirectReportsNonDraftExpense_IsIncluded()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var submittedId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");

        var ids = await GetItemIdsAsync(managerClient, "/api/expenses");

        Assert.Contains(submittedId, ids);
    }

    // Scenario: Manager does not see a direct report's Draft expense
    [Fact]
    public async Task GetAll_Manager_DirectReportsDraftExpense_IsExcluded()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var draftId = await CreateExpenseAsync(reportClient, attachmentId, "Draft");

        var ids = await GetItemIdsAsync(managerClient, "/api/expenses");

        Assert.DoesNotContain(draftId, ids);
    }

    // Scenario: Manager does not see an indirect report's expense
    [Fact]
    public async Task GetAll_Manager_IndirectReportsExpense_IsExcluded()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (_, directReportId) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var (indirectReportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, directReportId);
        var attachmentId = await UploadAttachmentAsync(indirectReportClient);
        var submittedId = await CreateExpenseAsync(indirectReportClient, attachmentId, "Submit");

        var ids = await GetItemIdsAsync(managerClient, "/api/expenses");

        Assert.DoesNotContain(submittedId, ids);
    }

    // Scenario: Manager does not see an unrelated employee's expense
    [Fact]
    public async Task GetAll_Manager_UnrelatedEmployeesExpense_IsExcluded()
    {
        var (managerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (unrelatedClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(unrelatedClient);
        var submittedId = await CreateExpenseAsync(unrelatedClient, attachmentId, "Submit");

        var ids = await GetItemIdsAsync(managerClient, "/api/expenses");

        Assert.DoesNotContain(submittedId, ids);
    }

    // Scenario: Finance sees non-Draft expenses of any employee; Finance does not see Draft expenses
    [Fact]
    public async Task GetAll_Finance_SeesNonDraft_ExcludesDraft()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var nonDraftAttachment = await UploadAttachmentAsync(employeeClient);
        var nonDraftId = await CreateExpenseAsync(employeeClient, nonDraftAttachment, "Submit");
        var draftAttachment = await UploadAttachmentAsync(employeeClient);
        var draftId = await CreateExpenseAsync(employeeClient, draftAttachment, "Draft");

        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var ids = await GetItemIdsAsync(financeClient, "/api/expenses?pageSize=100&sortBy=createdAt&sortDirection=desc");

        Assert.Contains(nonDraftId, ids);
        Assert.DoesNotContain(draftId, ids);
    }

    // Scenario: Compliance sees an Approved Client Entertainment expense;
    // Scenario: Compliance sees a Compliance Approved Client Entertainment expense
    [Fact]
    public async Task GetAll_Compliance_SeesApprovedAndComplianceApprovedClientEntertainment()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);

        var approvedAttachment = await UploadAttachmentAsync(employeeClient);
        var approvedId = await CreateExpenseAsync(employeeClient, approvedAttachment, "Submit");
        await SetExpenseCategoryAsync(approvedId, ExpenseCategory.ClientEntertainment);
        await SetExpenseStatusAsync(approvedId, ExpenseStatus.Approved);

        var complianceApprovedAttachment = await UploadAttachmentAsync(employeeClient);
        var complianceApprovedId = await CreateExpenseAsync(employeeClient, complianceApprovedAttachment, "Submit");
        await SetExpenseCategoryAsync(complianceApprovedId, ExpenseCategory.ClientEntertainment);
        await SetExpenseStatusAsync(complianceApprovedId, ExpenseStatus.ComplianceApproved);

        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var ids = await GetItemIdsAsync(complianceClient, "/api/expenses?pageSize=100&sortBy=createdAt&sortDirection=desc");

        Assert.Contains(approvedId, ids);
        Assert.Contains(complianceApprovedId, ids);
    }

    // Scenario: Compliance does not see a Reimbursed Client Entertainment expense
    [Fact]
    public async Task GetAll_Compliance_ExcludesReimbursedClientEntertainment()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");
        await SetExpenseCategoryAsync(expenseId, ExpenseCategory.ClientEntertainment);
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Reimbursed);

        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var ids = await GetItemIdsAsync(complianceClient, "/api/expenses?pageSize=100");

        Assert.DoesNotContain(expenseId, ids);
    }

    // Scenario: Compliance does not see non-Client-Entertainment expenses
    [Fact]
    public async Task GetAll_Compliance_ExcludesNonClientEntertainmentApproved()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var ids = await GetItemIdsAsync(complianceClient, "/api/expenses?pageSize=100");

        Assert.DoesNotContain(expenseId, ids);
    }

    // Scenario: Compliance does not see a Draft or Submitted Client Entertainment expense
    [Theory]
    [InlineData("Draft")]
    [InlineData("Submit")]
    public async Task GetAll_Compliance_ExcludesDraftOrSubmittedClientEntertainment(string action)
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, action);
        await SetExpenseCategoryAsync(expenseId, ExpenseCategory.ClientEntertainment);

        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var ids = await GetItemIdsAsync(complianceClient, "/api/expenses?pageSize=100");

        Assert.DoesNotContain(expenseId, ids);
    }

    // Scenario: Defaults apply when no query parameters are given
    [Fact]
    public async Task GetAll_NoQueryParameters_DefaultsApply()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);

        var response = await client.GetAsync("/api/expenses");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(20, json.RootElement.GetProperty("pageSize").GetInt32());
    }

    // Scenario: Valid custom paging and sorting is honored
    [Fact]
    public async Task GetAll_ValidCustomPagingAndSorting_IsHonored()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachment1 = await UploadAttachmentAsync(client);
        await CreateExpenseAsync(client, attachment1, "Draft", amount: 300.00m);
        var attachment2 = await UploadAttachmentAsync(client);
        await CreateExpenseAsync(client, attachment2, "Draft", amount: 50.00m);
        var attachment3 = await UploadAttachmentAsync(client);
        await CreateExpenseAsync(client, attachment3, "Draft", amount: 150.00m);

        var response = await client.GetAsync("/api/expenses?pageSize=10&sortBy=amount&sortDirection=asc");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(10, json.RootElement.GetProperty("pageSize").GetInt32());
        var amounts = json.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("amount").GetDecimal())
            .ToList();
        Assert.Equal(amounts.OrderBy(a => a), amounts);
    }

    // Scenario: Invalid pageSize is rejected
    [Fact]
    public async Task GetAll_InvalidPageSize_Returns400()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);

        var response = await client.GetAsync("/api/expenses?pageSize=15");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VALIDATION_ERROR", body);
    }

    // Scenario: Invalid sortBy is rejected
    [Fact]
    public async Task GetAll_InvalidSortBy_Returns400()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);

        var response = await client.GetAsync("/api/expenses?sortBy=bogus");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VALIDATION_ERROR", body);
    }

    // ---- status query filter (ET010) ----

    // Scenario: Manager filters to only Submitted expenses
    [Fact]
    public async Task GetAll_Manager_StatusFilterSubmitted_ReturnsOnlySubmittedFromVisibleSet()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var submittedAttachment = await UploadAttachmentAsync(reportClient);
        var submittedId = await CreateExpenseAsync(reportClient, submittedAttachment, "Submit");
        var draftAttachment = await UploadAttachmentAsync(managerClient);
        await CreateExpenseAsync(managerClient, draftAttachment, "Draft");

        var ids = await GetItemIdsAsync(managerClient, "/api/expenses?status=Submitted&pageSize=100");

        Assert.Contains(submittedId, ids);
        Assert.Single(ids);
    }

    // Scenario: Status filter never expands visibility
    [Fact]
    public async Task GetAll_StatusFilterNeverExpandsVisibility_EmployeeSeesOnlyOwnApproved()
    {
        var (employeeClient, employeeId) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var ownAttachment = await UploadAttachmentAsync(employeeClient);
        var ownExpenseId = await CreateExpenseAsync(employeeClient, ownAttachment, "Submit");
        await SetExpenseStatusAsync(ownExpenseId, ExpenseStatus.Approved);

        var (otherClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var otherAttachment = await UploadAttachmentAsync(otherClient);
        var otherExpenseId = await CreateExpenseAsync(otherClient, otherAttachment, "Submit");
        await SetExpenseStatusAsync(otherExpenseId, ExpenseStatus.Approved);

        var ids = await GetItemIdsAsync(employeeClient, "/api/expenses?status=Approved&pageSize=100");

        Assert.Contains(ownExpenseId, ids);
        Assert.DoesNotContain(otherExpenseId, ids);
    }

    // Scenario: No status filter behaves as before
    [Fact]
    public async Task GetAll_NoStatusParameter_BehavesUnchangedFromBeforeThisChange()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var draftAttachment = await UploadAttachmentAsync(client);
        var draftId = await CreateExpenseAsync(client, draftAttachment, "Draft");
        var submittedAttachment = await UploadAttachmentAsync(client);
        var submittedId = await CreateExpenseAsync(client, submittedAttachment, "Submit");

        var ids = await GetItemIdsAsync(client, "/api/expenses?pageSize=100");

        Assert.Contains(draftId, ids);
        Assert.Contains(submittedId, ids);
    }

    // Scenario: Invalid status value is rejected
    [Fact]
    public async Task GetAll_InvalidStatusValue_Returns400()
    {
        var (client, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);

        var response = await client.GetAsync("/api/expenses?status=NotARealStatus");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VALIDATION_ERROR", body);
    }

    // ---- GET /api/expenses/{id} widened visibility ----

    // Scenario: Manager retrieves their own expense
    [Fact]
    public async Task GetById_ManagerOwnExpenseAnyStatus_Returns200()
    {
        var (managerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var attachmentId = await UploadAttachmentAsync(managerClient);
        var draftId = await CreateExpenseAsync(managerClient, attachmentId, "Draft");

        var response = await managerClient.GetAsync($"/api/expenses/{draftId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Scenario: Manager retrieves a direct report's non-Draft expense
    [Fact]
    public async Task GetById_ManagerDirectReportsNonDraftExpense_Returns200()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var submittedId = await CreateExpenseAsync(reportClient, attachmentId, "Submit");

        var response = await managerClient.GetAsync($"/api/expenses/{submittedId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Scenario: Manager is rejected for a direct report's Draft expense
    [Fact]
    public async Task GetById_ManagerDirectReportsDraftExpense_Returns403()
    {
        var (managerClient, managerId) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (reportClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee, managerId);
        var attachmentId = await UploadAttachmentAsync(reportClient);
        var draftId = await CreateExpenseAsync(reportClient, attachmentId, "Draft");

        var response = await managerClient.GetAsync($"/api/expenses/{draftId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Scenario: Manager is rejected for an unrelated employee's expense
    [Fact]
    public async Task GetById_ManagerUnrelatedEmployeesExpense_Returns403()
    {
        var (managerClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Manager);
        var (unrelatedClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(unrelatedClient);
        var submittedId = await CreateExpenseAsync(unrelatedClient, attachmentId, "Submit");

        var response = await managerClient.GetAsync($"/api/expenses/{submittedId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Scenario: Finance retrieves any non-Draft expense
    [Fact]
    public async Task GetById_FinanceAnyNonDraftExpense_Returns200()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var submittedId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");

        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var response = await financeClient.GetAsync($"/api/expenses/{submittedId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Scenario: Finance is rejected for a Draft expense
    [Fact]
    public async Task GetById_FinanceDraftExpense_Returns403()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var draftId = await CreateExpenseAsync(employeeClient, attachmentId, "Draft");

        var (financeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Finance);
        var response = await financeClient.GetAsync($"/api/expenses/{draftId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Scenario: Compliance retrieves an Approved or Compliance Approved Client Entertainment expense (Approved case)
    [Fact]
    public async Task GetById_ComplianceApprovedClientEntertainmentExpense_Returns200()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");
        await SetExpenseCategoryAsync(expenseId, ExpenseCategory.ClientEntertainment);
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var response = await complianceClient.GetAsync($"/api/expenses/{expenseId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Scenario: Compliance retrieves an Approved or Compliance Approved Client Entertainment expense (ComplianceApproved case)
    [Fact]
    public async Task GetById_ComplianceComplianceApprovedClientEntertainmentExpense_Returns200()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");
        await SetExpenseCategoryAsync(expenseId, ExpenseCategory.ClientEntertainment);
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.ComplianceApproved);

        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var response = await complianceClient.GetAsync($"/api/expenses/{expenseId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Scenario: Compliance is rejected for a Reimbursed Client Entertainment expense
    [Fact]
    public async Task GetById_ComplianceReimbursedClientEntertainmentExpense_Returns403()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");
        await SetExpenseCategoryAsync(expenseId, ExpenseCategory.ClientEntertainment);
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Reimbursed);

        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var response = await complianceClient.GetAsync($"/api/expenses/{expenseId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Scenario: Compliance is rejected for a non-Client-Entertainment expense
    [Fact]
    public async Task GetById_ComplianceNonClientEntertainmentExpense_Returns403()
    {
        var (employeeClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.Employee);
        var attachmentId = await UploadAttachmentAsync(employeeClient);
        var expenseId = await CreateExpenseAsync(employeeClient, attachmentId, "Submit");
        await SetExpenseStatusAsync(expenseId, ExpenseStatus.Approved);

        var (complianceClient, _) = await CreateAuthorizedClientAsync(EmployeeRole.ComplianceOfficer);
        var response = await complianceClient.GetAsync($"/api/expenses/{expenseId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Helpers ----

    private static ExpenseRequestBody CreateExpenseBody(Guid attachmentId, string action, decimal amount) => new(
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        "Travel",
        amount,
        "INR",
        "Integration test expense",
        attachmentId,
        action);

    private async Task<Guid> CreateExpenseAsync(HttpClient client, Guid attachmentId, string action, decimal amount = 100.00m)
    {
        var response = await client.PostAsJsonAsync("/api/expenses", CreateExpenseBody(attachmentId, action, amount));
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("expense").GetProperty("id").GetGuid();
    }

    private async Task<List<Guid>> GetItemIdsAsync(HttpClient client, string requestUri)
    {
        var response = await client.GetAsync(requestUri);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();
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

    private async Task<string> GetEmployeeNumberAsync(Guid employeeId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var employee = await dbContext.Employees.FindAsync(employeeId);
        return employee!.EmployeeNumber;
    }

    private static async Task<Guid> UploadAttachmentAsync(HttpClient client)
    {
        var fileContent = new ByteArrayContent(new byte[1024]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var multipart = new MultipartFormDataContent { { fileContent, "file", "et009-test-receipt.pdf" } };

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
