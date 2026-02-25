# Validates that a dotnet publish output directory contains the required deploy artifacts
# and that the main exe is 32-bit (win-x86). Azure App Service dev is 32-bit; deploying
# 64-bit causes HTTP 500.30 on startup.
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

# Ensure Atlas.Api.exe is 32-bit (PE machine 0x14c). Azure App Service is 32-bit; 64-bit deploy = 500.30.
$exePath = Join-Path $PublishPath 'Atlas.Api.exe'
$bytes = [System.IO.File]::ReadAllBytes($exePath)
if ($bytes.Length -lt 64) {
    Write-Error "Atlas.Api.exe is too small to be a valid PE file."
    exit 1
}
$peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
if ($peOffset -lt 0 -or $peOffset -gt $bytes.Length - 6) {
    Write-Error "Atlas.Api.exe: invalid PE header offset."
    exit 1
}
$machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
# 0x14c = IMAGE_FILE_MACHINE_I386 (32-bit), 0x8664 = AMD64 (64-bit)
if ($machine -ne 0x14c) {
    Write-Error "Atlas.Api.exe is 64-bit (PE machine 0x$($machine.ToString('X4'))). Azure App Service is 32-bit; this would cause HTTP 500.30 on startup. Publish with -r win-x86 (see ci-deploy-dev.yml / deploy-prod.yml)."
    exit 1
}
Write-Host "Atlas.Api.exe is 32-bit (win-x86) — OK for Azure App Service."

Write-Host "All required deploy artifacts are present in '$PublishPath'."
exit 0
