using ArtifactGrade.Domain;

namespace ArtifactGrade.Domain.Tests;

public sealed class CharacterScoringProfileTests
{
    [Fact]
    public void DescribesCastoriceAsHpBasedCriticalDealer()
    {
        var profile = CharacterScoringProfile.Create(
            "1407",
            "카스토리스",
            CreateWeights(
                (RelicStat.HpPercent, 1m),
                (RelicStat.CritRate, 1m),
                (RelicStat.CritDamage, 1m),
                (RelicStat.FlatHp, 0.3m),
                (RelicStat.Speed, 0.1m)),
            CreateMainStatWeights((RelicSlot.Head, RelicMainStat.FlatHp, 1m)),
            10m,
            "test source");

        Assert.Equal("HP 기반 · 치명타 중심", profile.Archetype);
        Assert.Equal(12, profile.StatWeights.Count);
        Assert.Equal(10m, profile.MaximumSubstatScore);
        Assert.Equal(
            CharacterStatPriority.Core,
            profile.MainStatWeights.Single().Priority);
        Assert.Equal(
            CharacterStatPriority.Core,
            profile.StatWeights.Single(item => item.Stat == RelicStat.HpPercent).Priority);
        Assert.Equal(
            CharacterStatPriority.Unused,
            profile.StatWeights.Single(item => item.Stat == RelicStat.AttackPercent).Priority);
    }

    [Fact]
    public void ScoresRelicsWithTheCharacterSpecificWeights()
    {
        var profile = CharacterScoringProfile.Create(
            "1407",
            "카스토리스",
            CreateWeights(
                (RelicStat.HpPercent, 1m),
                (RelicStat.CritRate, 1m),
                (RelicStat.CritDamage, 1m)),
            CreateMainStatWeights((RelicSlot.Head, RelicMainStat.FlatHp, 1m)),
            10m,
            "test source");
        var relic = new ImportedRelic(
            "castorice-relic",
            "Test relic",
            "Test set",
            "카스토리스",
            5,
            15,
            RelicSlot.Head,
            RelicMainStat.FlatHp,
            [
                new RelicSubstat(RelicStat.HpPercent, 4.32m),
                new RelicSubstat(RelicStat.AttackPercent, 4.32m)
            ],
            705.6m,
            EquippedCharacterId: "1407");

        var scored = Assert.Single(RelicBatchScorer.Calculate(profile, [relic]));

        Assert.Equal(56m, scored.Result.Score);
        Assert.Equal(50m, scored.Result.MainStat!.Score);
        Assert.Equal(6m, scored.Result.Contributions[0].Score);
        Assert.Equal(0m, scored.Result.Contributions[1].Score);
    }

    [Fact]
    public void ScoresTheTheoreticalBestRelicAtOneHundred()
    {
        var profile = CharacterScoringProfile.Create(
            "test-character",
            "테스트 캐릭터",
            CreateWeights(
                (RelicStat.HpPercent, 1m),
                (RelicStat.Speed, 1m),
                (RelicStat.CritRate, 1m),
                (RelicStat.CritDamage, 1m)),
            CreateMainStatWeights((RelicSlot.Head, RelicMainStat.FlatHp, 1m)),
            10.8m,
            "test source");
        var relic = new ImportedRelic(
            "perfect-relic",
            "Perfect relic",
            "Test set",
            "테스트 캐릭터",
            5,
            15,
            RelicSlot.Head,
            RelicMainStat.FlatHp,
            [
                new RelicSubstat(RelicStat.HpPercent, 25.92m),
                new RelicSubstat(RelicStat.Speed, 2.6m),
                new RelicSubstat(RelicStat.CritRate, 3.24m),
                new RelicSubstat(RelicStat.CritDamage, 6.48m)
            ],
            705.6m,
            EquippedCharacterId: "test-character");

        var scored = Assert.Single(RelicBatchScorer.Calculate(profile, [relic]));

        Assert.Equal(100m, scored.Result.Score);
        Assert.Equal(50m, scored.Result.MainStat!.Score);
        Assert.Equal(50m, scored.Result.Contributions.Sum(item => item.Score));
    }

    [Fact]
    public void RejectsIncompleteCharacterWeightData()
    {
        var weights = CreateWeights((RelicStat.HpPercent, 1m));
        weights.Remove(RelicStat.BreakEffect);

        var exception = Assert.Throws<ArgumentException>(() => CharacterScoringProfile.Create(
            "1407",
            "카스토리스",
            weights,
            CreateMainStatWeights((RelicSlot.Head, RelicMainStat.FlatHp, 1m)),
            10m,
            "test source"));

        Assert.Contains("12개", exception.Message, StringComparison.Ordinal);
    }

    private static Dictionary<RelicStat, decimal> CreateWeights(
        params (RelicStat Stat, decimal Weight)[] overrides)
    {
        var weights = Enum.GetValues<RelicStat>().ToDictionary(stat => stat, _ => 0m);
        foreach (var (stat, weight) in overrides)
        {
            weights[stat] = weight;
        }

        return weights;
    }

    private static Dictionary<RelicSlot, IReadOnlyDictionary<RelicMainStat, decimal>> CreateMainStatWeights(
        params (RelicSlot Slot, RelicMainStat Stat, decimal Weight)[] values) => values
        .GroupBy(item => item.Slot)
        .ToDictionary(
            group => group.Key,
            group => (IReadOnlyDictionary<RelicMainStat, decimal>)group.ToDictionary(
                item => item.Stat,
                item => item.Weight));
}
