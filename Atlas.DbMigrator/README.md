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
