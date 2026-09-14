using ArtifactGrade.Domain;

namespace ArtifactGrade.Api;

internal static class StarRailAssetUrls
{
    private const string BaseUrl = "https://raw.githubusercontent.com/Mar-7th/StarRailRes/master/";

    public static string? FromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalizedPath = path.TrimStart('/');
        if ((!normalizedPath.StartsWith("icon/", StringComparison.Ordinal)
                && !normalizedPath.StartsWith("image/", StringComparison.Ordinal))
            || normalizedPath.Contains("..", StringComparison.Ordinal)
            || normalizedPath.Contains('\\', StringComparison.Ordinal))
        {
            return null;
        }

        return $"{BaseUrl}{normalizedPath}";
    }

    public static string? CharacterPortrait(string? characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)
            || characterId.Any(character => !char.IsAsciiDigit(character)))
        {
            return null;
        }

        return $"{BaseUrl}image/character_portrait/{characterId}.png";
    }

    public static string? RelicIcon(string? setId, RelicSlot slot)
    {
        if (string.IsNullOrWhiteSpace(setId)
            || setId.Any(character => !char.IsAsciiDigit(character)))
        {
            return null;
        }

        var slotIndex = slot switch
        {
            RelicSlot.Head => 0,
            RelicSlot.Hands => 1,
            RelicSlot.Body => 2,
            RelicSlot.Feet => 3,
            RelicSlot.PlanarSphere => 0,
            RelicSlot.LinkRope => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), "지원하지 않는 유물 부위입니다.")
        };

        return $"{BaseUrl}icon/relic/{setId}_{slotIndex}.png";
    }
}
