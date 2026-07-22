using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace IntegrationTests;

/// <summary>
/// Regression test for ADR-0021. WebApplicationFactory&lt;Program&gt; defaults to the
/// Development environment (see OpenApiDocumentationTests, which depends on the
/// Development-only /scalar and /openapi routes) — no explicit environment override is needed
/// here to exercise the branch this fix guards. ASPNETCORE_HTTPS_PORT is set explicitly because,
/// without a determinable HTTPS port, UseHttpsRedirection no-ops even when it runs — this test
/// would pass regardless of the fix unless a port is configured.
/// </summary>
public class HttpsRedirectionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HttpsRedirectionTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ASPNETCORE_HTTPS_PORT"] = "5001",
                });
            });
        });
    }

    [Fact]
    public async Task DevelopmentEnvironment_PlainHttpRequest_IsNotRedirectedToHttps()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/health");

        Assert.NotEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    }
}
