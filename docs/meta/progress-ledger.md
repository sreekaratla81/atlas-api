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

**Why guest 401s (e.g. GET /listings, GET /pricing/breakdown) aren’t caught by integration tests:** Integration tests use `CustomWebApplicationFactory` with `ASPNETCORE_ENVIRONMENT=IntegrationTest`. When the environment is not Production (or when `Jwt__Key` is unset), the API uses a permissive default authorization policy, so `[Authorize]` does not require a JWT and unauthenticated requests succeed. On dev/prod, JWT is enabled and those same requests return 401 until the endpoint has `[AllowAnonymous]`. To catch guest 401s locally, run E2E (or a manual curl) against a host that has JWT enabled (e.g. dev API URL with `Jwt__Key` set).
