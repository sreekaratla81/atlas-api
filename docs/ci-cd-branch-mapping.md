# CI/CD Branch Mapping

## Branch → Workflow → App → Connection String Diagram

```text
Dev branch
  dev → dev_atlas-homes-api-dev.yml → atlas-homes-api-dev → ATLAS_DEV_SQL_CONNECTION_STRING

Main branch
  main → deploy.yml → atlas-homes-api → ATLAS_PROD_SQL_CONNECTION_STRING
  (prod migrations gated)
```

## Workflow Reference Table

| Workflow file | Trigger(s) | Azure app name (`with.app-name`) | Auth method | DbMigrator usage |
| --- | --- | --- | --- | --- |
| `.github/workflows/dev_atlas-homes-api-dev.yml` | `on.push.branches: [dev]`, `workflow_dispatch` | `atlas-homes-api-dev` | `azure/login@v2` with Azure App Service OIDC secrets | Validates dev connection, checks pending migrations, applies migrations using `ATLAS_DEV_SQL_CONNECTION_STRING`. |
| `.github/workflows/deploy.yml` | `on.push.branches: [main]`, `workflow_dispatch` | `atlas-homes-api` | `azure/webapps-deploy@v3` with publish profile or `azure/login@v2` with OIDC secrets | Validates connection + checks pending migrations on `main` (or `release/*`); applies migrations only for `workflow_dispatch` + `inputs.environment == dev` (prod migrations gated). |
| `.github/workflows/prod-migrate.yml` | `workflow_dispatch` only | _N/A_ (no app deploy) | _N/A_ | Validates prod connection, checks pending migrations, applies migrations only when `inputs.confirm == 'APPLY_PROD_MIGRATIONS'` using `ATLAS_PROD_SQL_CONNECTION_STRING`. |

## Required secrets by workflow

| Workflow file | Required/used secret identifiers (exact names from workflow YAML) |
| --- | --- |
| `.github/workflows/dev_atlas-homes-api-dev.yml` | `ATLAS_DEV_SQL_CONNECTION_STRING`, `AZUREAPPSERVICE_CLIENTID_549666B25F124F47A8A02ABB67C651ED`, `AZUREAPPSERVICE_TENANTID_B891A9E8DB8C42D095F9439D8E364707`, `AZUREAPPSERVICE_SUBSCRIPTIONID_21B2EDBA7F42470F91A861E168D2DAC9` |
| `.github/workflows/deploy.yml` | `AZURE_WEBAPP_PUBLISH_PROFILE_DEV`, `AZURE_WEBAPP_PUBLISH_PROFILE_PROD`, `ATLAS_DEV_SQL_CONNECTION_STRING`, `ATLAS_PROD_SQL_CONNECTION_STRING`, `AZURE_CLIENT_ID_DEV`, `AZURE_TENANT_ID_DEV`, `AZURE_SUBSCRIPTION_ID_DEV`, `AZURE_CLIENT_ID_PROD`, `AZURE_TENANT_ID_PROD`, `AZURE_SUBSCRIPTION_ID_PROD`, `AZUREAPPSERVICE_CLIENTID_549666B25F124F47A8A02ABB67C651ED`, `AZUREAPPSERVICE_TENANTID_B891A9E8DB8C42D095F9439D8E364707`, `AZUREAPPSERVICE_SUBSCRIPTIONID_21B2EDBA7F42470F91A861E168D2DAC9` |

`AZURE_CLIENT_ID_*`, `AZURE_TENANT_ID_*`, and `AZURE_SUBSCRIPTION_ID_*` correspond to `*_DEV` and `*_PROD` variants shown above.

## Checklist for Verification

- **Branch triggers**: Confirm the correct branch is listed under `on.push.branches` in each workflow.
  - `dev_atlas-homes-api-dev.yml` should list `dev`.
  - `deploy.yml` should list `main`.
- **App name**: Confirm `with.app-name` matches the intended Azure Web App name.
  - Dev workflow → `atlas-homes-api-dev`.
  - Main workflow → `atlas-homes-api`.
- **Migration gating**: Review `if:` conditions on migration steps.
  - `deploy.yml` should apply migrations only on `workflow_dispatch` when `inputs.environment == dev`.
  - `prod-migrate.yml` should apply migrations only when `inputs.confirm == 'APPLY_PROD_MIGRATIONS'`.

## Prod deployment strategy (32-bit Free/Shared tier)

- **Why 500.31 happens**: On Azure App Service Free/Shared (Windows), the host is **32-bit only** and does not ship the .NET 8 runtime. A **framework-dependent** publish expects `Microsoft.NetCore.App` / `Microsoft.AspNetCore.App` to be installed, so ANCM fails with "Failed to Find Native Dependencies" (500.31).
- **Fix**: Prod is published as **self-contained win-x86** (`-r win-x86 --self-contained true`). The app and its runtime are bundled, so no host runtime is required and the 32-bit worker process can load the app.
- **Logs**: After deploy, stdout and ANCM logs are written under the app’s home directory. In **Kudu** (Advanced Tools → Debug console), open **LogFiles** and check **stdout** (and related logs). See `docs/startup-diagnostics.md` for capturing startup failure reports.
- **Post-deploy**: The workflow restarts the Web App and runs a smoke test on `/swagger/v1/swagger.json`. Optional repo variable `AZURE_WEBAPP_RG` (default `atlas-api-rg`) is used for restart and smoke test.

### Verification after deploy

- **Swagger**: `GET /swagger` or `GET /swagger/v1/swagger.json` returns **200** (workflow smoke test asserts this).
- **Business endpoint**: At least one API endpoint (e.g. `GET /listings/public` or health) returns a non-5xx response.

### PR description (500.31 fix)

- **Why 500.31 happened**: Prod runs on Azure App Service Free/Shared (Windows), which is 32-bit and does not install the .NET 8 runtime. A framework-dependent publish expects `Microsoft.NetCore.App` / `Microsoft.AspNetCore.App` on the host, so ANCM fails with "Failed to Find Native Dependencies" (500.31).
- **Why self-contained win-x86 fixes it**: Publishing with `-r win-x86 --self-contained true` bundles the app and its runtime, so no host runtime is required and the 32-bit worker can load the app. No plan upgrade needed.

## Common Misconfigurations

- **Same app name in both workflows**: `atlas-homes-api` and `atlas-homes-api-dev` must remain distinct to prevent deploys from overwriting the wrong environment.
- **Wrong auth secret type**:
  - `dev_atlas-homes-api-dev.yml` expects Azure App Service OIDC secrets.
  - `deploy.yml` supports publish profile and OIDC secret sets.
- **Missing/typo secrets**:
  - `ATLAS_DEV_SQL_CONNECTION_STRING` and `ATLAS_PROD_SQL_CONNECTION_STRING` must match the workflow references exactly.
  - Ensure secrets are present in the repo/organization for the relevant environment.
