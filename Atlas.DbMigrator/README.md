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
dotnet run --project Atlas.DbMigrator -- --connection "${ATLAS_DB_CONNECTION}" --check-only

# apply migrations
dotnet run --project Atlas.DbMigrator -- --connection "${ATLAS_DB_CONNECTION}"
```
