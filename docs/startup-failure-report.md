# Startup Failure Report

- Log root scanned: `D:\home\LogFiles`
- First exception type: `Pending capture from deployed App Service logs`
- First module: `Pending capture from deployed App Service logs`
- Evidence: `Run tools/appservice/Get-StartupFailureReport.ps1 from Kudu/worker to populate`

## Native dependency assessment

Pending capture. If the first exception is `System.BadImageFormatException`, validate x86/x64 alignment and pin native dependencies (for example `Microsoft.Data.SqlClient` / SNI runtime assets) to x86-compatible binaries for `win-x86` publishes.
