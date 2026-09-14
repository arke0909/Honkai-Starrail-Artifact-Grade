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
    private const string AppearanceWarning =
        "착용 외형 정보를 불러오지 못해 기본 캐릭터 이미지를 표시합니다.";
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
            if (!string.IsNullOrWhiteSpace(cachedJson)
                && MihomoAppearanceMetadata.HasBeenChecked(cachedJson))
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
        var appearanceWarning = false;
        try
        {
            using var appearanceResponse = await _httpClient.GetAsync(
                $"sr_info/{uid}",
                cancellationToken);
            if (!appearanceResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"MiHoMo 외형 조회가 {(int)appearanceResponse.StatusCode} 상태를 반환했습니다.",
                    null,
                    appearanceResponse.StatusCode);
            }

            var rawJson = await appearanceResponse.Content.ReadAsStringAsync(cancellationToken);
            json = MihomoAppearanceMetadata.Merge(json, rawJson);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            HttpRequestException or
            TaskCanceledException or
            System.Text.Json.JsonException)
        {
            appearanceWarning = true;
            _logger.LogWarning(exception, "MiHoMo 착용 외형 정보를 읽지 못했습니다.");
        }

        var result = MihomoRelicParser.Parse(json);
        if (appearanceWarning)
        {
            result = result with { Warnings = [.. result.Warnings, AppearanceWarning] };
        }

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
