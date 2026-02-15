# Atlas API Documentation

This folder is the single source of truth for API, schema, and operations. **These docs are kept in sync for use as context in AI tools (e.g. ChatGPT, Cursor).** When you add or change controllers, DTOs, or migrations, update the relevant doc.

## API Docs
- [API Contract](api-contract.md) — endpoints, request/response shapes, tenant resolution; includes `GET /health` (liveness)
- [API Examples](api-examples.http) — runnable HTTP examples (REST Client / IDE)
- [DB Schema](db-schema.md) — tables, columns, FKs, indexes (aligned with AppDbContext)

## Gates & feature roadmap
- [DEVSECOPS-GATES-BASELINE.md](DEVSECOPS-GATES-BASELINE.md) — gate definition, commands per repo, verify in CI, branch protection
- [ATLAS-HIGH-VALUE-BACKLOG.md](ATLAS-HIGH-VALUE-BACKLOG.md) — prioritized feature roadmap and implementation status
- [ATLAS-FEATURE-EXECUTION-PROMPT.md](ATLAS-FEATURE-EXECUTION-PROMPT.md) — workflow for implementing the next feature from the backlog
- [ATLAS-BACKLOG-ANALYSIS.md](ATLAS-BACKLOG-ANALYSIS.md) — how the backlog was derived and what’s already implemented

## Runbooks & Ops
- [API testing before deploy](API-TESTING-BEFORE-DEPLOY.md) — Ensure host and UI-critical flows pass before deploy.
- [Quoted pricing runbook](quoted-pricing-runbook.md)
- [Migrations troubleshooting](migrations-troubleshooting.md), [Migrations audit](migrations-audit.md)
- [Startup diagnostics](startup-diagnostics.md), [Startup failure report](startup-failure-report.md)
- [CI/CD branch mapping](ci-cd-branch-mapping.md)

## Testing Docs
- [Testing Guide](testing-guide.md)
- [Endpoint Coverage Matrix](testing/endpoint-coverage.md)

## Update Rule
Any controller or DTO change requires a documentation update in this folder.

## Using this folder as ChatGPT (or other AI) context
- **Recommended set**: `api-contract.md`, `db-schema.md`, `api-examples.http`, and this `README.md` give the API surface, schema, and examples. Add runbooks (`quoted-pricing-runbook.md`, `migrations-troubleshooting.md`) when the AI needs ops or pricing behavior.
- Keep paths and request/response shapes in sync with the codebase so the model has accurate context.
