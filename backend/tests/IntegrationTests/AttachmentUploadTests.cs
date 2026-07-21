using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.Auth;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

public class AttachmentUploadTests : IAsyncLifetime
{
    private readonly AttachmentFunctionalWebApplicationFactory _factory = new();
    private readonly List<Guid> _employeeIdsToCleanUp = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var userIds = await dbContext.Users
                .Where(u => _employeeIdsToCleanUp.Contains(u.EmployeeId))
                .Select(u => u.Id)
                .ToListAsync();

            dbContext.Attachments.RemoveRange(dbContext.Attachments.Where(a => a.OriginalFileName.StartsWith("et006-test-")));
            await dbContext.SaveChangesAsync();

            dbContext.Users.RemoveRange(dbContext.Users.Where(u => _employeeIdsToCleanUp.Contains(u.EmployeeId)));
            await dbContext.SaveChangesAsync();

            dbContext.Employees.RemoveRange(dbContext.Employees.Where(e => _employeeIdsToCleanUp.Contains(e.EmployeeId)));
            await dbContext.SaveChangesAsync();

            _ = userIds;
        }

        _factory.Dispose();
    }

    [Fact]
    public async Task ValidUpload_ByEmployee_Returns201WithAttachmentId()
    {
        var (userId, _) = await CreateEmployeeAndUserAsync(EmployeeRole.Employee);
        var client = CreateAuthorizedClient(GenerateToken(userId));

        var response = await client.PostAsync("/api/attachments", CreateMultipartContent("et006-test-receipt.pdf", "application/pdf", 1024));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        var attachmentId = json.RootElement.GetProperty("attachmentId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attachment = await dbContext.Attachments.FindAsync(attachmentId);
        Assert.NotNull(attachment);
        Assert.Null(attachment!.Expense);
    }

    [Fact]
    public async Task ValidUpload_RecordsUploadedByEmployeeId_MatchingTheCaller()
    {
        var (userId, employeeId) = await CreateEmployeeAndUserAsync(EmployeeRole.Employee);
        var client = CreateAuthorizedClient(GenerateToken(userId));

        var response = await client.PostAsync("/api/attachments", CreateMultipartContent("et006-test-uploader.pdf", "application/pdf", 1024));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        var attachmentId = json.RootElement.GetProperty("attachmentId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attachment = await dbContext.Attachments.FindAsync(attachmentId);
        Assert.NotNull(attachment);
        Assert.Equal(employeeId, attachment!.UploadedByEmployeeId);
    }

    [Fact]
    public async Task NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/attachments", CreateMultipartContent("et006-test-receipt.pdf", "application/pdf", 1024));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(EmployeeRole.Finance)]
    [InlineData(EmployeeRole.ComplianceOfficer)]
    public async Task RoleOutsideEmployeeOrManager_Returns403(EmployeeRole role)
    {
        var (userId, _) = await CreateEmployeeAndUserAsync(role);
        var client = CreateAuthorizedClient(GenerateToken(userId));

        var response = await client.PostAsync("/api/attachments", CreateMultipartContent("et006-test-receipt.pdf", "application/pdf", 1024));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DisallowedFileType_Returns400ValidationError_NoRowCreated()
    {
        var (userId, _) = await CreateEmployeeAndUserAsync(EmployeeRole.Employee);
        var client = CreateAuthorizedClient(GenerateToken(userId));

        var response = await client.PostAsync("/api/attachments", CreateMultipartContent("et006-test-receipt.docx", "application/msword", 1024));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VALIDATION_ERROR", body);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await dbContext.Attachments.AnyAsync(a => a.OriginalFileName == "et006-test-receipt.docx"));
    }

    [Fact]
    public async Task OversizedFile_Returns400ValidationError_NoRowCreated()
    {
        var (userId, _) = await CreateEmployeeAndUserAsync(EmployeeRole.Employee);
        var client = CreateAuthorizedClient(GenerateToken(userId));

        var response = await client.PostAsync(
            "/api/attachments",
            CreateMultipartContent("et006-test-oversized.pdf", "application/pdf", 10 * 1024 * 1024 + 1));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VALIDATION_ERROR", body);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await dbContext.Attachments.AnyAsync(a => a.OriginalFileName == "et006-test-oversized.pdf"));
    }

    private static MultipartFormDataContent CreateMultipartContent(string fileName, string contentType, int sizeBytes)
    {
        var fileContent = new ByteArrayContent(new byte[sizeBytes]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var multipart = new MultipartFormDataContent { { fileContent, "file", fileName } };
        return multipart;
    }

    private HttpClient CreateAuthorizedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
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
}
