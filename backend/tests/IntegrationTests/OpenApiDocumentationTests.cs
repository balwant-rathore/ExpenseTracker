using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationTests;

public class OpenApiDocumentationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OpenApiDocumentationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ScalarUiRoute_RendersSuccessfully()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OpenApiDocument_ListsAllAuthEndpoints()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var paths = document.GetProperty("paths");

        foreach (var expectedPath in new[]
                 {
                     "/api/auth/register",
                     "/api/auth/login",
                     "/api/auth/refresh",
                     "/api/auth/logout",
                     "/api/auth/forgot-password",
                     "/api/auth/reset-password",
                 })
        {
            Assert.True(paths.TryGetProperty(expectedPath, out _), $"Expected OpenAPI document to list {expectedPath}");
        }
    }
}
