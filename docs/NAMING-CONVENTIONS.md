# Naming Conventions

## Workflows (`.github/workflows/`)

### Convention
- **Filenames**: `kebab-case.yml`. Include environment or purpose so the file is self-describing (e.g. `deploy-prod.yml` not just `deploy.yml`).
- **Display name** (`name:` in YAML): Title Case or purpose phrase; should match intent of the filename (e.g. "Gate", "Build and Deploy to Prod", "Lockfile guard").
- **Status check**: Use a short, stable id for branch protection (e.g. `gate`, `build`). Comments in the workflow should state the status check name.

### atlas-api
| File | Display name | Purpose |
|------|--------------|---------|
| `gate.yml` | Gate | CI + dev deploy: restore, build, test; on dev branch publish and deploy to atlas-homes-api-dev. |
| `deploy-prod.yml` | Build and Deploy to Prod | Prod deploy on push to main or workflow_dispatch. |

### Other repos (reference)
- **atlas-admin-portal**: `gate.yml` → "Gate" (CI gate).
- **RatebotaiRepo**: `ci.yml` → "CI"; `lockfile-guard.yml` → "Lockfile guard" (Title Case).
- **sreekaratla**: `ci.yml` → "CI"; status check name in branch protection can remain "build".

---

## Docs (`docs/`)

### Convention
- **General docs**: `kebab-case.md` (e.g. `api-contract.md`, `ci-cd-branch-mapping.md`, `migrations-audit.md`).
- **Standards / baselines**: `UPPER-WITH-DASHES.md` is used for some (e.g. `DEVSECOPS-GATES-BASELINE.md`, `API-TESTING-BEFORE-DEPLOY.md`). Prefer one style per repo; either all kebab-case or reserve UPPER for baselines/standards only.
- **Root-level**: `README.md`, `CONTRIBUTING.md`, `AGENTS.md`, `GOVERNANCE.md` (common uppercase for visibility).

---

## Summary of changes applied
- Workflow `deploy.yml` renamed to `deploy-prod.yml` so the file name reflects prod-only deploy.
- Doc and README references updated to `deploy-prod.yml`.
- RatebotaiRepo: `lockfile-guard.yml` display name set to "Lockfile guard"; added status-check comment.
- sreekaratla: `ci.yml` display name set to "CI" (status check can stay "build" in branch protection).

**Branch protection:** If you had a required status check for the old "Build and Deploy to Prod" workflow, ensure the rule still applies to the new `deploy-prod.yml` run (GitHub typically keys on workflow file path).
