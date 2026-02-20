# ATLAS – Feature Execution (High-Value Backlog)

*(Versioned copy in atlas-api/docs; canonical at workspace root.)*

Use this prompt when implementing the **next feature** from the prioritized backlog. Do **not** re-specify stability commands; use the existing runbook and baseline.

**Prereqs:** `ATLAS-HIGH-VALUE-BACKLOG.md` (this folder or workspace root), `DEVSECOPS-GATES-BASELINE.md`, `DEVSECOPS-WORKSPACE-SANITY-PROMPT.md`.

---

## PHASE 1 – Workspace stability

**Do not duplicate the runbook.** Do one of:

- Run the sanity suite per **DEVSECOPS-WORKSPACE-SANITY-PROMPT.md** (Phase 1: atlas-api restore/build/test, portals install/lint/build/test), or  
- Confirm **Gate/CI is green** per **DEVSECOPS-GATES-BASELINE.md** §5 (Actions tab).

If anything is red: fix minimal issues, re-run until green. Do not introduce new dependencies unnecessarily.

---

## PHASE 2 – Feature selection

Open **ATLAS-HIGH-VALUE-BACKLOG.md**.

- Use the **Current implementation status** table and **Next step** column.
- Choose the Tier 1 (or next) item that: is **not** fully done, has highest ROI vs effort, and has clearest “Next step.”

Output: selected feature, why, and estimated scope (API / Admin / Guest).

---

## PHASE 3 – Design before code

Before implementing:

- Short architecture summary.
- Identify: DB changes? API endpoints? Events? UI? Tests?
- Ensure alignment with multi-tenant SaaS.

---

## PHASE 4 – Implement

- Minimal, clean changes; separation of concerns.
- Unit tests for business logic; integration tests if backend changes.
- Keep portals and API gates green (lint, build, test).

---

## PHASE 5 – Verify & commit

- Re-run: API tests, portal builds (or rely on Gate/CI).
- If green: commit with message e.g. `feat: <feature name> – <short description>`.
- If not green: do not commit; report failure summary.

---

## PHASE 6 – Update backlog

Update **ATLAS-HIGH-VALUE-BACKLOG.md**:

- Set status for the feature (e.g. “Done” or “Partial” with note).
- Adjust “Next step” if more work remains.
- Add a one-line implementation note if useful.

Start with **PHASE 1** (stability per runbook or baseline).
