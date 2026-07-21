using Application.Expenses;
using Domain.Entities;
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
}

internal sealed class FakeCompanyClock : ICompanyClock
{
    public DateOnly FixedToday { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly Today() => FixedToday;
}
