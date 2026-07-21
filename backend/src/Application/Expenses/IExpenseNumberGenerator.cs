namespace Application.Expenses;

public interface IExpenseNumberGenerator
{
    Task<string> GenerateAsync(CancellationToken cancellationToken);
}
