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
provide a connection string via the `Atlas_TestDb` environment variable.

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
dotnet run --project Atlas.DbMigrator -- --connection "${ATLAS_DB_CONNECTION}" --check-only

# apply migrations
dotnet run --project Atlas.DbMigrator -- --connection "${ATLAS_DB_CONNECTION}"
```

Set `ATLAS_DB_CONNECTION` (or another env var) using your secret manager, GitHub
Actions secrets, or Azure App Service configuration. Logs are redacted and
should not include connection string secrets.

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

Production deploys are automated through the GitHub Actions workflow at
`.github/workflows/deploy.yml`.

- **Triggers:** Pushes to `main` automatically build, test, publish, and deploy
  to Azure App Service. You can also trigger a manual run from the GitHub UI
  via **Actions → Deploy .NET API to Azure → Run workflow**.
- **Required secret:** `AZURE_WEBAPP_PUBLISH_PROFILE` (App Service publish
  profile XML from Azure Portal → App Service **atlas-homes-api** → Get publish
  profile → paste into repository secret).
- **Required DB secrets:** `ATLAS_DEV_SQL_CONNECTION_STRING` and
  `ATLAS_PROD_SQL_CONNECTION_STRING` (environment-specific SQL Server connection
  strings used by `Atlas.DbMigrator`; keep these values in GitHub Secrets or App
  Service configuration and never log them).
- **What it does:** Checks out code, installs .NET 8 SDK, restores packages,
  builds Release, runs unit + integration tests, checks/applies database
  migrations via `Atlas.DbMigrator`, publishes to `./publish`, and deploys using
  the publish profile.
- **Troubleshooting:** YAML indentation errors or malformed keys will cause the
  workflow to be rejected by GitHub. Verify the workflow YAML structure matches
  the example in `.github/workflows/deploy.yml`. Ensure the publish profile
  secret is present and valid if deployment fails.

### CI/CD troubleshooting

- The deploy workflow lets `dotnet test` build the test project (no `--no-build`)
  because disabling the build step can cause VSTest to treat the test DLL as an
  invalid argument on GitHub-hosted runners.
- The deploy pipeline runs unit and integration tests; if LocalDb is unavailable
  on the runner, update the workflow to use a compatible SQL Server target.
