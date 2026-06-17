#Requires -Version 5.1
param(
    # Optional: override the baseUrl collection variable, e.g. http://localhost:64963
    [string]$BaseUrl = ""
)

$ErrorActionPreference = 'Stop'

function Import-EnvFile([string]$Path) {
    Get-Content $Path | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]*?)\s*=\s*(.*)\s*$') {
            Set-Variable -Name $Matches[1] -Value $Matches[2] -Scope Script
        }
    }
}

# Load base defaults from .env, then let .env.dev override
$envFile    = Join-Path $PSScriptRoot '.env'
$envDevFile = Join-Path $PSScriptRoot '.env.dev'

if (Test-Path $envFile)    { Import-EnvFile $envFile }
if (Test-Path $envDevFile) { Import-EnvFile $envDevFile }

if (-not $POSTMAN_API_KEY) { throw "POSTMAN_API_KEY is not set. Fill in .env.dev." }
if (-not $COLLECTION_ID)   { throw "COLLECTION_ID is not set. Fill in .env.dev." }

postman login --with-api-key $POSTMAN_API_KEY

if ($BaseUrl) {
    postman collection run $COLLECTION_ID --env-var "baseUrl=$BaseUrl"
} else {
    postman collection run $COLLECTION_ID
}
