namespace ArtifactGrade.Api;

internal static class EnkaAssetUrls
{
    private const string SkinPortraitBaseUrl =
        "https://enka.network/ui/hsr/SpriteOutput/AvatarDrawCard/AvatarSkin/";

    public static string? CharacterSkinPortrait(string? skinId)
    {
        if (string.IsNullOrWhiteSpace(skinId)
            || skinId.Any(character => !char.IsAsciiDigit(character)))
        {
            return null;
        }

        return $"{SkinPortraitBaseUrl}{skinId}.png";
    }
}
