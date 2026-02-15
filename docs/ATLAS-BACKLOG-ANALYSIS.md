# Analysis: ChatGPT “High-Value Backlog” Proposal

*(Versioned copy in atlas-api/docs; canonical at workspace root `docs/`.)*

## 1. Does it make sense?

**Yes, with adjustments.**

- A **single prioritized feature backlog** (ATLAS-HIGH-VALUE-BACKLOG.md) is useful so priorities are explicit and AI/developers don’t “hallucinate” what to build next.
- A **feature execution workflow** (stability → pick feature → design → implement → verify → update backlog) is a good discipline and is **not** the same as the existing **sanity/gate** workflow.

So the **idea** is sound: add a product roadmap and a repeatable “next feature” flow.

---

## 2. What’s already implemented (overlap check)

We already have:

| ChatGPT Tier 1 item | Current state in workspace |
|--------------------|----------------------------|
| **1. Guest Communication Template Engine** | **Largely implemented.** `MessageTemplatesController` (CRUD), `MessageTemplate` model, DTOs, tenant-owned. `BookingsController` uses templates in `EnqueueBookingConfirmedWorkflowAsync`: selects SMS/WhatsApp/Email templates, creates `CommunicationLog` rows and `OutboxMessage` (event). Integration tests in `MessageTemplatesApiTests.cs`. What may remain: more event types (pre-arrival, check-in, post-checkout, review), and actually sending via SMS/WhatsApp/Email providers (outbox consumer). |
| **2. Direct Booking Engine (Property Portal)** | **Implemented.** RatebotaiRepo is the guest-facing site; `BookingsController`, `RazorpayController`, booking flow and payment exist. |
| **3. Event-Driven Notification System** | **Partially implemented.** Outbox pattern (`OutboxMessage`), booking-confirmed event, `CommunicationLog` for pending sends. Missing: generic event bus, T-1/T+1/review triggers, and a consumer that sends via providers. |
| **4. Availability & Inventory Engine** | **Implemented.** `AvailabilityController`, `AvailabilityService`, blocks, inventory. |
| **5. Unified Notification Gateway** | **Not implemented.** No abstraction over SMS/WhatsApp/Email providers (MSG91, etc.); only in-memory/outbox and template selection exist. |

So Tier 1 should **not** be copied as if everything is “not yet implemented.” The backlog should either mark items as done/partial or be rewritten to reflect “next steps” (e.g. “Template engine: add pre-arrival/check-in events and wire to notification gateway”).

---

## 3. Overlap with existing docs and workflows

| ChatGPT element | Our existing equivalent | Verdict |
|-----------------|-------------------------|--------|
| **PHASE 1 – Workspace stability** (restore, build, test for api + portals) | **DEVSECOPS-WORKSPACE-SANITY-PROMPT.md** Phase 1 and **DEVSECOPS-GATES-BASELINE.md** §2 (commands), §4 (CI gates) | **Duplicate.** Phase 1 should **reference** the runbook and baseline (e.g. “Run the sanity suite per DEVSECOPS-WORKSPACE-SANITY-PROMPT.md and ensure gates are green”) instead of re-listing the same commands. |
| **“Must maintain: atlas-api tests green, portals build/lint green”** | Baseline “Execution Rules”, CONTRIBUTING, Gate/CI workflows | **Already enforced** by gates and CONTRIBUTING. |
| **“No large refactors”** | Baseline and runbook “minimal, no large refactors” | **Already there.** |
| **Feature selection from backlog** | Not present | **New.** Useful. |
| **Design before code + update backlog after** | Not present | **New.** Useful. |

So: keep the **feature-selection and design/backlog-update** parts; **don’t** re-specify Phase 1; instead **point to** the existing runbook and baseline.

---

## 4. Recommendations

1. **Create ATLAS-HIGH-VALUE-BACKLOG.md** at workspace root **only after** adding a **“Current implementation status”** (or similar) section that maps each Tier 1 item to what exists (as in the table above). Optionally add “Next step” per item (e.g. “Template engine: add pre-arrival/check-in events; implement outbox consumer and gateway”).
2. **Do not paste the generic Cursor prompt verbatim.** It duplicates Phase 1. Instead:
   - Add a **short** “Feature execution” workflow that:
     - **Phase 1:** “Ensure workspace is stable: run DEVSECOPS-WORKSPACE-SANITY-PROMPT.md (or ensure Gate/CI is green per DEVSECOPS-GATES-BASELINE.md §5).”
     - **Phase 2:** Select highest-ROI feature from ATLAS-HIGH-VALUE-BACKLOG.md **not yet fully done** (using the new status section).
     - **Phase 3–5:** Design, implement, verify (gates green), commit.
     - **Phase 6:** Update ATLAS-HIGH-VALUE-BACKLOG.md (mark implemented / add notes).
3. **Portals: “typecheck”** – Our gates use **lint + build** (TypeScript is checked at build in Vite). No need to add a separate typecheck step unless you want it; the ChatGPT prompt’s “typecheck” is satisfied by build for these repos.
4. **Coverage** – ChatGPT suggests `--collect:"XPlat Code Coverage"` for atlas-api. Our current gate runs **unit tests only** (no coverage). Adding coverage is optional; keep it out of the **required** stability check unless you decide to enforce it.

---

## 5. Summary

| Question | Answer |
|----------|--------|
| Does the backlog + prompt **make sense**? | Yes, as a **product roadmap** and **feature execution** workflow. |
| Is it **already implemented**? | The **file** and **feature-selection/backlog-update** workflow are not. The **stability check** part is already in the runbook and baseline. Several **Tier 1 features** are partly or fully implemented (templates, booking, availability, outbox). |
| What to do? | (1) Add ATLAS-HIGH-VALUE-BACKLOG.md with a **current status** section so the backlog reflects reality. (2) Add a **short** feature-execution prompt that **references** the runbook/baseline for Phase 1 and adds feature selection, design, implement, verify, update backlog. (3) Do not duplicate Phase 1 commands. |

**Done:** `ATLAS-HIGH-VALUE-BACKLOG.md` and `ATLAS-FEATURE-EXECUTION-PROMPT.md` have been added at workspace root with current status and a Phase 1–6 workflow that references the runbook and baseline. Versioned copies live in **atlas-api/docs/**.
