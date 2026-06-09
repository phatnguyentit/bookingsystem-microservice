# Reads infra-versions.json and upserts version variables into docker/.env.
# Other variables already in .env are preserved unchanged.
# Run this once after cloning, and again whenever infra-versions.json changes.
#   Usage: .\docker\sync-versions.ps1

$versionsFile = "$PSScriptRoot\..\infra-versions.json"
$envFile      = "$PSScriptRoot\.env"

$v = Get-Content $versionsFile -Raw | ConvertFrom-Json

$updates = [ordered]@{
    'KAFKA_VERSION'         = $v.kafka
    'POSTGRES_VERSION'      = $v.postgres
    'REDIS_VERSION'         = $v.redis
    'ELASTICSEARCH_VERSION' = $v.elasticsearch
}

# Load existing lines, or start empty
$lines = if (Test-Path $envFile) { [System.IO.File]::ReadAllLines($envFile) } else { @() }

# Upsert: replace matching key lines in-place
$seen = @{}
$lines = @($lines | ForEach-Object {
    $line = $_
    foreach ($key in $updates.Keys) {
        if ($line -match "^$key\s*=") {
            $seen[$key] = $true
            $line = "$key=$($updates[$key])"
            break
        }
    }
    $line
})

# Append any keys not yet present
foreach ($key in $updates.Keys) {
    if (-not $seen.ContainsKey($key)) {
        $lines += "$key=$($updates[$key])"
    }
}

[System.IO.File]::WriteAllLines($envFile, $lines)

Write-Host "Synced: $envFile"
foreach ($key in $updates.Keys) {
    $status = if ($seen.ContainsKey($key)) { 'updated' } else { 'added' }
    Write-Host "  $($key.PadRight(24)) = $($updates[$key])  ($status)"
}
