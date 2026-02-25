# Testing Guide

## Integration test database lifecycle

Integration tests share a single SQL Server database for the duration of a test run. The database name is derived from a per-run identifier so test executions do not collide:

- `ATLAS_TEST_RUN_ID` (optional): when set, this value is used verbatim as the run identifier.
- If the variable is not set, a UTC timestamp (`yyyyMMddHHmmss`) is generated at startup.
- The shared database is named `AtlasHomestays_TestDb_{TestRunId}`.

During test startup, the `SqlServerTestDatabase` fixture:

1. Builds a connection string against `(localdb)\MSSQLLocalDB` using the shared database name.
2. Creates the database if it does not exist.
3. Applies Entity Framework Core migrations.
4. Exposes the connection string through the `ConnectionStrings__DefaultConnection` environment variable so the application factory can use it.
5. Validates the connection string to ensure it targets LocalDb and a test database name prefix.

When the test run completes, the fixture drops the database unless told to retain it.

- `ATLAS_TEST_KEEP_DB=true` skips the drop step so you can inspect the schema or data after the run.

> Note: Parallel test execution is disabled to protect the shared database. Each test class runs sequentially inside the `IntegrationTests` collection.

### Database reset strategy

Integration tests use the core `Respawn` package with `DbAdapter.SqlServer` to wipe data between tests. During first use, the respawner migrates the database, scopes to the `dbo` schema, and ignores the `__EFMigrationsHistory` table. Before each test, the respawner truncates the included tables and reseeds a minimal baseline record so every scenario starts from the same state.

The `IntegrationTestBase` fixture runs the respawner before every test to guarantee isolation. After each reset, baseline seed data is applied: a default tenant (Id=1, Slug=atlas, Status=active), an environment marker, and a default property when empty. The default tenant is required for tenant resolution and for SaveChanges auto-population of TenantId. Tests must not depend on execution order or shared state and should assume the database is clean (aside from baseline seed data) at the start of each test.

### Running integration tests locally

1. Ensure you have access to SQL Server; by default the suite uses `(localdb)\MSSQLLocalDB`. If you set `Atlas_TestDb`, the connection string must target LocalDb and use a database name that starts with `AtlasHomestays_TestDb_` (for example, `AtlasHomestays_TestDb_202401010101`). This guard prevents accidentally pointing tests at production data.
2. (Optional) Set `ATLAS_TEST_RUN_ID` to label the test database. This is helpful when running multiple suites in parallel across machines.
3. (Optional) Set `ATLAS_TEST_KEEP_DB=true` to keep the database after the run for inspection.
4. (Optional) Set `ATLAS_ALLOW_NON_LOCALDB_TESTS=true` only when you deliberately need to target a non-LocalDb instance for tests.
5. Execute the integration tests, for example:

   ```bash
   dotnet test Atlas.Api.IntegrationTests/Atlas.Api.IntegrationTests.csproj
   ```
