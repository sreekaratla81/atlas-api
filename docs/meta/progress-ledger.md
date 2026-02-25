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

**Full release gate (run before committing)** — runs the same steps as the Docs Guardrails workflow (link check, Mermaid, OpenAPI generate + diff, markdown lint, spell check):

```bash
node ./scripts/docs/run-release-gate.mjs
```

Link check and Mermaid only (faster, but does not run markdown lint or spell check):

```bash
node ./scripts/docs/guardrails.mjs
```

## OpenAPI artifact (repo-local, enforced in CI)

Regenerate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\generate-openapi.ps1
```

Optional (lint/spell a subset of files manually):

```bash
npx --yes markdownlint-cli2 "docs/**/*.md" "README.md"
npx --yes cspell lint -c ./cspell.json "docs/**/*.md" "README.md"
```

**Why CI failed but local didn’t:** `guardrails.mjs` only checks links and Mermaid. Markdown lint and spell check run as separate workflow steps in CI. Use `run-release-gate.mjs` locally to run the full gate before pushing.
