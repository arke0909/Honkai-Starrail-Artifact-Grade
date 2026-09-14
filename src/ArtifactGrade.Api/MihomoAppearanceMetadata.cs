using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArtifactGrade.Api;

internal static class MihomoAppearanceMetadata
{
    private const string CheckedPropertyName = "_appearance_checked";
    private const string SkinIdsPropertyName = "_dressed_skin_ids";

    public static bool HasBeenChecked(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(CheckedPropertyName, out var checkedElement)
            && checkedElement.ValueKind == JsonValueKind.True;
    }

    public static string Merge(string parsedJson, string rawJson)
    {
        using var rawDocument = JsonDocument.Parse(rawJson);
        if (!rawDocument.RootElement.TryGetProperty("detailInfo", out var detailInfo)
            || detailInfo.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("MiHoMo 원본 응답에 캐릭터 상세 정보가 없습니다.");
        }

        var skinIds = new JsonObject();
        var hasAvatarList = ReadCharacters(detailInfo, "avatarDetailList", skinIds);
        var hasAssistList = ReadCharacters(detailInfo, "assistAvatarList", skinIds);
        if (!hasAvatarList && !hasAssistList)
        {
            throw new JsonException("MiHoMo 원본 응답에 캐릭터 목록이 없습니다.");
        }

        var parsedRoot = JsonNode.Parse(parsedJson) as JsonObject
            ?? throw new JsonException("MiHoMo parsed 응답 루트가 객체가 아닙니다.");
        parsedRoot[CheckedPropertyName] = true;
        parsedRoot[SkinIdsPropertyName] = skinIds;
        return parsedRoot.ToJsonString();
    }

    public static string? FindSkinId(JsonElement parsedRoot, string characterId)
    {
        if (!parsedRoot.TryGetProperty(SkinIdsPropertyName, out var skinIds)
            || skinIds.ValueKind != JsonValueKind.Object
            || !skinIds.TryGetProperty(characterId, out var skinId))
        {
            return null;
        }

        return ReadId(skinId);
    }

    private static bool ReadCharacters(
        JsonElement detailInfo,
        string propertyName,
        JsonObject skinIds)
    {
        if (!detailInfo.TryGetProperty(propertyName, out var characters))
        {
            return false;
        }

        if (characters.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"MiHoMo 원본 응답의 {propertyName} 형식이 올바르지 않습니다.");
        }

        foreach (var character in characters.EnumerateArray())
        {
            if (!character.TryGetProperty("avatarId", out var characterIdElement)
                || !character.TryGetProperty("dressedSkinId", out var skinIdElement))
            {
                continue;
            }

            var characterId = ReadId(characterIdElement);
            var skinId = ReadId(skinIdElement);
            if (characterId is not null && skinId is not null)
            {
                skinIds[characterId] = skinId;
            }
        }

        return true;
    }

    private static string? ReadId(JsonElement element)
    {
        var value = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            _ => null
        };

        return !string.IsNullOrWhiteSpace(value)
            && value.Any(character => character != '0')
            && value.All(char.IsAsciiDigit)
                ? value
                : null;
    }
}
