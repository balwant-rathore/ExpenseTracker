using Application.Reports;
using Domain.Entities;
using Domain.Enums;
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
        var service = new ReportService(repository);

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
        var service = new ReportService(repository);

        var records = await service.GetMonthlyReimbursementAsync(2026, 7, CancellationToken.None);

        var record = Assert.Single(records);
        Assert.Equal(approvedAt, record.ApprovalDate);
    }

    [Fact]
    public async Task GetMonthlyReimbursementAsync_MonthWithNoReimbursements_ReturnsEmptyList()
    {
        var repository = new FakeExpenseRepository();
        var service = new ReportService(repository);

        var records = await service.GetMonthlyReimbursementAsync(2020, 1, CancellationToken.None);

        Assert.Empty(records);
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
