using ArtifactGrade.Api;
using ArtifactGrade.Domain;

namespace ArtifactGrade.Api.Tests;

public sealed class HsrScannerRelicParserTests
{
    [Fact]
    public void ParsesVersionFourRelicAndResolvesEquippedCharacter()
    {
        const string json = """
            {
              "source": "HSR-Scanner",
              "build": "v1.5.0",
              "version": 4,
              "relics": [
                {
                  "name": "Musketeer of Wild Wheat",
                  "slot": "Hands",
                  "rarity": 5,
                  "level": 15,
                  "mainstat": "ATK",
                  "substats": [
                    { "key": "DEF", "value": 16 },
                    { "key": "DEF_", "value": 5.4 },
                    { "key": "CRIT Rate_", "value": 5.1 },
                    { "key": "CRIT DMG_", "value": 31.7 }
                  ],
                  "location": "1101",
                  "_uid": "relic_1"
                }
              ],
              "characters": [
                { "id": "1101", "name": "Bronya" }
              ]
            }
            """;

        var result = HsrScannerRelicParser.Parse(json);

        Assert.Equal("HSR Scanner", result.Source);
        var relic = Assert.Single(result.Relics);
        Assert.Equal("relic_1", relic.Key);
        Assert.Equal("Bronya", relic.EquippedBy);
        Assert.Equal(RelicSlot.Hands, relic.Slot);
        Assert.Equal(RelicMainStat.FlatAttack, relic.MainStat);
        Assert.Equal(
            [
                new RelicSubstat(RelicStat.FlatDefense, 16m),
                new RelicSubstat(RelicStat.DefensePercent, 5.4m),
                new RelicSubstat(RelicStat.CritRate, 5.1m),
                new RelicSubstat(RelicStat.CritDamage, 31.7m)
            ],
            relic.Substats);
    }

    [Fact]
    public void ReportsMalformedScannerDocumentAsJsonError()
    {
        const string json = """
            {
              "source": "HSR-Scanner",
              "version": 4
            }
            """;

        var exception = Assert.Throws<System.Text.Json.JsonException>(() =>
            HsrScannerRelicParser.Parse(json));

        Assert.Contains("유물 목록", exception.Message, StringComparison.Ordinal);
    }
}
