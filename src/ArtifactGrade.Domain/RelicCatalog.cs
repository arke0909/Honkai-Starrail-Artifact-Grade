namespace ArtifactGrade.Domain;

public sealed record CatalogItem<T>(T Value, string Name, string Unit = "");

public static class RelicCatalog
{
    public static IReadOnlyList<CatalogItem<ScoringProfileId>> Profiles { get; } =
    [
        new(ScoringProfileId.Critical, "치명타 딜러"),
        new(ScoringProfileId.Attack, "공격력 딜러"),
        new(ScoringProfileId.Hp, "HP 기반"),
        new(ScoringProfileId.Defense, "방어력 기반"),
        new(ScoringProfileId.Break, "격파 특수효과"),
        new(ScoringProfileId.Support, "효과 명중 서포터")
    ];

    public static IReadOnlyList<CatalogItem<RelicStat>> Substats { get; } =
    [
        new(RelicStat.FlatHp, "HP", string.Empty),
        new(RelicStat.FlatAttack, "공격력", string.Empty),
        new(RelicStat.FlatDefense, "방어력", string.Empty),
        new(RelicStat.HpPercent, "HP", "%"),
        new(RelicStat.AttackPercent, "공격력", "%"),
        new(RelicStat.DefensePercent, "방어력", "%"),
        new(RelicStat.Speed, "속도", string.Empty),
        new(RelicStat.CritRate, "치명타 확률", "%"),
        new(RelicStat.CritDamage, "치명타 피해", "%"),
        new(RelicStat.EffectHitRate, "효과 명중", "%"),
        new(RelicStat.EffectResistance, "효과 저항", "%"),
        new(RelicStat.BreakEffect, "격파 특수효과", "%")
    ];

    public static IReadOnlyList<CatalogItem<RelicSlot>> Slots { get; } =
    [
        new(RelicSlot.Head, "머리"),
        new(RelicSlot.Hands, "손"),
        new(RelicSlot.Body, "몸통"),
        new(RelicSlot.Feet, "다리"),
        new(RelicSlot.PlanarSphere, "차원 구체"),
        new(RelicSlot.LinkRope, "연결 매듭")
    ];

    private static readonly IReadOnlyDictionary<RelicSlot, IReadOnlyList<CatalogItem<RelicMainStat>>> MainStats =
        new Dictionary<RelicSlot, IReadOnlyList<CatalogItem<RelicMainStat>>>
        {
            [RelicSlot.Head] = [new(RelicMainStat.FlatHp, "HP")],
            [RelicSlot.Hands] = [new(RelicMainStat.FlatAttack, "공격력")],
            [RelicSlot.Body] =
            [
                new(RelicMainStat.HpPercent, "HP", "%"),
                new(RelicMainStat.AttackPercent, "공격력", "%"),
                new(RelicMainStat.DefensePercent, "방어력", "%"),
                new(RelicMainStat.CritRate, "치명타 확률", "%"),
                new(RelicMainStat.CritDamage, "치명타 피해", "%"),
                new(RelicMainStat.OutgoingHealing, "치유량 보너스", "%"),
                new(RelicMainStat.EffectHitRate, "효과 명중", "%")
            ],
            [RelicSlot.Feet] =
            [
                new(RelicMainStat.HpPercent, "HP", "%"),
                new(RelicMainStat.AttackPercent, "공격력", "%"),
                new(RelicMainStat.DefensePercent, "방어력", "%"),
                new(RelicMainStat.Speed, "속도")
            ],
            [RelicSlot.PlanarSphere] =
            [
                new(RelicMainStat.HpPercent, "HP", "%"),
                new(RelicMainStat.AttackPercent, "공격력", "%"),
                new(RelicMainStat.DefensePercent, "방어력", "%"),
                new(RelicMainStat.PhysicalDamage, "물리 속성 피해", "%"),
                new(RelicMainStat.FireDamage, "화염 속성 피해", "%"),
                new(RelicMainStat.IceDamage, "얼음 속성 피해", "%"),
                new(RelicMainStat.LightningDamage, "번개 속성 피해", "%"),
                new(RelicMainStat.WindDamage, "바람 속성 피해", "%"),
                new(RelicMainStat.QuantumDamage, "양자 속성 피해", "%"),
                new(RelicMainStat.ImaginaryDamage, "허수 속성 피해", "%")
            ],
            [RelicSlot.LinkRope] =
            [
                new(RelicMainStat.HpPercent, "HP", "%"),
                new(RelicMainStat.AttackPercent, "공격력", "%"),
                new(RelicMainStat.DefensePercent, "방어력", "%"),
                new(RelicMainStat.BreakEffect, "격파 특수효과", "%"),
                new(RelicMainStat.EnergyRegenerationRate, "에너지 회복효율", "%")
            ]
        };

    public static IReadOnlyList<CatalogItem<RelicMainStat>> MainStatsFor(RelicSlot slot) => MainStats[slot];
}

public static class RelicMainStatValues
{
    private static readonly IReadOnlyDictionary<RelicMainStat, (decimal Base, decimal Step)> Growth =
        new Dictionary<RelicMainStat, (decimal Base, decimal Step)>
        {
            [RelicMainStat.FlatHp] = (112.896m, 39.5136m),
            [RelicMainStat.FlatAttack] = (56.448m, 19.7568m),
            [RelicMainStat.HpPercent] = (6.912m, 2.4192m),
            [RelicMainStat.AttackPercent] = (6.912m, 2.4192m),
            [RelicMainStat.DefensePercent] = (8.64m, 3.024m),
            [RelicMainStat.CritRate] = (5.184m, 1.8144m),
            [RelicMainStat.CritDamage] = (10.368m, 3.6288m),
            [RelicMainStat.OutgoingHealing] = (5.5296m, 1.9354m),
            [RelicMainStat.EffectHitRate] = (6.912m, 2.4192m),
            [RelicMainStat.Speed] = (4.032m, 1.4m),
            [RelicMainStat.PhysicalDamage] = (6.2208m, 2.1773m),
            [RelicMainStat.FireDamage] = (6.2208m, 2.1773m),
            [RelicMainStat.IceDamage] = (6.2208m, 2.1773m),
            [RelicMainStat.LightningDamage] = (6.2208m, 2.1773m),
            [RelicMainStat.WindDamage] = (6.2208m, 2.1773m),
            [RelicMainStat.QuantumDamage] = (6.2208m, 2.1773m),
            [RelicMainStat.ImaginaryDamage] = (6.2208m, 2.1773m),
            [RelicMainStat.BreakEffect] = (10.368m, 3.6288m),
            [RelicMainStat.EnergyRegenerationRate] = (3.1104m, 1.0886m)
        };

    public static decimal Calculate(RelicMainStat mainStat, int level)
    {
        if (level is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "강화 단계는 0부터 15 사이여야 합니다.");
        }

        if (!Growth.TryGetValue(mainStat, out var growth))
        {
            throw new ArgumentOutOfRangeException(nameof(mainStat), "지원하지 않는 주옵션입니다.");
        }

        return growth.Base + growth.Step * level;
    }
}
