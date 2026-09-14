using ArtifactGrade.Domain;

namespace ArtifactGrade.Domain.Tests;

public sealed class RelicBatchScorerTests
{
    [Fact]
    public void ScoresEveryImportedRelicWithSelectedProfile()
    {
        var relics = new[]
        {
            new ImportedRelic(
                "first",
                "First relic",
                "First set",
                "Character",
                5,
                15,
                RelicSlot.Head,
                RelicMainStat.FlatHp,
                [new RelicSubstat(RelicStat.CritRate, 6.48m)]),
            new ImportedRelic(
                "second",
                "Second relic",
                "Second set",
                null,
                5,
                15,
                RelicSlot.Hands,
                RelicMainStat.FlatAttack,
                [new RelicSubstat(RelicStat.CritDamage, 12.96m)])
        };

        var results = RelicBatchScorer.Calculate(ScoringProfileId.Critical, relics);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(20m, result.Result.Score));
        Assert.Equal("first", results[0].Relic.Key);
        Assert.Equal("second", results[1].Relic.Key);
    }

    [Fact]
    public void RejectsImportedRelicThatIsNotFiveStar()
    {
        var relic = new ImportedRelic(
            "four-star",
            "Four-star relic",
            "Test set",
            null,
            4,
            12,
            RelicSlot.Head,
            RelicMainStat.FlatHp,
            [new RelicSubstat(RelicStat.CritRate, 6.48m)]);

        var result = Assert.Single(RelicBatchScorer.Calculate(
            ScoringProfileId.Critical,
            [relic]));

        Assert.False(result.Result.IsValid);
        Assert.Contains(result.Result.Errors, error => error.Contains("5성", StringComparison.Ordinal));
    }
}
