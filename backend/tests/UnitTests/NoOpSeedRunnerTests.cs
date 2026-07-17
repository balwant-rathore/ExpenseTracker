using Infrastructure.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests;

public class NoOpSeedRunnerTests
{
    [Fact]
    public async Task SeedAsync_ResolvedFromDi_CompletesWithoutThrowing()
    {
        var services = new ServiceCollection();
        services.AddScoped<ISeedRunner, NoOpSeedRunner>();
        await using var provider = services.BuildServiceProvider();

        var seedRunner = provider.GetRequiredService<ISeedRunner>();

        var exception = await Record.ExceptionAsync(() => seedRunner.SeedAsync(CancellationToken.None));

        Assert.Null(exception);
    }
}
