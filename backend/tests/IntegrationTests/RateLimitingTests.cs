using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationTests;

public class RateLimitingTests : IClassFixture<RateLimitedWebApplicationFactory>
{
    private readonly RateLimitedWebApplicationFactory _factory;

    public RateLimitingTests(RateLimitedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RequestsWithinConfiguredLimit_ArePermitted()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsync("/__test/rate-limited", content: null);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task RequestExceedingConfiguredLimit_Returns429()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            await client.PostAsync("/__test/rate-limited", content: null);
        }

        var response = await client.PostAsync("/__test/rate-limited", content: null);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task RateLimitedResponse_IsIdenticalRegardlessOfRequestContent()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            await client.PostAsync("/__test/rate-limited", content: null);
        }

        var responseA = await client.PostAsync("/__test/rate-limited", JsonContent.Create(new { wouldBeValid = true }));
        var errorA = await responseA.Content.ReadFromJsonAsync<JsonElement>();

        var responseB = await client.PostAsync("/__test/rate-limited", JsonContent.Create(new { wouldBeValid = false }));
        var errorB = await responseB.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.TooManyRequests, responseA.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, responseB.StatusCode);

        // traceId legitimately differs per request (a diagnostic correlation id, not a credential-validity signal);
        // code/message/fields must be identical regardless of whether the credentials would have been valid.
        Assert.Equal(
            errorA.GetProperty("error").GetProperty("code").GetString(),
            errorB.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(
            errorA.GetProperty("error").GetProperty("message").GetString(),
            errorB.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(
            errorA.GetProperty("error").GetProperty("fields").GetRawText(),
            errorB.GetProperty("error").GetProperty("fields").GetRawText());
    }
}
