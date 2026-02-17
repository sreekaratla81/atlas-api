# DevSecOps Gates Baseline — Workspace Sanity Run

**Versioned copy (in git):** `atlas-api/docs/DEVSECOPS-GATES-BASELINE.md`

**Run date:** 2026-02-17  
**Runbook:** `DEVSECOPS-WORKSPACE-SANITY-PROMPT.md`  
**Versioned copy:** `atlas-api/docs/DEVSECOPS-GATES-BASELINE.md`

> **Update policy:** Re-run `DEVSECOPS-WORKSPACE-SANITY-PROMPT` and update this baseline **before every commit**.

**Quick reference:** §2 = commands per repo; §4 = CI gates (workflow files); §5–6 = verify in Actions, branch protection, re-run before release. **Feature work:** see atlas-api/docs/ATLAS-HIGH-VALUE-BACKLOG.md and ATLAS-FEATURE-EXECUTION-PROMPT.md.

---

## 1. Summary

| Repo                | Status   | Notes |
|---------------------|----------|--------|
| **atlas-api**       | **GREEN** | Build green. Unit tests 111 passed, 2 skipped (obsolete workflow tests). Integration tests require SQL Server/LocalDB. |
| **atlas-admin-portal** | **GREEN** | Lint 1 warning; build ok; **tests** 55 passed (16 files). |
| **RatebotaiRepo**   | **GREEN** | Lint 0 errors, 2 warnings; build ok; tests 160 passed / 32 skipped. |
| **atlas-e2e**       | **GREEN** | Smoke tests 2 passed. Full E2E requires API + SQL Server + Auth0. |

**Overall:** All four repos sanity-ready.

**Re-run 2026-02-17:** atlas-api: EF migration `SyncPendingChanges` added; unit test `Create_PersistsBooking_WhenWorkflowPublisherFails` skipped (obsolete outbox flow). RatebotaiRepo: validate:legal fixed (vitest.legal.config.ts); dom-accessibility-api override; SearchPage unused `index` removed. atlas-e2e: smoke tests pass.

---

## 2. Commands that define “green” per repo

### atlas-api (from `atlas-api/`)

```bash
dotnet restore
dotnet build -c Release --no-incremental
dotnet test -c Release --no-build
```

- **What was run:** All of the above. Build succeeded. Unit: Api.Tests 111 passed / 2 skipped (obsolete workflow tests); DbMigrator.Tests 9 passed. Integration tests require SQL Server/LocalDB; may fail with "Cannot open database" / "Database already exists" if DB unavailable or stale.
- **Note (obsolete):** `Atlas.Api.Tests` was not selected for building in solution configuration `Release|Any CPU` (solution file). The test run executed: **Atlas.DbMigrator.Tests**, **Atlas.DbMigrator.IntegrationTests**, **Atlas.Api.IntegrationTests**. All reported “Test Run Successful.” If you need API unit tests in the gate, add `Atlas.Api.Tests` to the Release build in the solution.

### atlas-admin-portal (from `atlas-admin-portal/`)

```bash
npm ci
npm run lint
npm run build
npx vitest run
```

- **Current status:**  
  - **Lint:** **0 errors**, 1 warning (AvailabilityCalendar exhaustive-deps).  
  - **Build:** **GREEN** — `npm run build` (no-localhost + vite build) exit 0.  
  - **Tests:** **GREEN** — `npx vitest run` 16 files, 55 tests passed.

### RatebotaiRepo (from `RatebotaiRepo/`)

```bash
npm ci
npm run lint
npm run build
npm test
```

- **Current status (2026-02-17):**  
  - **Lint:** **0 errors**, 2 warnings (UnitBookingWidget exhaustive-deps).  
  - **Build:** **GREEN** — `npm run build` exit 0.  
  - **Tests:** **GREEN** — `npm test` 160 passed, 32 skipped. Uses `vitest.legal.config.ts` for validate:legal (no jest-dom); `dom-accessibility-api` override for main suite.

---

## 3. What failed / what was fixed

- **atlas-api:** EF migration `SyncPendingChanges` added (removes obsolete ListingDailyRate FK). Obsolete `Create_PersistsBooking_WhenWorkflowPublisherFails` unit test skipped (controller uses async outbox, no sync publisher call).
- **atlas-admin-portal:** Lint 1 warning (AvailabilityCalendar exhaustive-deps). Build and tests green.
- **RatebotaiRepo:** validate:legal fixed — `vitest.legal.config.ts` (node env, no jest-dom) for legal tests; `dom-accessibility-api` npm override; SearchPage unused `index` removed. Build and tests green.

---

## 4. CI gates (implementation)

- **atlas-api:** `.github/workflows/ci-deploy-dev.yml` (CI and Deploy to Dev) — on push to `dev` and on PRs to `main`/`dev`: `dotnet restore` → `dotnet build -c Release` → unit + integration tests. On dev branch also publishes and deploys to dev.
- **atlas-admin-portal:** `.github/workflows/ci.yml` (CI) — on push/PR to `main` and `dev`: `npm ci` → `npm run lint` → `npm run build` → `npx vitest run`. Uses `eslint.config.cjs` (flat config) for ESLint 9+.
- **RatebotaiRepo:** `.github/workflows/ci.yml` — on push to `main` and PRs: `npm ci` → `npm run lint` → `npm run build` → `npm test` → `npm run check:local-network`. Node 22.12.0.

---

## 5. Runbook for running the sanity suite locally

1. **atlas-api**  
   From `atlas-api/`:  
   `dotnet restore` → `dotnet build -c Release` → `dotnet test -c Release --no-build`.  
   Ensure SQL Server/LocalDB available for integration tests.

2. **atlas-admin-portal**  
   From `atlas-admin-portal/`:  
   Close anything locking `node_modules` (IDE, terminals, antivirus).  
   `npm ci` (or `npm install`) → `npm run lint` → `npm run build` → `npx vitest run`.

3. **RatebotaiRepo**  
   From `RatebotaiRepo/`:  
   Close anything locking `node_modules`.  
   `npm ci` → `npm run lint` → `npm run build` → `npm test`.  
   Optionally use Node >=22.12.0.

4. **atlas-e2e**  
   From `atlas-e2e/`:  
   `npm ci` → `npx playwright test --grep smoke`. Full E2E needs API + SQL Server + admin/guest portals; smoke tests only hit load/redirect.

5. **Baseline**
   **Update this file before every commit** (re-run sanity per steps 1–4). When all are green: update this file, then single commit with message:
   `chore: workspace sanity pass (api tests green, portals build green)`
   and push. If not all green, do not commit; document failures here and fix environment/code before re-run.

6. **Verify gates in CI**
   After pushing to `dev` (or opening a PR to `main`/`dev`), the Gate or CI workflow runs in GitHub Actions. Check the Actions tab to confirm the run completes successfully. To require the gate before merge: repo **Settings → Branches → Add rule** for `main` (and optionally `dev`) → **Require status checks to pass before merging** → select the workflow’s job name. **Status check names by repo:** atlas-api → **gate**; atlas-admin-portal → **gate**; RatebotaiRepo → **build**.

7. **Before a release**
   Re-run the full sanity suite (this section, steps 1–4) locally for all repos to confirm they are still green before tagging or releasing. In production, atlas-api `GET /health` returns 200 for liveness (load balancer / platform health checks).
