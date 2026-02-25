# Migrations Audit

## Current state summary

- Application startup does not run EF Core migrations; it only configures the `AppDbContext` with the resolved connection string and logs it for debugging. Reference: `Atlas.Api/Program.cs`.
- The DbMigrator CLI applies or checks pending migrations for a provided SQL Server connection string (`--connection`) and supports a `--check-only` mode. Reference: `Atlas.DbMigrator/*`.
- Integration tests use LocalDB, set the environment to `IntegrationTest`, and apply migrations/reset data per test run via respawn and explicit `MigrateAsync` calls. References: `Atlas.Api.IntegrationTests/CustomWebApplicationFactory.cs`, `Atlas.Api.IntegrationTests/IntegrationTestBase.cs`, `Atlas.Api.IntegrationTests/SqlServerTestDatabase.cs`, `Atlas.DbMigrator.IntegrationTests/MigratorIntegrationTests.cs`.
- CI workflows run unit/integration tests and enforce a DbMigrator `--check-only` gate. Dev deploys (ci-deploy-dev.yml deploy-dev job) auto-apply migrations; production migrations are **not** auto-applied on `main` deploys and must be executed via the `prod-migrate.yml` workflow with explicit confirmation. References: `.github/workflows/ci-deploy-dev.yml`, `.github/workflows/deploy-prod.yml`, `.github/workflows/prod-migrate.yml`.

## Risks found

- There is no migrate-on-startup behavior, so schema changes rely entirely on explicit DbMigrator steps in CI/workflows; if those steps are skipped or fail, the app may deploy against an outdated schema. Production migrations additionally require the manual `prod-migrate.yml` confirmation step. (`.github/workflows/ci-deploy-dev.yml`, `.github/workflows/deploy-prod.yml`, `.github/workflows/prod-migrate.yml`)
- Migration safety relies on the correctness of workflow secrets for the connection string; a misconfigured secret could target the wrong database. (`.github/workflows/ci-deploy-dev.yml`, `.github/workflows/deploy-prod.yml`)
- The API logs the resolved connection string at startup, which can expose sensitive details in logs if not masked downstream. (`Atlas.Api/Program.cs`)

## Recommended changes with rationale

- Keep the explicit DbMigrator `--check-only` gate as required for all deployment workflows, and ensure the production apply path remains isolated to the confirmed `prod-migrate.yml` workflow. (`.github/workflows/ci-deploy-dev.yml`, `.github/workflows/deploy-prod.yml`, `.github/workflows/prod-migrate.yml`, `Atlas.DbMigrator/*`)
- Consider adding an explicit environment/connection-string validation step in the workflows or application startup to prevent accidental cross-environment migrations. (`.github/workflows/ci-deploy-dev.yml`, `.github/workflows/deploy-prod.yml`, `Atlas.Api/Program.cs`)
- Reduce connection string exposure in logs (avoid printing full connection strings) to minimize operational risk while still enabling debugging. (`Atlas.Api/Program.cs`)

## What changed

- Added this migrations audit document to capture migration safety, environment handling, and recommended improvements. (`docs/migrations-audit.md`)
