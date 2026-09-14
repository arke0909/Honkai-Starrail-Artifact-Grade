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
    IReadOnlyList<RelicSubstat> Substats);

public sealed record RelicImportResponse(
    string Source,
    string? PlayerName,
    IReadOnlyList<ImportedRelic> Relics,
    IReadOnlyList<string> Warnings,
    bool FromCache = false);

public sealed record RelicBatchScoreRequest(
    ScoringProfileId Profile,
    IReadOnlyList<ImportedRelic> Relics);

public sealed record ScoredImportedRelic(
    ImportedRelic Relic,
    ScoreResult Result);

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
}
