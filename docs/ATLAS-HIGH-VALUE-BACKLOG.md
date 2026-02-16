# ATLAS – High Value Feature Backlog

*(Versioned copy in atlas-api/docs; canonical at workspace root.)*

Prioritized feature roadmap for Atlas Homestays. All development must align with revenue impact, SaaS scalability, automation readiness, multi-tenant architecture, and operational efficiency.

**See also:** `ATLAS-BACKLOG-ANALYSIS.md` (this folder; how this backlog was derived and what’s already in the codebase).

---

## Current implementation status (Tier 1)

| # | Feature | Status | Next step |
|---|--------|--------|-----------|
| 1 | Guest Communication Template Engine | **Largely done** | Add event types (pre-arrival, check-in, post-checkout, review); implement outbox consumer + provider gateway to actually send. |
| 2 | Direct Booking Engine (Property Portal) | **Done** | — |
| 3 | Event-Driven Notification System | **Partial** | Azure Service Bus + outbox + consumers exist; add T-1/T+1/review triggers. |
| 4 | Availability & Inventory Engine | **Done** | — |
| 5 | Unified Notification Gateway | **Partial** | Msg91NotificationProvider + NotificationOrchestrator exist; add WhatsApp/Email providers and wire to consumers. |

---

# TIER 1 – Immediate High-ROI (remaining work)

## 1. Guest Communication Template Engine (expand)
- **Remaining:** More event types (pre-arrival, check-in instructions, post-checkout, review request); outbox consumer; wire to notification gateway.
- **Why:** Standardizes hospitality, reduces manual communication, improves ratings.
- **Dependency:** Notification gateway (#5).

## 2. Direct Booking Engine — ✅ Implemented
- RatebotaiRepo + BookingsController + RazorpayController.

## 3. Event-Driven Notification System (expand)
- **Implemented:** Azure Service Bus, outbox pattern, OutboxDispatcherHostedService, BookingEventsNotificationConsumer, StayEventsNotificationConsumer.
- **Remaining:** T-1 check-in reminder, T+1 follow-up, review request triggers.
- **Dependency:** Notification gateway (#5) for actual sends.

## 4. Availability & Inventory Engine — ✅ Implemented
- AvailabilityController, AvailabilityService, blocks, inventory.

## 5. Unified Notification Gateway (partial)
- **Implemented:** INotificationProvider, Msg91NotificationProvider, NotificationOrchestrator.
- **Remaining:** WhatsApp/Email provider implementations; wire consumers to orchestrator.
- **Why:** Vendor flexibility (SMS/WhatsApp/Email providers e.g. MSG91).

---

# TIER 2 – Strategic Growth

6. Channel Manager (OTA Sync)  
7. Dynamic Pricing Engine  
8. Multi-Tenant SaaS Core (tenant model exists; expand as needed)  
9. Management & Staff Dashboard (admin portal exists; expand)  
10. Kafka Event Backbone (or continue outbox + worker)

---

# TIER 3 – Long-Term Differentiators

11. AI Guest Personalization  
12. Revenue Intelligence Analytics  
13. Automated Operational Workflows  
14. Regulatory & Compliance (e.g. GST)  
15. Maps & Location Intelligence  

---

# Execution rules

1. Evaluate workspace health before feature work (see DEVSECOPS-GATES-BASELINE.md §5).
2. Choose highest-ROI item **not yet fully done** (use status table above).
3. Maintain: atlas-api tests green, portals build/lint green (Gate/CI).
4. No large refactors unless required.
5. Align with long-term SaaS architecture.

After implementing a feature, update this file: set status, add a short implementation note, and adjust “Next step” as needed.
