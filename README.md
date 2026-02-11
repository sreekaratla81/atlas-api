# atlas-api
.NET Core backend that powers all frontend apps, integrates with Azure SQL

## Controller conventions

Controllers should prefer the built-in helpers like `Ok()`, `BadRequest()`, and
`NotFound()` over returning raw `ObjectResult` instances. This keeps responses
consistent and leverages ASP.NET Core defaults for status codes and content
negotiation.

When returning a 200 payload, call `Ok(...)` instead of constructing an
`ObjectResult`/`StatusCode(200, ...)` manually so responses resolve to
`OkObjectResult` in both runtime and tests.

## Unit test conventions

- Use `Ok(...)` for 200 responses that return payloads so tests can assert
  `OkObjectResult`.
- Use `BadRequest(...)` for 400 responses to keep contract expectations aligned
  with `BadRequestObjectResult`.
- Avoid returning raw `ObjectResult` unless you explicitly set `StatusCode` for
  non-standard responses.

# Go to the directory where you want to store all repos
cd ~/Projects/AtlasHomestays  # or any preferred location

# Clone each repository
git clone https://github.com/sreekaratla81/atlas-guest-portal.git
git clone https://github.com/sreekaratla81/atlas-admin-portal.git
git clone https://github.com/sreekaratla81/atlas-api.git
git clone https://github.com/sreekaratla81/atlas-staff-app.git
git clone https://github.com/sreekaratla81/atlas-sql.git
git clone https://github.com/sreekaratla81/atlas-shared-utils.git

## NuGet packages

The repo includes a `NuGet.config` that pins the global packages folder to
`./.nuget/packages`. This keeps restore paths ASCII-only, which avoids Visual
Studio errors about package paths containing unexpected characters (e.g. when
your Windows username includes accents). If you see missing package errors,
delete the `.nuget/packages` folder and restore again.

## Running Integration Tests

Integration tests automatically detect and apply any pending EF Core migrations
at runtime. You don't need to run `dotnet ef database update` before testing.

Integration tests require SQL Server LocalDb. Ensure LocalDb is available or
provide a connection string via the `Atlas_TestDb` environment variable. The
connection string must target LocalDb and use a database name starting with
`AtlasHomestays_TestDb_`; the test harness enforces this to avoid accidental
production usage. Set `ATLAS_ALLOW_NON_LOCALDB_TESTS=true` only if you
deliberately need to override the guard.

## How to run migrations (local/dev/prod)

Use the `Atlas.DbMigrator` console app to list or apply EF Core migrations. The
app redacts connection details in logs and never prints credentials. When
`--check-only` finds pending migrations it prints only the migration names and
exits with code `2`.

The API does not apply schema migrations on startup in any environment. Use the
DbMigrator as the single authoritative path for schema updates; integration
tests handle migrations through their own test fixtures.

**Local (LocalDb, no secrets):**

```bash
dotnet run --project Atlas.DbMigrator -- --connection "Server=(localdb)\\MSSQLLocalDB;Database=AtlasHomestays_Local;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True"
```

**Dev/Prod (use secrets or environment variables):**

```bash
# check-only (fails with exit code 2 if pending migrations exist)
dotnet run --project Atlas.DbMigrator -- --connection "${ATLAS_DEV_SQL_CONNECTION_STRING}" --check-only

# apply migrations
dotnet run --project Atlas.DbMigrator -- --connection "${ATLAS_DEV_SQL_CONNECTION_STRING}"
```

**Visual Studio launch profile (Windows):**

If you set `ATLAS_DEV_SQL_CONNECTION_STRING` in the launch profile environment variables,
pass `%ATLAS_DEV_SQL_CONNECTION_STRING%` in the command line arguments so the migrator
expands it at runtime.

```
--connection "%ATLAS_DEV_SQL_CONNECTION_STRING%" --check-only
```

Set `ATLAS_DEV_SQL_CONNECTION_STRING` and `ATLAS_PROD_SQL_CONNECTION_STRING` via
your secret manager, GitHub Actions secrets, or Azure App Service configuration.
Logs are redacted and should not include connection string secrets.

## Runtime configuration

Configure production runtime settings in Azure App Service and GitHub Actions:

- **Azure App Service → Connection strings:** add `DefaultConnection` with type
  `SQLAzure` (or `SQLServer` for non-Azure SQL Server targets) and the expected
  database connection string.
- **Azure App Service → Application settings:** set `Jwt__Key` for JWT signing
  and any other required application settings (for example, Auth0/client IDs or
  tenant settings used by the API).
- **GitHub Actions secrets:** add `ATLAS_DEV_SQL_CONNECTION_STRING` and
  `ATLAS_PROD_SQL_CONNECTION_STRING` for CI/CD and migration workflows.

Reminder: never commit connection strings, JWT keys, or `.env` files to the
repository—use secret managers or platform configuration instead.

## CI validation

CI runs expect `dotnet test ./Atlas.Api.Tests/Atlas.Api.Tests.csproj -c Release`
to pass. Make sure this command succeeds locally before opening a pull
request.

## CORS allowlist

Admin surfaces rely on an explicit CORS allowlist defined in `Program.cs` via
the `AtlasCorsPolicy` policy. By default, we allow:

- `http://localhost:5173`
- `http://127.0.0.1:5173` (development only)
- `https://admin.atlashomestays.com`
- `https://devadmin.atlashomestays.com`
- `https://www.atlashomestays.com`
- `https://*.pages.dev` (for Cloudflare Pages previews)

To add a new admin domain, supply a `Cors:AdditionalOrigins` array entry in
configuration (appsettings, environment variables, or App Service settings).
The API merges these entries into the allowlist while de-duplicating values.

⚠️ **Do not use `AllowAnyOrigin` in production.** Doing so permits credentialed
requests from untrusted sites and will break cookie-based Auth0 flows. Always
add explicit domains to the allowlist instead.

## Deployment

Production deploys are automated through the GitHub Actions workflows at
`.github/workflows/deploy.yml` and `.github/workflows/dev_atlas-homes-api-dev.yml`.

- **Triggers:** Pushes to `main` (prod) and `dev` (dev) automatically build,
  test, publish, and deploy to Azure App Service. You can also trigger manual
  runs from the GitHub UI.
- **Migration/deploy flow:** Both workflows validate DbMigrator connection
  secrets and run a migration check gate. Dev deploy paths can apply pending
  migrations before deployment. Production migration application is gated to
  explicit manual flow.
- **What it does:** Checks out code, installs .NET 8 SDK, restores packages,
  builds Release, runs test suites, validates migration state, and deploys to
  the environment-specific Azure Web App.

### Required secrets by workflow

| Workflow file | Required/used secret identifiers (exact names from workflow YAML) |
| --- | --- |
| `.github/workflows/dev_atlas-homes-api-dev.yml` | `ATLAS_DEV_SQL_CONNECTION_STRING`, `AZUREAPPSERVICE_CLIENTID_549666B25F124F47A8A02ABB67C651ED`, `AZUREAPPSERVICE_TENANTID_B891A9E8DB8C42D095F9439D8E364707`, `AZUREAPPSERVICE_SUBSCRIPTIONID_21B2EDBA7F42470F91A861E168D2DAC9` |
| `.github/workflows/deploy.yml` | `AZURE_WEBAPP_PUBLISH_PROFILE_DEV`, `AZURE_WEBAPP_PUBLISH_PROFILE_PROD`, `ATLAS_DEV_SQL_CONNECTION_STRING`, `ATLAS_PROD_SQL_CONNECTION_STRING`, `AZURE_CLIENT_ID_DEV`, `AZURE_TENANT_ID_DEV`, `AZURE_SUBSCRIPTION_ID_DEV`, `AZURE_CLIENT_ID_PROD`, `AZURE_TENANT_ID_PROD`, `AZURE_SUBSCRIPTION_ID_PROD`, `AZUREAPPSERVICE_CLIENTID_549666B25F124F47A8A02ABB67C651ED`, `AZUREAPPSERVICE_TENANTID_B891A9E8DB8C42D095F9439D8E364707`, `AZUREAPPSERVICE_SUBSCRIPTIONID_21B2EDBA7F42470F91A861E168D2DAC9` |

The `AZURE_CLIENT_ID_*`, `AZURE_TENANT_ID_*`, and `AZURE_SUBSCRIPTION_ID_*`
patterns map to `*_DEV` and `*_PROD` variables above.

### CI/CD troubleshooting

- The deploy workflow lets `dotnet test` build the test project (no `--no-build`)
  because disabling the build step can cause VSTest to treat the test DLL as an
  invalid argument on GitHub-hosted runners.
- The deploy pipeline runs unit and integration tests; if LocalDb is unavailable
  on the runner, update the workflow to use a compatible SQL Server target.
- If you see `Missing value for --connection`, verify the GitHub Actions
  secrets exist (`ATLAS_DEV_SQL_CONNECTION_STRING` and
  `ATLAS_PROD_SQL_CONNECTION_STRING`) and that the workflow passes the correct
  value into the DbMigrator step as an environment variable or argument.

## Unit test coverage workflow

Unit-test coverage is tracked in CI using `.github/workflows/coverage-unit.yml`.

- Coverage collection runs only against `Atlas.Api.Tests` so DB-heavy integration setup does not dilute unit-test metrics.
- Reports are generated from `coverage.cobertura.xml` and uploaded as workflow artifacts (Cobertura XML + HTML report).
- The target line coverage is **90%+**, but the threshold step is **non-blocking** (`continue-on-error: true`) so failed coverage emits a warning without blocking merges.

### Run coverage locally

```bash
./tools/coverage/run.sh
```

This script runs unit tests with `--collect:"XPlat Code Coverage"`, writes coverage output to `Atlas.Api.Tests/TestResults/**/coverage.cobertura.xml`, and generates HTML + summary output under `tools/coverage/report/`.

### Coverage include/exclude policy

Coverage collection is focused on maintainable business logic. We exclude:

- `**/Migrations/*` (generated schema artifacts)
- `**/*.g.cs` and `**/*.Designer.cs` (generated files)
- `**/Program.cs` (startup wiring noise)
- `**/obj/**` (build output)

These defaults are centralized in `Directory.Build.props` and applied in `coverage.runsettings`.

### Optional strict check before release

Use `.github/workflows/unitests-coverage-strict.yml` (`workflow_dispatch`) when you want enforced coverage checks. This manual workflow fails if line coverage is below 90%, which is useful as a release-readiness gate.
