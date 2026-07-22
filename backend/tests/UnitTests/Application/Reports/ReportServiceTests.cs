using Application.Reports;
using Domain.Entities;
using Domain.Enums;
using Domain.Reporting;
using UnitTests.Application.Expenses;

namespace UnitTests.Application.Reports;

public class ReportServiceTests
{
    [Fact]
    public async Task GetMonthlyReimbursementAsync_ClientEntertainment_UsesComplianceApprovedAtAsApprovalDate()
    {
        var employeeId = Guid.NewGuid();
        var approvedAt = new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc);
        var complianceApprovedAt = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var reimbursedAt = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var expense = CreateReimbursedExpense(employeeId, ExpenseCategory.ClientEntertainment, approvedAt, complianceApprovedAt, reimbursedAt);
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(expense);
        var service = new ReportService(repository, new FakeMonthlyReimbursementReportGenerator(), new FakeCompanyClock());

        var records = await service.GetMonthlyReimbursementAsync(2026, 7, CancellationToken.None);

        var record = Assert.Single(records);
        Assert.Equal(complianceApprovedAt, record.ApprovalDate);
    }

    [Fact]
    public async Task GetMonthlyReimbursementAsync_NonClientEntertainment_UsesApprovedAtAsApprovalDate()
    {
        var employeeId = Guid.NewGuid();
        var approvedAt = new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc);
        var reimbursedAt = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var expense = CreateReimbursedExpense(employeeId, ExpenseCategory.Travel, approvedAt, complianceApprovedAt: null, reimbursedAt);
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(expense);
        var service = new ReportService(repository, new FakeMonthlyReimbursementReportGenerator(), new FakeCompanyClock());

        var records = await service.GetMonthlyReimbursementAsync(2026, 7, CancellationToken.None);

        var record = Assert.Single(records);
        Assert.Equal(approvedAt, record.ApprovalDate);
    }

    [Fact]
    public async Task GetMonthlyReimbursementAsync_MonthWithNoReimbursements_ReturnsEmptyList()
    {
        var repository = new FakeExpenseRepository();
        var service = new ReportService(repository, new FakeMonthlyReimbursementReportGenerator(), new FakeCompanyClock());

        var records = await service.GetMonthlyReimbursementAsync(2020, 1, CancellationToken.None);

        Assert.Empty(records);
    }

    [Fact]
    public async Task GenerateMonthlyReimbursementExcelAsync_PassesFetchedRecordsToGenerator()
    {
        var employeeId = Guid.NewGuid();
        var approvedAt = new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc);
        var reimbursedAt = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var expense = CreateReimbursedExpense(employeeId, ExpenseCategory.Travel, approvedAt, complianceApprovedAt: null, reimbursedAt);
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(expense);
        var generator = new FakeMonthlyReimbursementReportGenerator();
        var service = new ReportService(repository, generator, new FakeCompanyClock());

        var content = await service.GenerateMonthlyReimbursementExcelAsync(2026, 7, CancellationToken.None);

        var record = Assert.Single(generator.ReceivedRecords!);
        Assert.Equal(expense.ExpenseNumber, record.ExpenseNumber);
        Assert.Same(generator.ReturnValue, content);
    }

    [Fact]
    public async Task GetMonthlyReimbursementAsync_ReimbursedJustAfterLocalMonthStart_IsIncludedInCompanyLocalMonth()
    {
        var employeeId = Guid.NewGuid();
        // 2026-06-30T19:00:00Z is 2026-07-01T00:30 IST (UTC+5:30) - already July in company-local
        // time, even though it's still June in UTC. A UTC-naive range would wrongly exclude it.
        var reimbursedAt = new DateTime(2026, 6, 30, 19, 0, 0, DateTimeKind.Utc);
        var expense = CreateReimbursedExpense(employeeId, ExpenseCategory.Travel, reimbursedAt, complianceApprovedAt: null, reimbursedAt);
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(expense);
        var service = new ReportService(repository, new FakeMonthlyReimbursementReportGenerator(), new FakeCompanyClock());

        var records = await service.GetMonthlyReimbursementAsync(2026, 7, CancellationToken.None);

        var record = Assert.Single(records);
        Assert.Equal(expense.ExpenseNumber, record.ExpenseNumber);
    }

    [Fact]
    public async Task GetMonthlyReimbursementAsync_ReimbursedJustAfterLocalMonthEnd_IsExcludedFromCompanyLocalMonth()
    {
        var employeeId = Guid.NewGuid();
        // 2026-07-31T19:00:00Z is 2026-08-01T00:30 IST (UTC+5:30) - already August in
        // company-local time, even though it's still July in UTC. A UTC-naive range would
        // wrongly include it in July.
        var reimbursedAt = new DateTime(2026, 7, 31, 19, 0, 0, DateTimeKind.Utc);
        var expense = CreateReimbursedExpense(employeeId, ExpenseCategory.Travel, reimbursedAt, complianceApprovedAt: null, reimbursedAt);
        var repository = new FakeExpenseRepository();
        repository.Expenses.Add(expense);
        var service = new ReportService(repository, new FakeMonthlyReimbursementReportGenerator(), new FakeCompanyClock());

        var records = await service.GetMonthlyReimbursementAsync(2026, 7, CancellationToken.None);

        Assert.Empty(records);
    }

    private sealed class FakeMonthlyReimbursementReportGenerator : IMonthlyReimbursementReportGenerator
    {
        public byte[] ReturnValue { get; } = [1, 2, 3];

        public IReadOnlyList<MonthlyReimbursementRecord>? ReceivedRecords { get; private set; }

        public byte[] Generate(IReadOnlyList<MonthlyReimbursementRecord> records)
        {
            ReceivedRecords = records;
            return ReturnValue;
        }
    }

    private static Expense CreateReimbursedExpense(
        Guid employeeId,
        ExpenseCategory category,
        DateTime approvedAt,
        DateTime? complianceApprovedAt,
        DateTime reimbursedAt) => new()
        {
            Id = Guid.NewGuid(),
            ExpenseNumber = "EXP-TEST-0001",
            EmployeeId = employeeId,
            AttachmentId = Guid.NewGuid(),
            ExpenseDate = DateOnly.FromDateTime(approvedAt),
            Category = category,
            Amount = 100m,
            Currency = "INR",
            Description = "Existing expense",
            Status = ExpenseStatus.Reimbursed,
            ApprovedAt = approvedAt,
            ComplianceApprovedAt = complianceApprovedAt,
            ReimbursedAt = reimbursedAt,
            CreatedAt = approvedAt,
            UpdatedAt = reimbursedAt,
            Employee = new Employee
            {
                EmployeeId = employeeId,
                EmployeeNumber = "EMP-TEST",
                FirstName = "Test",
                LastName = "Employee",
                Email = "test-employee@example.com",
                Role = EmployeeRole.Employee,
                IsActive = true,
                CreatedAt = approvedAt,
                UpdatedAt = approvedAt,
            },
        };
}
