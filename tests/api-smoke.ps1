$ErrorActionPreference = 'Stop'

$requestBody = @{
    profile = 'Critical'
    level = 15
    substats = @(
        @{ stat = 'CritRate'; value = 6.48 }
        @{ stat = 'CritDamage'; value = 12.96 }
        @{ stat = 'AttackPercent'; value = 8.64 }
    )
} | ConvertTo-Json -Depth 4

$response = Invoke-RestMethod `
    -Method Post `
    -Uri 'http://localhost:5072/api/scores' `
    -ContentType 'application/json' `
    -Body $requestBody

if (-not $response.result.isValid) {
    throw '정상 입력이 유효한 계산 결과를 반환하지 않았습니다.'
}

if ($response.result.score -ne 50) {
    throw "예상 점수는 50점이지만 실제 점수는 $($response.result.score)점입니다."
}

if ($null -eq $response.redisSaved) {
    throw 'Redis 저장 여부가 응답에 없습니다.'
}

$invalidBody = @{
    profile = 999
    level = 15
    slot = 999
    mainStat = 999
    substats = @(@{ stat = 999; value = 1 })
} | ConvertTo-Json -Depth 4

$invalidStatusCode = $null
try {
    Invoke-WebRequest `
        -Method Post `
        -Uri 'http://localhost:5072/api/scores' `
        -ContentType 'application/json' `
        -Body $invalidBody | Out-Null
}
catch {
    $invalidStatusCode = [int]$_.Exception.Response.StatusCode
}

if ($invalidStatusCode -ne 400) {
    throw "잘못된 enum 입력은 400을 반환해야 하지만 실제 상태는 $invalidStatusCode 입니다."
}

Write-Output "API 스모크 테스트 통과: $($response.result.grade) 등급, $($response.result.score)점, Redis 저장=$($response.redisSaved), 잘못된 입력=400"
