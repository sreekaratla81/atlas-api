# atlas-api

.NET 8 backend that powers Atlas Homestays frontend apps and integrates with Azure SQL, Azure Service Bus, Razorpay, and MSG91.

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

## Clone (workspace setup)

From the directory where you want to store all repos:

```bash
git clone https://github.com/sreekaratla81/atlas-guest-portal.git
git clone https://github.com/sreekaratla81/atlas-admin-portal.git
git clone https://github.com/sreekaratla81/atlas-api.git
git clone https://github.com/sreekaratla81/atlas-staff-app.git
git clone https://github.com/sreekaratla81/atlas-sql.git
git clone https://github.com/sreekaratla81/atlas-shared-utils.git
```

## NuGet packages (atlas-api)

The repo includes a `NuGet.config` that pins the global packages folder to
`./.nuget/packages`. This keeps restore paths ASCII-only, which avoids Visual
Studio errors about package paths containing unexpected characters (e.g. when
your Windows username includes accents). If you see missing package errors,
delete the `.nuget/packages` folder and restore again.

## Test Fix Summary

- **BookingsController.GetAll** now accepts optional `listingId` and `bookingId` query parameters (API gap implementation). Unit tests in `Atlas.Api.Tests/BookingsControllerTests.cs` were updated to pass the new optional parameters (`null`, `null`) so all tests remain green.

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
See [docs/migrations-troubleshooting.md](docs/migrations-troubleshooting.md) for common migration issues.

Logs are redacted and should not include connection string secrets.

## Runtime configuration

Configure production runtime settings in Azure App Service and GitHub Actions:

- **Azure App Service → Connection strings:** add `DefaultConnection` with type
  `SQLAzure` (or `SQLServer` for non-Azure SQL Server targets) and the expected
  database connection string.
- **Azure App Service → Application settings:** set `Jwt__Key` for JWT signing;
  `Smtp__FromEmail`, `Smtp__Username`, `Smtp__Password`; `Msg91__AuthKey`, `Msg91__SenderId` for SMS;
  `AzureServiceBus__ConnectionString` for eventing; and any other required settings (Auth0/client IDs, tenant settings).
- **GitHub Actions secrets:** add `ATLAS_DEV_SQL_CONNECTION_STRING` and
  `ATLAS_PROD_SQL_CONNECTION_STRING` for CI/CD and migration workflows.

Reminder: never commit connection strings, JWT keys, or `.env` files to the
repository—use secret managers or platform configuration instead.

- **Health:** `GET /health` returns 200 with `{ "status": "healthy" }` for liveness (e.g. load balancer or Azure App Service). See `docs/api-contract.md`.
- **Azure Service Bus (eventing):** Set `AzureServiceBus__ConnectionString` in App Service or environment. Topics: `booking.events`, `stay.events`. With empty connection string, event bus uses in-memory publisher (suitable for local dev).

## CI validation

The **CI and Deploy to Dev** workflow (`ci-deploy-dev.yml`) runs restore → build (Release) → unit tests → **integration tests** (including UI contract tests). Integration tests use LocalDb on `windows-latest` and validate host startup and the same flows the guest/admin portals use (see `docs/API-TESTING-BEFORE-DEPLOY.md`).

Run locally before opening a PR: unit tests as in CONTRIBUTING; for full validation before deploy, run integration tests too (`dotnet test ./Atlas.Api.IntegrationTests/Atlas.Api.IntegrationTests.csproj -c Release`). See `CONTRIBUTING.md` and `docs/DEVSECOPS-GATES-BASELINE.md`.

## Documentation

See `docs/README.md` for the full doc index. Key files:

- **AGENTS.md** — Instructions for AI assistants (gate, feature backlog, docs sync).
- **docs/api-contract.md** — Endpoint reference, request/response shapes, tenant resolution.
- **docs/api-examples.http** — Runnable HTTP examples (REST Client / IDE).
- **docs/db-schema.md** — Tables, columns, FKs (aligned with AppDbContext).
- **CONTRIBUTING.md** — PR checklist and gate commands.
- **docs/DEVSECOPS-GATES-BASELINE.md** — CI/gate definition per repo, verify in CI, branch protection.
- **docs/API-TESTING-BEFORE-DEPLOY.md** — Unit vs integration vs UI contract tests; run integration tests before deploy.
- **docs/ci-cd-branch-mapping.md** — Branch → workflow → app mapping and secrets.
- **docs/ATLAS-HIGH-VALUE-BACKLOG.md** — Prioritized feature roadmap and current implementation status.
- **docs/ATLAS-FEATURE-EXECUTION-PROMPT.md** — Workflow for implementing the next feature from the backlog.
- **docs/eventing-servicebus-implementation-plan.md** — Azure Service Bus eventing architecture and implementation notes.
- **Swagger UI** — Available at `/swagger` when not running in Production (see `docs/api-contract.md`).
- **CHANGELOG.md** — Version history and notable changes.
- **SECURITY.md** — Vulnerability reporting.

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
`.github/workflows/ci-deploy-dev.yml` (dev) and `.github/workflows/deploy-prod.yml` (prod).

- **Triggers:** **Prod** — `deploy-prod.yml` runs on push to `main` (when `DEPLOY_PROD_ON_MAIN` is set) or via **workflow_dispatch** (choose prod). **Dev** — `ci-deploy-dev.yml` runs on push to `dev` (ci job then deploy-dev job; one build, deploy only after tests pass); it can also be run via **workflow_dispatch**. See `docs/ci-cd-branch-mapping.md` for branch → workflow → app mapping.
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
| `.github/workflows/ci-deploy-dev.yml` (deploy-dev job) | `ATLAS_DEV_SQL_CONNECTION_STRING`, `AZURE_CLIENT_ID_DEV`, `AZURE_TENANT_ID_DEV`, `AZURE_SUBSCRIPTION_ID_DEV` |
| `.github/workflows/deploy-prod.yml` | `ATLAS_DEV_SQL_CONNECTION_STRING`, `ATLAS_PROD_SQL_CONNECTION_STRING`, `AZURE_CLIENT_ID_DEV`, `AZURE_TENANT_ID_DEV`, `AZURE_SUBSCRIPTION_ID_DEV`, `AZURE_CLIENT_ID_PROD`, `AZURE_TENANT_ID_PROD`, `AZURE_SUBSCRIPTION_ID_PROD` |

The `AZURE_CLIENT_ID_*`, `AZURE_TENANT_ID_*`, and `AZURE_SUBSCRIPTION_ID_*`
patterns map to `*_DEV` and `*_PROD` variables above. Set `Smtp__*`, `Msg91__*`, and
`AzureServiceBus__ConnectionString` in **Azure App Service → Application settings** (or Key Vault references) for email, SMS, and eventing.

### Validate deploy artifacts locally

To catch missing publish outputs before pushing (and avoid deploy-dev failures in CI), run the same validation the **ci** job uses:

```powershell
dotnet publish ./Atlas.Api/Atlas.Api.csproj -c Release -o ./publish -r win-x64 --self-contained true
./scripts/validate-publish.ps1 -PublishPath ./publish
```

If any required file is missing, the script exits non-zero and lists what’s missing. Note: the deploy-dev job downloads the artifact on the runner where layout can be `./net-app` or `./net-app/publish`; the workflow now resolves this automatically. Local validation only checks `./publish` (same as the ci job), so it would not have caught a “path not found” that was due to layout differences after download—the workflow fix handles both layouts.

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

## Troubleshooting

- **LocalDb not found** — Ensure SQL Server LocalDb is installed. Integration tests require it. Set `ATLAS_TestDb` if using a different connection string.
- **Migration check fails in CI** — Verify `ATLAS_DEV_SQL_CONNECTION_STRING` (and prod if applicable) are set in GitHub Actions secrets.
- **Swagger not loading** — Swagger is disabled in Production; use Development or Staging.
