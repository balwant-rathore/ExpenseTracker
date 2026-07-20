using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>
/// Factory for functional auth-flow tests (register/login/refresh/logout scenarios).
///
/// NOTE ON RATE LIMITING: this factory attempts to raise the auth rate limit via
/// <c>ConfigureAppConfiguration</c>, the same pattern <see cref="RateLimitedWebApplicationFactory"/>
/// uses. In practice that override does NOT reliably reach <c>AddAuthRateLimiting</c>, because
/// Program.cs reads <c>AuthRateLimitOptions</c> via a one-time <c>IConfiguration.Get&lt;T&gt;()</c>
/// snapshot synchronously while building services — before WebApplicationFactory's
/// ConfigureAppConfiguration additions are merged into the WebApplicationBuilder's
/// ConfigurationManager (that only happens once Build() finishes). See the suspected test-infra
/// gap noted in the ET004 test report; it is not patched here since it's a production wiring
/// question, not a test bug.
///
/// The real mitigation functional tests rely on: every functional test class creates its OWN
/// instance of this factory per test method (rather than sharing one via IClassFixture), so each
/// test starts with a pristine, per-host rate-limiter counter. As long as an individual test keeps
/// its own register/login call count under the production default (5 per policy per 300s window,
/// see appsettings.json), it never trips the limiter regardless of whether the override above
/// actually applies.
/// </summary>
public class AuthFunctionalWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:AuthEndpoints:PermitLimit"] = "1000",
                ["RateLimiting:AuthEndpoints:WindowSeconds"] = "300",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestEndpointsStartupFilter>();
        });
    }
}
