namespace ArtifactGrade.Domain;

public sealed record CharacterGradeCount(string Grade, int Count);

public sealed record CharacterRelicSetCount(string SetName, int PieceCount);

public sealed record CharacterCoreStatContribution(RelicStat Stat, decimal Score);

public sealed record CharacterLoadoutSummary(
    decimal AverageScore,
    string AverageGrade,
    int CoreMainStatCount,
    int ValidRelicCount,
    IReadOnlyList<CharacterGradeCount> GradeCounts,
    IReadOnlyList<CharacterRelicSetCount> SetCounts,
    ScoredImportedRelic ReplacementPriority,
    string ReplacementReason,
    IReadOnlyList<CharacterCoreStatContribution> CoreStatContributions);

public static class CharacterLoadoutSummarizer
{
    public static CharacterLoadoutSummary? Create(
        CharacterScoringProfile profile,
        IReadOnlyList<ScoredImportedRelic> scores)
    {
        var validScores = scores.Where(item => item.Result.IsValid).ToArray();
        if (validScores.Length == 0)
        {
            return null;
        }

        var averageScore = decimal.Round(
            validScores.Average(item => item.Result.Score),
            1,
            MidpointRounding.AwayFromZero);
        var gradeCounts = CharacterGradeCatalog.Grades
            .Select(grade => new CharacterGradeCount(
                grade,
                validScores.Count(item => item.Result.Grade == grade)))
            .Where(item => item.Count > 0)
            .ToArray();
        var setCounts = scores
            .GroupBy(item => item.Relic.SetName)
            .Select(group => new CharacterRelicSetCount(group.Key, group.Count()))
            .OrderByDescending(item => item.PieceCount)
            .ThenBy(item => item.SetName, StringComparer.Ordinal)
            .ToArray();
        var weakest = validScores
            .OrderBy(item => item.Result.Score)
            .ThenBy(item => item.Relic.Slot)
            .First();
        var coreStats = profile.StatWeights
            .Where(item => item.Priority == CharacterStatPriority.Core)
            .Select(item => item.Stat)
            .ToHashSet();
        var coreContributions = validScores
            .SelectMany(item => item.Result.Contributions)
            .Where(item => coreStats.Contains(item.Stat))
            .GroupBy(item => item.Stat)
            .Select(group => new CharacterCoreStatContribution(
                group.Key,
                group.Sum(item => item.Score)))
            .Where(item => item.Score > 0m)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Stat)
            .Take(3)
            .ToArray();

        return new CharacterLoadoutSummary(
            averageScore,
            CharacterGradeCatalog.GetGrade(averageScore),
            validScores.Count(item => item.Result.MainStat?.Priority == CharacterStatPriority.Core),
            validScores.Length,
            gradeCounts,
            setCounts,
            weakest,
            ReplacementReason(profile, weakest),
            coreContributions);
    }

    private static string ReplacementReason(
        CharacterScoringProfile profile,
        ScoredImportedRelic scoredRelic)
    {
        var mainStatReason = scoredRelic.Result.MainStat?.Priority switch
        {
            null => "주옵션 평가 없음",
            CharacterStatPriority.Unused => "주옵션 비유효",
            CharacterStatPriority.Secondary => "주옵션 보조",
            CharacterStatPriority.Useful => "주옵션 유효 · 핵심 아님",
            _ => null
        };
        if (mainStatReason is not null)
        {
            return mainStatReason;
        }

        var unusedStats = profile.StatWeights
            .Where(item => item.Priority == CharacterStatPriority.Unused)
            .Select(item => item.Stat)
            .ToHashSet();
        var unusedSubstatCount = scoredRelic.Relic.Substats.Count(item => unusedStats.Contains(item.Stat));
        return unusedSubstatCount > 0
            ? $"비유효 부옵션 {unusedSubstatCount}개"
            : "현재 장착 유물 중 최저점";
    }
}
