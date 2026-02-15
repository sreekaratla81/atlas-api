# Agent instructions (atlas-api)

For AI assistants (Cursor, Codex, etc.) working in this repo:

- **PRs and gates:** Run the gate locally before suggesting a PR (see [CONTRIBUTING.md](CONTRIBUTING.md)). The Gate workflow (`.github/workflows/gate.yml`) must pass before merge.
- **Feature work:** To implement the next high-value feature, use [docs/ATLAS-HIGH-VALUE-BACKLOG.md](docs/ATLAS-HIGH-VALUE-BACKLOG.md) and [docs/ATLAS-FEATURE-EXECUTION-PROMPT.md](docs/ATLAS-FEATURE-EXECUTION-PROMPT.md). Phase 1 references the workspace sanity runbook; do not duplicate stability commands.
- **Docs:** Keep [docs/api-contract.md](docs/api-contract.md) and [docs/db-schema.md](docs/db-schema.md) in sync when changing controllers, DTOs, or migrations. See [docs/README.md](docs/README.md) for the full doc index.
