using Domain.Entities;
using Domain.Enums;
using Domain.Notifications;
using Domain.Repositories;
using Infrastructure.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UnitTests.Infrastructure.Notifications;

public class HtmlNotificationServiceTests
{
    [Fact]
    public async Task NotifyAsync_Submitted_ResolvesOwnerAndManager()
    {
        var manager = CreateEmployee(role: EmployeeRole.Manager, email: "manager@example.com");
        var owner = CreateEmployee(email: "owner@example.com", manager: manager);
        var expense = CreateExpense(owner.EmployeeId);
        var employeeRepository = new FakeEmployeeRepository([owner, manager]);
        var logWriter = new FakeNotificationLogWriter();
        var service = CreateService(employeeRepository, logWriter);

        await service.NotifyAsync(NotificationEvent.Submitted, expense, CancellationToken.None);

        var entry = Assert.Single(logWriter.Entries);
        Assert.Contains("owner@example.com", entry);
        Assert.Contains("manager@example.com", entry);
    }

    [Fact]
    public async Task NotifyAsync_Submitted_OwnerHasNoManager_OmitsManagerCc()
    {
        var owner = CreateEmployee(email: "owner@example.com");
        var expense = CreateExpense(owner.EmployeeId);
        var employeeRepository = new FakeEmployeeRepository([owner]);
        var logWriter = new FakeNotificationLogWriter();
        var service = CreateService(employeeRepository, logWriter);

        await service.NotifyAsync(NotificationEvent.Submitted, expense, CancellationToken.None);

        var entry = Assert.Single(logWriter.Entries);
        Assert.Contains("<strong>CC:</strong> </p>", entry);
    }

    [Fact]
    public async Task NotifyAsync_Approved_CcsManagerAndEveryActiveFinanceEmployee()
    {
        var manager = CreateEmployee(role: EmployeeRole.Manager, email: "manager@example.com");
        var owner = CreateEmployee(email: "owner@example.com", manager: manager);
        var finance1 = CreateEmployee(role: EmployeeRole.Finance, email: "finance1@example.com");
        var finance2 = CreateEmployee(role: EmployeeRole.Finance, email: "finance2@example.com");
        var inactiveFinance = CreateEmployee(role: EmployeeRole.Finance, email: "finance3@example.com", isActive: false);
        var approver = CreateEmployee(role: EmployeeRole.Manager, email: "approver@example.com");
        var expense = CreateExpense(owner.EmployeeId);
        expense.ApprovedAt = DateTime.UtcNow;
        expense.ApprovedByEmployeeId = approver.EmployeeId;
        var employeeRepository = new FakeEmployeeRepository([owner, manager, finance1, finance2, inactiveFinance, approver]);
        var logWriter = new FakeNotificationLogWriter();
        var service = CreateService(employeeRepository, logWriter);

        await service.NotifyAsync(NotificationEvent.Approved, expense, CancellationToken.None);

        var entry = Assert.Single(logWriter.Entries);
        Assert.Contains("manager@example.com", entry);
        Assert.Contains("finance1@example.com", entry);
        Assert.Contains("finance2@example.com", entry);
        Assert.DoesNotContain("finance3@example.com", entry);
    }

    [Fact]
    public async Task NotifyAsync_Rejected_HasEmptyCcList()
    {
        var manager = CreateEmployee(role: EmployeeRole.Manager, email: "manager@example.com");
        var owner = CreateEmployee(email: "owner@example.com", manager: manager);
        var rejector = CreateEmployee(role: EmployeeRole.Manager, email: "rejector@example.com");
        var expense = CreateExpense(owner.EmployeeId);
        expense.RejectedAt = DateTime.UtcNow;
        expense.RejectedByEmployeeId = rejector.EmployeeId;
        expense.RejectionComment = "Missing receipt";
        var employeeRepository = new FakeEmployeeRepository([owner, manager, rejector]);
        var logWriter = new FakeNotificationLogWriter();
        var service = CreateService(employeeRepository, logWriter);

        await service.NotifyAsync(NotificationEvent.Rejected, expense, CancellationToken.None);

        var entry = Assert.Single(logWriter.Entries);
        Assert.Contains("<strong>CC:</strong> </p>", entry);
        Assert.DoesNotContain("manager@example.com", entry);
    }

    [Fact]
    public async Task NotifyAsync_ComplianceApproved_CcsManagerAndFinance()
    {
        var manager = CreateEmployee(role: EmployeeRole.Manager, email: "manager@example.com");
        var owner = CreateEmployee(email: "owner@example.com", manager: manager);
        var finance = CreateEmployee(role: EmployeeRole.Finance, email: "finance@example.com");
        var complianceOfficer = CreateEmployee(role: EmployeeRole.ComplianceOfficer, email: "compliance@example.com");
        var expense = CreateExpense(owner.EmployeeId, category: ExpenseCategory.ClientEntertainment);
        expense.ComplianceApprovedAt = DateTime.UtcNow;
        expense.ComplianceApprovedByEmployeeId = complianceOfficer.EmployeeId;
        var employeeRepository = new FakeEmployeeRepository([owner, manager, finance, complianceOfficer]);
        var logWriter = new FakeNotificationLogWriter();
        var service = CreateService(employeeRepository, logWriter);

        await service.NotifyAsync(NotificationEvent.ComplianceApproved, expense, CancellationToken.None);

        var entry = Assert.Single(logWriter.Entries);
        Assert.Contains("manager@example.com", entry);
        Assert.Contains("finance@example.com", entry);
    }

    [Fact]
    public async Task NotifyAsync_ComplianceRejected_HasEmptyCcList_MatchingRejected()
    {
        var manager = CreateEmployee(role: EmployeeRole.Manager, email: "manager@example.com");
        var owner = CreateEmployee(email: "owner@example.com", manager: manager);
        var complianceOfficer = CreateEmployee(role: EmployeeRole.ComplianceOfficer, email: "compliance@example.com");
        var expense = CreateExpense(owner.EmployeeId, category: ExpenseCategory.ClientEntertainment);
        expense.RejectedAt = DateTime.UtcNow;
        expense.RejectedByEmployeeId = complianceOfficer.EmployeeId;
        expense.RejectionComment = "Not a valid client entertainment expense";
        var employeeRepository = new FakeEmployeeRepository([owner, manager, complianceOfficer]);
        var logWriter = new FakeNotificationLogWriter();
        var service = CreateService(employeeRepository, logWriter);

        await service.NotifyAsync(NotificationEvent.ComplianceRejected, expense, CancellationToken.None);

        var entry = Assert.Single(logWriter.Entries);
        Assert.Contains("<strong>CC:</strong> </p>", entry);
        Assert.DoesNotContain("manager@example.com", entry);
    }

    [Fact]
    public async Task NotifyAsync_LogWriterThrows_DoesNotThrowAndLogsFailure()
    {
        var owner = CreateEmployee(email: "owner@example.com");
        var expense = CreateExpense(owner.EmployeeId);
        var employeeRepository = new FakeEmployeeRepository([owner]);
        var logWriter = new FakeNotificationLogWriter { ShouldThrow = true };
        var logger = new FakeLogger<HtmlNotificationService>();
        var service = CreateService(employeeRepository, logWriter, logger);

        var exception = await Record.ExceptionAsync(
            () => service.NotifyAsync(NotificationEvent.Submitted, expense, CancellationToken.None));

        Assert.Null(exception);
        Assert.Contains(logger.Logs, l => l.Level == LogLevel.Error);
    }

    [Fact]
    public async Task NotifyAsync_OneFailedCall_DoesNotAffectSubsequentCall()
    {
        var owner = CreateEmployee(email: "owner@example.com");
        var expense = CreateExpense(owner.EmployeeId);
        var employeeRepository = new FakeEmployeeRepository([owner]);
        var logWriter = new FakeNotificationLogWriter { ShouldThrow = true };
        var service = CreateService(employeeRepository, logWriter);

        await service.NotifyAsync(NotificationEvent.Submitted, expense, CancellationToken.None);

        logWriter.ShouldThrow = false;
        await service.NotifyAsync(NotificationEvent.Submitted, expense, CancellationToken.None);

        Assert.Single(logWriter.Entries);
    }

    private static HtmlNotificationService CreateService(
        FakeEmployeeRepository employeeRepository, FakeNotificationLogWriter logWriter, ILogger<HtmlNotificationService>? logger = null) =>
        new(
            employeeRepository,
            logWriter,
            Options.Create(new NotificationOptions()),
            logger ?? new FakeLogger<HtmlNotificationService>());

    private static Employee CreateEmployee(
        EmployeeRole role = EmployeeRole.Employee, string email = "employee@example.com", Employee? manager = null, bool isActive = true) => new()
        {
            EmployeeId = Guid.NewGuid(),
            EmployeeNumber = $"EMP-{Guid.NewGuid():N}"[..12],
            FirstName = "Test",
            LastName = "Employee",
            Email = email,
            Role = role,
            ManagerId = manager?.EmployeeId,
            Manager = manager,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private static Expense CreateExpense(Guid employeeId, ExpenseCategory category = ExpenseCategory.Travel) => new()
    {
        Id = Guid.NewGuid(),
        ExpenseNumber = "EXP-TEST-0001",
        EmployeeId = employeeId,
        AttachmentId = Guid.NewGuid(),
        ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
        Category = category,
        Amount = 100m,
        Currency = "INR",
        Description = "Test expense",
        Status = ExpenseStatus.Submitted,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}

internal sealed class FakeEmployeeRepository : IEmployeeRepository
{
    private readonly List<Employee> _employees;

    public FakeEmployeeRepository(IEnumerable<Employee> employees)
    {
        _employees = [.. employees];
    }

    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_employees.FirstOrDefault(e => e.EmployeeId == id));

    public Task AddAsync(Employee entity, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Employee?> GetByIdWithManagerAsync(Guid employeeId, CancellationToken cancellationToken) =>
        Task.FromResult(_employees.FirstOrDefault(e => e.EmployeeId == employeeId));

    public Task<IReadOnlyList<Employee>> GetByRoleAsync(EmployeeRole role, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Employee>>(_employees.Where(e => e.Role == role && e.IsActive).ToList());
}

internal sealed class FakeNotificationLogWriter : INotificationLogWriter
{
    public List<string> Entries { get; } = [];

    public bool ShouldThrow { get; set; }

    public Task AppendAsync(string htmlEntry, CancellationToken cancellationToken)
    {
        if (ShouldThrow)
        {
            throw new IOException("Simulated write failure");
        }

        Entries.Add(htmlEntry);
        return Task.CompletedTask;
    }
}

internal sealed class FakeLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Logs { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Logs.Add((logLevel, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
