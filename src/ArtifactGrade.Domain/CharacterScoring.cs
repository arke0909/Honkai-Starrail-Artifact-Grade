namespace ArtifactGrade.Domain;

public enum CharacterStatPriority
{
    Unused,
    Secondary,
    Useful,
    Core
}

public sealed record CharacterStatWeight(
    RelicStat Stat,
    decimal Weight,
    CharacterStatPriority Priority);

public sealed record CharacterMainStatWeight(
    RelicSlot Slot,
    RelicMainStat Stat,
    decimal Weight,
    CharacterStatPriority Priority);

public sealed record CharacterScoringProfile(
    string CharacterId,
    string CharacterName,
    string Archetype,
    IReadOnlyList<CharacterStatWeight> StatWeights,
    IReadOnlyList<CharacterMainStatWeight> MainStatWeights,
    decimal MaximumSubstatScore,
    string Source)
{
    public static CharacterScoringProfile Create(
        string characterId,
        string characterName,
        IReadOnlyDictionary<RelicStat, decimal> weights,
        IReadOnlyDictionary<RelicSlot, IReadOnlyDictionary<RelicMainStat, decimal>> mainStatWeights,
        decimal maximumSubstatScore,
        string source)
    {
        if (weights.Count != Enum.GetValues<RelicStat>().Length
            || Enum.GetValues<RelicStat>().Any(stat => !weights.ContainsKey(stat)))
        {
            throw new ArgumentException("캐릭터 평가에는 12개 부옵션 가중치가 모두 필요합니다.", nameof(weights));
        }

        if (weights.Any(item => item.Value is < 0m or > 1m))
        {
            throw new ArgumentException("부옵션 가중치는 0부터 1 사이여야 합니다.", nameof(weights));
        }

        if (mainStatWeights.Count == 0
            || mainStatWeights.Any(slot => !Enum.IsDefined(slot.Key))
            || mainStatWeights.SelectMany(slot => slot.Value)
                .Any(item => !Enum.IsDefined(item.Key) || item.Value is < 0m or > 1m))
        {
            throw new ArgumentException("주옵션 가중치는 지원하는 부위와 주옵션에 대해 0부터 1 사이여야 합니다.", nameof(mainStatWeights));
        }

        if (maximumSubstatScore <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumSubstatScore),
                "부옵션 정규화 최대값은 0보다 커야 합니다.");
        }

        var statWeights = Enum.GetValues<RelicStat>()
            .Select(stat => new CharacterStatWeight(stat, weights[stat], GetPriority(weights[stat])))
            .ToArray();
        var mainWeights = mainStatWeights
            .SelectMany(slot => slot.Value.Select(item => new CharacterMainStatWeight(
                slot.Key,
                item.Key,
                item.Value,
                GetPriority(item.Value))))
            .ToArray();

        return new CharacterScoringProfile(
            characterId,
            characterName,
            DescribeArchetype(weights),
            statWeights,
            mainWeights,
            maximumSubstatScore,
            source);
    }

    private static CharacterStatPriority GetPriority(decimal weight) => weight switch
    {
        >= 0.8m => CharacterStatPriority.Core,
        >= 0.4m => CharacterStatPriority.Useful,
        > 0m => CharacterStatPriority.Secondary,
        _ => CharacterStatPriority.Unused
    };

    private static string DescribeArchetype(IReadOnlyDictionary<RelicStat, decimal> weights)
    {
        var hasCriticalScaling = weights[RelicStat.CritRate] >= 0.8m
            && weights[RelicStat.CritDamage] >= 0.8m;
        var primaryScaling = new[]
            {
                (Stat: RelicStat.HpPercent, Name: "HP"),
                (Stat: RelicStat.AttackPercent, Name: "공격력"),
                (Stat: RelicStat.DefensePercent, Name: "방어력")
            }
            .OrderByDescending(item => weights[item.Stat])
            .First();

        if (hasCriticalScaling && weights[primaryScaling.Stat] >= 0.6m)
        {
            return $"{primaryScaling.Name} 기반 · 치명타 중심";
        }

        if (weights[RelicStat.BreakEffect] >= 0.8m)
        {
            return weights[primaryScaling.Stat] >= 0.5m
                ? $"{primaryScaling.Name} · 격파 중심"
                : "격파 · 속도 중심";
        }

        if (weights[RelicStat.EffectHitRate] >= 0.8m)
        {
            return weights[RelicStat.AttackPercent] >= 0.5m
                ? "공격력 · 효과 명중 중심"
                : "효과 명중 · 속도 중심";
        }

        if (weights[RelicStat.CritDamage] >= 0.8m && weights[RelicStat.CritRate] < 0.5m)
        {
            return "치명타 피해 · 속도 중심";
        }

        if (weights[primaryScaling.Stat] >= 0.8m)
        {
            return $"{primaryScaling.Name} 중심";
        }

        return "속도 · 생존 중심";
    }
}
