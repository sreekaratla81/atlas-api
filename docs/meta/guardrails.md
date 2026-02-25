# Docs Guardrails (atlas-api)

**Purpose:** Prevent docs (API contract, schema notes, runbooks) from drifting away from the `atlas-api` implementation.

**Audience:** Developer | Delivery

**Owner:** Atlas Tech Solutions

**Last updated:** 2026-02-22

**Related:** [docs index](../README.md) | [api-contract](../api-contract.md) | [db-schema](../db-schema.md)

---

## Non-negotiable rules

- **Do not delete docs.** Prefer **DEPRECATED** banners + canonical links.
- **Relative links must work** within this repo’s `docs/**`.
- **API changes require contract updates**:
  - Endpoint changes → update `docs/api-contract.md` and regenerate OpenAPI artifact (if enforced).
  - Eventing changes → update `docs/eventing-servicebus-implementation-plan.md` and any referenced canonical docs.

## Freshness rules

- If `Atlas.Api/Controllers/**` changes → review `docs/api-contract.md`.
- If tenancy middleware or tenant scoping changes → review tenancy sections in `docs/api-contract.md` and `docs/ci-cd-branch-mapping.md`.
- If pricing/availability logic changes → review `docs/quoted-pricing-runbook.md` and schema references.

## Guardrails checks

See [progress-ledger](progress-ledger.md) for exact local commands.
