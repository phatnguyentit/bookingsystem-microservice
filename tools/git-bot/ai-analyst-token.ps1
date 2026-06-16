<#
.SYNOPSIS
    "Mode switch" into the booking-ai-analyst[bot] identity for the CURRENT shell:
    mints an installation token, sets $env:GH_TOKEN, rewrites the 'origin' remote
    with the token, and sets the repo git author to the bot.

    For one-off bot actions that DON'T mutate the repo, prefer bot.ps1
    (bot commit / push / pr / issue). This script is the heavier, stateful path
    kept for when you want a whole session to be "the bot".

.PARAMETER AppId
    Numeric App ID. Defaults to the value in bot-session.ps1.

.PARAMETER InstallationId
    Numeric installation ID. Defaults to the value in bot-session.ps1.

.PARAMETER PemPath
    Path to the GitHub App private key .pem. Defaults to the value in bot-session.ps1.

.PARAMETER RepoPath
    Git repository to configure. Defaults to the current directory.

.EXAMPLE
    .\tools\git-bot\ai-analyst-token.ps1
#>
param(
    [long]$AppId,
    [long]$InstallationId,
    [string]$PemPath,
    [string]$RepoPath = (Get-Location).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot 'bot-session.ps1')

# Fall back to the shared defaults when params are omitted.
if (-not $AppId)          { $AppId = $BotAppId }
if (-not $InstallationId) { $InstallationId = $BotInstallationId }
if (-not $PemPath)        { $PemPath = $BotPemPath }

# 1. Mint (or reuse cached) installation token via the shared helper.
$token = Get-BotToken -AppId $AppId -InstallationId $InstallationId -PemPath $PemPath

# 2. Set GH_TOKEN so the gh CLI uses the bot identity.
$env:GH_TOKEN = $token

# 3. Configure git: authenticated remote + bot author identity.
Push-Location $RepoPath
try {
    $remoteUrl = git remote get-url origin 2>$null
    if ($LASTEXITCODE -eq 0 -and $remoteUrl) {
        $cleanUrl  = $remoteUrl -replace 'https://[^@]+@', 'https://'
        $authedUrl = $cleanUrl  -replace 'https://', "https://x-access-token:$token@"
        git remote set-url origin $authedUrl
        Write-Host "git remote 'origin' updated with bot token."
    } else {
        Write-Host "No 'origin' remote found, skipping remote URL update."
    }

    git config user.name  $BotName
    git config user.email $BotEmail
    Write-Host "git user set to: $BotName ($BotEmail)"
}
finally {
    Pop-Location
}

# 4. Summary
Write-Host ""
Write-Host "Bot session ready (token valid ~1 hour)"
Write-Host "  GH_TOKEN  : set for gh CLI and API calls"
Write-Host "  git remote: push authenticated as bot"
Write-Host "  git author: $BotName"
Write-Host ""
Write-Host "Commits pushed from '$RepoPath' will appear as '$BotName' on GitHub."
Write-Host "To return to your personal account, reset the remote and git config:"
Write-Host "  git remote set-url origin https://github.com/phatnguyentit/bookingsystem-microservice.git"
Write-Host "  git config user.name 'Zip'; git config user.email 'phatnguyen.tit@gmail.com'"
