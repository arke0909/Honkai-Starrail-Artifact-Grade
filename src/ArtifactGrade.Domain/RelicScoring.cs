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

public sealed record CharacterScoreRequest(
    int Level,
    IReadOnlyList<RelicSubstat> Substats,
    IReadOnlyDictionary<RelicStat, decimal> Weights,
    decimal MainStatWeight,
    decimal MaximumSubstatScore,
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
    IReadOnlyList<string> Errors,
    MainStatContribution? MainStat = null);

public sealed record MainStatContribution(
    decimal Weight,
    decimal Score,
    CharacterStatPriority Priority);

public static class CharacterGradeCatalog
{
    private static readonly (string Grade, decimal MinimumScore)[] Thresholds =
    [
        ("SSS", 97m),
        ("SS", 90m),
        ("S", 80m),
        ("A", 70m),
        ("B", 60m)
    ];

    public static string Summary { get; } = string.Join(
        " · ",
        Thresholds.Select(item => $"{item.Grade} {item.MinimumScore:0}")) + "점 이상";

    public static string GetGrade(decimal score) =>
        Thresholds.FirstOrDefault(item => score >= item.MinimumScore).Grade ?? "C";
}

public static class ScoreCalculator
{
    private const decimal ImportedValueTolerance = 0.001m;
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

    private static readonly IReadOnlyDictionary<RelicStat, SubstatRollValues> SrsRollValues =
        new Dictionary<RelicStat, SubstatRollValues>
        {
            [RelicStat.FlatHp] = new(33.87004m, 4.233755m),
            [RelicStat.FlatAttack] = new(16.935019m, 2.116877m),
            [RelicStat.FlatDefense] = new(16.935019m, 2.116877m),
            [RelicStat.HpPercent] = new(3.4560002m, 0.43200003m),
            [RelicStat.AttackPercent] = new(3.4560002m, 0.43200003m),
            [RelicStat.DefensePercent] = new(4.32m, 0.54m),
            [RelicStat.Speed] = new(2m, 0.3m),
            [RelicStat.CritRate] = new(2.592m, 0.32400002m),
            [RelicStat.CritDamage] = new(5.184m, 0.64800004m),
            [RelicStat.EffectHitRate] = new(3.4560002m, 0.43200003m),
            [RelicStat.EffectResistance] = new(3.4560002m, 0.43200003m),
            [RelicStat.BreakEffect] = new(5.184m, 0.64800004m)
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
        if (!Enum.IsDefined(request.Profile))
        {
            var invalidProfileErrors = Validate(request.Level, request.Substats, request.Slot, request.MainStat);
            invalidProfileErrors.Insert(0, "지원하지 않는 평가 프로필입니다.");
            return new ScoreResult(false, 0m, "-", [], invalidProfileErrors);
        }

        var errors = Validate(request.Level, request.Substats, request.Slot, request.MainStat);
        if (errors.Count > 0)
        {
            return new ScoreResult(false, 0m, "-", [], errors);
        }

        var contributions = CalculateContributions(request.Substats, ProfileWeights[request.Profile], 10m);

        var total = contributions.Sum(contribution => contribution.Score);

        return new ScoreResult(
            true,
            total,
            GetLegacyGrade(total),
            contributions,
            []);
    }

    public static ScoreResult Calculate(CharacterScoreRequest request)
    {
        var errors = Validate(request.Level, request.Substats, request.Slot, request.MainStat);
        if (request.Weights is null
            || request.Weights.Any(item => !Enum.IsDefined(item.Key) || item.Value is < 0m or > 1m))
        {
            errors.Add("부옵션 가중치는 지원하는 스탯에 대해 0부터 1 사이여야 합니다.");
        }

        if (request.MainStatWeight is < 0m or > 1m)
        {
            errors.Add("주옵션 가중치는 0부터 1 사이여야 합니다.");
        }

        if (request.MaximumSubstatScore <= 0m)
        {
            errors.Add("부옵션 정규화 최대값은 0보다 커야 합니다.");
        }

        if (errors.Count > 0)
        {
            return new ScoreResult(false, 0m, "-", [], errors);
        }

        var contributions = CalculateCharacterContributions(
            request.Substats,
            request.Weights!,
            request.Level,
            request.MaximumSubstatScore);
        var mainStatScore = decimal.Round(
            (request.Level + 1m) / 16m * request.MainStatWeight * 50m,
            1,
            MidpointRounding.AwayFromZero);
        var total = mainStatScore + contributions.Sum(contribution => contribution.Score);
        var priority = request.MainStatWeight switch
        {
            >= 0.8m => CharacterStatPriority.Core,
            >= 0.4m => CharacterStatPriority.Useful,
            > 0m => CharacterStatPriority.Secondary,
            _ => CharacterStatPriority.Unused
        };

        return new ScoreResult(
            true,
            total,
            CharacterGradeCatalog.GetGrade(total),
            contributions,
            [],
            new MainStatContribution(request.MainStatWeight, mainStatScore, priority));
    }

    private static IReadOnlyList<StatContribution> CalculateContributions(
        IReadOnlyList<RelicSubstat> substats,
        IReadOnlyDictionary<RelicStat, decimal> weights,
        decimal scoreMultiplier) => substats.Select(substat =>
    {
        var weight = weights.GetValueOrDefault(substat.Stat);
        var score = decimal.Round(
            substat.Value / HighestRolls[substat.Stat] * weight * scoreMultiplier,
            1,
            MidpointRounding.AwayFromZero);
        return new StatContribution(substat.Stat, score, weight);
    }).ToArray();

    private static IReadOnlyList<StatContribution> CalculateCharacterContributions(
        IReadOnlyList<RelicSubstat> substats,
        IReadOnlyDictionary<RelicStat, decimal> weights,
        int level,
        decimal maximumSubstatScore)
    {
        var maximumRollCount = 1 + level / 3;
        var rawContributions = substats.Select(substat =>
        {
            var weight = weights.GetValueOrDefault(substat.Stat);
            var rollUnits = EstimateSrsRollUnits(substat, maximumRollCount);
            return (
                substat.Stat,
                Weight: weight,
                Score: rollUnits * weight * 50m / maximumSubstatScore);
        }).ToArray();
        var contributions = rawContributions
            .Select(item => new StatContribution(
                item.Stat,
                decimal.Round(item.Score, 1, MidpointRounding.AwayFromZero),
                item.Weight))
            .ToArray();

        var targetTotal = decimal.Round(
            rawContributions.Sum(item => item.Score),
            1,
            MidpointRounding.AwayFromZero);
        var adjustment = targetTotal - contributions.Sum(item => item.Score);
        if (adjustment != 0m && contributions.Length > 0)
        {
            var adjustmentIndex = Enumerable.Range(0, contributions.Length)
                .OrderByDescending(index => adjustment < 0m
                    ? contributions[index].Score - rawContributions[index].Score
                    : rawContributions[index].Score - contributions[index].Score)
                .First();
            contributions[adjustmentIndex] = contributions[adjustmentIndex] with
            {
                Score = contributions[adjustmentIndex].Score + adjustment
            };
        }

        return contributions;
    }

    private static decimal EstimateSrsRollUnits(RelicSubstat substat, int maximumRollCount)
    {
        if (substat.Value == 0m)
        {
            return 0m;
        }

        var rollValues = SrsRollValues[substat.Stat];
        var nearestDifference = decimal.MaxValue;
        var nearestUnits = 0m;
        for (var rollCount = 1; rollCount <= maximumRollCount; rollCount++)
        {
            for (var boostCount = 0; boostCount <= rollCount * 2; boostCount++)
            {
                var expectedValue = rollCount * rollValues.BaseValue
                    + boostCount * rollValues.StepValue;
                var difference = decimal.Abs(substat.Value - expectedValue);
                if (difference < nearestDifference)
                {
                    nearestDifference = difference;
                    nearestUnits = rollCount + boostCount * 0.1m;
                }
            }
        }

        return nearestUnits;
    }

    private static List<string> Validate(
        int level,
        IReadOnlyList<RelicSubstat> substats,
        RelicSlot slot,
        RelicMainStat mainStat)
    {
        var errors = new List<string>();

        var slotIsValid = Enum.IsDefined(slot);
        if (!slotIsValid)
        {
            errors.Add("지원하지 않는 유물 부위입니다.");
        }

        var mainStatIsValid = Enum.IsDefined(mainStat);
        if (!mainStatIsValid)
        {
            errors.Add("지원하지 않는 주옵션입니다.");
        }

        if (level is < 0 or > 15)
        {
            errors.Add("강화 단계는 0부터 15 사이여야 합니다.");
        }

        if (slotIsValid
            && mainStatIsValid
            && !RelicCatalog.MainStatsFor(slot).Any(item => item.Value == mainStat))
        {
            errors.Add("선택한 유물 부위에는 해당 주옵션이 존재할 수 없습니다.");
        }

        if (substats is null)
        {
            errors.Add("부옵션 목록이 필요합니다.");
            return errors;
        }

        if (substats.Any(substat => substat is null))
        {
            errors.Add("비어 있는 부옵션 항목은 입력할 수 없습니다.");
            return errors;
        }

        var substatKindsAreValid = substats.All(substat => Enum.IsDefined(substat.Stat));
        if (!substatKindsAreValid)
        {
            errors.Add("지원하지 않는 부옵션 종류가 포함되어 있습니다.");
        }

        if (mainStatIsValid
            && MainStatSubstatMatches.TryGetValue(mainStat, out var matchingSubstat)
            && substats.Any(substat => substat.Stat == matchingSubstat))
        {
            errors.Add("주옵션과 같은 종류의 부옵션은 입력할 수 없습니다.");
        }

        if (substats.Count is < 1 or > 4)
        {
            errors.Add("부옵션은 1개부터 4개까지 입력할 수 있습니다.");
        }

        if (substats.Select(substat => substat.Stat).Distinct().Count() != substats.Count)
        {
            errors.Add("동일한 부옵션을 중복해서 입력할 수 없습니다.");
        }

        if (substats.Any(substat => substat.Value < 0m))
        {
            errors.Add("부옵션 수치는 0 이상이어야 합니다.");
        }

        if (level is >= 0 and <= 15 && substatKindsAreValid)
        {
            var maximumRollCount = 1 + level / 3;
            if (substats.Any(substat =>
                    substat.Value > HighestRolls[substat.Stat] * maximumRollCount + ImportedValueTolerance))
            {
                errors.Add("부옵션 수치가 현재 강화 단계에서 가능한 최대치를 초과했습니다.");
            }
        }

        return errors;
    }

    private static string GetLegacyGrade(decimal score) => score switch
    {
        >= 75m => "SSS",
        >= 65m => "SS",
        >= 55m => "S",
        >= 45m => "A",
        >= 35m => "B",
        _ => "C"
    };

    private readonly record struct SubstatRollValues(decimal BaseValue, decimal StepValue);
}
