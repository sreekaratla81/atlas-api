# API testing before deploy

This doc explains how to ensure the API is safe to deploy and that **UI dependencies** (the way the guest portal and admin portal call the API) are validated.

## The gap

- **Unit tests** (Atlas.Api.Tests, Atlas.DbMigrator.Tests): Fast, no SQL. They do **not** run the real host, so DI/startup issues (e.g. middleware mis-registration) are not caught.
- **Integration tests** (Atlas.Api.IntegrationTests): Run the full host and DB (LocalDb). Many tests do **not** send `X-Tenant-Slug` because in the `IntegrationTest` environment the API allows null tenant. In production, the API requires tenant resolution; if the UIs send the header but tests never do, we can ship broken tenant behavior.
- **UI contract tests** (same project, `[Trait("Suite", "UIContract")]`): Call the API **exactly as the UIs do** — same headers (`X-Tenant-Slug: atlas`), same routes, same payload shapes. They live in `GuestPortalContractTests.cs` and `AdminPortalContractTests.cs`.

## What each layer validates

| Layer | What it catches |
| --- | --- |
| Unit tests | Logic, mocks, no host. |
| Integration tests | Host builds, DB + migrations, endpoint behavior with test data. |
| UI contract tests | Same requests as guest/admin portals; tenant header; no "Tenant could not be resolved" in responses. |

Running **integration tests** (which include UI contract tests) before deploy ensures:

1. **Host builds** — same DI/startup path as when you run the API locally or in Azure. A bad middleware registration fails here.
2. **UI-critical flows** — GET listing by id, POST Razorpay order, GET/PUT admin calendar, GET reports, all with `X-Tenant-Slug`, so production behavior matches.

## Commands

**Unit only (fast, no SQL):**

```bash
dotnet test ./Atlas.Api.Tests/Atlas.Api.Tests.csproj -c Release
dotnet test ./Atlas.DbMigrator.Tests/Atlas.DbMigrator.Tests.csproj -c Release
```

**Full API validation (host + DB + UI contract) — run before deploying to any env:**

```bash
dotnet build -c Release
dotnet test ./Atlas.Api.IntegrationTests/Atlas.Api.IntegrationTests.csproj -c Release --no-build
```

Requires SQL Server LocalDb (or set `Atlas_TestDb` to a test connection string). This runs all integration tests, including:

- `GuestPortalContractTests` — listing by id, listings/public, Razorpay order (guest portal flows).
- `AdminPortalContractTests` — calendar availability, reports/bookings, calendar bulk upsert (admin portal flows).

**Run only UI contract tests:**

```bash
dotnet test ./Atlas.Api.IntegrationTests/Atlas.Api.IntegrationTests.csproj -c Release --filter "Suite=UIContract"
```

## CI

- **CI and Deploy to Dev** (`.github/workflows/ci-deploy-dev.yml`): It runs **unit and integration tests** (including UI contract). To catch host/UI issues before merge, run integration tests in the gate (they need LocalDb, which is available on `windows-latest`). See the workflow and DEVSECOPS-GATES-BASELINE for the exact commands.
- **Deploy**: Run integration tests (and optionally UI contract tests) before deploy so the same host and UI flows are validated; then deploy.

## Adding new UI-mirror tests

When the guest or admin portal adds a new critical call:

1. Add a test in `GuestPortalContractTests.cs` or `AdminPortalContractTests.cs`.
2. Use `X-Tenant-Slug: atlas` (and any other headers the UI sends).
3. Use the same route and payload shape as the UI.
4. Assert success or a known error; assert the response does not contain "Tenant could not be resolved".

This keeps the API contract aligned with the UIs and catches deploy-time failures before they hit production.
