# App Service startup diagnostics (Development only)

## 1) Enable temporary ANCM stdout + detailed errors for Development

This repo now ships:

- `Atlas.Api/web.config` with safe production defaults:
  - `stdoutLogEnabled="false"`
  - `ASPNETCORE_DETAILEDERRORS=false`
- `Atlas.Api/web.Debug.config` and `Atlas.Api/web.Development.config` transforms that enable:
  - `stdoutLogEnabled="true"`
  - `stdoutLogFile="\\?\D:\home\LogFiles\stdout"`
  - `ASPNETCORE_DETAILEDERRORS=true`

For a Development deployment, publish using `Debug` configuration (or explicitly apply the `web.Development.config` transform in your release pipeline).

## 2) Capture and summarize startup exception from `D:\home\LogFiles`

Run this script from Kudu PowerShell / App Service console:

```powershell
pwsh -File tools/appservice/Get-StartupFailureReport.ps1 \
  -LogRoot 'D:\home\LogFiles' \
  -OutputPath 'docs/startup-failure-report.md'
```

The generated report records:

- First thrown exception type
- First module name (if present)
- Evidence file + line
- Native dependency hint for `System.BadImageFormatException`

## 3) If win-x86 native dependency mismatch is identified

If the report surfaces `System.BadImageFormatException` and an SNI/native module:

1. Keep publish RID as `win-x86` for the diagnostic run.
2. Confirm App Service **Platform** is set to **32-bit**.
3. Pin the offending package to a release that provides `win-x86` native assets.
4. Re-publish and re-run the report script to confirm startup succeeds.

> After diagnostics, revert `stdoutLogEnabled` and `ASPNETCORE_DETAILEDERRORS` to production-safe values.
