using ArtifactGrade.Domain;

namespace ArtifactGrade.Domain.Tests;

public sealed class CharacterLoadoutSummaryTests
{
    [Fact]
    public void SummarizesCharacterRelicScoresForTheShowcase()
    {
        var profile = new CharacterScoringProfile(
            "1407",
            "카스토리스",
            "HP 기반 · 치명타 중심",
            [
                new CharacterStatWeight(RelicStat.HpPercent, 1m, CharacterStatPriority.Core),
                new CharacterStatWeight(RelicStat.CritRate, 1m, CharacterStatPriority.Core),
                new CharacterStatWeight(RelicStat.AttackPercent, 0m, CharacterStatPriority.Unused)
            ],
            [],
            10m,
            "test source");
        var scores = new[]
        {
            CreateScore("head", "구세주", RelicSlot.Head, 98m, "SSS", CharacterStatPriority.Core,
                new StatContribution(RelicStat.HpPercent, 10m, 1m),
                new StatContribution(RelicStat.CritRate, 5m, 1m)),
            CreateScore("hands", "구세주", RelicSlot.Hands, 92m, "SS", CharacterStatPriority.Core,
                new StatContribution(RelicStat.HpPercent, 8m, 1m),
                new StatContribution(RelicStat.CritRate, 5m, 1m)),
            CreateScore("body", "습골지", RelicSlot.Body, 72m, "A", CharacterStatPriority.Unused,
                new StatContribution(RelicStat.HpPercent, 2m, 1m),
                new StatContribution(RelicStat.AttackPercent, 0m, 0m))
        };

        var summary = CharacterLoadoutSummarizer.Create(profile, scores);

        Assert.NotNull(summary);
        Assert.Equal(87.3m, summary.AverageScore);
        Assert.Equal("S", summary.AverageGrade);
        Assert.Equal(2, summary.CoreMainStatCount);
        Assert.Equal(
            [("SSS", 1), ("SS", 1), ("A", 1)],
            summary.GradeCounts.Select(item => (item.Grade, item.Count)));
        Assert.Equal(
            [("구세주", 2), ("습골지", 1)],
            summary.SetCounts.Select(item => (item.SetName, item.PieceCount)));
        Assert.Equal("body", summary.ReplacementPriority.Relic.Key);
        Assert.Equal("주옵션 비유효", summary.ReplacementReason);
        Assert.Equal(
            [(RelicStat.HpPercent, 20m), (RelicStat.CritRate, 10m)],
            summary.CoreStatContributions.Select(item => (item.Stat, item.Score)));
    }

    [Fact]
    public void DoesNotCreateSummaryWithoutAValidScore()
    {
        var invalid = CreateScore(
            "invalid",
            "테스트 세트",
            RelicSlot.Head,
            0m,
            "-",
            CharacterStatPriority.Unused) with
        {
            Result = new ScoreResult(false, 0m, "-", [], ["계산 실패"])
        };
        var profile = new CharacterScoringProfile(
            "test",
            "테스트",
            "테스트",
            [],
            [],
            1m,
            "test source");

        Assert.Null(CharacterLoadoutSummarizer.Create(profile, [invalid]));
    }

    private static ScoredImportedRelic CreateScore(
        string key,
        string setName,
        RelicSlot slot,
        decimal score,
        string grade,
        CharacterStatPriority mainStatPriority,
        params StatContribution[] contributions)
    {
        var relic = new ImportedRelic(
            key,
            $"{key} relic",
            setName,
            "테스트",
            5,
            15,
            slot,
            slot == RelicSlot.Hands ? RelicMainStat.FlatAttack : RelicMainStat.FlatHp,
            [new RelicSubstat(RelicStat.HpPercent, 4.32m)],
            1m);
        var result = new ScoreResult(
            true,
            score,
            grade,
            contributions,
            [],
            new MainStatContribution(1m, 50m, mainStatPriority));

        return new ScoredImportedRelic(relic, result);
    }
}
