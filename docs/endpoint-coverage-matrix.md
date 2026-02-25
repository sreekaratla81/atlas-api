# Endpoint Coverage Matrix

Track API surface area and the associated automated coverage here. Add a row when you create or substantially change an endpoint.

| Area | Endpoint or feature | Coverage (tests/contracts) | Notes |
| --- | --- | --- | --- |
| Bookings | `/bookings` CRUD and status transitions | Integration contract tests in `BookingsApiTests` | Expand as new states are added |
| Listings | `/listings` CRUD | Integration contract tests in `ListingsApiTests` | |
| Reports | `/reports/*` earnings and calendar views | Integration contract tests in `ReportsApiTests` | |

## Targets and exclusions

- Focus on domain logic, services, and helper classes; these are expected to have strong direct unit coverage.
- Controllers are covered indirectly via contract and smoke suites; they are not counted toward per-file targets.
- Entity Framework migrations are excluded from coverage (also enforced via `coverage.runsettings`).
