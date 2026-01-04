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
4. Exposes the connection string through the `DEFAULT_CONNECTION` environment variable so the application factory can use it.

When the test run completes, the fixture drops the database unless told to retain it.

- `ATLAS_TEST_KEEP_DB=true` skips the drop step so you can inspect the schema or data after the run.

> Note: Parallel test execution is disabled to protect the shared database. Each test class runs sequentially inside the `IntegrationTests` collection.
