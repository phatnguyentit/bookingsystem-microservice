<#
.SYNOPSIS
    Run a single git/gh action as the booking-ai-analyst[bot], without changing
    the repo's persistent identity. Your working repo stays on your personal
    account at all times; only the one command you run here is attributed to the bot.

.DESCRIPTION
    Actions (all extra arguments are passed straight through to git/gh):

      bot commit -m "msg"          Commit STAGED changes authored by the bot.
      bot push [<refspec|flags>]   Push (default: current branch) using a bot token.
      bot pr   <gh pr create args> Open a PR as the bot.
      bot issue <gh issue args>    Create an issue as the bot.
      bot comment <gh issue comment args>  Comment on an issue/PR as the bot.
      bot edit  <gh issue edit args>       Edit an issue (body/labels/etc.) as the bot.
      bot whoami                   Show bot identity, token cache state, and current repo identity.

.EXAMPLE
    git add .
    .\tools\git-bot\bot.ps1 commit -m "fix: tidy up"
    .\tools\git-bot\bot.ps1 push
    .\tools\git-bot\bot.ps1 pr --base main --head my-branch --title "Fix" --body "..."

.NOTES
    Nothing here rewrites 'origin' or runs 'git config' — commit uses per-command
    -c overrides, push uses an inline token URL, and pr/issue set GH_TOKEN only
    inside this script's own process. So no bot identity or expired token ever
    leaks into your next terminal.
#>
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('commit', 'push', 'pr', 'issue', 'comment', 'edit', 'whoami')]
    [string]$Action,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot 'bot-session.ps1')

function Get-CleanOriginHttps {
    $origin = (git remote get-url origin).Trim()
    if ($origin -match '^git@github\.com:(.+)$') { $origin = "https://github.com/$($Matches[1])" }
    # strip any embedded credentials (e.g. a previous x-access-token:...@)
    return ($origin -replace 'https://[^@/]+@', 'https://')
}

switch ($Action) {

    'commit' {
        git -c "user.name=$BotName" -c "user.email=$BotEmail" commit @Rest
        exit $LASTEXITCODE
    }

    'push' {
        $token   = Get-BotToken
        $pushUrl = (Get-CleanOriginHttps) -replace '^https://', "https://x-access-token:$token@"

        if ($Rest -and $Rest.Count -gt 0) {
            git push $pushUrl @Rest
        }
        else {
            $branch = (git branch --show-current).Trim()
            git push $pushUrl "HEAD:$branch"
        }
        exit $LASTEXITCODE
    }

    'pr' {
        $env:GH_TOKEN = Get-BotToken
        gh pr create @Rest
        exit $LASTEXITCODE
    }

    'issue' {
        $env:GH_TOKEN = Get-BotToken
        gh issue create @Rest
        exit $LASTEXITCODE
    }

    'comment' {
        $env:GH_TOKEN = Get-BotToken
        gh issue comment @Rest
        exit $LASTEXITCODE
    }

    'edit' {
        $env:GH_TOKEN = Get-BotToken
        gh issue edit @Rest
        exit $LASTEXITCODE
    }

    'whoami' {
        Write-Host "Bot identity (applied per-command, never persisted):"
        Write-Host "  name : $BotName"
        Write-Host "  email: $BotEmail"
        Write-Host ""

        $cacheFile = Join-Path $env:TEMP "ai-analyst-token-$BotAppId-$BotInstallationId.json"
        if (Test-Path $cacheFile) {
            try {
                $c   = Get-Content $cacheFile -Raw | ConvertFrom-Json
                $exp = [DateTimeOffset]::Parse($c.expiresAt)
                $mins = [int]($exp - [DateTimeOffset]::UtcNow).TotalMinutes
                if ($mins -gt 0) { Write-Host "Cached token: valid ~$mins more minute(s)." }
                else             { Write-Host "Cached token: expired (will mint fresh on next push/pr/issue)." }
            } catch { Write-Host "Cached token: present but unreadable." }
        } else {
            Write-Host "Cached token: none (will mint on next push/pr/issue)."
        }
        Write-Host ""

        $name  = (git config user.name)  2>$null
        $email = (git config user.email) 2>$null
        Write-Host "This repo's persistent identity (used by plain git/gh):"
        Write-Host "  name : $name"
        Write-Host "  email: $email"
        Write-Host "  origin: $(Get-CleanOriginHttps)"
    }
}
