# Atlas.DbMigrator

## Task stub: running the DbMigrator

Copy/paste one of the commands below to execute the migrator.

**Local (LocalDb):**

```bash

# check-only (exit code 2 if pending migrations exist)
dotnet run -- --connection "Server=.;Database=atlasdb-dev-latest;Trusted_Connection=True;TrustServerCertificate=True" --check-only

# apply migrations
dotnet run -- --connection "Server=.;Database=atlasdb-dev-latest;Trusted_Connection=True;TrustServerCertificate=True"
```

**Dev/Prod (env var connection string):**

```bash
# check-only (exit code 2 if pending migrations exist)
dotnet run --project Atlas.DbMigrator -- --connection "${ATLAS_DEV_SQL_CONNECTION}" --check-only

# apply migrations
dotnet run --project Atlas.DbMigrator -- --connection "${ATLAS_DEV_SQL_CONNECTION}"
```

**Required secrets:**

- `ATLAS_DEV_SQL_CONNECTION`
- `ATLAS_PROD_SQL_CONNECTION` (when production wiring is added)

**Deployment flow:** Check pending migrations → Apply migrations → Deploy.

**Troubleshooting:** If you see `Missing value for --connection`, confirm the
GitHub Actions secret exists and the workflow passes it to DbMigrator via
`--connection` or an environment variable.

## Placeholder migration warning

Placeholder migrations in `Atlas.Api/Migrations/` exist only to satisfy "migration exists in assembly" checks (for example, `DbMigrator --check-only` against environments whose `__EFMigrationsHistory` already contains those IDs).

Before applying production migrations (especially `20260204064128_AddRazorpayPaymentFields` through `20260209104230_AddRazorpayColumnsToPayments`), restore the real migration bodies and corresponding designer files from previous commits. EF relies on per-migration `TargetModel`, so placeholders are not safe for actual schema-apply workflows.
