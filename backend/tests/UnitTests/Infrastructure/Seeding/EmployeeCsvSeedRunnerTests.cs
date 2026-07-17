using Infrastructure.Seeding;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Infrastructure.Persistence;

namespace UnitTests.Infrastructure.Seeding;

public class EmployeeCsvSeedRunnerTests
{
    [Fact]
    public async Task SeedAsync_MissingCsvFile_ThrowsFileNotFoundException_WithoutParsing()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid()}.csv");
        var options = new EmployeeSeedOptions { EmployeeCsvPath = missingPath };

        using var dbContext = TestDbContextFactory.Create();
        var runner = new EmployeeCsvSeedRunner(
            dbContext,
            new ThrowingEmployeeCsvParser(),
            new EmployeeSeedPlanner(),
            options,
            NullLogger<EmployeeCsvSeedRunner>.Instance);

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => runner.SeedAsync(CancellationToken.None));

        Assert.Contains(missingPath, exception.Message);
        Assert.Equal(missingPath, exception.FileName);
    }

    private sealed class ThrowingEmployeeCsvParser : IEmployeeCsvParser
    {
        public Task<IReadOnlyList<EmployeeCsvRow>> ParseAsync(string filePath, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("ParseAsync should not be called when the CSV file is missing.");
        }
    }
}
