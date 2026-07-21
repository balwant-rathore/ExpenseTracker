using Api.Extensions;
using Application.Attachments;
using Domain.Repositories;
using Domain.Storage;
using Infrastructure.BackgroundServices;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntegrationTests;

public class AttachmentServiceCollectionExtensionsDiTests
{
    [Fact]
    public void AddAttachmentFoundation_RegistersAllAttachmentServices_ResolvableFromDi()
    {
        var services = new ServiceCollection();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer("Server=unused;Database=unused;"));
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment { ContentRootPath = Path.GetTempPath() });
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:AttachmentsRootPath"] = "unused",
                ["AttachmentCleanup:IntervalMinutes"] = "60",
                ["AttachmentCleanup:OrphanThresholdHours"] = "24",
            })
            .Build();

        services.AddAttachmentFoundation(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFileStorageService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAttachmentService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOrphanAttachmentSweeper>());
        Assert.Contains(provider.GetServices<IHostedService>(), s => s is OrphanAttachmentCleanupService);
    }
}

internal sealed class FakeHostEnvironment : IHostEnvironment
{
    public string ApplicationName { get; set; } = "Tests";

    public IFileProvider ContentRootFileProvider { get; set; } = null!;

    public string ContentRootPath { get; set; } = string.Empty;

    public string EnvironmentName { get; set; } = "Test";
}
