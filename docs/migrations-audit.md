# Migrations Audit

## Current state summary
- Application startup runs EF Core migrations when the environment is Development or IntegrationTest, and for other environments only when `RunMigrations` is enabled in configuration. Reference: `Atlas.Api/Program.cs`. 
- The DbMigrator CLI applies or checks pending migrations for a provided SQL Server connection string (`--connection`) and supports a `--check-only` mode. Reference: `Atlas.DbMigrator/*`.
- Integration tests use LocalDB, set the environment to `IntegrationTest`, and apply migrations/reset data per test run via respawn and explicit `MigrateAsync` calls. References: `Atlas.Api.IntegrationTests/CustomWebApplicationFactory.cs`, `Atlas.Api.IntegrationTests/IntegrationTestBase.cs`, `Atlas.Api.IntegrationTests/SqlServerTestDatabase.cs`, `Atlas.DbMigrator.IntegrationTests/MigratorIntegrationTests.cs`.
- CI workflows run unit/integration tests, check for pending migrations, and apply migrations for main/release using secrets. References: `.github/workflows/deploy.yml`, `.github/workflows/dev_atlas-homes-api-dev.yml`.

## Risks found
- Startup migrations in Development/IntegrationTest reduce friction but can mask missing migration steps in deployment pipelines if teams rely solely on app startup rather than the migrator or CI gate. (`Atlas.Api/Program.cs`)
- Production safety relies on correct configuration of `RunMigrations` and the correct connection string; a misconfiguration can apply migrations to an unintended database. (`Atlas.Api/Program.cs`, `.github/workflows/deploy.yml`)
- Environment marker validation is present but commented out, so there is no runtime guard against accidentally pointing dev/prod to the wrong database. (`Atlas.Api/Program.cs`)
- The dev deployment workflow does not run DbMigrator, so dev environments can drift if deployments occur without a separate migration step. (`.github/workflows/dev_atlas-homes-api-dev.yml`)

## Recommended changes with rationale
- Re-enable environment marker validation at startup to prevent dev/prod database mix-ups; this provides a strong safety net when connection strings are misconfigured. (`Atlas.Api/Program.cs`)
- Gate production migration application with explicit approvals or an opt-in workflow input, and keep `--check-only` as a preflight step; this reduces the risk of unintended schema changes during deployments. (`.github/workflows/deploy.yml`, `Atlas.DbMigrator/*`)
- Add a migration step to the dev deployment workflow (check/apply) so dev schema aligns with the codebase and tests. (`.github/workflows/dev_atlas-homes-api-dev.yml`, `Atlas.DbMigrator/*`)
- Reduce connection string exposure in logs (avoid printing full connection strings) to minimize operational risk while still enabling debugging. (`Atlas.Api/Program.cs`)

## What changed
- Added this migrations audit document to capture migration safety, environment handling, and recommended improvements. (`docs/migrations-audit.md`)
