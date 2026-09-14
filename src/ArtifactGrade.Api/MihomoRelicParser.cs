using System.Text.Json;
using ArtifactGrade.Domain;

namespace ArtifactGrade.Api;

public static class MihomoRelicParser
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
            throw new JsonException("MiHoMo 응답 필드 형식이 올바르지 않습니다.", exception);
        }
    }

    private static RelicImportResponse ParseDocument(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("player", out var player)
            || player.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("플레이어 정보가 없습니다.");
        }

        var playerName = player.TryGetProperty("nickname", out var nickname)
            ? nickname.GetString()
            : null;

        if (!root.TryGetProperty("characters", out var characters)
            || characters.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("캐릭터 목록이 없습니다.");
        }

        var relics = new List<ImportedRelic>();
        var importedCharacters = new List<ImportedCharacter>();
        var warnings = new List<string>();

        foreach (var character in characters.EnumerateArray())
        {
            if (!character.TryGetProperty("id", out var characterIdElement))
            {
                throw new JsonException("캐릭터 ID가 없습니다.");
            }

            var characterId = characterIdElement.GetString()
                ?? throw new JsonException("캐릭터 ID가 없습니다.");
            var characterName = character.TryGetProperty("name", out var characterNameElement)
                ? characterNameElement.GetString() ?? characterId
                : characterId;

            var defaultPortraitUrl = ParseAssetUrl(character, "portrait");
            var skinPortraitUrl = EnkaAssetUrls.CharacterSkinPortrait(
                MihomoAppearanceMetadata.FindSkinId(root, characterId));
            importedCharacters.Add(new ImportedCharacter(
                characterId,
                characterName,
                skinPortraitUrl ?? defaultPortraitUrl,
                skinPortraitUrl is null ? null : defaultPortraitUrl));

            if (!character.TryGetProperty("relics", out var characterRelics)
                || characterRelics.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("캐릭터의 유물 목록이 없습니다.");
            }

            foreach (var relic in characterRelics.EnumerateArray())
            {
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

                    var slotNumber = relic.GetProperty("type").GetInt32();
                    var mainAffix = relic.GetProperty("main_affix");
                    var mainStat = ParseMainStat(mainAffix);
                    var substats = relic.GetProperty("sub_affix")
                        .EnumerateArray()
                        .Select(ParseSubstat)
                        .ToArray();

                    relics.Add(new ImportedRelic(
                        $"{characterId}:{slotNumber}",
                        relic.GetProperty("name").GetString() ?? "이름 없는 유물",
                        relic.GetProperty("set_name").GetString() ?? "세트 정보 없음",
                        characterName,
                        5,
                        relic.GetProperty("level").GetInt32(),
                        ParseSlot(slotNumber),
                        mainStat,
                        substats,
                        ParseMainStatValue(mainAffix),
                        ParseAssetUrl(relic, "icon"),
                        characterId));
                }
                catch (Exception exception) when (IsJsonShapeException(exception))
                {
                    var relicName = relic.TryGetProperty("name", out var nameElement)
                        ? nameElement.GetString() ?? "이름 없는 유물"
                        : "이름 없는 유물";
                    var message = exception is JsonException
                        ? exception.Message
                        : "필수 필드 형식이 올바르지 않습니다.";
                    warnings.Add($"{relicName} 유물을 건너뛰었습니다. {message}");
                }
            }
        }

        return new RelicImportResponse(
            "MiHoMo UID",
            playerName,
            importedCharacters,
            relics,
            warnings);
    }

    private static RelicSubstat ParseSubstat(JsonElement element)
    {
        var field = element.GetProperty("field").GetString()
            ?? throw new JsonException("부옵션 종류가 없습니다.");
        var isPercent = element.GetProperty("percent").GetBoolean();
        var value = element.GetProperty("value").GetDecimal();

        return new RelicSubstat(
            ParseSubstatKind(field, isPercent),
            isPercent ? value * 100m : value);
    }

    private static RelicSlot ParseSlot(int type) => type switch
    {
        1 => RelicSlot.Head,
        2 => RelicSlot.Hands,
        3 => RelicSlot.Body,
        4 => RelicSlot.Feet,
        5 => RelicSlot.PlanarSphere,
        6 => RelicSlot.LinkRope,
        _ => throw new JsonException($"지원하지 않는 유물 부위 번호입니다: {type}")
    };

    private static RelicMainStat ParseMainStat(JsonElement element)
    {
        var field = element.GetProperty("field").GetString()
            ?? throw new JsonException("주옵션 종류가 없습니다.");
        var isPercent = element.GetProperty("percent").GetBoolean();

        return (field, isPercent) switch
        {
            ("hp", false) => RelicMainStat.FlatHp,
            ("atk", false) => RelicMainStat.FlatAttack,
            ("hp", true) => RelicMainStat.HpPercent,
            ("atk", true) => RelicMainStat.AttackPercent,
            ("def", true) => RelicMainStat.DefensePercent,
            ("crit_rate", true) => RelicMainStat.CritRate,
            ("crit_dmg", true) => RelicMainStat.CritDamage,
            ("heal_rate", true) => RelicMainStat.OutgoingHealing,
            ("effect_hit", true) => RelicMainStat.EffectHitRate,
            ("spd", false) => RelicMainStat.Speed,
            ("physical_dmg", true) => RelicMainStat.PhysicalDamage,
            ("fire_dmg", true) => RelicMainStat.FireDamage,
            ("ice_dmg", true) => RelicMainStat.IceDamage,
            ("thunder_dmg", true) => RelicMainStat.LightningDamage,
            ("lightning_dmg", true) => RelicMainStat.LightningDamage,
            ("wind_dmg", true) => RelicMainStat.WindDamage,
            ("quantum_dmg", true) => RelicMainStat.QuantumDamage,
            ("imaginary_dmg", true) => RelicMainStat.ImaginaryDamage,
            ("break_dmg", true) => RelicMainStat.BreakEffect,
            ("sp_rate", true) => RelicMainStat.EnergyRegenerationRate,
            _ => throw new JsonException($"지원하지 않는 주옵션입니다: {field}")
        };
    }

    private static decimal ParseMainStatValue(JsonElement element)
    {
        var value = element.GetProperty("value").GetDecimal();
        return element.GetProperty("percent").GetBoolean()
            ? value * 100m
            : value;
    }

    private static string? ParseAssetUrl(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var pathElement)
            || pathElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var path = pathElement.GetString();
        return StarRailAssetUrls.FromPath(path);
    }

    private static RelicStat ParseSubstatKind(string field, bool isPercent) => (field, isPercent) switch
    {
        ("hp", false) => RelicStat.FlatHp,
        ("atk", false) => RelicStat.FlatAttack,
        ("def", false) => RelicStat.FlatDefense,
        ("hp", true) => RelicStat.HpPercent,
        ("atk", true) => RelicStat.AttackPercent,
        ("def", true) => RelicStat.DefensePercent,
        ("spd", false) => RelicStat.Speed,
        ("crit_rate", true) => RelicStat.CritRate,
        ("crit_dmg", true) => RelicStat.CritDamage,
        ("effect_hit", true) => RelicStat.EffectHitRate,
        ("effect_res", true) => RelicStat.EffectResistance,
        ("break_dmg", true) => RelicStat.BreakEffect,
        _ => throw new JsonException($"지원하지 않는 부옵션입니다: {field}")
    };

    private static bool IsJsonShapeException(Exception exception) => exception is
        JsonException or
        KeyNotFoundException or
        InvalidOperationException or
        FormatException or
        OverflowException;
}
