using ArtifactGrade.Domain;

namespace ArtifactGrade.Domain.Tests;

public sealed class RelicCharacterGroupingTests
{
    [Fact]
    public void GroupsRelicsByEquippedCharacterAndPlacesUnequippedLast()
    {
        var bronyaHead = CreateRelic("bronya-head", "Bronya");
        var unequipped = CreateRelic("inventory-head", null);
        var bronyaHands = CreateRelic("bronya-hands", "Bronya");
        var seeleHead = CreateRelic("seele-head", "Seele");

        var groups = RelicCharacterGrouping.Create(
            [bronyaHead, unequipped, bronyaHands, seeleHead]);

        Assert.Equal(["Bronya", "Seele", null], groups.Select(group => group.CharacterName));
        Assert.Equal([bronyaHead, bronyaHands], groups[0].Relics);
        Assert.Equal([seeleHead], groups[1].Relics);
        Assert.Equal([unequipped], groups[2].Relics);
        Assert.Null(groups[2].CharacterName);
    }

    private static ImportedRelic CreateRelic(string key, string? equippedBy) => new(
        key,
        key,
        "Test Set",
        equippedBy,
        5,
        15,
        RelicSlot.Head,
        RelicMainStat.FlatHp,
        [new RelicSubstat(RelicStat.CritRate, 3.24m)]);
}
