using ArtifactGrade.Domain;

namespace ArtifactGrade.Domain.Tests;

public sealed class CharacterRelicBatchScoreRequestTests
{
    [Fact]
    public void AcceptsAValidImportedRelicBatch()
    {
        var request = CreateRequest("카스토리스", "구세주의 등정 후드");

        Assert.Null(request.Validate());
    }

    [Theory]
    [InlineData(101, 10)]
    [InlineData(10, 201)]
    public void RejectsOversizedNames(int characterNameLength, int relicNameLength)
    {
        var request = CreateRequest(
            new string('캐', characterNameLength),
            new string('유', relicNameLength));

        Assert.NotNull(request.Validate());
    }

    private static CharacterRelicBatchScoreRequest CreateRequest(
        string characterName,
        string relicName) => new(
        "1407",
        characterName,
        [
            new ImportedRelic(
                "relic-key",
                relicName,
                "천지를 재창조한 구세주",
                characterName,
                5,
                15,
                RelicSlot.Head,
                RelicMainStat.FlatHp,
                [new RelicSubstat(RelicStat.CritRate, 3.24m)],
                705.6m,
                EquippedCharacterId: "1407")
        ]);
}
