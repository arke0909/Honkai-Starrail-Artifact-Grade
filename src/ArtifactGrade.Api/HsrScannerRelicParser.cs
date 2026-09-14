using System.Text.Json;
using ArtifactGrade.Domain;

namespace ArtifactGrade.Api;

public static class HsrScannerRelicParser
{
    public static RelicImportResponse Parse(string json)
    {
        try
        {
            return ParseDocument(json);
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception) when (IsJsonShapeException(exception))
        {
            throw new JsonException("HSR Scanner JSON 필드 형식이 올바르지 않습니다.", exception);
        }
    }

    private static RelicImportResponse ParseDocument(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("version", out var version)
            || !version.TryGetInt32(out var versionNumber)
            || versionNumber != 4)
        {
            throw new JsonException("HSR Scanner JSON 버전 4만 지원합니다.");
        }

        var characterNames = ParseCharacterNames(root);
        var importedRelics = new List<ImportedRelic>();
        var warnings = new List<string>();
        var relicIndex = 0;

        if (!root.TryGetProperty("relics", out var relicElements)
            || relicElements.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("유물 목록이 없습니다.");
        }

        foreach (var relic in relicElements.EnumerateArray())
        {
            relicIndex++;
            try
            {
                if (!relic.TryGetProperty("rarity", out var rarityElement)
                    || !rarityElement.TryGetInt32(out var rarity))
                {
                    throw new JsonException("유물 등급 형식이 올바르지 않습니다.");
                }

                if (rarity != 5)
                {
                    continue;
                }

                importedRelics.Add(ParseRelic(relic, relicIndex, characterNames));
            }
            catch (Exception exception) when (IsJsonShapeException(exception))
            {
                warnings.Add($"{relicIndex}번째 유물을 건너뛰었습니다. {NormalizeMessage(exception)}");
            }
        }

        var importedCharacters = characterNames
            .Select(character => new ImportedCharacter(
                character.Key,
                character.Value,
                StarRailAssetUrls.CharacterPortrait(character.Key)))
            .ToArray();
        return new RelicImportResponse("HSR Scanner", null, importedCharacters, importedRelics, warnings);
    }

    private static IReadOnlyDictionary<string, string> ParseCharacterNames(JsonElement root)
    {
        if (!root.TryGetProperty("characters", out var characters))
        {
            return new Dictionary<string, string>();
        }

        if (characters.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("캐릭터 목록 형식이 올바르지 않습니다.");
        }

        var names = new Dictionary<string, string>();
        foreach (var character in characters.EnumerateArray())
        {
            if (!character.TryGetProperty("id", out var idElement))
            {
                continue;
            }

            var id = idElement.GetString();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            names[id] = character.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? id
                : id;
        }

        return names;
    }

    private static ImportedRelic ParseRelic(
        JsonElement relic,
        int relicIndex,
        IReadOnlyDictionary<string, string> characterNames)
    {
        var slot = ParseSlot(relic.GetProperty("slot").GetString());
        var setName = relic.GetProperty("name").GetString() ?? "세트 정보 없음";
        var location = relic.TryGetProperty("location", out var locationElement)
            ? locationElement.GetString()
            : null;
        var equippedBy = !string.IsNullOrWhiteSpace(location)
            ? characterNames.GetValueOrDefault(location, location)
            : null;
        var key = relic.TryGetProperty("_uid", out var uidElement)
            ? uidElement.GetString()
            : null;
        var substats = relic.GetProperty("substats")
            .EnumerateArray()
            .Select(ParseSubstat)
            .ToArray();
        var level = relic.GetProperty("level").GetInt32();
        var mainStat = ParseMainStat(relic.GetProperty("mainstat").GetString(), slot);
        decimal? mainStatValue = level is >= 0 and <= 15
            ? RelicMainStatValues.Calculate(mainStat, level)
            : null;
        var setId = relic.TryGetProperty("set_id", out var setIdElement)
            && setIdElement.ValueKind == JsonValueKind.String
                ? setIdElement.GetString()
                : null;

        return new ImportedRelic(
            string.IsNullOrWhiteSpace(key) ? $"scanner:{relicIndex}" : key,
            setName,
            setName,
            equippedBy,
            5,
            level,
            slot,
            mainStat,
            substats,
            mainStatValue,
            StarRailAssetUrls.RelicIcon(setId, slot),
            EquippedCharacterId: location);
    }

    private static RelicSubstat ParseSubstat(JsonElement element)
    {
        var key = element.GetProperty("key").GetString();
        var stat = key switch
        {
            "HP" => RelicStat.FlatHp,
            "ATK" => RelicStat.FlatAttack,
            "DEF" => RelicStat.FlatDefense,
            "HP_" => RelicStat.HpPercent,
            "ATK_" => RelicStat.AttackPercent,
            "DEF_" => RelicStat.DefensePercent,
            "SPD" => RelicStat.Speed,
            "CRIT Rate_" => RelicStat.CritRate,
            "CRIT DMG_" => RelicStat.CritDamage,
            "Effect Hit Rate_" => RelicStat.EffectHitRate,
            "Effect RES_" => RelicStat.EffectResistance,
            "Break Effect_" => RelicStat.BreakEffect,
            _ => throw new JsonException($"지원하지 않는 부옵션입니다: {key}")
        };

        return new RelicSubstat(stat, element.GetProperty("value").GetDecimal());
    }

    private static RelicSlot ParseSlot(string? slot) => slot switch
    {
        "Head" => RelicSlot.Head,
        "Hands" => RelicSlot.Hands,
        "Body" => RelicSlot.Body,
        "Feet" => RelicSlot.Feet,
        "Planar Sphere" => RelicSlot.PlanarSphere,
        "Link Rope" => RelicSlot.LinkRope,
        _ => throw new JsonException($"지원하지 않는 유물 부위입니다: {slot}")
    };

    private static RelicMainStat ParseMainStat(string? mainStat, RelicSlot slot) => (mainStat, slot) switch
    {
        ("HP", RelicSlot.Head) => RelicMainStat.FlatHp,
        ("ATK", RelicSlot.Hands) => RelicMainStat.FlatAttack,
        ("HP", _) => RelicMainStat.HpPercent,
        ("ATK", _) => RelicMainStat.AttackPercent,
        ("DEF", _) => RelicMainStat.DefensePercent,
        ("CRIT Rate", _) => RelicMainStat.CritRate,
        ("CRIT DMG", _) => RelicMainStat.CritDamage,
        ("Outgoing Healing Boost", _) => RelicMainStat.OutgoingHealing,
        ("Effect Hit Rate", _) => RelicMainStat.EffectHitRate,
        ("SPD", _) => RelicMainStat.Speed,
        ("Physical DMG Boost", _) => RelicMainStat.PhysicalDamage,
        ("Fire DMG Boost", _) => RelicMainStat.FireDamage,
        ("Ice DMG Boost", _) => RelicMainStat.IceDamage,
        ("Lightning DMG Boost", _) => RelicMainStat.LightningDamage,
        ("Wind DMG Boost", _) => RelicMainStat.WindDamage,
        ("Quantum DMG Boost", _) => RelicMainStat.QuantumDamage,
        ("Imaginary DMG Boost", _) => RelicMainStat.ImaginaryDamage,
        ("Break Effect", _) => RelicMainStat.BreakEffect,
        ("Energy Regeneration Rate", _) => RelicMainStat.EnergyRegenerationRate,
        _ => throw new JsonException($"지원하지 않는 주옵션입니다: {mainStat}")
    };

    private static bool IsJsonShapeException(Exception exception) => exception is
        JsonException or
        KeyNotFoundException or
        InvalidOperationException or
        FormatException or
        OverflowException;

    private static string NormalizeMessage(Exception exception) => exception is JsonException
        ? exception.Message
        : "필수 필드 형식이 올바르지 않습니다.";
}
