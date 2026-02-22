# Agent instructions (atlas-api)

For AI assistants (Cursor, Codex, etc.) working in this repo:

- **PRs and CI:** Run the release gate before suggesting a PR: `cd atlas-e2e; npm run release-gate` (see [CONTRIBUTING.md](CONTRIBUTING.md)). The CI and Deploy to Dev workflow (`.github/workflows/ci-deploy-dev.yml`) must pass before merge. Gate definition: [atlas-e2e/docs/delivery/DEVSECOPS-GATES-BASELINE.md](../atlas-e2e/docs/delivery/DEVSECOPS-GATES-BASELINE.md).
- **Feature work:** To implement the next high-value feature, use [ATLAS-HIGH-VALUE-BACKLOG.md](../atlas-e2e/docs/product/ATLAS-HIGH-VALUE-BACKLOG.md) and [ATLAS-FEATURE-EXECUTION-PROMPT.md](../atlas-e2e/docs/product/ATLAS-FEATURE-EXECUTION-PROMPT.md). Phase 1 = run release gate; do not duplicate stability commands.
- **Docs:** Keep [docs/api-contract.md](docs/api-contract.md) and [docs/db-schema.md](docs/db-schema.md) in sync when changing controllers, DTOs, or migrations. See [docs/README.md](docs/README.md) for the full doc index. Eventing: [docs/eventing-servicebus-implementation-plan.md](docs/eventing-servicebus-implementation-plan.md). Migrations: update [docs/db-schema.md](docs/db-schema.md) when adding/altering tables.
