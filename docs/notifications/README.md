# Notifications — DB Outbox + Worker

**Purpose:** Entry point for notification system design (WhatsApp, SMS, Email later) using a DB-backed Outbox and worker(s), without Service Bus/Kafka.

**Audience:** Platform architects, developers implementing notifications

**Owner:** Atlas Tech Solutions

**Last updated:** 2026-02-26

---

## Scope

- **In scope:** Notifications triggered when a **Booking** status transitions to **Confirmed** (old ≠ Confirmed, new = Confirmed). Channels: WhatsApp and SMS (India context); Email later. Implementation uses existing DB primitives: **OutboxMessage**, **MessageTemplate**, **AutomationSchedule**, **CommunicationLog**. Worker(s): Outbox materializer + Sender. Multi-tenant (TenantId) and idempotency throughout.
- **Out of scope (this release):** Azure Service Bus / Kafka; delivery webhooks or fallback after delivery failure; marketing journeys; complex Channex sync (architecture remains ready for Channex events later).

## Documents

| Document | Description |
|----------|-------------|
| [HLD — High Level Design](HLD-notifications-outbox-worker.md) | Executive summary, problem/goals, architecture, reliability, multi-tenant, Channex readiness, observability, security. |
| [LLD — Low Level Design](LLD-notifications-outbox-worker.md) | Data model mapping, event schemas, worker algorithms, template rendering, provider adapters, admin/API behaviour, retries, runbook, test plan. |

## Quick links

- **Canonical schema:** [db-schema.md](../db-schema.md) — OutboxMessage, MessageTemplate, AutomationSchedule, CommunicationLog.
- **API contract:** [api-contract.md](../api-contract.md) — Bookings, MessageTemplates, CommunicationLogs, AutomationSchedules, Ops.
- **Event types:** `Atlas.Api/Events/EventTypes.cs` — e.g. `BookingConfirmed`, `booking.confirmed`.
