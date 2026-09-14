namespace ArtifactGrade.Domain;

public sealed record ImportedRelic(
    string Key,
    string Name,
    string SetName,
    string? EquippedBy,
    int Rarity,
    int Level,
    RelicSlot Slot,
    RelicMainStat MainStat,
    IReadOnlyList<RelicSubstat> Substats,
    decimal? MainStatValue,
    string? ImageUrl = null,
    string? EquippedCharacterId = null);

public sealed record ImportedCharacter(
    string Id,
    string Name,
    string? ImageUrl,
    string? FallbackImageUrl = null);

public sealed record RelicImportResponse(
    string Source,
    string? PlayerName,
    IReadOnlyList<ImportedCharacter> Characters,
    IReadOnlyList<ImportedRelic> Relics,
    IReadOnlyList<string> Warnings,
    bool FromCache = false);

public sealed record RelicBatchScoreRequest(
    ScoringProfileId Profile,
    IReadOnlyList<ImportedRelic> Relics);

public sealed record CharacterRelicBatchScoreRequest(
    string CharacterId,
    string CharacterName,
    IReadOnlyList<ImportedRelic> Relics)
{
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(CharacterId)
            || CharacterId.Length > 32
            || CharacterId.Any(character => !char.IsAsciiDigit(character)))
        {
            return "캐릭터 ID는 32자 이하의 숫자여야 합니다.";
        }

        if (string.IsNullOrWhiteSpace(CharacterName) || CharacterName.Length > 100)
        {
            return "캐릭터 이름은 1자부터 100자까지 입력할 수 있습니다.";
        }

        if (Relics is null
            || Relics.Count is < 1 or > 300
            || Relics.Any(static relic => relic is null))
        {
            return "한 번에 1개부터 300개 유물까지 계산할 수 있습니다.";
        }

        if (Relics.Any(relic =>
                string.IsNullOrWhiteSpace(relic.Key) || relic.Key.Length > 256
                || string.IsNullOrWhiteSpace(relic.Name) || relic.Name.Length > 200
                || string.IsNullOrWhiteSpace(relic.SetName) || relic.SetName.Length > 200
                || relic.EquippedBy?.Length > 100
                || relic.ImageUrl?.Length > 2048
                || relic.EquippedCharacterId?.Length > 32))
        {
            return "유물의 이름, 세트, 식별자 또는 이미지 주소가 허용 길이를 초과했습니다.";
        }

        return null;
    }
}

public sealed record CharacterRelicBatchScoreResponse(
    CharacterScoringProfile Profile,
    IReadOnlyList<ScoredImportedRelic> Scores,
    bool RedisSaved,
    string? Warning);

public sealed record ScoredImportedRelic(
    ImportedRelic Relic,
    ScoreResult Result);

public sealed record RelicCharacterGroup(
    string? CharacterId,
    string? CharacterName,
    IReadOnlyList<ImportedRelic> Relics);

public static class RelicCharacterGrouping
{
    public static IReadOnlyList<RelicCharacterGroup> Create(
        IReadOnlyList<ImportedRelic> relics) => relics
        .GroupBy(relic => (
            CharacterId: string.IsNullOrWhiteSpace(relic.EquippedCharacterId)
                ? null
                : relic.EquippedCharacterId.Trim(),
            CharacterName: string.IsNullOrWhiteSpace(relic.EquippedBy)
                ? null
                : relic.EquippedBy.Trim()))
        .OrderBy(group => group.Key.CharacterId is null && group.Key.CharacterName is null ? 1 : 0)
        .Select(group => new RelicCharacterGroup(
            group.Key.CharacterId,
            group.Key.CharacterName,
            group.ToArray()))
        .ToArray();
}

public static class RelicBatchScorer
{
    public static IReadOnlyList<ScoredImportedRelic> Calculate(
        ScoringProfileId profile,
        IReadOnlyList<ImportedRelic> relics) => relics
        .Select(relic => relic.Rarity == 5
            ? new ScoredImportedRelic(
                relic,
                ScoreCalculator.Calculate(new ScoreRequest(
                    profile,
                    relic.Level,
                    relic.Substats,
                    relic.Slot,
                    relic.MainStat)))
            : new ScoredImportedRelic(
                relic,
                new ScoreResult(false, 0m, "-", [], ["5성 유물만 계산할 수 있습니다."])))
        .ToArray();

    public static IReadOnlyList<ScoredImportedRelic> Calculate(
        CharacterScoringProfile profile,
        IReadOnlyList<ImportedRelic> relics)
    {
        var weights = profile.StatWeights.ToDictionary(item => item.Stat, item => item.Weight);
        return relics
            .Select(relic => Score(profile, weights, relic))
            .ToArray();
    }

    private static ScoredImportedRelic Score(
        CharacterScoringProfile profile,
        IReadOnlyDictionary<RelicStat, decimal> weights,
        ImportedRelic relic)
    {
        if (relic.Rarity != 5)
        {
            return new ScoredImportedRelic(
                relic,
                new ScoreResult(false, 0m, "-", [], ["5성 유물만 계산할 수 있습니다."]));
        }

        var mainStatWeight = profile.MainStatWeights
            .FirstOrDefault(item => item.Slot == relic.Slot && item.Stat == relic.MainStat)
            ?.Weight ?? 0m;
        return new ScoredImportedRelic(
            relic,
            ScoreCalculator.Calculate(new CharacterScoreRequest(
                relic.Level,
                relic.Substats,
                weights,
                mainStatWeight,
                profile.MaximumSubstatScore,
                relic.Slot,
                relic.MainStat)));
    }
}
