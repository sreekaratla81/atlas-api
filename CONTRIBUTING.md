# Contributing

## Before you open a PR

1. **Run the release gate** (the single pre-commit gate for all repos):
   ```bash
   cd atlas-e2e; npm run release-gate
   ```
   This validates atlas-api build + unit/integration tests, portal lint + tests + builds, migrations, smoke curls, and E2E. See [atlas-e2e/docs/PROD_READINESS_CHECKLIST.md](../atlas-e2e/docs/PROD_READINESS_CHECKLIST.md) for the full 16-gate DevSecOps mapping.

2. **Open a PR** to `main` or `dev`.

3. **Ensure the CI workflow passes** — `.github/workflows/ci-deploy-dev.yml` runs the same checks on push/PR; it must pass before merge.

### Quick reference: atlas-api only (for troubleshooting individual failures)

```bash
dotnet restore
dotnet build -c Release --no-incremental
dotnet test -c Release --no-build
```

For deployment and secrets, see [README → Deployment](README.md#deployment) and `docs/ci-cd-branch-mapping.md`. To pick and implement the next high-value feature, see [atlas-e2e/docs/product/ATLAS-HIGH-VALUE-BACKLOG.md](../atlas-e2e/docs/product/ATLAS-HIGH-VALUE-BACKLOG.md) and [ATLAS-FEATURE-EXECUTION-PROMPT.md](../atlas-e2e/docs/product/ATLAS-FEATURE-EXECUTION-PROMPT.md). **For AI agents:** see [AGENTS.md](AGENTS.md) for gate and feature-backlog pointers.
