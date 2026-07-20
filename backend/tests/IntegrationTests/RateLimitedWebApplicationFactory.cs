using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

public class RateLimitedWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:AuthEndpoints:PermitLimit"] = "3",
                ["RateLimiting:AuthEndpoints:WindowSeconds"] = "60",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestEndpointsStartupFilter>();
        });
    }
}
