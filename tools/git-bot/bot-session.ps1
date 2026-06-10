<#
.SYNOPSIS
    Shared, NON-mutating helpers for acting as the booking-ai-analyst[bot].

    Dot-source this file to get:
      - $BotName / $BotEmail            : the bot's git author identity (constants)
      - $BotAppId / $BotInstallationId  : default GitHub App identifiers
      - $BotPemPath                     : default path to the App private key
      - Get-BotToken                    : mints (and caches ~1h) an installation token

    Unlike ai-analyst-token.ps1, nothing here rewrites the git remote or repo
    git config. Callers apply the bot identity per-command, so the working repo
    always stays in the personal account's state on disk.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Bot identity (stable; resolved once from the GitHub App, see git-identity-switch.md) ---
$BotName  = 'booking-ai-analyst[bot]'
$BotEmail = '292146594+booking-ai-analyst[bot]@users.noreply.github.com'

# --- Default GitHub App identifiers (public; not secrets) ---
$BotAppId          = 4008612
$BotInstallationId = 139135129
$BotPemPath        = Join-Path $PSScriptRoot 'ai-analyst-app.pem'

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

function ConvertTo-Base64Url([string]$s) {
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($s)) `
        -replace '=+$','' -replace '\+','-' -replace '/','_'
}

<#
.SYNOPSIS
    Returns a GitHub App installation access token (~1h lifetime).
    Caches the token in $env:TEMP and reuses it until it is within 5 minutes
    of expiry, so a commit -> push -> pr sequence only mints once.
#>
function Get-BotToken {
    param(
        [long]$AppId          = $BotAppId,
        [long]$InstallationId = $BotInstallationId,
        [string]$PemPath      = $BotPemPath,
        [switch]$NoCache
    )

    $cacheFile = Join-Path $env:TEMP "ai-analyst-token-$AppId-$InstallationId.json"

    if (-not $NoCache -and (Test-Path $cacheFile)) {
        try {
            $cached = Get-Content $cacheFile -Raw | ConvertFrom-Json
            $exp = [DateTimeOffset]::Parse($cached.expiresAt)
            if (($exp - [DateTimeOffset]::UtcNow) -gt [TimeSpan]::FromMinutes(5)) {
                return $cached.token
            }
        } catch { }   # corrupt/stale cache -> fall through and mint fresh
    }

    if (-not (Test-Path $PemPath)) {
        throw "Bot private key not found at '$PemPath'. Place the GitHub App .pem there (it is gitignored)."
    }

    # 1. Build JWT (RS256), 10-min expiry
    $now         = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $headerJson  = '{"alg":"RS256","typ":"JWT"}'
    $payloadJson = '{"iat":' + ($now - 60) + ',"exp":' + ($now + 540) + ',"iss":' + $AppId + '}'

    $header   = ConvertTo-Base64Url $headerJson
    $payload  = ConvertTo-Base64Url $payloadJson
    $sigInput = "$header.$payload"

    $pemRaw = (Get-Content $PemPath -Raw) `
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

    # 2. Exchange JWT for an installation access token
    $response = Invoke-RestMethod `
        -Uri     "https://api.github.com/app/installations/$InstallationId/access_tokens" `
        -Method  POST `
        -Headers @{
            Authorization          = "Bearer $jwt"
            Accept                 = "application/vnd.github+json"
            "X-GitHub-Api-Version" = "2022-11-28"
        }

    @{ token = $response.token; expiresAt = $response.expires_at } |
        ConvertTo-Json | Set-Content $cacheFile -Encoding utf8

    return $response.token
}
