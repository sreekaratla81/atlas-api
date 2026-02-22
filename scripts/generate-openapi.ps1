param(
  [string]$Configuration = "Release",
  [string]$OutputPath = "docs/api/openapi.json",
  [string]$ApiProject = "Atlas.Api/Atlas.Api.csproj",
  [string]$SwaggerDoc = "v1"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Restoring local dotnet tools (Atlas.Api/.config/dotnet-tools.json)..."
Push-Location "Atlas.Api"
dotnet tool restore
Pop-Location

Write-Host "Building API ($Configuration)..."
dotnet build $ApiProject -c $Configuration

$dll = "Atlas.Api/bin/$Configuration/net8.0/Atlas.Api.dll"
if (-not (Test-Path $dll)) {
  throw "Expected build output not found: $dll"
}

Write-Host "Generating OpenAPI to $OutputPath ..."
New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath) | Out-Null

# Uses Swashbuckle CLI to generate swagger.json without running the web host.
# The tool manifest lives under `Atlas.Api/.config`, so execute from that directory.
$root = (Get-Location).Path
$outAbs = Join-Path $root $OutputPath
$dllAbs = Join-Path $root $dll

Push-Location "Atlas.Api"
dotnet tool run swagger tofile --output $outAbs $dllAbs $SwaggerDoc
Pop-Location

Write-Host "Done."

