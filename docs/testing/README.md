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

## Coverage expectations
Keep the endpoint coverage matrix up to date when you add or change API surface area. Domain logic, services, and helper classes should be the focus of coverage. Controllers and EF migrations are excluded from the target calculations (controllers are covered indirectly via contract tests; migrations are explicitly excluded from reports).

- Endpoint coverage matrix: [docs/endpoint-coverage-matrix.md](../endpoint-coverage-matrix.md)
