# Testing Overview

This repository organizes automated tests into suites to keep feedback focused. Use the `Suite` trait when adding tests so they can be filtered in CI and locally.

## Suites and commands

- **Unit** (Atlas.Api.Tests): fast tests that isolate domain logic, services, and helper classes. Run the full suite:

  ```bash
  dotnet test Atlas.Api.Tests/Atlas.Api.Tests.csproj --filter "Suite=Unit"
  ```

- **Smoke** (Atlas.Api.IntegrationTests): thin, happy-path integration checks to validate the application can start, hit key endpoints, and return basic data. Run with:

  ```bash
  dotnet test Atlas.Api.IntegrationTests/Atlas.Api.IntegrationTests.csproj --filter "Suite=Smoke"
  ```

- **Contract** (Atlas.Api.IntegrationTests): higher-fidelity tests that assert request/response contracts and regression coverage for complex flows. Execute with:

  ```bash
  dotnet test Atlas.Api.IntegrationTests/Atlas.Api.IntegrationTests.csproj --filter "Suite=Contract"
  ```

If you need to target multiple suites at once, combine filters, for example to exclude slower contract checks:

```bash
dotnet test Atlas.Api.IntegrationTests/Atlas.Api.IntegrationTests.csproj --filter "Suite!=Contract"
```

> **Note:** Existing tests will be retrofitted with `Suite` traits; add them to any new tests you author so filtering remains reliable.

## Routing helper and `/api` path base

Non-production builds apply `UsePathBase("/api")`, so local and test URLs start with `/api`, while production routes do not. Avoid hard-coding `api/` into controller routes; use the `ApiRoute` helper when you need to build URLs in tests or clients so the base path is applied only once. Controllers that include `api/` in their `[Route]` attributes (for example `UsersController` and `IncidentsController`) must be called as `/api/api/...` locally; using the helper prevents accidental double prefixes.

- Path base configuration: see `Atlas.Api/Program.cs`.
- Example of a controller that already embeds `api/`: see `Atlas.Api/Controllers/UsersController.cs`.

## Controller coverage gate (integration tests only)

Controller coverage is enforced via **integration tests only**. Unit tests do not participate in this gate.

To keep deployment fast, coverage enforcement runs in a dedicated CI workflow (`.github/workflows/controller-coverage.yml`) and is not part of the deployment workflow.

Run locally:

```bash
dotnet test Atlas.Api.IntegrationTests/Atlas.Api.IntegrationTests.csproj --configuration Release --collect:"XPlat Code Coverage" --settings tests/coverage/coverage.controllers.runsettings
```

The controller coverage workflow parses the generated `coverage.cobertura.xml` report and fails if controller coverage is below:

- Line coverage: **95%**
- Branch coverage: **95%**

Teams should still aim for 100% controller coverage where practical.

When adding a new endpoint:

1. Add/extend integration tests that hit every controller action path (success + validation + not-found/invalid state branches).
2. Update `docs/endpoint-coverage-matrix.md` with the new endpoint scenarios.
3. Re-run the controller coverage command above before pushing.

Migrations remain excluded from coverage calculations.

- Endpoint coverage matrix: [docs/endpoint-coverage-matrix.md](../endpoint-coverage-matrix.md)
