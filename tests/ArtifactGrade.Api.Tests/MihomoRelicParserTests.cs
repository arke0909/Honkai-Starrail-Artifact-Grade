using ArtifactGrade.Api;
using ArtifactGrade.Domain;

namespace ArtifactGrade.Api.Tests;

public sealed class MihomoRelicParserTests
{
    [Fact]
    public void ParsesFiveStarRelicIntoCalculatorFields()
    {
        const string json = """
            {
              "player": { "nickname": "Trailblazer" },
              "characters": [
                {
                  "id": "1212",
                  "name": "Jingliu",
                  "portrait": "image/character_portrait/1212.png",
                  "relics": [
                    {
                      "id": "61023",
                      "name": "Musketeer's Wind-Hunting Shawl",
                      "icon": "icon/relic/102_2.png",
                      "type": 3,
                      "set_name": "Musketeer of Wild Wheat",
                      "rarity": 5,
                      "level": 15,
                      "main_affix": { "field": "atk", "value": 0.432, "percent": true },
                      "sub_affix": [
                        { "field": "crit_rate", "value": 0.0648, "percent": true },
                        { "field": "crit_dmg", "value": 0.1296, "percent": true },
                        { "field": "spd", "value": 5.2, "percent": false },
                        { "field": "atk", "value": 21.168754, "percent": false }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var result = MihomoRelicParser.Parse(json);

        Assert.Equal("MiHoMo UID", result.Source);
        Assert.Equal("Trailblazer", result.PlayerName);
        var relic = Assert.Single(result.Relics);
        Assert.Equal("1212:3", relic.Key);
        Assert.Equal("Jingliu", relic.EquippedBy);
        Assert.Equal("1212", relic.EquippedCharacterId);
        Assert.Equal(RelicSlot.Body, relic.Slot);
        Assert.Equal(RelicMainStat.AttackPercent, relic.MainStat);
        Assert.Equal(43.2m, relic.MainStatValue);
        Assert.Equal(
            "https://raw.githubusercontent.com/Mar-7th/StarRailRes/master/icon/relic/102_2.png",
            relic.ImageUrl);
        Assert.Equal(
            [
                new RelicSubstat(RelicStat.CritRate, 6.48m),
                new RelicSubstat(RelicStat.CritDamage, 12.96m),
                new RelicSubstat(RelicStat.Speed, 5.2m),
                new RelicSubstat(RelicStat.FlatAttack, 21.168754m)
            ],
            relic.Substats);
        var character = Assert.Single(result.Characters);
        Assert.Equal("1212", character.Id);
        Assert.Equal("Jingliu", character.Name);
        Assert.Equal(
            "https://raw.githubusercontent.com/Mar-7th/StarRailRes/master/image/character_portrait/1212.png",
            character.ImageUrl);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void KeepsRelicDataWhenOptionalImageFieldsAreMalformed()
    {
        const string json = """
            {
              "player": { "nickname": "Trailblazer" },
              "characters": [
                {
                  "id": "1212",
                  "name": "Jingliu",
                  "portrait": 1212,
                  "relics": [
                    {
                      "id": "61021",
                      "name": "Hunter's Artaius Hood",
                      "icon": { "invalid": true },
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

        var result = MihomoRelicParser.Parse(json);

        Assert.Null(Assert.Single(result.Characters).ImageUrl);
        Assert.Null(Assert.Single(result.Relics).ImageUrl);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void SkipsUnsupportedRelicAndReportsWarning()
    {
        const string json = """
            {
              "player": { "nickname": "Trailblazer" },
              "characters": [
                {
                  "id": "1212",
                  "name": "Jingliu",
                  "relics": [
                    {
                      "id": "future-relic",
                      "name": "Future Relic",
                      "type": 3,
                      "set_name": "Future Set",
                      "rarity": 5,
                      "level": 15,
                      "main_affix": { "field": "future_stat", "value": 1, "percent": true },
                      "sub_affix": [
                        { "field": "crit_rate", "value": 0.0648, "percent": true }
                      ]
                    },
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

        var result = MihomoRelicParser.Parse(json);

        Assert.Single(result.Relics);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("Future Relic", warning, StringComparison.Ordinal);
        Assert.Contains("지원하지 않는 주옵션", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsMissingCharactersAsJsonError()
    {
        const string json = """
            {
              "player": { "nickname": "Trailblazer" }
            }
            """;

        var exception = Assert.Throws<System.Text.Json.JsonException>(() =>
            MihomoRelicParser.Parse(json));

        Assert.Contains("캐릭터 목록", exception.Message, StringComparison.Ordinal);
    }
}
