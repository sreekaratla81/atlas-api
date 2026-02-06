# Atlas.DbMigrator

## Task stub: running the DbMigrator

Copy/paste one of the commands below to execute the migrator.

**Local (LocalDb):**

```bash
dotnet run --project Atlas.DbMigrator -- --connection "Server=(localdb)\\MSSQLLocalDB;Database=AtlasHomestays_Local;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True"
```

**Dev/Prod (env var connection string):**

```bash
# check-only (exit code 2 if pending migrations exist)
dotnet run --project Atlas.DbMigrator -- --connection "${ATLAS_DB_CONNECTION}" --check-only

# apply migrations
dotnet run --project Atlas.DbMigrator -- --connection "${ATLAS_DB_CONNECTION}"
```
