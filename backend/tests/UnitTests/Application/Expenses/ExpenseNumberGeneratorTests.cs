using System.Linq.Expressions;
using Application.Expenses;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;

namespace UnitTests.Application.Expenses;

public class ExpenseNumberGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_ReflectsCompanyClockDate()
    {
        var repository = new FakeExpenseRepository();
        var companyClock = new FakeCompanyClock { FixedToday = new DateOnly(2026, 3, 15) };
        var generator = new ExpenseNumberGenerator(repository, companyClock);

        var expenseNumber = await generator.GenerateAsync(CancellationToken.None);

        Assert.Equal("EXP-20260315-0001", expenseNumber);
    }

    [Fact]
    public async Task GenerateAsync_IncrementsPastExistingSameDayCount()
    {
        var repository = new FakeExpenseRepository();
        var companyClock = new FakeCompanyClock { FixedToday = new DateOnly(2026, 3, 15) };
        var prefix = "EXP-20260315-";
        repository.Expenses.Add(new Expense { ExpenseNumber = $"{prefix}0001" });
        repository.Expenses.Add(new Expense { ExpenseNumber = $"{prefix}0002" });
        var generator = new ExpenseNumberGenerator(repository, companyClock);

        var expenseNumber = await generator.GenerateAsync(CancellationToken.None);

        Assert.Equal($"{prefix}0003", expenseNumber);
    }
}

internal sealed class FakeExpenseRepository : IExpenseRepository
{
    public List<Expense> Expenses { get; } = [];

    public Task<Expense?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Expenses.FirstOrDefault(e => e.Id == id));

    public Task AddAsync(Expense entity, CancellationToken cancellationToken)
    {
        Expenses.Add(entity);
        return Task.CompletedTask;
    }

    public IQueryable<Expense> Query() => Expenses.AsQueryable();

    public Task<int> CountByExpenseNumberPrefixAsync(string prefix, CancellationToken cancellationToken) =>
        Task.FromResult(Expenses.Count(e => e.ExpenseNumber.StartsWith(prefix, StringComparison.Ordinal)));

    public Task<bool> ExistsByAttachmentIdAsync(Guid attachmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Expenses.Any(e => e.AttachmentId == attachmentId));

    public Queue<ExpenseInsertOutcome>? OutcomeQueue { get; set; }

    public Task<ExpenseInsertOutcome> TryAddAsync(Expense expense, CancellationToken cancellationToken)
    {
        if (OutcomeQueue is { Count: > 0 })
        {
            var outcome = OutcomeQueue.Dequeue();
            if (outcome == ExpenseInsertOutcome.Success)
            {
                Expenses.Add(expense);
            }

            return Task.FromResult(outcome);
        }

        Expenses.Add(expense);
        return Task.FromResult(ExpenseInsertOutcome.Success);
    }

    public Task<bool> TryUpdateAsync(Expense expense, CancellationToken cancellationToken)
    {
        var conflict = Expenses.Any(e => e.Id != expense.Id && e.AttachmentId == expense.AttachmentId);
        return Task.FromResult(!conflict);
    }

    public Task<Expense?> GetByIdWithEmployeeAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Expenses.FirstOrDefault(e => e.Id == id));

    public Task<(IReadOnlyList<Expense> Items, int TotalRecords)> GetPagedAsync(
        Expression<Func<Expense, bool>> visibilityPredicate,
        ExpenseSortField sortBy,
        bool descending,
        int page,
        int pageSize,
        ExpenseStatus? statusFilter,
        CancellationToken cancellationToken)
    {
        var filtered = Expenses.AsQueryable().Where(visibilityPredicate);

        if (statusFilter.HasValue)
        {
            filtered = filtered.Where(e => e.Status == statusFilter.Value);
        }

        var total = filtered.Count();

        Func<Expense, object?> keySelector = sortBy switch
        {
            ExpenseSortField.ExpenseNumber => e => e.ExpenseNumber,
            ExpenseSortField.CreatedAt => e => e.CreatedAt,
            ExpenseSortField.Amount => e => e.Amount,
            ExpenseSortField.SubmittedAt => e => e.SubmittedAt,
            ExpenseSortField.ApprovedAt => e => e.ApprovedAt,
            ExpenseSortField.ReimbursedAt => e => e.ReimbursedAt,
            ExpenseSortField.RejectedAt => e => e.RejectedAt,
            _ => e => e.ExpenseDate,
        };

        var ordered = descending
            ? filtered.OrderByDescending(keySelector)
            : filtered.OrderBy(keySelector);

        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult<(IReadOnlyList<Expense> Items, int TotalRecords)>((items, total));
    }

    public Task<(IReadOnlyList<Expense> Items, int TotalRecords)> SearchPagedAsync(
        string? expenseNumber,
        string? employeeName,
        ExpenseCategory? category,
        ExpenseStatus? status,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        ExpenseSortField sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var filtered = Expenses.AsQueryable().Where(e => e.Status != ExpenseStatus.Draft);

        if (!string.IsNullOrWhiteSpace(expenseNumber))
        {
            filtered = filtered.Where(e => e.ExpenseNumber.Equals(expenseNumber, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(employeeName))
        {
            filtered = filtered.Where(e => $"{e.Employee.FirstName} {e.Employee.LastName}".Contains(employeeName, StringComparison.OrdinalIgnoreCase));
        }

        if (category.HasValue)
        {
            filtered = filtered.Where(e => e.Category == category.Value);
        }

        if (status.HasValue)
        {
            filtered = filtered.Where(e => e.Status == status.Value);
        }

        if (createdFromUtc.HasValue)
        {
            filtered = filtered.Where(e => e.CreatedAt >= createdFromUtc.Value);
        }

        if (createdToUtc.HasValue)
        {
            filtered = filtered.Where(e => e.CreatedAt <= createdToUtc.Value);
        }

        var total = filtered.Count();

        Func<Expense, object?> keySelector = sortBy switch
        {
            ExpenseSortField.ExpenseNumber => e => e.ExpenseNumber,
            ExpenseSortField.CreatedAt => e => e.CreatedAt,
            ExpenseSortField.Amount => e => e.Amount,
            ExpenseSortField.SubmittedAt => e => e.SubmittedAt,
            ExpenseSortField.ApprovedAt => e => e.ApprovedAt,
            ExpenseSortField.ReimbursedAt => e => e.ReimbursedAt,
            ExpenseSortField.RejectedAt => e => e.RejectedAt,
            _ => e => e.ExpenseDate,
        };

        var ordered = descending
            ? filtered.OrderByDescending(keySelector)
            : filtered.OrderBy(keySelector);

        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult<(IReadOnlyList<Expense> Items, int TotalRecords)>((items, total));
    }

    public Task<IReadOnlyList<Expense>> GetReimbursedForReportAsync(
        DateTime rangeStartUtcInclusive,
        DateTime rangeEndUtcExclusive,
        CancellationToken cancellationToken)
    {
        var items = Expenses
            .Where(e => e.Status == ExpenseStatus.Reimbursed
                && e.ReimbursedAt >= rangeStartUtcInclusive
                && e.ReimbursedAt < rangeEndUtcExclusive)
            .OrderBy(e => e.ReimbursedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<Expense>>(items);
    }

    public Task<IReadOnlyList<StatusCategoryCount>> GetStatusCategoryCountsAsync(
        Expression<Func<Expense, bool>> scopePredicate,
        CancellationToken cancellationToken)
    {
        var counts = Expenses.AsQueryable()
            .Where(scopePredicate)
            .GroupBy(e => new { e.Status, e.Category })
            .Select(g => new StatusCategoryCount(g.Key.Status, g.Key.Category, g.Count()))
            .ToList();

        return Task.FromResult<IReadOnlyList<StatusCategoryCount>>(counts);
    }
}

internal sealed class FakeCompanyClock : ICompanyClock
{
    public DateOnly FixedToday { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly Today() => FixedToday;
}
