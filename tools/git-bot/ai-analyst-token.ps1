<#
.SYNOPSIS
    Generates a GitHub App installation access token for the AI Analyst bot,
    then configures the current git repo so commits and pushes go out under
    the bot identity.

.PARAMETER AppId
    Numeric App ID from the GitHub App settings page.

.PARAMETER InstallationId
    Numeric installation ID (github.com/settings/installations/<ID>).

.PARAMETER PemPath
    Path to the private key .pem file downloaded from the GitHub App settings.

.PARAMETER RepoPath
    Optional. Git repository to configure. Defaults to the current directory.

.EXAMPLE
    .\ai-analyst-token.ps1 -AppId 12345 -InstallationId 67890 -PemPath ".\ai-analyst-app.pem"
#>
param(
    [Parameter(Mandatory)][long]$AppId,
    [Parameter(Mandatory)][long]$InstallationId,
    [Parameter(Mandatory)][string]$PemPath,
    [string]$RepoPath = (Get-Location).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Compile a C# helper for PKCS#1 DER parsing (works on .NET Framework 4.x)
# ---------------------------------------------------------------------------
if (-not ([System.Management.Automation.PSTypeName]'RsaPkcs1Helper').Type) {
    Add-Type -TypeDefinition @'
using System;
using System.Security.Cryptography;

public static class RsaPkcs1Helper {
    public static RSACryptoServiceProvider LoadKey(byte[] der) {
        int pos = 0;
        pos++;                          // skip SEQUENCE tag (0x30)
        ReadLength(der, ref pos);       // skip outer length

        ReadInteger(der, ref pos);      // skip version

        var p = new RSAParameters {
            Modulus  = ReadInteger(der, ref pos),
            Exponent = ReadInteger(der, ref pos),
            D        = ReadInteger(der, ref pos),
            P        = ReadInteger(der, ref pos),
            Q        = ReadInteger(der, ref pos),
            DP       = ReadInteger(der, ref pos),
            DQ       = ReadInteger(der, ref pos),
            InverseQ = ReadInteger(der, ref pos)
        };

        var rsa = new RSACryptoServiceProvider();
        rsa.ImportParameters(p);
        return rsa;
    }

    static int ReadLength(byte[] der, ref int pos) {
        int b = der[pos++];
        if ((b & 0x80) == 0) return b;
        int n = b & 0x7F, len = 0;
        for (int i = 0; i < n; i++) len = (len << 8) | der[pos++];
        return len;
    }

    static byte[] ReadInteger(byte[] der, ref int pos) {
        pos++;                          // skip INTEGER tag (0x02)
        int len = ReadLength(der, ref pos);
        var buf = new byte[len];
        Array.Copy(der, pos, buf, 0, len);
        pos += len;
        // strip ASN.1 sign byte
        if (buf.Length > 1 && buf[0] == 0x00) {
            var t = new byte[buf.Length - 1];
            Array.Copy(buf, 1, t, 0, t.Length);
            return t;
        }
        return buf;
    }
}
'@
}

# ---------------------------------------------------------------------------
# 1. Build JWT (RS256)
# ---------------------------------------------------------------------------
$now  = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

$headerJson  = '{"alg":"RS256","typ":"JWT"}'
$payloadJson = '{"iat":' + ($now - 60) + ',"exp":' + ($now + 540) + ',"iss":' + $AppId + '}'

function ConvertTo-Base64Url([string]$s) {
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($s)) `
        -replace '=+$','' -replace '\+','-' -replace '/','_'
}

$header  = ConvertTo-Base64Url $headerJson
$payload = ConvertTo-Base64Url $payloadJson

$sigInput = "$header.$payload"

# Load PKCS#1 PEM private key
$pemRaw   = (Get-Content $PemPath -Raw) `
              -replace '-----BEGIN RSA PRIVATE KEY-----|-----END RSA PRIVATE KEY-----','' `
              -replace '-----BEGIN PRIVATE KEY-----|-----END PRIVATE KEY-----','' `
              -replace '\s',''
$keyBytes = [Convert]::FromBase64String($pemRaw)

$rsa      = [RsaPkcs1Helper]::LoadKey($keyBytes)
$sigBytes = $rsa.SignData(
    [Text.Encoding]::UTF8.GetBytes($sigInput),
    [Security.Cryptography.SHA256CryptoServiceProvider]::new()
)
$sig = [Convert]::ToBase64String($sigBytes) -replace '=+$','' -replace '\+','-' -replace '/','_'

$jwt = "$header.$payload.$sig"

# ---------------------------------------------------------------------------
# 2. Exchange JWT for installation access token
# ---------------------------------------------------------------------------
$response = Invoke-RestMethod `
    -Uri     "https://api.github.com/app/installations/$InstallationId/access_tokens" `
    -Method  POST `
    -Headers @{
        Authorization          = "Bearer $jwt"
        Accept                 = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
    }

$token = $response.token

# ---------------------------------------------------------------------------
# 3. Fetch App slug to build the bot commit identity
#    Format: {app-id}+{slug}[bot]@users.noreply.github.com
# ---------------------------------------------------------------------------
$appMeta = Invoke-RestMethod `
    -Uri     "https://api.github.com/app" `
    -Headers @{
        Authorization          = "Bearer $jwt"
        Accept                 = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
    }

$slug = $appMeta.slug

# Resolve the bot's GitHub *user* ID (distinct from the App ID).
# Git commit emails must use the user ID, not the App ID, for GitHub
# to link commits to the bot avatar.
$botUser  = Invoke-RestMethod `
    -Uri     "https://api.github.com/users/$([Uri]::EscapeDataString($slug + '[bot]'))" `
    -Headers @{
        Authorization          = "Bearer $token"
        Accept                 = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
    }
$botUserId = $botUser.id
$botName   = $slug + "[bot]"
$botEmail  = $botUserId.ToString() + "+" + $slug + "[bot]@users.noreply.github.com"

# ---------------------------------------------------------------------------
# 4. Set GH_TOKEN so the gh CLI uses the bot identity
# ---------------------------------------------------------------------------
$env:GH_TOKEN = $token

# ---------------------------------------------------------------------------
# 5. Configure git: authenticated remote + bot author identity
# ---------------------------------------------------------------------------
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

    git config user.name  $botName
    git config user.email $botEmail
    Write-Host "git user set to: $botName ($botEmail)"
}
finally {
    Pop-Location
}

# ---------------------------------------------------------------------------
# 6. Summary
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "Bot session ready (token valid ~1 hour)"
Write-Host "  GH_TOKEN  : set for gh CLI and API calls"
Write-Host "  git remote: push authenticated as bot"
Write-Host "  git author: $botName"
Write-Host ""
Write-Host "Commits pushed from '$RepoPath' will appear as '$botName' on GitHub."
Write-Host "Re-run this script before the hour expires to refresh the token."
