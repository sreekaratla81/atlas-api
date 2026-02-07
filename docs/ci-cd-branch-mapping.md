# CI/CD Branch → Environment → Azure Web App Mapping

## Quick diagram

```
dev  → dev_atlas-homes-api-dev.yml → atlas-homes-api-dev → ATLAS_DEV_SQL_CONNECTION_STRING
main → deploy.yml                 → atlas-homes-api     → ATLAS_PROD_SQL_CONNECTION_STRING
manual prod migrations → prod-migrate.yml → (no deploy) → ATLAS_PROD_SQL_CONNECTION_STRING (gated)
```

## Workflow inventory and deployment targets

| Workflow file | Trigger branches | Deploy target app-name | Auth method | Uses DbMigrator? | Dev/Prod |
| --- | --- | --- | --- | --- | --- |
| `.github/workflows/dev_atlas-homes-api-dev.yml` | `on.push.branches: [dev]` and `workflow_dispatch` | `atlas-homes-api-dev` | `azure/login@v2` (client/tenant/subscription secrets) | Yes (validate/check/apply) | Dev |
| `.github/workflows/deploy.yml` | `on.push.branches: [main]` and `workflow_dispatch` | `atlas-homes-api` | `azure/webapps-deploy@v3` with publish profile | Yes (validate/check/apply, dev-only apply for manual dispatch) | Prod |
| `.github/workflows/prod-migrate.yml` | `workflow_dispatch` only | _None (migrations only)_ | _None (no deploy)_ | Yes (validate/check/apply gated) | Prod |

## Branch → environment mapping logic (exact mechanism)

### Dev branch
- `dev_atlas-homes-api-dev.yml` uses a dedicated workflow with a fixed branch trigger: `on.push.branches: - dev` and deploys to `app-name: 'atlas-homes-api-dev'`. This is a **separate workflow per branch** mapping. See `.github/workflows/dev_atlas-homes-api-dev.yml` `on.push.branches` and `Deploy to Azure Web App` `app-name` keys.

### Main branch (prod)
- `deploy.yml` uses a dedicated workflow with a fixed branch trigger: `on.push.branches: - main` and deploys to `app-name: atlas-homes-api`. This is also a **separate workflow per branch** mapping. See `.github/workflows/deploy.yml` `on.push.branches` and `Deploy to Azure Web App` `app-name` keys.

### Manual dispatch nuance
- `deploy.yml` also supports `workflow_dispatch` with an `environment` input (`dev` or `prod`), but the **deploy target app-name remains `atlas-homes-api`**, so manual runs still deploy to the prod app unless edited. The input is only used to decide which DB connection string is used for migrator checks/apply in this workflow.

## Azure auth binding per workflow

### Dev deployment (`dev_atlas-homes-api-dev.yml`)
- Auth uses `azure/login@v2` with these secrets:
  - `AZUREAPPSERVICE_CLIENTID_549666B25F124F47A8A02ABB67C651ED`
  - `AZUREAPPSERVICE_TENANTID_B891A9E8DB8C42D095F9439D8E364707`
  - `AZUREAPPSERVICE_SUBSCRIPTIONID_21B2EDBA7F42470F91A861E168D2DAC9`
- The target app is set by `app-name: 'atlas-homes-api-dev'` in the deploy step.

### Prod deployment (`deploy.yml`)
- Auth uses `azure/webapps-deploy@v3` with a publish profile secret:
  - `AZURE_WEBAPP_PUBLISH_PROFILE`
- The target app is set by `app-name: atlas-homes-api` in the deploy step.

## DbMigrator environment mapping

### Dev workflow (dev branch)
- `dev_atlas-homes-api-dev.yml` uses `ATLAS_DEV_SQL_CONNECTION_STRING` for:
  - `Validate migrator connection`
  - `Check for pending migrations`
  - `Apply migrations`

### Prod workflow (main branch)
- `deploy.yml` uses a conditional expression to select the connection string:
  - `ATLAS_DEV_SQL_CONNECTION_STRING` only when `workflow_dispatch` and `inputs.environment == 'dev'`
  - Otherwise `ATLAS_PROD_SQL_CONNECTION_STRING`
- Migrations **apply** only when `workflow_dispatch` and `inputs.environment == 'dev'` (`Apply migrations` step `if`), so prod applies are not automatic in this workflow.

### Prod migrations (manual gated)
- `prod-migrate.yml` uses `ATLAS_PROD_SQL_CONNECTION_STRING`.
- Apply step is **explicitly gated** by `if: inputs.confirm == 'APPLY_PROD_MIGRATIONS'`.

## How to verify quickly

- **Branch triggers:** check `on.push.branches` in each workflow YAML.
- **App target:** check `Deploy to Azure Web App` → `app-name`.
- **Migration gating:** check `Apply migrations` step `if:` conditions in `deploy.yml` and `prod-migrate.yml`.

## Common misconfigurations to avoid

- Using the same `app-name` in both workflows (would deploy dev and prod to the same Azure Web App).
- Using the wrong publish profile secret for prod (deploy would succeed but target the wrong app or fail authorization).
- Missing or misspelled DB connection string secrets (migrator will fail at the validation step).
