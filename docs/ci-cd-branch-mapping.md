# CI/CD Branch Mapping

## Branch → environment rule

| Branch | Deploys to | How |
| --- | --- | --- |
| **dev** | **Dev only** (`atlas-homes-api-dev`) | `ci-deploy-dev.yml` (CI and Deploy to Dev) runs on push to dev; deploy-dev job runs only when ref is dev (push or workflow_dispatch from dev). |
| **main** | **Prod only** (`atlas-homes-api`) | `deploy-prod.yml` runs on push to main (when `DEPLOY_PROD_ON_MAIN` is set); manual prod deploy via workflow_dispatch is allowed only when run from the main branch (guard step fails otherwise). |

Dev branch never deploys to prod. Main branch never deploys to dev on push; manual deploy to dev from main is possible via `deploy-prod.yml` workflow_dispatch with environment = dev.

## Branch → Workflow → App → Connection String Diagram

```text
Dev branch
  dev → ci-deploy-dev.yml (ci job + deploy-dev job) → atlas-homes-api-dev → ATLAS_DEV_SQL_CONNECTION_STRING

Main branch
  main → deploy-prod.yml → atlas-homes-api → ATLAS_PROD_SQL_CONNECTION_STRING
  (prod migrations gated)
```

## Workflow Reference Table

| Workflow file | Trigger(s) | Azure app name (`with.app-name`) | Auth method | DbMigrator usage |
| --- | --- | --- | --- | --- |
| `.github/workflows/ci-deploy-dev.yml` | `on.push.branches: [dev]`, `pull_request` (main, dev), `workflow_dispatch` | `atlas-homes-api-dev` (deploy-dev job only on push/workflow_dispatch to dev) | `azure/login@v2` with OIDC secrets in deploy-dev job | deploy-dev: validates dev connection, checks pending migrations, applies migrations using `ATLAS_DEV_SQL_CONNECTION_STRING`. |
| `.github/workflows/deploy-prod.yml` | `on.push.branches: [main]`, `workflow_dispatch` | `atlas-homes-api` (prod) or `atlas-homes-api-dev` (manual dev) | `azure/login@v2` with OIDC secrets | Prod deploy only from main (guard step enforces this on workflow_dispatch). Validates connection + checks pending migrations; applies migrations only for `workflow_dispatch` + `inputs.environment == dev` (prod migrations gated). |
| `.github/workflows/prod-migrate.yml` | `workflow_dispatch` only | _N/A_ (no app deploy) | _N/A_ | Validates prod connection, checks pending migrations, applies migrations only when `inputs.confirm == 'APPLY_PROD_MIGRATIONS'` using `ATLAS_PROD_SQL_CONNECTION_STRING`. |

## Required secrets by workflow

| Workflow file | Required/used secret identifiers (exact names from workflow YAML) |
| --- | --- |
| `.github/workflows/ci-deploy-dev.yml` (deploy-dev job) | `ATLAS_DEV_SQL_CONNECTION_STRING`, `AZURE_CLIENT_ID_DEV`, `AZURE_TENANT_ID_DEV`, `AZURE_SUBSCRIPTION_ID_DEV` |
| `.github/workflows/deploy-prod.yml` | `ATLAS_DEV_SQL_CONNECTION_STRING`, `ATLAS_PROD_SQL_CONNECTION_STRING`, `AZURE_CLIENT_ID_DEV`, `AZURE_TENANT_ID_DEV`, `AZURE_SUBSCRIPTION_ID_DEV`, `AZURE_CLIENT_ID_PROD`, `AZURE_TENANT_ID_PROD`, `AZURE_SUBSCRIPTION_ID_PROD` |

`AZURE_CLIENT_ID_*`, `AZURE_TENANT_ID_*`, and `AZURE_SUBSCRIPTION_ID_*` correspond to `*_DEV` and `*_PROD` variants shown above.

## Repo variables used by deploy-prod.yml

| Variable | Purpose |
| --- | --- |
| `PROD_API_URL` | Base URL for prod smoke test (e.g. `https://atlas-homes-api.azurewebsites.net`). Required when deploying to prod (push to main or workflow_dispatch → prod). |
| `DEV_API_URL` | Base URL for dev smoke test (e.g. `https://atlas-homes-api-dev.azurewebsites.net`). Required when deploying to dev (workflow_dispatch → dev). |
| `DEPLOY_PROD_ON_MAIN` | Set to `true` to enable deploy to prod on push to `main`. |

Resource group is **not** a variable: prod uses `atlas-prod-rg`, dev uses `atlas-dev-rg`.

## Checklist for Verification

- **Branch triggers**: Confirm the correct branch is listed under `on.push.branches` in each workflow.
  - `ci-deploy-dev.yml` lists `dev` for push; deploy-dev job runs only when ref is dev (push or workflow_dispatch).
  - `deploy-prod.yml` should list `main`.
- **App name**: Confirm `with.app-name` matches the intended Azure Web App name.
  - CI and Deploy to Dev workflow, deploy-dev job → `atlas-homes-api-dev`.
  - Deploy workflow → `atlas-homes-api`.
- **Migration gating**: Review `if:` conditions on migration steps.
  - `deploy-prod.yml` should apply migrations only on `workflow_dispatch` when `inputs.environment == dev`.
  - `prod-migrate.yml` should apply migrations only when `inputs.confirm == 'APPLY_PROD_MIGRATIONS'`.

## Prod deployment strategy (32-bit Free/Shared tier)

- **Why 500.31 happens**: On Azure App Service Free/Shared (Windows), the host is **32-bit only** and does not ship the .NET 8 runtime. A **framework-dependent** publish expects `Microsoft.NetCore.App` / `Microsoft.AspNetCore.App` to be installed, so ANCM fails with "Failed to Find Native Dependencies" (500.31).
- **Fix**: Prod is published as **self-contained win-x86** (`-r win-x86 --self-contained true`). The app and its runtime are bundled, so no host runtime is required and the 32-bit worker process can load the app.
- **Logs**: After deploy, stdout and ANCM logs are written under the app’s home directory. In **Kudu** (Advanced Tools → Debug console), open **LogFiles** and check **stdout** (and related logs). See `docs/startup-diagnostics.md` for capturing startup failure reports.
- **Runtime assets**: The full publish output (including **`runtimes/`** and its subtree, e.g. `runtimes/win/lib/net8.0/Microsoft.Data.SqlClient.dll`) must be deployed. If the deploy package omits `runtimes/`, the app fails at startup with 500.31 and stdout shows "An assembly specified in the application dependencies manifest (Atlas.Api.deps.json) was not found" for `Microsoft.Data.SqlClient`. The workflow zips the **contents** of `./publish` (e.g. `Compress-Archive -Path './publish/*'`) so nested directories are included, and validates that `runtimes/win/lib/net8.0/Microsoft.Data.SqlClient.dll` exists before deploy.
- **Post-deploy**: The workflow restarts the Web App and runs a smoke test. Resource group is chosen by target: **prod** → `atlas-prod-rg` (app `atlas-homes-api`), **dev** (workflow_dispatch with environment = dev) → `atlas-dev-rg` (app `atlas-homes-api-dev`). Repo variables **`PROD_API_URL`** and **`DEV_API_URL`** must be set for the smoke test (e.g. `https://atlas-homes-api.azurewebsites.net`, `https://atlas-homes-api-dev.azurewebsites.net`); if unset, the smoke test URL is empty and the step fails.

### Verification after deploy

- **Swagger**: `GET /swagger` or `GET /swagger/v1/swagger.json` returns **200** (workflow smoke test asserts this).
- **Business endpoint**: At least one API endpoint (e.g. `GET /listings/public` or health) returns a non-5xx response.

### PR description (500.31 fix)

- **Why 500.31 happened**: Prod runs on Azure App Service Free/Shared (Windows), which is 32-bit and does not install the .NET 8 runtime. A framework-dependent publish expects `Microsoft.NetCore.App` / `Microsoft.AspNetCore.App` on the host, so ANCM fails with "Failed to Find Native Dependencies" (500.31).
- **Why self-contained win-x86 fixes it**: Publishing with `-r win-x86 --self-contained true` bundles the app and its runtime, so no host runtime is required and the 32-bit worker can load the app. No plan upgrade needed.

## Common Misconfigurations

- **Same app name in both workflows**: `atlas-homes-api` and `atlas-homes-api-dev` must remain distinct to prevent deploys from overwriting the wrong environment.
- **Wrong auth secret type**:
  - `ci-deploy-dev.yml` (deploy-dev job) expects `AZURE_CLIENT_ID_DEV`, `AZURE_TENANT_ID_DEV`, `AZURE_SUBSCRIPTION_ID_DEV` (OIDC).
  - `deploy-prod.yml` uses OIDC secret sets for dev and prod.
- **Missing/typo secrets**:
  - `ATLAS_DEV_SQL_CONNECTION_STRING` and `ATLAS_PROD_SQL_CONNECTION_STRING` must match the workflow references exactly.
  - Ensure secrets are present in the repo/organization for the relevant environment.
