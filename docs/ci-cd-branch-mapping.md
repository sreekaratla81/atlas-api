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
| `.github/workflows/dev_atlas-homes-api-dev.yml` | `on.push.branches: [dev]`, `workflow_dispatch` | `atlas-homes-api-dev` | `azure/login@v2` with `client-id`, `tenant-id`, `subscription-id` secrets | Validates dev connection, checks pending migrations, applies migrations using `ATLAS_DEV_SQL_CONNECTION_STRING`. |
| `.github/workflows/deploy.yml` | `on.push.branches: [main]`, `workflow_dispatch` | `atlas-homes-api` | `azure/webapps-deploy@v3` with publish profile secret `AZURE_WEBAPP_PUBLISH_PROFILE` | Validates connection + checks pending migrations on `main` (or `release/*`); applies migrations only for `workflow_dispatch` + `inputs.environment == dev` (prod migrations gated). |
| `.github/workflows/prod-migrate.yml` | `workflow_dispatch` only | _N/A_ (no app deploy) | _N/A_ | Validates prod connection, checks pending migrations, applies migrations only when `inputs.confirm == 'APPLY_PROD_MIGRATIONS'` using `ATLAS_PROD_SQL_CONNECTION_STRING`. |

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

## Common Misconfigurations

- **Same app name in both workflows**: `atlas-homes-api` and `atlas-homes-api-dev` must remain distinct to prevent deploys from overwriting the wrong environment.
- **Wrong auth secret type**:
  - `dev_atlas-homes-api-dev.yml` expects `azure/login` secrets (client/tenant/subscription IDs).
  - `deploy.yml` expects `AZURE_WEBAPP_PUBLISH_PROFILE` for publish-profile deployments.
- **Missing/typo secrets**:
  - `ATLAS_DEV_SQL_CONNECTION_STRING` and `ATLAS_PROD_SQL_CONNECTION_STRING` must match the workflow references exactly.
  - Ensure secrets are present in the repo/organization for the relevant environment.
