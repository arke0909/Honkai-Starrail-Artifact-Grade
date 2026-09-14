$ErrorActionPreference = 'Stop'

function Invoke-RedisCli {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $output = docker compose exec -T redis redis-cli @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "redis-cli 실행에 실패했습니다: $($Arguments -join ' ')"
    }

    return $output
}

$health = Invoke-RestMethod -Uri 'http://localhost:5072/api/health'
if ($health.status -ne 'healthy' -or $health.redis -ne 'connected') {
    throw 'API가 Redis 연결 정상 상태를 반환하지 않았습니다.'
}

$countText = Invoke-RedisCli -Arguments @('GET', 'artifact-grade:calculations:count')
$countBefore = if ([string]::IsNullOrWhiteSpace($countText)) { 0L } else { [long]$countText }

$requestBody = @{
    profile = 'Critical'
    level = 15
    slot = 'Body'
    mainStat = 'CritDamage'
    substats = @(
        @{ stat = 'CritRate'; value = 6.48 }
        @{ stat = 'AttackPercent'; value = 8.64 }
        @{ stat = 'Speed'; value = 5.2 }
    )
} | ConvertTo-Json -Depth 4

1..21 | ForEach-Object {
    $response = Invoke-RestMethod `
        -Method Post `
        -Uri 'http://localhost:5072/api/scores' `
        -ContentType 'application/json' `
        -Body $requestBody

    if (-not $response.redisSaved) {
        throw "요청 $_의 Redis 저장이 실패했습니다."
    }
}

$recentCount = [int](Invoke-RedisCli -Arguments @('LLEN', 'artifact-grade:calculations:recent'))
$countAfter = [long](Invoke-RedisCli -Arguments @('GET', 'artifact-grade:calculations:count'))

if ($recentCount -ne 20) {
    throw "최근 계산은 20개여야 하지만 실제 값은 $recentCount개입니다."
}

if ($countAfter -ne $countBefore + 21) {
    throw "누적 계산 횟수가 21 증가해야 하지만 $($countAfter - $countBefore) 증가했습니다."
}

Write-Output "Redis 통합 테스트 통과: 최근 $recentCount개, 누적 횟수 +$($countAfter - $countBefore)"
