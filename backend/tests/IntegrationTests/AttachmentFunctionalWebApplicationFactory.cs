using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>
/// Factory for attachment upload tests. Overrides Storage:AttachmentsRootPath to a per-instance
/// temp directory so tests never write into the real dev storage/attachments folder; the temp
/// directory is removed on dispose.
/// </summary>
public class AttachmentFunctionalWebApplicationFactory : WebApplicationFactory<Program>
{
    public string StorageRootPath { get; } =
        Path.Combine(Path.GetTempPath(), "et006-attachment-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:AttachmentsRootPath"] = StorageRootPath,
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
    }
}
