using System.Net;
using ArtifactGrade.Api;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArtifactGrade.Api.Tests;

public sealed class MihomoRelicImporterTests
{
    private const string ResponseJson = """
        {
          "player": { "nickname": "Trailblazer" },
          "characters": [
            {
              "id": "1212",
              "name": "Jingliu",
              "relics": [
                {
                  "id": "61021",
                  "name": "Hunter's Artaius Hood",
                  "type": 1,
                  "set_name": "Hunter of Glacial Forest",
                  "rarity": 5,
                  "level": 15,
                  "main_affix": { "field": "hp", "value": 705.6, "percent": false },
                  "sub_affix": [
                    { "field": "crit_rate", "value": 0.0648, "percent": true }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task UsesRedisCacheAfterFirstUidRequest()
    {
        var handler = new StubHttpMessageHandler(ResponseJson);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.mihomo.me/")
        };
        var cache = new MemoryImportCache();
        var importer = new MihomoRelicImporter(
            httpClient,
            cache,
            NullLogger<MihomoRelicImporter>.Instance);

        var first = await importer.ImportAsync("100000999", CancellationToken.None);
        var second = await importer.ImportAsync("100000999", CancellationToken.None);

        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("100000999", cache.LastWrittenUid);
    }

    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            });
        }
    }

    private sealed class MemoryImportCache : IRelicImportCache
    {
        private string? _json;
        public string? LastWrittenUid { get; private set; }

        public Task<string?> GetAsync(string uid, CancellationToken cancellationToken) =>
            Task.FromResult(_json);

        public Task SetAsync(
            string uid,
            string json,
            TimeSpan expiry,
            CancellationToken cancellationToken)
        {
            LastWrittenUid = uid;
            _json = json;
            return Task.CompletedTask;
        }
    }
}
