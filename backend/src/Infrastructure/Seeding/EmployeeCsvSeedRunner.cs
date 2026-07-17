using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Seeding;

public class EmployeeCsvSeedRunner : ISeedRunner
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmployeeCsvParser _parser;
    private readonly EmployeeSeedPlanner _planner;
    private readonly EmployeeSeedOptions _options;
    private readonly ILogger<EmployeeCsvSeedRunner> _logger;

    public EmployeeCsvSeedRunner(
        ApplicationDbContext dbContext,
        IEmployeeCsvParser parser,
        EmployeeSeedPlanner planner,
        EmployeeSeedOptions options,
        ILogger<EmployeeCsvSeedRunner> logger)
    {
        _dbContext = dbContext;
        _parser = parser;
        _planner = planner;
        _options = options;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.EmployeeCsvPath))
        {
            throw new FileNotFoundException(
                $"Employee seed CSV not found at '{_options.EmployeeCsvPath}'.", _options.EmployeeCsvPath);
        }

        var csvRows = await _parser.ParseAsync(_options.EmployeeCsvPath, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existingEmployeesByNumber = await _dbContext.Employees
            .Select(e => new { e.EmployeeNumber, e.EmployeeId })
            .ToDictionaryAsync(e => e.EmployeeNumber, e => e.EmployeeId, cancellationToken);

        var plan = _planner.BuildPlan(csvRows, existingEmployeesByNumber);

        foreach (var warning in plan.UnresolvedManagerWarnings)
        {
            _logger.LogWarning("{Warning}", warning);
        }

        if (plan.EmployeesToInsert.Count > 0)
        {
            await _dbContext.Employees.AddRangeAsync(plan.EmployeesToInsert, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
