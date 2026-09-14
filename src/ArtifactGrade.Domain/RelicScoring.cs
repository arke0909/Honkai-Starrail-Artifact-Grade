namespace ArtifactGrade.Domain;

public enum RelicStat
{
    FlatHp,
    FlatAttack,
    FlatDefense,
    HpPercent,
    AttackPercent,
    DefensePercent,
    Speed,
    CritRate,
    CritDamage,
    EffectHitRate,
    EffectResistance,
    BreakEffect
}

public enum ScoringProfileId
{
    Critical,
    Attack,
    Hp,
    Defense,
    Break,
    Support
}

public enum RelicSlot
{
    Head,
    Hands,
    Body,
    Feet,
    PlanarSphere,
    LinkRope
}

public enum RelicMainStat
{
    FlatHp,
    FlatAttack,
    HpPercent,
    AttackPercent,
    DefensePercent,
    CritRate,
    CritDamage,
    OutgoingHealing,
    EffectHitRate,
    Speed,
    PhysicalDamage,
    FireDamage,
    IceDamage,
    LightningDamage,
    WindDamage,
    QuantumDamage,
    ImaginaryDamage,
    BreakEffect,
    EnergyRegenerationRate
}

public sealed record RelicSubstat(RelicStat Stat, decimal Value);

public sealed record ScoreRequest(
    ScoringProfileId Profile,
    int Level,
    IReadOnlyList<RelicSubstat> Substats,
    RelicSlot Slot = RelicSlot.Head,
    RelicMainStat MainStat = RelicMainStat.FlatHp);

public sealed record StatContribution(
    RelicStat Stat,
    decimal Score,
    decimal Weight);

public sealed record ScoreResult(
    bool IsValid,
    decimal Score,
    string Grade,
    IReadOnlyList<StatContribution> Contributions,
    IReadOnlyList<string> Errors);

public static class ScoreCalculator
{
    private static readonly IReadOnlyDictionary<RelicStat, decimal> HighestRolls =
        new Dictionary<RelicStat, decimal>
        {
            [RelicStat.FlatHp] = 42.33751m,
            [RelicStat.FlatAttack] = 21.168754m,
            [RelicStat.FlatDefense] = 21.168754m,
            [RelicStat.HpPercent] = 4.32m,
            [RelicStat.AttackPercent] = 4.32m,
            [RelicStat.DefensePercent] = 5.4m,
            [RelicStat.Speed] = 2.6m,
            [RelicStat.CritRate] = 3.24m,
            [RelicStat.CritDamage] = 6.48m,
            [RelicStat.EffectHitRate] = 4.32m,
            [RelicStat.EffectResistance] = 4.32m,
            [RelicStat.BreakEffect] = 6.48m
        };

    private static readonly IReadOnlyDictionary<ScoringProfileId, IReadOnlyDictionary<RelicStat, decimal>> ProfileWeights =
        new Dictionary<ScoringProfileId, IReadOnlyDictionary<RelicStat, decimal>>
        {
            [ScoringProfileId.Critical] = new Dictionary<RelicStat, decimal>
            {
                [RelicStat.CritRate] = 1m,
                [RelicStat.CritDamage] = 1m,
                [RelicStat.Speed] = 0.8m,
                [RelicStat.AttackPercent] = 0.5m,
                [RelicStat.FlatAttack] = 0.2m
            },
            [ScoringProfileId.Attack] = new Dictionary<RelicStat, decimal>
            {
                [RelicStat.AttackPercent] = 1m,
                [RelicStat.FlatAttack] = 0.5m,
                [RelicStat.Speed] = 0.8m,
                [RelicStat.CritRate] = 0.7m,
                [RelicStat.CritDamage] = 0.7m
            },
            [ScoringProfileId.Hp] = new Dictionary<RelicStat, decimal>
            {
                [RelicStat.HpPercent] = 1m,
                [RelicStat.FlatHp] = 0.5m,
                [RelicStat.Speed] = 0.8m,
                [RelicStat.EffectResistance] = 0.5m,
                [RelicStat.CritRate] = 0.4m,
                [RelicStat.CritDamage] = 0.4m
            },
            [ScoringProfileId.Defense] = new Dictionary<RelicStat, decimal>
            {
                [RelicStat.DefensePercent] = 1m,
                [RelicStat.FlatDefense] = 0.5m,
                [RelicStat.Speed] = 0.8m,
                [RelicStat.EffectResistance] = 0.5m,
                [RelicStat.CritRate] = 0.4m,
                [RelicStat.CritDamage] = 0.4m
            },
            [ScoringProfileId.Break] = new Dictionary<RelicStat, decimal>
            {
                [RelicStat.BreakEffect] = 1m,
                [RelicStat.Speed] = 1m,
                [RelicStat.AttackPercent] = 0.4m,
                [RelicStat.EffectResistance] = 0.3m
            },
            [ScoringProfileId.Support] = new Dictionary<RelicStat, decimal>
            {
                [RelicStat.EffectHitRate] = 1m,
                [RelicStat.Speed] = 1m,
                [RelicStat.EffectResistance] = 0.7m,
                [RelicStat.HpPercent] = 0.5m,
                [RelicStat.DefensePercent] = 0.5m
            }
        };

    private static readonly IReadOnlyDictionary<RelicMainStat, RelicStat> MainStatSubstatMatches =
        new Dictionary<RelicMainStat, RelicStat>
        {
            [RelicMainStat.FlatHp] = RelicStat.FlatHp,
            [RelicMainStat.FlatAttack] = RelicStat.FlatAttack,
            [RelicMainStat.HpPercent] = RelicStat.HpPercent,
            [RelicMainStat.AttackPercent] = RelicStat.AttackPercent,
            [RelicMainStat.DefensePercent] = RelicStat.DefensePercent,
            [RelicMainStat.CritRate] = RelicStat.CritRate,
            [RelicMainStat.CritDamage] = RelicStat.CritDamage,
            [RelicMainStat.EffectHitRate] = RelicStat.EffectHitRate,
            [RelicMainStat.Speed] = RelicStat.Speed,
            [RelicMainStat.BreakEffect] = RelicStat.BreakEffect
        };

    public static ScoreResult Calculate(ScoreRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return new ScoreResult(false, 0m, "-", [], errors);
        }

        var weights = ProfileWeights[request.Profile];
        var contributions = request.Substats.Select(substat =>
        {
            var weight = weights.GetValueOrDefault(substat.Stat);
            var score = decimal.Round(
                substat.Value / HighestRolls[substat.Stat] * weight * 10m,
                1,
                MidpointRounding.AwayFromZero);
            return new StatContribution(substat.Stat, score, weight);
        }).ToArray();

        var total = contributions.Sum(contribution => contribution.Score);

        return new ScoreResult(
            true,
            total,
            GetGrade(total),
            contributions,
            []);
    }

    private static IReadOnlyList<string> Validate(ScoreRequest request)
    {
        var errors = new List<string>();

        var profileIsValid = Enum.IsDefined(request.Profile);
        if (!profileIsValid)
        {
            errors.Add("지원하지 않는 평가 프로필입니다.");
        }

        var slotIsValid = Enum.IsDefined(request.Slot);
        if (!slotIsValid)
        {
            errors.Add("지원하지 않는 유물 부위입니다.");
        }

        var mainStatIsValid = Enum.IsDefined(request.MainStat);
        if (!mainStatIsValid)
        {
            errors.Add("지원하지 않는 주옵션입니다.");
        }

        if (request.Level is < 0 or > 15)
        {
            errors.Add("강화 단계는 0부터 15 사이여야 합니다.");
        }

        if (slotIsValid
            && mainStatIsValid
            && !RelicCatalog.MainStatsFor(request.Slot).Any(item => item.Value == request.MainStat))
        {
            errors.Add("선택한 유물 부위에는 해당 주옵션이 존재할 수 없습니다.");
        }

        if (request.Substats is null)
        {
            errors.Add("부옵션 목록이 필요합니다.");
            return errors;
        }

        if (request.Substats.Any(substat => substat is null))
        {
            errors.Add("비어 있는 부옵션 항목은 입력할 수 없습니다.");
            return errors;
        }

        var substatKindsAreValid = request.Substats.All(substat => Enum.IsDefined(substat.Stat));
        if (!substatKindsAreValid)
        {
            errors.Add("지원하지 않는 부옵션 종류가 포함되어 있습니다.");
        }

        if (mainStatIsValid
            && MainStatSubstatMatches.TryGetValue(request.MainStat, out var matchingSubstat)
            && request.Substats.Any(substat => substat.Stat == matchingSubstat))
        {
            errors.Add("주옵션과 같은 종류의 부옵션은 입력할 수 없습니다.");
        }

        if (request.Substats.Count is < 1 or > 4)
        {
            errors.Add("부옵션은 1개부터 4개까지 입력할 수 있습니다.");
        }

        if (request.Substats.Select(substat => substat.Stat).Distinct().Count() != request.Substats.Count)
        {
            errors.Add("동일한 부옵션을 중복해서 입력할 수 없습니다.");
        }

        if (request.Substats.Any(substat => substat.Value < 0m))
        {
            errors.Add("부옵션 수치는 0 이상이어야 합니다.");
        }

        if (request.Level is >= 0 and <= 15 && substatKindsAreValid)
        {
            var maximumRollCount = 1 + request.Level / 3;
            if (request.Substats.Any(substat =>
                    substat.Value > HighestRolls[substat.Stat] * maximumRollCount))
            {
                errors.Add("부옵션 수치가 현재 강화 단계에서 가능한 최대치를 초과했습니다.");
            }
        }

        return errors;
    }

    private static string GetGrade(decimal score) => score switch
    {
        >= 75m => "SSS",
        >= 65m => "SS",
        >= 55m => "S",
        >= 45m => "A",
        >= 35m => "B",
        _ => "C"
    };
}
