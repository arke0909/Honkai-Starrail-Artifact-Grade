using ArtifactGrade.Domain;

namespace ArtifactGrade.Api;

public interface IRelicImportCache
{
    Task<string?> GetAsync(string uid, CancellationToken cancellationToken);
    Task SetAsync(string uid, string json, TimeSpan expiry, CancellationToken cancellationToken);
}

public sealed class MihomoRelicImporter
{
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);
    private readonly HttpClient _httpClient;
    private readonly IRelicImportCache _cache;
    private readonly ILogger<MihomoRelicImporter> _logger;

    public MihomoRelicImporter(
        HttpClient httpClient,
        IRelicImportCache cache,
        ILogger<MihomoRelicImporter> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<RelicImportResponse> ImportAsync(
        string uid,
        CancellationToken cancellationToken)
    {
        if (uid.Length != 9 || uid.Any(character => !char.IsAsciiDigit(character)))
        {
            throw new ArgumentException("UID는 숫자 9자리여야 합니다.", nameof(uid));
        }

        try
        {
            var cachedJson = await _cache.GetAsync(uid, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cachedJson))
            {
                return MihomoRelicParser.Parse(cachedJson) with { FromCache = true };
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis UID 캐시를 읽지 못했습니다.");
        }

        using var response = await _httpClient.GetAsync(
            $"sr_info_parsed/{uid}?lang=kr&version=v2",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"MiHoMo가 {(int)response.StatusCode} 상태를 반환했습니다.",
                null,
                response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = MihomoRelicParser.Parse(json);

        try
        {
            await _cache.SetAsync(uid, json, CacheExpiry, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis UID 캐시를 저장하지 못했습니다.");
        }

        return result;
    }
}
