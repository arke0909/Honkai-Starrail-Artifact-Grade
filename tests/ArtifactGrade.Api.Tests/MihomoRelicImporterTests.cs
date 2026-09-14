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
              "portrait": "image/character_portrait/1212.png",
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

    private const string RawResponseJson = """
        {
          "detailInfo": {
            "avatarDetailList": [],
            "assistAvatarList": [
              { "avatarId": 1212, "dressedSkinId": 1121201 }
            ]
          }
        }
        """;

    [Fact]
    public async Task UsesRedisCacheAfterFirstUidRequest()
    {
        var handler = new StubHttpMessageHandler(ResponseJson, RawResponseJson);
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
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal("100000999", cache.LastWrittenUid);
        Assert.Equal(
            "https://enka.network/ui/hsr/SpriteOutput/AvatarDrawCard/AvatarSkin/1121201.png",
            Assert.Single(first.Characters).ImageUrl);
        Assert.Equal(Assert.Single(first.Characters), Assert.Single(second.Characters));
    }

    [Fact]
    public async Task KeepsImportUsableWhenAppearanceRequestFails()
    {
        var handler = new StubHttpMessageHandler(
            ResponseJson,
            RawResponseJson,
            HttpStatusCode.ServiceUnavailable);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.mihomo.me/")
        };
        var importer = new MihomoRelicImporter(
            httpClient,
            new MemoryImportCache(),
            NullLogger<MihomoRelicImporter>.Instance);

        var result = await importer.ImportAsync("100000999", CancellationToken.None);

        Assert.Equal(
            "https://raw.githubusercontent.com/Mar-7th/StarRailRes/master/image/character_portrait/1212.png",
            Assert.Single(result.Characters).ImageUrl);
        Assert.Contains(
            result.Warnings,
            warning => warning.Contains("기본 캐릭터 이미지", StringComparison.Ordinal));
        Assert.Single(result.Relics);
    }

    [Fact]
    public async Task KeepsImportUsableWhenAppearanceListsAreMissing()
    {
        const string rawResponseWithoutCharacterLists = """
            { "detailInfo": {} }
            """;
        var handler = new StubHttpMessageHandler(
            ResponseJson,
            rawResponseWithoutCharacterLists);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.mihomo.me/")
        };
        var importer = new MihomoRelicImporter(
            httpClient,
            new MemoryImportCache(),
            NullLogger<MihomoRelicImporter>.Instance);

        var result = await importer.ImportAsync("100000999", CancellationToken.None);

        Assert.Equal(
            "https://raw.githubusercontent.com/Mar-7th/StarRailRes/master/image/character_portrait/1212.png",
            Assert.Single(result.Characters).ImageUrl);
        Assert.Contains(
            result.Warnings,
            warning => warning.Contains("기본 캐릭터 이미지", StringComparison.Ordinal));
    }

    private sealed class StubHttpMessageHandler(
        string parsedResponseJson,
        string rawResponseJson,
        HttpStatusCode rawStatusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            var isRawRequest = request.RequestUri?.AbsolutePath.Contains(
                "/sr_info/",
                StringComparison.Ordinal) == true;
            return Task.FromResult(new HttpResponseMessage(
                isRawRequest ? rawStatusCode : HttpStatusCode.OK)
            {
                Content = new StringContent(isRawRequest ? rawResponseJson : parsedResponseJson)
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
