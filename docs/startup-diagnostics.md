# App Service startup diagnostics (Development only)

## Required Application Setting (P0 — app will not start without it)

**Jwt__Key** (or **Jwt:Key**) must be set in **Azure App Service → Configuration → Application settings** for the app to start when `ASPNETCORE_ENVIRONMENT=Production`.

- **Name:** `Jwt__Key`
- **Value:** A secret key of **at least 32 characters** (used for JWT signing).
- If missing, the app throws at startup: *"JWT authentication is required in Production. Set Jwt__Key (or Jwt:Key) to a 32+ character secret in Azure App Service > Configuration > Application Settings."* and you see **HTTP 500.30** (ASP.NET Core app failed to start).

Add the setting, **Save**, then **Restart** the app.

## 0) HTTP 500.30 — app failed to start (platform mismatch)

If you see **HTTP Error 500.30 - ASP.NET Core app failed to start** on Azure:

- **Cause**: App Service is **32-bit** but the deployed app is 64-bit (or vice versa). Basic tier is 32-bit.
- **Prevention**: CI publishes with `-r win-x86`, validates the exe is 32-bit via `scripts/validate-publish.ps1`, and **sets the App Service to 32-bit** before each deploy (`az webapp config set --use-32bit-worker-process true`), so the host always matches the artifact even if the portal was changed. Do not change the publish step to `win-x64` unless you set Azure App Service **Configuration > General settings > Platform** to **64 Bit** and remove the 32-bit config step from the workflow.
- **Fix**: In Azure Portal, **App Service > Configuration > General settings**: set **Platform** to **32 Bit**. Redeploy the artifact produced by CI (which is win-x86). Or switch to 64-bit and use `-r win-x64` in the workflow (and update the validate script).

## View Log Stream (when 500.30 or startup failures recur)

To see live app and web server logs (including startup exceptions):

1. Open **Azure Portal** → your subscription → **App Service** (e.g. `atlas-homes-api-dev` for dev).
2. In the left menu, under **Monitoring**, click **Log stream**.
3. Ensure **Application Logging** is enabled: **App Service** → **App Service logs** → set **Application Logging** to **File System** (or **Blob** if you prefer), set level (e.g. **Information**), **Save**.
4. In **Log stream**, choose what to stream:
   - **Application logs** — ASP.NET Core stdout and your app’s logging (e.g. startup errors, exceptions).
   - **Web server logs** — IIS/ANCM messages.
5. Trigger a request or restart the app (**Overview** → **Restart**) to reproduce the failure; the stream will show new lines in real time.

For file-based logs (e.g. `stdout`, failure report), use **Advanced Tools (Kudu)** → **Debug console** → **PowerShell** and browse `D:\home\LogFiles` (see section 2 below).

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
