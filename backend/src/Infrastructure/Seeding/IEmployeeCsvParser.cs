namespace Infrastructure.Seeding;

public interface IEmployeeCsvParser
{
    Task<IReadOnlyList<EmployeeCsvRow>> ParseAsync(string filePath, CancellationToken cancellationToken);
}
