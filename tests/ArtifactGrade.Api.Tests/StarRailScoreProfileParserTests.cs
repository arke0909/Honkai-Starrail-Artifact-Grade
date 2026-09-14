using ArtifactGrade.Api;
using ArtifactGrade.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArtifactGrade.Api.Tests;

public sealed class StarRailScoreProfileParserTests
{
    private const string CastoriceProfileJson = """
        {
          "1407": {
            "main": {
              "1": { "HPDelta": 1 }
            },
            "weight": {
              "HPDelta": 0.3,
              "AttackDelta": 0,
              "DefenceDelta": 0,
              "HPAddedRatio": 1,
              "AttackAddedRatio": 0,
              "DefenceAddedRatio": 0.1,
              "SpeedDelta": 0.1,
              "CriticalChanceBase": 1,
              "CriticalDamageBase": 1,
              "StatusProbabilityBase": 0,
              "StatusResistanceBase": 0.1,
              "BreakDamageAddedRatioBase": 0.1
            },
            "max": 9.24
          }
        }
        """;

    [Fact]
    public void ParsesAllTwelveCastoriceWeights()
    {
        var profiles = StarRailScoreProfileParser.Parse(CastoriceProfileJson);

        var profile = Assert.Single(profiles).Value;
        Assert.Equal(12, profile.SubstatWeights.Count);
        Assert.Equal(1m, profile.SubstatWeights[RelicStat.HpPercent]);
        Assert.Equal(0m, profile.SubstatWeights[RelicStat.AttackPercent]);
        Assert.Equal(1m, profile.SubstatWeights[RelicStat.CritRate]);
        Assert.Equal(1m, profile.SubstatWeights[RelicStat.CritDamage]);
        Assert.Equal(1m, profile.MainStatWeights[RelicSlot.Head][RelicMainStat.FlatHp]);
        Assert.Equal(9.24m, profile.MaximumSubstatScore);
    }

    [Fact]
    public void RejectsProfileWhenAWeightIsMissing()
    {
        const string json = """
            {
              "1407": {
                "main": {
                  "1": { "HPDelta": 1 }
                },
                "weight": {
                  "HPDelta": 0.3
                },
                "max": 9.24
              }
            }
            """;

        var exception = Assert.Throws<System.Text.Json.JsonException>(() =>
            StarRailScoreProfileParser.Parse(json));

        Assert.Contains("1407", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderDownloadsProfilesOnlyOncePerProcess()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(CastoriceProfileJson)
        });
        var cache = new StubCharacterProfileCache();
        var provider = CreateProvider(handler, cache);

        var first = await provider.GetAsync("1407", "카스토리스", CancellationToken.None);
        var second = await provider.GetAsync("1407", "카스토리스", CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(1, cache.SetCount);
        Assert.Equal(CastoriceProfileJson, cache.StoredJson);
    }

    [Fact]
    public async Task ProviderUsesRedisCacheWhenSourceRequestFails()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
        var cache = new StubCharacterProfileCache { StoredJson = CastoriceProfileJson };
        var provider = CreateProvider(handler, cache);

        var profile = await provider.GetAsync("1407", "카스토리스", CancellationToken.None);

        Assert.NotNull(profile);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(1, cache.GetCount);
        Assert.Equal(0, cache.SetCount);
    }

    private static StarRailScoreProfileProvider CreateProvider(
        HttpMessageHandler handler,
        ICharacterProfileCache cache)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };
        return new StarRailScoreProfileProvider(
            client,
            cache,
            NullLogger<StarRailScoreProfileProvider>.Instance);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class StubCharacterProfileCache : ICharacterProfileCache
    {
        public string? StoredJson { get; set; }

        public int GetCount { get; private set; }

        public int SetCount { get; private set; }

        public Task<string?> GetCharacterProfilesAsync(CancellationToken cancellationToken)
        {
            GetCount++;
            return Task.FromResult(StoredJson);
        }

        public Task SetCharacterProfilesAsync(
            string json,
            TimeSpan expiry,
            CancellationToken cancellationToken)
        {
            SetCount++;
            StoredJson = json;
            return Task.CompletedTask;
        }
    }
}
