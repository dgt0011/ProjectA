using System.Net;
using Xunit;

namespace ProjectA.Api.Tests;

[Collection(nameof(ApiCollection))]
public class HealthEndpointTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetHealth_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetReady_ReturnsHealthy_WhenDatabaseIsAvailable()
    {
        var response = await _client.GetAsync("/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
