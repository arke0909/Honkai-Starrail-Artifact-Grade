param(
    [string]$ApiBaseUrl = "http://localhost:5072",
    [string]$Uid = ""
)

$ErrorActionPreference = "Stop"

$scannerPayload = @{
    source = "HSR-Scanner"
    version = 4
    relics = @(
        @{
            name = "Musketeer of Wild Wheat"
            slot = "Hands"
            rarity = 5
            level = 15
            mainstat = "ATK"
            substats = @(
                @{ key = "CRIT Rate_"; value = 6.48 }
                @{ key = "CRIT DMG_"; value = 12.96 }
            )
            location = "1101"
            _uid = "import-smoke-relic"
        }
    )
    characters = @(
        @{ id = "1101"; name = "Bronya" }
    )
} | ConvertTo-Json -Depth 8

$import = Invoke-RestMethod `
    -Method Post `
    -Uri "$ApiBaseUrl/api/import/scanner" `
    -ContentType "application/json" `
    -Body $scannerPayload

if ($import.source -ne "HSR Scanner" -or $import.relics.Count -ne 1) {
    throw "Unexpected scanner import result."
}

if ($import.relics[0].equippedBy -ne "Bronya" -or $import.relics[0].mainStat -ne "FlatAttack") {
    throw "Unexpected scanner relic mapping."
}

$batchPayload = @{
    profile = "Critical"
    relics = $import.relics
} | ConvertTo-Json -Depth 8

$scores = Invoke-RestMethod `
    -Method Post `
    -Uri "$ApiBaseUrl/api/scores/batch" `
    -ContentType "application/json" `
    -Body $batchPayload

if ($scores.Count -ne 1 -or -not $scores[0].result.isValid -or $scores[0].result.score -ne 40) {
    throw "Unexpected batch score result."
}

Write-Host "Scanner import and batch score passed: 1 relic, 40 points"

if (-not [string]::IsNullOrWhiteSpace($Uid)) {
    $uidImport = Invoke-RestMethod -Uri "$ApiBaseUrl/api/import/uid/$Uid"
    if ($uidImport.source -ne "MiHoMo UID" -or $uidImport.relics.Count -lt 1) {
        throw "Unexpected UID import result."
    }

    Write-Host "UID import passed: $($uidImport.relics.Count) relics"
}
