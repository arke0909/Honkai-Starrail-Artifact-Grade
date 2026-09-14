using System.Text.Json;
using ArtifactGrade.Domain;

namespace ArtifactGrade.Api;

public interface ICharacterProfileCache
{
    Task<string?> GetCharacterProfilesAsync(CancellationToken cancellationToken);

    Task SetCharacterProfilesAsync(
        string json,
        TimeSpan expiry,
        CancellationToken cancellationToken);
}

public sealed class StarRailScoreProfileProvider(
    HttpClient client,
    ICharacterProfileCache cache,
    ILogger<StarRailScoreProfileProvider> logger)
{
    private const string SourceName = "StarRailScore 캐릭터별 가중치";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(30);
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private IReadOnlyDictionary<string, StarRailScoreProfileDefinition>? _profiles;

    public async Task<CharacterScoringProfile?> GetAsync(
        string characterId,
        string characterName,
        CancellationToken cancellationToken)
    {
        if (_profiles is null)
        {
            await _loadLock.WaitAsync(cancellationToken);
            try
            {
                if (_profiles is null)
                {
                    _profiles = await LoadProfilesAsync(cancellationToken);
                }
            }
            finally
            {
                _loadLock.Release();
            }
        }

        return _profiles.TryGetValue(characterId, out var definition)
            ? CharacterScoringProfile.Create(
                characterId,
                characterName,
                definition.SubstatWeights,
                definition.MainStatWeights,
                definition.MaximumSubstatScore,
                SourceName)
            : null;
    }

    private async Task<IReadOnlyDictionary<string, StarRailScoreProfileDefinition>> LoadProfilesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await client.GetStringAsync("score.json", cancellationToken);
            var profiles = StarRailScoreProfileParser.Parse(json);
            try
            {
                await cache.SetCharacterProfilesAsync(json, CacheDuration, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "캐릭터 가중치 원본을 Redis에 캐시하지 못했습니다.");
            }

            return profiles;
        }
        catch (Exception sourceException) when (sourceException is HttpRequestException or JsonException or TaskCanceledException)
        {
            try
            {
                var cachedJson = await cache.GetCharacterProfilesAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(cachedJson))
                {
                    logger.LogWarning(sourceException, "원본 캐릭터 가중치 조회에 실패해 Redis 캐시를 사용합니다.");
                    return StarRailScoreProfileParser.Parse(cachedJson);
                }
            }
            catch (Exception cacheException) when (cacheException is not OperationCanceledException)
            {
                logger.LogWarning(cacheException, "Redis의 캐릭터 가중치 캐시도 읽지 못했습니다.");
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(sourceException).Throw();
            throw;
        }
    }
}

public sealed record StarRailScoreProfileDefinition(
    IReadOnlyDictionary<RelicStat, decimal> SubstatWeights,
    IReadOnlyDictionary<RelicSlot, IReadOnlyDictionary<RelicMainStat, decimal>> MainStatWeights,
    decimal MaximumSubstatScore);

public static class StarRailScoreProfileParser
{
    private static readonly IReadOnlyDictionary<string, RelicStat> StatFields =
        new Dictionary<string, RelicStat>(StringComparer.Ordinal)
        {
            ["HPDelta"] = RelicStat.FlatHp,
            ["AttackDelta"] = RelicStat.FlatAttack,
            ["DefenceDelta"] = RelicStat.FlatDefense,
            ["HPAddedRatio"] = RelicStat.HpPercent,
            ["AttackAddedRatio"] = RelicStat.AttackPercent,
            ["DefenceAddedRatio"] = RelicStat.DefensePercent,
            ["SpeedDelta"] = RelicStat.Speed,
            ["CriticalChanceBase"] = RelicStat.CritRate,
            ["CriticalDamageBase"] = RelicStat.CritDamage,
            ["StatusProbabilityBase"] = RelicStat.EffectHitRate,
            ["StatusResistanceBase"] = RelicStat.EffectResistance,
            ["BreakDamageAddedRatioBase"] = RelicStat.BreakEffect
        };

    private static readonly IReadOnlyDictionary<string, RelicSlot> SlotFields =
        new Dictionary<string, RelicSlot>(StringComparer.Ordinal)
        {
            ["1"] = RelicSlot.Head,
            ["2"] = RelicSlot.Hands,
            ["3"] = RelicSlot.Body,
            ["4"] = RelicSlot.Feet,
            ["5"] = RelicSlot.PlanarSphere,
            ["6"] = RelicSlot.LinkRope
        };

    private static readonly IReadOnlyDictionary<string, RelicMainStat> MainStatFields =
        new Dictionary<string, RelicMainStat>(StringComparer.Ordinal)
        {
            ["HPDelta"] = RelicMainStat.FlatHp,
            ["AttackDelta"] = RelicMainStat.FlatAttack,
            ["HPAddedRatio"] = RelicMainStat.HpPercent,
            ["AttackAddedRatio"] = RelicMainStat.AttackPercent,
            ["DefenceAddedRatio"] = RelicMainStat.DefensePercent,
            ["CriticalChanceBase"] = RelicMainStat.CritRate,
            ["CriticalDamageBase"] = RelicMainStat.CritDamage,
            ["HealRatioBase"] = RelicMainStat.OutgoingHealing,
            ["StatusProbabilityBase"] = RelicMainStat.EffectHitRate,
            ["SpeedDelta"] = RelicMainStat.Speed,
            ["PhysicalAddedRatio"] = RelicMainStat.PhysicalDamage,
            ["FireAddedRatio"] = RelicMainStat.FireDamage,
            ["IceAddedRatio"] = RelicMainStat.IceDamage,
            ["ThunderAddedRatio"] = RelicMainStat.LightningDamage,
            ["WindAddedRatio"] = RelicMainStat.WindDamage,
            ["QuantumAddedRatio"] = RelicMainStat.QuantumDamage,
            ["ImaginaryAddedRatio"] = RelicMainStat.ImaginaryDamage,
            ["BreakDamageAddedRatioBase"] = RelicMainStat.BreakEffect,
            ["SPRatioBase"] = RelicMainStat.EnergyRegenerationRate
        };

    public static IReadOnlyDictionary<string, StarRailScoreProfileDefinition> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("캐릭터 평가 데이터의 최상위 형식이 올바르지 않습니다.");
        }

        var profiles = new Dictionary<string, StarRailScoreProfileDefinition>(StringComparer.Ordinal);
        foreach (var character in document.RootElement.EnumerateObject())
        {
            if (!character.Value.TryGetProperty("weight", out var weightElement)
                || weightElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException($"캐릭터 {character.Name}의 부옵션 가중치가 없습니다.");
            }

            var weights = new Dictionary<RelicStat, decimal>();
            foreach (var field in StatFields)
            {
                if (!weightElement.TryGetProperty(field.Key, out var valueElement)
                    || !valueElement.TryGetDecimal(out var value)
                    || value is < 0m or > 1m)
                {
                    throw new JsonException($"캐릭터 {character.Name}의 {field.Key} 가중치가 올바르지 않습니다.");
                }

                weights[field.Value] = value;
            }

            if (!character.Value.TryGetProperty("main", out var mainElement)
                || mainElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException($"캐릭터 {character.Name}의 주옵션 가중치가 없습니다.");
            }

            var mainStatWeights = new Dictionary<RelicSlot, IReadOnlyDictionary<RelicMainStat, decimal>>();
            foreach (var slotField in SlotFields)
            {
                if (!mainElement.TryGetProperty(slotField.Key, out var slotElement)
                    || slotElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var slotWeights = new Dictionary<RelicMainStat, decimal>();
                foreach (var mainStat in slotElement.EnumerateObject())
                {
                    if (!MainStatFields.TryGetValue(mainStat.Name, out var mappedStat))
                    {
                        continue;
                    }

                    if (!mainStat.Value.TryGetDecimal(out var value) || value is < 0m or > 1m)
                    {
                        throw new JsonException(
                            $"캐릭터 {character.Name}의 {slotField.Key}.{mainStat.Name} 주옵션 가중치가 올바르지 않습니다.");
                    }

                    slotWeights[mappedStat] = value;
                }

                mainStatWeights[slotField.Value] = slotWeights;
            }

            if (!character.Value.TryGetProperty("max", out var maximumElement)
                || !maximumElement.TryGetDecimal(out var maximumSubstatScore)
                || maximumSubstatScore <= 0m)
            {
                throw new JsonException($"캐릭터 {character.Name}의 부옵션 정규화 최대값이 올바르지 않습니다.");
            }

            profiles[character.Name] = new StarRailScoreProfileDefinition(
                weights,
                mainStatWeights,
                maximumSubstatScore);
        }

        return profiles;
    }
}
