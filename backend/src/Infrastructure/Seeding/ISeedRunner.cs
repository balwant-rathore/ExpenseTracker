namespace Infrastructure.Seeding;

public interface ISeedRunner
{
    Task SeedAsync(CancellationToken cancellationToken);
}
