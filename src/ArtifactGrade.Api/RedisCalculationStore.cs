using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using ArtifactGrade.Domain;
using StackExchange.Redis;

namespace ArtifactGrade.Api;

public sealed class RedisCalculationStore : IAsyncDisposable, IRelicImportCache, ICharacterProfileCache
{
    private const string RecentCalculationsKey = "artifact-grade:calculations:recent";
    private const string CalculationCountKey = "artifact-grade:calculations:count";
    private const string CharacterProfilesKey =
        "artifact-grade:profiles:starrailscore:fb8268bc6345c52501bd4ec23f8df89b26497e0a";
    private readonly ConfigurationOptions _connectionConfiguration;
    private readonly ILogger<RedisCalculationStore> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private ConnectionMultiplexer? _connection;
    private bool _disposed;

    public RedisCalculationStore(string connectionString, ILogger<RedisCalculationStore> logger)
    {
        _connectionConfiguration = RedisConnectionConfiguration.Parse(connectionString);
        _logger = logger;
    }

    public async Task SaveAsync(
        ScoreRequest request,
        ScoreResult result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = (await GetConnectionAsync()).GetDatabase();
        var record = new StoredCalculation(DateTimeOffset.UtcNow, request, result);
        var json = JsonSerializer.Serialize(record, _jsonOptions);

        await database.ListLeftPushAsync(RecentCalculationsKey, json);
        await database.ListTrimAsync(RecentCalculationsKey, 0, 19);
        await database.StringIncrementAsync(CalculationCountKey);
    }

    public async Task SaveCharacterBatchAsync(
        CharacterScoringProfile profile,
        IReadOnlyList<ScoredImportedRelic> scores,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = (await GetConnectionAsync()).GetDatabase();
        var record = new StoredCharacterCalculationBatch(
            DateTimeOffset.UtcNow,
            profile.CharacterId,
            profile.CharacterName,
            scores);
        var json = JsonSerializer.Serialize(record, _jsonOptions);

        await database.ListLeftPushAsync(RecentCalculationsKey, json);
        await database.ListTrimAsync(RecentCalculationsKey, 0, 19);
        await database.StringIncrementAsync(CalculationCountKey, scores.Count);
    }

    public async Task<TimeSpan> PingAsync()
    {
        var database = (await GetConnectionAsync()).GetDatabase();
        return await database.PingAsync();
    }

    public async Task<string?> GetAsync(string uid, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = (await GetConnectionAsync()).GetDatabase();
        var value = await database.StringGetAsync(ImportCacheKey(uid));
        return value.HasValue ? value.ToString() : null;
    }

    public async Task SetAsync(
        string uid,
        string json,
        TimeSpan expiry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = (await GetConnectionAsync()).GetDatabase();
        await database.StringSetAsync(ImportCacheKey(uid), json, expiry);
    }

    public async Task<string?> GetCharacterProfilesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = (await GetConnectionAsync()).GetDatabase();
        var value = await database.StringGetAsync(CharacterProfilesKey);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task SetCharacterProfilesAsync(
        string json,
        TimeSpan expiry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = (await GetConnectionAsync()).GetDatabase();
        await database.StringSetAsync(CharacterProfilesKey, json, expiry);
    }

    private static string ImportCacheKey(string uid)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(uid));
        return $"artifact-grade:imports:uid:{Convert.ToHexStringLower(hash)}";
    }

    private async Task<ConnectionMultiplexer> GetConnectionAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is not null)
        {
            return _connection;
        }

        await _connectionLock.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_connection is not null)
            {
                return _connection;
            }

            _logger.LogInformation("Redis 연결을 시도합니다.");
            _connection = await ConnectionMultiplexer.ConnectAsync(_connectionConfiguration);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private sealed record StoredCalculation(
        DateTimeOffset CalculatedAt,
        ScoreRequest Request,
        ScoreResult Result);

    private sealed record StoredCharacterCalculationBatch(
        DateTimeOffset CalculatedAt,
        string CharacterId,
        string CharacterName,
        IReadOnlyList<ScoredImportedRelic> Scores);
}
