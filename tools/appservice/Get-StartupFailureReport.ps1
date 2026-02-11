param(
    [string]$LogRoot = 'D:\home\LogFiles',
    [string]$OutputPath = 'docs/startup-failure-report.md'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $LogRoot)) {
    throw "Log root not found: $LogRoot"
}

$logFiles = Get-ChildItem -LiteralPath $LogRoot -Recurse -File |
    Where-Object { $_.Extension -in '.txt', '.log', '.xml' } |
    Sort-Object LastWriteTime

if (-not $logFiles) {
    throw "No log files found under $LogRoot"
}

$exceptionPattern = [regex]'(?<type>[A-Za-z_][A-Za-z0-9_.]+Exception)'
$modulePattern = [regex]'(module|Module|Faulting module name)\s*[:=]\s*(?<module>[A-Za-z0-9_.-]+)'

$firstException = $null
$firstModule = $null
$evidenceFile = $null
$evidenceLine = $null

foreach ($file in $logFiles) {
    $lineNumber = 0
    Get-Content -LiteralPath $file.FullName | ForEach-Object {
        $lineNumber++
        $line = $_

        if (-not $firstException) {
            $exceptionMatch = $exceptionPattern.Match($line)
            if ($exceptionMatch.Success) {
                $firstException = $exceptionMatch.Groups['type'].Value
                $evidenceFile = $file.FullName
                $evidenceLine = $lineNumber
            }
        }

        if (-not $firstModule) {
            $moduleMatch = $modulePattern.Match($line)
            if ($moduleMatch.Success) {
                $firstModule = $moduleMatch.Groups['module'].Value
                if (-not $evidenceFile) {
                    $evidenceFile = $file.FullName
                    $evidenceLine = $lineNumber
                }
            }
        }
    }

    if ($firstException -and $firstModule) {
        break
    }
}

if (-not $firstException) {
    $firstException = 'Not found in scanned logs'
}

if (-not $firstModule) {
    $firstModule = 'Not found in scanned logs'
}

$nativeDependencyHint = 'No native dependency indicator detected.'
if ($firstException -eq 'System.BadImageFormatException') {
    $nativeDependencyHint = @(
        'Potential architecture mismatch detected (x86/x64).',
        'For win-x86 deployments, ensure App Service Platform is set to 32-bit and native runtime assets resolve to win-x86.',
        'If Microsoft.Data.SqlClient.SNI is implicated, pin Microsoft.Data.SqlClient to a version with win-x86 native assets.'
    ) -join ' '
}

$report = @"
# Startup Failure Report

- Log root scanned: `$LogRoot`
- First exception type: `$firstException`
- First module: `$firstModule`
- Evidence: `$evidenceFile`:$evidenceLine

## Native dependency assessment
$nativeDependencyHint
"@

$dir = Split-Path -Parent $OutputPath
if ($dir -and -not (Test-Path -LiteralPath $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

$report | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Report written to $OutputPath"
