# App Service startup diagnostics (Development only)

## 0) HTTP 500.30 — app failed to start (platform mismatch)

If you see **HTTP Error 500.30 - ASP.NET Core app failed to start** on Azure:

- **Cause**: App Service is **32-bit** but the deployed app is 64-bit (or vice versa). Basic tier is 32-bit.
- **Prevention**: CI already publishes with `-r win-x86` and `scripts/validate-publish.ps1` **fails the pipeline** if the exe is not 32-bit, so deploy never uploads a mismatched build. Do not change the publish step to `win-x64` unless you set Azure App Service **Configuration > General settings > Platform** to **64 Bit**.
- **Fix**: In Azure Portal, **App Service > Configuration > General settings**: set **Platform** to **32 Bit**. Redeploy the artifact produced by CI (which is win-x86). Or switch to 64-bit and use `-r win-x64` in the workflow (and update the validate script).

## 1) Enable temporary ANCM stdout + detailed errors for Development

This repo now uses transform-based behavior so diagnostics are only enabled for Development deployments.

- `Atlas.Api/web.config` (baseline / production)
  - `processPath=".\\Atlas.Api.exe"` for self-contained `win-x86` deployment on 32-bit App Service
  - `stdoutLogEnabled="true"` so ANCM writes startup logs to `%home%\LogFiles\stdout` (avoids blind 500.31/500.32 failures)
  - `stdoutLogFile="\\?\%home%\\LogFiles\\stdout"`
  - `ASPNETCORE_DETAILEDERRORS=false`
- `Atlas.Api/web.Development.config` and `Atlas.Api/web.Debug.config` (diagnostic toggle ON)
  - `stdoutLogEnabled="true"`
  - `stdoutLogFile="\\?\D:\home\LogFiles\stdout"`
  - `ASPNETCORE_DETAILEDERRORS=true`
- `Atlas.Api/web.Release.config` (explicit production-safe override)
  - `stdoutLogEnabled="false"`
  - `ASPNETCORE_DETAILEDERRORS=false`

Use Development/Debug publish for short-lived diagnostics, then re-deploy Release to disable verbose startup logging.

For a quick liveness check without opening Kudu, call `GET /health`; it returns 200 with `{ "status": "healthy" }` when the app is running.

## 2) Capture and summarize startup exception from `D:\home\LogFiles`

Run from Kudu PowerShell / App Service console:

```powershell
pwsh -File tools/appservice/Get-StartupFailureReport.ps1 \
  -LogRoot 'D:\home\LogFiles' \
  -OutputPath 'docs/startup-failure-report.md'
```

The report records:

- First thrown exception type
- First module (faulting module or first `.dll` token)
- Evidence file + line
- Native dependency hint for `System.BadImageFormatException`

## 3) If win-x86 native dependency mismatch is identified

If the report surfaces `System.BadImageFormatException` and an SNI/native module:

1. Keep publish RID as `win-x86` for the diagnostic run.
2. Confirm App Service **Platform** is set to **32-bit**.
3. Pin or adjust the offending package/runtime assets to x86-compatible binaries.
   - Common offender: `Microsoft.Data.SqlClient.SNI.dll`
   - Pin `Microsoft.Data.SqlClient` to a version that ships matching `win-x86` native assets.
4. Re-publish and re-run the report script until startup succeeds.

> After diagnostics, redeploy with Release transform so `stdoutLogEnabled` and `ASPNETCORE_DETAILEDERRORS` are reset to production-safe values.
