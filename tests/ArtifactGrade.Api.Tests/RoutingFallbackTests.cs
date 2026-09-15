using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArtifactGrade.Api.Tests;

public sealed class RoutingFallbackTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RoutingFallbackTests(WebApplicationFactory<Program> factory)
    {
        _client = factory
            .WithWebHostBuilder(builder =>
                builder.UseWebRoot(Path.Combine(AppContext.BaseDirectory, "wwwroot")))
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
    }

    [Fact]
    public async Task LivenessEndpoint_DoesNotDependOnRedis()
    {
        using var response = await _client.GetAsync("/api/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnknownApiRoute_ReturnsNotFoundJson()
    {
        using var response = await _client.GetAsync("/api/not-real");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnknownClientRoute_ReturnsSpaIndex()
    {
        using var response = await _client.GetAsync("/characters/example");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("artifact-grade-test-index", await response.Content.ReadAsStringAsync());
    }
}
