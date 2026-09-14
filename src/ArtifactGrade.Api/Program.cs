using System.Text.Json.Serialization;
using ArtifactGrade.Api;
using ArtifactGrade.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var clientOrigins = builder.Configuration.GetSection("ClientOrigins").Get<string[]>()
    ?? ["http://localhost:5111", "https://localhost:7109"];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(clientOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddSingleton(provider => new RedisCalculationStore(
    builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379,abortConnect=false,connectTimeout=500,syncTimeout=500,asyncTimeout=500,connectRetry=0",
    provider.GetRequiredService<ILogger<RedisCalculationStore>>()));

var app = builder.Build();

app.UseCors();

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

app.Run();

public sealed record ScoreSubmissionResponse(
    ScoreResult Result,
    bool RedisSaved,
    string? Warning);
