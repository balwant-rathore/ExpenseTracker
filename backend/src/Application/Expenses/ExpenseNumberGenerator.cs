using Domain.Repositories;

namespace Application.Expenses;

public class ExpenseNumberGenerator : IExpenseNumberGenerator
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly ICompanyClock _companyClock;

    public ExpenseNumberGenerator(IExpenseRepository expenseRepository, ICompanyClock companyClock)
    {
        _expenseRepository = expenseRepository;
        _companyClock = companyClock;
    }

    public async Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        var prefix = $"EXP-{_companyClock.Today():yyyyMMdd}-";
        var count = await _expenseRepository.CountByExpenseNumberPrefixAsync(prefix, cancellationToken);
        return $"{prefix}{count + 1:D4}";
    }
}
