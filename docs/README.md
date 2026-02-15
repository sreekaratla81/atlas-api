# Atlas API Documentation

This folder is the single source of truth for API, schema, and operations. **These docs are kept in sync for use as context in AI tools (e.g. ChatGPT, Cursor).** When you add or change controllers, DTOs, or migrations, update the relevant doc.

## API Docs
- [API Contract](api-contract.md) — endpoints, request/response shapes, tenant resolution
- [API Examples](api-examples.http) — runnable HTTP examples (REST Client / IDE)
- [DB Schema](db-schema.md) — tables, columns, FKs, indexes (aligned with AppDbContext)

## Runbooks & Ops
- [Quoted pricing runbook](quoted-pricing-runbook.md)
- [Migrations troubleshooting](migrations-troubleshooting.md), [Migrations audit](migrations-audit.md)
- [Startup diagnostics](startup-diagnostics.md), [Startup failure report](startup-failure-report.md)
- [CI/CD branch mapping](ci-cd-branch-mapping.md)

## Testing Docs
- [Testing Guide](testing-guide.md)
- [Endpoint Coverage Matrix](testing/endpoint-coverage.md)

## Update Rule
Any controller or DTO change requires a documentation update in this folder.
