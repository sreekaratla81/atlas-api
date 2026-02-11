# Startup Failure Report

- Log root scanned: `D:\home\LogFiles`
- First exception type: `Pending capture from deployed App Service logs`
- First module: `Pending capture from deployed App Service logs`
- Evidence: `Not available in local development container`

## Notes

The deployment log path is only available on Azure App Service workers. Use `tools/appservice/Get-StartupFailureReport.ps1` from Kudu/remote PowerShell to populate this report with the real first exception and module.
