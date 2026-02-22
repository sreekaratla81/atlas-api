# Docs Guardrails — Progress Ledger (atlas-api)

**Purpose:** Track docs-as-code guardrails and how to run them locally.

**Audience:** Developer

**Owner:** Atlas Tech Solutions

**Last updated:** 2026-02-22

**Related:** [guardrails](guardrails.md) | [docs index](../README.md)

---

## Current guardrails

- Workflow: `.github/workflows/docs-guardrails.yml`
- Script: `scripts/docs/guardrails.mjs`

## How to run locally

From `atlas-api/`:

```bash
node ./scripts/docs/guardrails.mjs
```

## OpenAPI artifact (repo-local, enforced in CI)

Regenerate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\generate-openapi.ps1
```

Optional (lint only changed files):

```bash
npx --yes markdownlint-cli2 "docs/**/*.md" "README.md"
npx --yes cspell lint -c ./cspell.json "docs/**/*.md" "README.md"
```

