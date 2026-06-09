param(
    [string]$Registry = "phatnt.net:5000",
    [string]$Tag = "latest",
    [string[]]$Services = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root = Split-Path $PSScriptRoot -Parent

$Projects = [ordered]@{}
$Projects["api-gateway"]          = "src/Gateway/BookingSystem.ApiGateway/BookingSystem.ApiGateway.csproj"
$Projects["booking-service"]      = "src/Services/BookingService/BookingSystem.BookingService.Api/BookingSystem.BookingService.Api.csproj"
$Projects["catalog-service"]      = "src/Services/CatalogService/BookingSystem.CatalogService.Api/BookingSystem.CatalogService.Api.csproj"
$Projects["notification-service"] = "src/Services/NotificationService/BookingSystem.NotificationService.Api/BookingSystem.NotificationService.Api.csproj"
$Projects["payment-service"]      = "src/Services/PaymentService/BookingSystem.PaymentService.Api/BookingSystem.PaymentService.Api.csproj"
$Projects["review-service"]       = "src/Services/ReviewService/BookingSystem.ReviewService.Api/BookingSystem.ReviewService.Api.csproj"
$Projects["search-service"]       = "src/Services/SearchService/BookingSystem.SearchService.Api/BookingSystem.SearchService.Api.csproj"
$Projects["user-service"]         = "src/Services/UserService/BookingSystem.UserService.Api/BookingSystem.UserService.Api.csproj"

$Assemblies = [ordered]@{}
$Assemblies["api-gateway"]          = "BookingSystem.ApiGateway"
$Assemblies["booking-service"]      = "BookingSystem.BookingService.Api"
$Assemblies["catalog-service"]      = "BookingSystem.CatalogService.Api"
$Assemblies["notification-service"] = "BookingSystem.NotificationService.Api"
$Assemblies["payment-service"]      = "BookingSystem.PaymentService.Api"
$Assemblies["review-service"]       = "BookingSystem.ReviewService.Api"
$Assemblies["search-service"]       = "BookingSystem.SearchService.Api"
$Assemblies["user-service"]         = "BookingSystem.UserService.Api"

$TargetServices = if ($Services.Count -gt 0) { $Services } else { [string[]]$Projects.Keys }

foreach ($svc in $TargetServices) {
    if (-not $Projects.Contains($svc)) {
        Write-Warning "Unknown service '$svc' - skipping."
        continue
    }

    $projectPath = $Projects[$svc]
    $assemblyName = $Assemblies[$svc]
    $image = "${Registry}/booking-system/${svc}:${Tag}"

    Write-Host ""
    Write-Host "==> Building $image" -ForegroundColor Cyan

    $buildArgs = @(
        "build",
        "--build-arg", "SERVICE_PROJECT_PATH=$projectPath",
        "--build-arg", "ASSEMBLY_NAME=$assemblyName",
        "-t", $image,
        "-f", "$Root/Dockerfile",
        $Root
    )
    & docker @buildArgs
    if ($LASTEXITCODE -ne 0) { Write-Error "Build failed for '$svc'"; exit 1 }

    # Write-Host "==> Pushing $image" -ForegroundColor Cyan
    # & docker push $image
    # if ($LASTEXITCODE -ne 0) { Write-Error "Push failed for '$svc'"; exit 1 }
}

Write-Host ""
Write-Host "Done - all images built and pushed to $Registry." -ForegroundColor Green
