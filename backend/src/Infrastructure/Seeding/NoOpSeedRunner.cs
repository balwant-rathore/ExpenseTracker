namespace Infrastructure.Seeding;

public class NoOpSeedRunner : ISeedRunner
{
    public Task SeedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
