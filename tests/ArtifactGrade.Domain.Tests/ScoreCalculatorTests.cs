using ArtifactGrade.Domain;

namespace ArtifactGrade.Domain.Tests;

public sealed class ScoreCalculatorTests
{
    [Fact]
    public void CalculatesContributionsAndGradeUsingHighestRollsAndProfileWeights()
    {
        var result = ScoreCalculator.Calculate(new ScoreRequest(
            ScoringProfileId.Critical,
            15,
            [
                new RelicSubstat(RelicStat.CritRate, 6.48m),
                new RelicSubstat(RelicStat.CritDamage, 12.96m),
                new RelicSubstat(RelicStat.AttackPercent, 8.64m)
            ]));

        Assert.True(result.IsValid);
        Assert.Equal(50m, result.Score);
        Assert.Equal("A", result.Grade);
        Assert.Equal(3, result.Contributions.Count);
        Assert.Equal(20m, result.Contributions[0].Score);
        Assert.Equal(1m, result.Contributions[0].Weight);
    }

    [Fact]
    public void ReturnsErrorsForInvalidLevelAndSubstats()
    {
        var result = ScoreCalculator.Calculate(new ScoreRequest(
            ScoringProfileId.Critical,
            16,
            [
                new RelicSubstat(RelicStat.CritRate, 3.24m),
                new RelicSubstat(RelicStat.CritRate, -1m)
            ]));

        Assert.False(result.IsValid);
        Assert.Equal(0m, result.Score);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains(result.Errors, error => error.Contains("강화 단계", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("중복", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("0 이상", StringComparison.Ordinal));
    }

    [Fact]
    public void AppliesBreakProfileWeights()
    {
        var result = ScoreCalculator.Calculate(new ScoreRequest(
            ScoringProfileId.Break,
            15,
            [
                new RelicSubstat(RelicStat.BreakEffect, 12.96m),
                new RelicSubstat(RelicStat.Speed, 5.2m)
            ]));

        Assert.True(result.IsValid);
        Assert.Equal(40m, result.Score);
        Assert.Equal("B", result.Grade);
    }

    [Fact]
    public void CalculatesEverySupportedProfile()
    {
        var cases = new[]
        {
            (ScoringProfileId.Attack, RelicStat.AttackPercent),
            (ScoringProfileId.Hp, RelicStat.HpPercent),
            (ScoringProfileId.Defense, RelicStat.DefensePercent),
            (ScoringProfileId.Support, RelicStat.EffectHitRate)
        };

        foreach (var (profile, stat) in cases)
        {
            var result = ScoreCalculator.Calculate(new ScoreRequest(
                profile,
                15,
                [new RelicSubstat(stat, HighestRoll(stat))]));

            Assert.True(result.IsValid);
            Assert.Equal(10m, result.Score);
        }
    }

    [Fact]
    public void ProvidesDisplayMetadataForEveryProfileAndSubstat()
    {
        Assert.Equal(6, RelicCatalog.Profiles.Count);
        Assert.Equal(12, RelicCatalog.Substats.Count);
        Assert.Equal("치명타 확률", RelicCatalog.Substats.Single(item => item.Value == RelicStat.CritRate).Name);
        Assert.Equal("%", RelicCatalog.Substats.Single(item => item.Value == RelicStat.CritRate).Unit);
    }

    [Fact]
    public void RejectsMainStatThatCannotAppearOnSelectedSlot()
    {
        var result = ScoreCalculator.Calculate(new ScoreRequest(
            ScoringProfileId.Critical,
            15,
            [new RelicSubstat(RelicStat.CritRate, 3.24m)],
            RelicSlot.Body,
            RelicMainStat.Speed));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("주옵션", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsSubstatThatMatchesMainStat()
    {
        var result = ScoreCalculator.Calculate(new ScoreRequest(
            ScoringProfileId.Hp,
            15,
            [new RelicSubstat(RelicStat.FlatHp, 42.33751m)],
            RelicSlot.Head,
            RelicMainStat.FlatHp));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("주옵션과 같은", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsSubstatValueAboveTheMaximumPossibleAtItsLevel()
    {
        var result = ScoreCalculator.Calculate(new ScoreRequest(
            ScoringProfileId.Critical,
            0,
            [new RelicSubstat(RelicStat.CritDamage, 12.96m)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("강화 단계에서 가능한 최대치", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsUndefinedEnumValuesWithoutThrowing()
    {
        var result = ScoreCalculator.Calculate(new ScoreRequest(
            (ScoringProfileId)999,
            15,
            [new RelicSubstat((RelicStat)999, 1m)],
            (RelicSlot)999,
            (RelicMainStat)999));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("평가 프로필", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("유물 부위", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("주옵션", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("부옵션 종류", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsNullSubstatsWithoutThrowing()
    {
        var result = ScoreCalculator.Calculate(new ScoreRequest(
            ScoringProfileId.Critical,
            15,
            null!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("부옵션 목록", StringComparison.Ordinal));
    }

    private static decimal HighestRoll(RelicStat stat) => stat switch
    {
        RelicStat.AttackPercent => 4.32m,
        RelicStat.HpPercent => 4.32m,
        RelicStat.DefensePercent => 5.4m,
        RelicStat.EffectHitRate => 4.32m,
        _ => throw new ArgumentOutOfRangeException(nameof(stat))
    };
}
