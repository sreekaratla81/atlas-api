# Validates that a dotnet publish output directory contains the required deploy artifacts.
# Run locally after: dotnet publish ./Atlas.Api/Atlas.Api.csproj -c Release -o ./publish -r win-x86 --self-contained true
# Usage: ./scripts/validate-publish.ps1 [-PublishPath <path>]
param(
    [string]$PublishPath = './publish'
)

$requiredFiles = @(
    'Atlas.Api.exe',
    'Atlas.Api.dll',
    'Atlas.Api.deps.json',
    'Atlas.Api.runtimeconfig.json',
    'web.config'
)

if (-not (Test-Path $PublishPath)) {
    Write-Error "Publish path does not exist: $PublishPath"
    exit 1
}

$missingFiles = $requiredFiles | Where-Object {
    -not (Test-Path (Join-Path $PublishPath $_))
}

if ($missingFiles.Count -gt 0) {
    Write-Error "Missing required deploy artifacts in '$PublishPath': $($missingFiles -join ', ')"
    exit 1
}

Write-Host "All required deploy artifacts are present in '$PublishPath'."
exit 0
