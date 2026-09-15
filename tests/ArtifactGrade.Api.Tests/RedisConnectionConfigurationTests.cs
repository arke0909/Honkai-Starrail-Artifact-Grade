using System.Net;

namespace ArtifactGrade.Api.Tests;

public sealed class RedisConnectionConfigurationTests
{
    [Fact]
    public void KeepsStackExchangeConnectionStrings()
    {
        var configuration = RedisConnectionConfiguration.Parse(
            "localhost:6379,abortConnect=false,connectTimeout=500");

        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(configuration.EndPoints));
        Assert.Equal("localhost", endpoint.Host);
        Assert.Equal(6379, endpoint.Port);
        Assert.False(configuration.AbortOnConnectFail);
        Assert.Equal(500, configuration.ConnectTimeout);
    }

    [Fact]
    public void ConvertsRedisUrlFromManagedHosting()
    {
        var configuration = RedisConnectionConfiguration.Parse(
            "redis://artifact-grade-redis:6379");

        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(configuration.EndPoints));
        Assert.Equal("artifact-grade-redis", endpoint.Host);
        Assert.Equal(6379, endpoint.Port);
        Assert.False(configuration.Ssl);
        Assert.False(configuration.AbortOnConnectFail);
    }

    [Fact]
    public void ConvertsAuthenticatedTlsRedisUrl()
    {
        var configuration = RedisConnectionConfiguration.Parse(
            "rediss://default:p%40ss@redis.example.com:6380");

        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(configuration.EndPoints));
        Assert.Equal("redis.example.com", endpoint.Host);
        Assert.Equal(6380, endpoint.Port);
        Assert.True(configuration.Ssl);
        Assert.Equal("default", configuration.User);
        Assert.Equal("p@ss", configuration.Password);
    }
}
