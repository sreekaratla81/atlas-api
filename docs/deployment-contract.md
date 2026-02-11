# Deployment Contract (Windows App Service Free/Shared)

This repository deploys the API with the following **locked deployment contract**:

- **Target host:** Windows App Service (32-bit, Free/Shared tier)
- **Publish mode:** `dotnet publish` self-contained for `win-x86`
- **Startup expectation:** default ASP.NET Core ANCM startup behavior (no custom startup command unless explicitly required)

## CI Guardrail

A CI validation script checks `.github/workflows/deploy.yml` to ensure the publish command remains:

- `-r win-x86`
- `--self-contained true`
- no custom startup command setting

If these values are changed (for example to `win-x64` or framework-dependent publish), CI fails and requires explicit review/update of the deployment contract guard.
