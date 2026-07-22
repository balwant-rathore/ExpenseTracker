using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>
/// Factory for notification tests. Overrides Storage:AttachmentsRootPath (expenses require an
/// uploaded attachment) and Notification:LogDirectory to per-instance temp directories so tests
/// never write into the real dev storage/ folders; both temp directories are removed on dispose.
/// </summary>
public class NotificationFunctionalWebApplicationFactory : WebApplicationFactory<Program>
{
    public string StorageRootPath { get; } =
        Path.Combine(Path.GetTempPath(), "et015-notification-storage-tests", Guid.NewGuid().ToString("N"));

    public string NotificationLogDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "et015-notification-log-tests", Guid.NewGuid().ToString("N"));

    public string NotificationLogFilePath => Path.Combine(NotificationLogDirectory, "notifications.html");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:AttachmentsRootPath"] = StorageRootPath,
                ["Notification:LogDirectory"] = NotificationLogDirectory,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestEndpointsStartupFilter>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (Directory.Exists(StorageRootPath))
        {
            Directory.Delete(StorageRootPath, recursive: true);
        }

        if (Directory.Exists(NotificationLogDirectory))
        {
            Directory.Delete(NotificationLogDirectory, recursive: true);
        }
    }
}
