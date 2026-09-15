using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ArtifactGrade.Api;
using ArtifactGrade.Domain;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    foreach (var value in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
    {
        if (!System.Net.IPNetwork.TryParse(value, out var network))
        {
            throw new InvalidOperationException($"신뢰 프록시 네트워크 형식이 올바르지 않습니다: {value}");
        }

        options.KnownIPNetworks.Add(network);
    }
});

var clientOrigins = builder.Configuration.GetSection("ClientOrigins").Get<string[]>()
    ?? ["http://localhost:5111", "https://localhost:7109"];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(clientOrigins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "UID 조회 요청이 많습니다. 잠시 후 다시 시도해 주세요." },
            cancellationToken);
    };
    options.AddPolicy("uid-import", httpContext =>
    {
        var uid = httpContext.Request.RouteValues["uid"]?.ToString();
        if (uid is null || uid.Length != 9 || uid.Any(character => !char.IsAsciiDigit(character)))
        {
            return RateLimitPartition.GetNoLimiter("invalid-uid");
        }

        var clientAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            clientAddress,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });
});

builder.Services.AddSingleton(provider => new RedisCalculationStore(
    builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379,abortConnect=false,connectTimeout=500,syncTimeout=500,asyncTimeout=500,connectRetry=0",
    provider.GetRequiredService<ILogger<RedisCalculationStore>>()));
builder.Services.AddSingleton<IRelicImportCache>(provider =>
    provider.GetRequiredService<RedisCalculationStore>());
builder.Services.AddSingleton<ICharacterProfileCache>(provider =>
    provider.GetRequiredService<RedisCalculationStore>());
builder.Services.AddHttpClient<MihomoRelicImporter>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Mihomo:BaseUrl"] ?? "https://api.mihomo.me/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Honkai-Starrail-Artifact-Grade/1.0");
});
builder.Services.AddHttpClient("StarRailScore", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["StarRailScore:BaseUrl"]
        ?? "https://raw.githubusercontent.com/Mar-7th/StarRailScore/fb8268bc6345c52501bd4ec23f8df89b26497e0a/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Honkai-Starrail-Artifact-Grade/1.0");
});
builder.Services.AddSingleton(provider => new StarRailScoreProfileProvider(
    provider.GetRequiredService<IHttpClientFactory>().CreateClient("StarRailScore"),
    provider.GetRequiredService<ICharacterProfileCache>(),
    provider.GetRequiredService<ILogger<StarRailScoreProfileProvider>>()));

var app = builder.Build();
var staticFileTypes = new FileExtensionContentTypeProvider();
staticFileTypes.Mappings[".dat"] = "application/octet-stream";

app.UseForwardedHeaders();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticFileTypes });
app.UseCors();
app.UseRateLimiter();

app.MapPost("/api/scores", async (
    ScoreRequest request,
    RedisCalculationStore historyStore,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var result = ScoreCalculator.Calculate(request);
    if (!result.IsValid)
    {
        return Results.BadRequest(new ScoreSubmissionResponse(result, false, null));
    }

    try
    {
        await historyStore.SaveAsync(request, result, cancellationToken);
        return Results.Ok(new ScoreSubmissionResponse(result, true, null));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception exception)
    {
        logger.LogWarning(exception, "Redis에 계산 기록을 저장하지 못했습니다.");
        return Results.Ok(new ScoreSubmissionResponse(
            result,
            false,
            "계산은 완료했지만 Redis에 최근 기록을 저장하지 못했습니다."));
    }
});

app.MapPost("/api/scores/batch", (RelicBatchScoreRequest request) =>
{
    if (!Enum.IsDefined(request.Profile))
    {
        return Results.BadRequest(new { message = "지원하지 않는 평가 프로필입니다." });
    }

    if (request.Relics is null
        || request.Relics.Count is < 1 or > 300
        || request.Relics.Any(static relic => relic is null))
    {
        return Results.BadRequest(new { message = "한 번에 1개부터 300개 유물까지 계산할 수 있습니다." });
    }

    return Results.Ok(RelicBatchScorer.Calculate(request.Profile, request.Relics));
});

app.MapPost("/api/scores/character-batch", async (
    CharacterRelicBatchScoreRequest request,
    StarRailScoreProfileProvider profileProvider,
    RedisCalculationStore historyStore,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (request.Validate() is { } validationError)
    {
        return Results.BadRequest(new { message = validationError });
    }

    try
    {
        var profile = await profileProvider.GetAsync(
            request.CharacterId.Trim(),
            request.CharacterName.Trim(),
            cancellationToken);
        if (profile is null)
        {
            return Results.NotFound(new
            {
                message = $"{request.CharacterName}의 전용 평가 데이터가 아직 없습니다. 임의의 점수는 표시하지 않습니다."
            });
        }

        var scores = RelicBatchScorer.Calculate(profile, request.Relics);
        try
        {
            await historyStore.SaveCharacterBatchAsync(profile, scores, cancellationToken);
            return Results.Ok(new CharacterRelicBatchScoreResponse(profile, scores, true, null));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Redis에 캐릭터별 계산 기록을 저장하지 못했습니다.");
            return Results.Ok(new CharacterRelicBatchScoreResponse(
                profile,
                scores,
                false,
                "점수는 정상 계산했지만 Redis에 최근 기록을 저장하지 못했습니다."));
        }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (TaskCanceledException)
    {
        return Results.Json(
            new { message = "캐릭터별 평가 기준을 불러오는 시간이 초과되었습니다." },
            statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (Exception exception) when (exception is HttpRequestException or JsonException)
    {
        logger.LogWarning(exception, "캐릭터별 유물 평가 기준을 불러오지 못했습니다.");
        return Results.Json(
            new { message = "캐릭터별 평가 기준에 연결하지 못했습니다. 잠시 후 다시 시도해 주세요." },
            statusCode: StatusCodes.Status502BadGateway);
    }
}).WithMetadata(new RequestSizeLimitAttribute(1024 * 1024));

app.MapGet("/api/import/uid/{uid}", async (
    string uid,
    MihomoRelicImporter importer,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await importer.ImportAsync(uid, cancellationToken));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { message = exception.Message });
    }
    catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
    {
        return Results.NotFound(new { message = "UID를 찾지 못했거나 공개 프로필 정보를 조회할 수 없습니다." });
    }
    catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
    {
        return Results.Json(
            new { message = "공개 프로필 조회 요청이 많습니다. 잠시 후 다시 시도해 주세요." },
            statusCode: StatusCodes.Status429TooManyRequests);
    }
    catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return Results.Json(
            new { message = "공개 프로필 조회 시간이 초과되었습니다." },
            statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (JsonException exception)
    {
        logger.LogWarning(exception, "MiHoMo 응답 형식을 읽지 못했습니다.");
        return Results.Json(
            new { message = "공개 프로필의 유물 데이터 형식을 읽지 못했습니다." },
            statusCode: StatusCodes.Status502BadGateway);
    }
    catch (HttpRequestException exception)
    {
        logger.LogWarning(exception, "MiHoMo 공개 프로필 조회에 실패했습니다.");
        return Results.Json(
            new { message = "공개 프로필 서비스에 연결하지 못했습니다." },
            statusCode: StatusCodes.Status502BadGateway);
    }
}).RequireRateLimiting("uid-import");

app.MapPost("/api/import/scanner", async (
    HttpRequest request,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    const int maximumJsonBytes = 10 * 1024 * 1024;
    if (request.ContentLength is > maximumJsonBytes)
    {
        return Results.Json(
            new { message = "JSON 파일은 10MB 이하여야 합니다." },
            statusCode: StatusCodes.Status413PayloadTooLarge);
    }

    try
    {
        var json = await ReadJsonBodyAsync(request.Body, maximumJsonBytes, cancellationToken);
        return Results.Ok(HsrScannerRelicParser.Parse(json));
    }
    catch (InvalidDataException exception)
    {
        return Results.Json(
            new { message = exception.Message },
            statusCode: StatusCodes.Status413PayloadTooLarge);
    }
    catch (JsonException exception)
    {
        logger.LogWarning(exception, "HSR Scanner JSON을 읽지 못했습니다.");
        return Results.BadRequest(new { message = exception.Message });
    }
});

app.MapGet("/api/health", async (
    RedisCalculationStore historyStore,
    ILogger<Program> logger) =>
{
    try
    {
        var latency = await historyStore.PingAsync();
        return Results.Ok(new { status = "healthy", redis = "connected", latencyMs = latency.TotalMilliseconds });
    }
    catch (Exception exception)
    {
        logger.LogWarning(exception, "Redis 상태 확인에 실패했습니다.");
        return Results.Json(
            new { status = "degraded", redis = "disconnected", message = "Redis 연결을 확인해 주세요." },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/api/health/live", () =>
    Results.Ok(new { status = "alive" }));

app.MapFallback("/api/{**path}", () =>
    Results.NotFound(new { message = "요청한 API 주소를 찾을 수 없습니다." }));

app.MapFallbackToFile("index.html");

app.Run();

static async Task<string> ReadJsonBodyAsync(
    Stream body,
    int maximumBytes,
    CancellationToken cancellationToken)
{
    await using var buffer = new MemoryStream();
    var chunk = new byte[81920];

    while (true)
    {
        var count = await body.ReadAsync(chunk, cancellationToken);
        if (count == 0)
        {
            break;
        }

        if (buffer.Length + count > maximumBytes)
        {
            throw new InvalidDataException("JSON 파일은 10MB 이하여야 합니다.");
        }

        await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
    }

    return Encoding.UTF8.GetString(buffer.ToArray());
}

public sealed record ScoreSubmissionResponse(
    ScoreResult Result,
    bool RedisSaved,
    string? Warning);

public partial class Program
{
}
