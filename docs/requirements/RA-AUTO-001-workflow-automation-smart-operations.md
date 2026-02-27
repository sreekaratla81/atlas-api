# RA-AUTO-001 — Workflow Automation & Smart Operations Engine Requirements

| Field | Value |
|-------|-------|
| **ID** | RA-AUTO-001 |
| **Title** | Workflow Automation & Smart Operations Engine |
| **Status** | Draft |
| **Author** | Workflow Systems Architect |
| **Created** | 2026-02-27 |
| **Dependencies** | RA-001 (Marketplace/Commission), RA-004 (Trust/Fraud), RA-005 (Billing), RA-006 (Operational Excellence), RA-AI-001 (Pricing Intelligence), RA-DATA-001 (Data Platform/Events), RA-DASH-001 (Dashboards) |
| **Stack** | Azure App Service · Azure SQL · DB-backed outbox · No Service Bus (V1) |
| **Constraints** | Single developer · No heavy workflow engines · No third-party workflow SaaS · DB-backed outbox pattern · Scale to 100k tenants · Deterministic and auditable |

---

## Table of Contents

1. [Automation Engine Vision (V1 vs V2)](#1-automation-engine-vision-v1-vs-v2)
2. [Core Automation Architecture](#2-core-automation-architecture)
3. [Booking Lifecycle Automation Requirements](#3-booking-lifecycle-automation-requirements)
4. [Staff Task Automation](#4-staff-task-automation)
5. [Escalation & SLA Engine](#5-escalation--sla-engine)
6. [Guest Communication Automation](#6-guest-communication-automation)
7. [Rate & Restriction Automation (Advanced V1)](#7-rate--restriction-automation-advanced-v1)
8. [Automation Audit & Observability](#8-automation-audit--observability)
9. [Tenant-Level Custom Automation Rules](#9-tenant-level-custom-automation-rules)
10. [Automation Safety Guardrails](#10-automation-safety-guardrails)
11. [Performance & Scale Requirements](#11-performance--scale-requirements)
12. [Acceptance Criteria & Test Matrix](#12-acceptance-criteria--test-matrix)
13. [Definition of Done — Automation Engine V1](#13-definition-of-done--automation-engine-v1)

---

## 1. Automation Engine Vision (V1 vs V2)

### 1.1 Strategic intent

Atlas's target hosts (0–10 keys, India) run their operations largely manually — checking WhatsApp for bookings, calling guests for check-in, chasing payments on paper. The automation engine turns Atlas from a passive record-keeper into an active operations partner, executing predictable workflows reliably at scale.

### 1.2 V1 scope — Rule-based automation

V1 is entirely rule-based. Every trigger, condition, and action is deterministic and config-driven. No ML, no probabilistic logic. The existing `AutomationSchedule` + `OutboxMessage` + `CommunicationLog` pipeline is the execution backbone.

| Capability | V1 | Implementation approach |
|---|---|---|
| **Event-based triggers** | Yes | Domain events from `OutboxMessage` (RA-DATA-001 §2) |
| **Time-based triggers** | Yes | `AutomationSchedule.DueAtUtc` polling (existing, 30s cycle) |
| **Booking lifecycle automation** | Yes | Pre-built workflows: confirm → reminders → check-in → check-out → review |
| **Staff task creation** | Yes | Auto-create `StaffTask` rows on trigger events |
| **Escalation engine** | Yes | SLA-based escalation via timed re-evaluation |
| **Guest communication** | Yes | Multi-channel via existing `CommunicationLog` + `MessageTemplate` |
| **Rate automation** | Yes | Integration with RA-AI-001 pricing engine (suggest/auto-apply) |
| **Metric-based triggers** | Yes | Threshold checks on `DailyPerformanceSnapshot` / `BookingVelocityMetric` |
| **Custom tenant rules** | Predefined templates only | Tenant selects from a library of rule templates |

### 1.3 V2 scope — AI-driven automation (future)

| Capability | V2 | Notes |
|---|---|---|
| **AI-driven automation suggestions** | V2 | "Based on your booking patterns, we recommend enabling auto-check-in reminders" |
| **Predictive escalation** | V2 | Flag high-risk bookings before problems occur (payment delay prediction, no-show prediction) |
| **Auto-optimization loops** | V2 | Self-tuning thresholds based on outcome data (e.g., adjust payment reminder timing based on conversion rate) |
| **Custom rule builder UI** | V2 | Visual rule builder: IF {trigger} AND {condition} THEN {action} |
| **Cross-property coordination** | V2 | Coordinate cleaning/maintenance across multiple properties |
| **Guest sentiment automation** | V2 | Trigger actions based on mid-stay feedback sentiment |

- VIS-01: V1 MUST NOT depend on any ML model or external AI service.
- VIS-02: V1 architecture MUST support plugging in V2 AI components without schema changes — the rule evaluator is an interface; V2 adds an AI-powered implementation alongside the rule-based one.
- VIS-03: All V1 automations produce the same `AutomationExecutionLog` entries regardless of trigger source, enabling V2 to learn from historical execution data.

### 1.4 Feature flags

| Flag | V1 default | Scope | Description |
|------|:----------:|-------|-------------|
| `Automation:Enabled` | `true` | Global | Master kill-switch |
| `Automation:BookingLifecycleEnabled` | `true` | Global | Booking workflow automations |
| `Automation:StaffTasksEnabled` | `true` | Global | Auto-created staff tasks |
| `Automation:EscalationEnabled` | `true` | Global | SLA-based escalation |
| `Automation:GuestCommsEnabled` | `true` | Global | Guest messaging automation |
| `Automation:RateAutomationEnabled` | `false` | Global | Rate/restriction auto-apply (requires RA-AI-001) |
| `Automation:TenantCustomRulesEnabled` | `false` | Global | Tenant rule templates (V1 late) |

---

## 2. Core Automation Architecture

### 2.1 Component overview

```
┌───────────────────────────────────────────────────────────────────────┐
│                        Automation Engine                              │
│                                                                       │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────────────┐    │
│  │ Event Source  │───→│ Rule         │───→│ Condition Engine     │    │
│  │ (OutboxMsg,  │    │ Evaluator    │    │ (AND/OR, thresholds, │    │
│  │  Schedule,   │    │ (match rules │    │  date comparisons)   │    │
│  │  Metric)     │    │  to events)  │    │                      │    │
│  └──────────────┘    └──────────────┘    └──────────┬───────────┘    │
│                                                      │               │
│                                           ┌──────────▼───────────┐   │
│                                           │ Action Executor      │   │
│                                           │ (send message,       │   │
│                                           │  create task,        │   │
│                                           │  update status,      │   │
│                                           │  adjust rate)        │   │
│                                           └──────────┬───────────┘   │
│                                                      │               │
│                    ┌─────────────────┐    ┌──────────▼───────────┐   │
│                    │ Retry Queue     │◄──→│ Execution Log        │   │
│                    │ (backoff,       │    │ (audit trail,        │   │
│                    │  dead-letter)   │    │  status tracking)    │   │
│                    └─────────────────┘    └──────────────────────┘   │
└───────────────────────────────────────────────────────────────────────┘
```

### 2.2 Trigger types

#### 2.2.1 Event-based triggers

Fire when a domain event is published to the outbox.

| Trigger | Source event | Examples |
|---------|-------------|---------|
| Booking confirmed | `booking.confirmed` | Send confirmation message, create cleaning task |
| Booking cancelled | `booking.cancelled` | Notify staff, release block |
| Payment received | `settlement.completed` | Update payment status, send receipt |
| Check-in completed | `stay.checked_in` | Send house rules, create welcome task |
| Check-out completed | `stay.checked_out` | Trigger review request, create cleaning task |
| Sync failed | `sync.failed` | Alert tenant, escalate if repeated |
| Rate updated | `rate.updated` | Trigger ARI push (existing) |
| Suggestion accepted | `suggestion.accepted` | Trigger rate write + ARI push |
| TrustScore dropped | `trustscore.updated` | Alert if below threshold |

#### 2.2.2 Time-based triggers

Fire when a scheduled time arrives. Managed by the existing `AutomationSchedule` table + `AutomationSchedulerHostedService`.

| Trigger | Schedule relative to | Examples |
|---------|---------------------|---------|
| Pre-arrival reminder | Check-in − X hours | Send check-in instructions |
| Day-of check-in | Check-in date 09:00 local | Send welcome message |
| Mid-stay check | Check-in + N days | "How's your stay?" message |
| Pre-checkout reminder | Check-out − X hours | Send checkout instructions |
| Post-stay review | Check-out + X hours | Send review request |
| Payment follow-up | Booking created + X hours (if unpaid) | Payment reminder |
| SLA escalation timer | Task created + SLA hours | Escalate if incomplete |
| Subscription renewal | Next invoice date − X days | Pre-renewal notice |

#### 2.2.3 Metric-based triggers

Fire when a computed metric crosses a threshold. Evaluated by a periodic sweep job (every 4 hours, aligned with RA-AI-001 signal detection).

| Trigger | Metric | Threshold | Examples |
|---------|--------|-----------|---------|
| Low occupancy window | Forward occupancy < X% | Configurable (default 30%) | Trigger discount suggestion |
| High occupancy weekend | Weekend occupancy > X% | Configurable (default 80%) | Trigger uplift suggestion |
| Payment overdue | Amount due > 0 for > X hours | Configurable (default 24h) | Escalate to owner |
| Sync stale | Last sync > X hours ago | Configurable (default 6h) | Alert tenant |
| Cancellation spike | > X cancellations in Y days | Configurable | Alert tenant + admin |

### 2.3 Condition engine

Conditions are evaluated after a trigger fires and before actions execute. They determine whether the action should proceed.

#### 2.3.1 Condition types

| Type | Syntax | Example |
|------|--------|---------|
| **Simple equality** | `field == value` | `Booking.BookingStatus == 'Confirmed'` |
| **Threshold check** | `field > value` | `DaysUntilCheckin < 2` |
| **Date comparison** | `field > NOW()` | `Booking.CheckinDate > NOW()` |
| **Boolean flag** | `field == true` | `Tenant.IsActive == true` |
| **AND** | `condition1 AND condition2` | `BookingStatus == 'Confirmed' AND AmountReceived == 0` |
| **OR** | `condition1 OR condition2` | `BookingSource == 'Walk-in' OR BookingSource == 'marketplace_direct'` |
| **NOT** | `NOT condition` | `NOT Booking.BookingStatus == 'Cancelled'` |
| **IN** | `field IN (values)` | `BookingStatus IN ('Confirmed', 'CheckedIn')` |

- COND-01: V1 conditions are evaluated in-memory after loading the entity from the database. No dynamic SQL generation.
- COND-02: Conditions are defined as JSON in the `AutomationRule.ConditionsJson` field (section 2.5).
- COND-03: The condition engine is a simple recursive evaluator (AND/OR tree) — not a full expression parser.

### 2.4 Action types

| Action type | Code | Description | Target system |
|---|---|---|---|
| **Send WhatsApp** | `SEND_WHATSAPP` | Queue WhatsApp message via template | `CommunicationLog` + provider |
| **Send SMS** | `SEND_SMS` | Queue SMS message | `CommunicationLog` + provider |
| **Send Email** | `SEND_EMAIL` | Queue email message | `CommunicationLog` + provider |
| **Create Staff Task** | `CREATE_TASK` | Insert `StaffTask` row with assignment | `StaffTask` table |
| **Update Booking Status** | `UPDATE_BOOKING_STATUS` | Transition booking status | `Booking` table |
| **Apply Restriction** | `APPLY_RESTRICTION` | Create/update `ListingPricingRule` | `ListingPricingRule` table |
| **Adjust Rate** | `ADJUST_RATE` | Accept price suggestion or write `ListingDailyRate` | `PriceSuggestion` / `ListingDailyRate` |
| **Notify Admin** | `NOTIFY_ADMIN` | Send alert to Atlas admin dashboard | `AuditLog` + structured log |
| **Escalate** | `ESCALATE` | Promote task/alert to next escalation level | `StaffTask.EscalationLevel` |
| **Create Outbox Event** | `CREATE_EVENT` | Write a new `OutboxMessage` for downstream processing | `OutboxMessage` |

- ACT-01: Each action type is implemented as an `IAutomationAction` class with a single `ExecuteAsync()` method.
- ACT-02: Actions MUST be idempotent. Re-executing the same action for the same trigger MUST NOT create duplicates (checked via `AutomationExecutionLog`).
- ACT-03: Message actions (WhatsApp, SMS, Email) reuse the existing `CommunicationLog.IdempotencyKey` mechanism.

### 2.5 `AutomationRule` (new entity)

Defines a reusable automation rule. Tenant-scoped (tenants select from templates) or platform-scoped (system defaults).

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `int` (PK, identity) | Auto-increment |
| `TenantId` | `int?` | NULL for platform rules, set for tenant-specific |
| `Code` | `varchar(50)` | Unique rule code (e.g., `BOOKING_CONFIRM_MSG`) |
| `Name` | `nvarchar(200)` | Human-readable name |
| `Description` | `nvarchar(500)` | What this rule does |
| `TriggerType` | `varchar(20)` | `EVENT`, `TIME`, `METRIC` |
| `TriggerEventType` | `varchar(80)` | Event type to match (e.g., `booking.confirmed`) |
| `TriggerScheduleOffset` | `varchar(50)` | Relative schedule (e.g., `checkin-24h`, `checkout+2h`) |
| `TriggerMetricType` | `varchar(50)` | Metric to evaluate (for METRIC triggers) |
| `TriggerMetricThreshold` | `decimal(10,4)?` | Threshold value |
| `TriggerMetricOperator` | `varchar(5)` | `>`, `<`, `>=`, `<=`, `==` |
| `ConditionsJson` | `nvarchar(max)` | JSON condition tree |
| `ActionType` | `varchar(30)` | From action types enum |
| `ActionConfigJson` | `nvarchar(max)` | Action-specific parameters (template ID, task type, etc.) |
| `Priority` | `int` | Execution priority (lower = higher priority) |
| `IsActive` | `bit` | Active/inactive |
| `Scope` | `varchar(20)` | `PLATFORM` (system default) or `TENANT` (custom) |
| `MaxExecutionsPerBooking` | `int` | Maximum times this rule fires for a single booking (default 1) |
| `CooldownMinutes` | `int` | Minimum minutes between executions for same entity (default 0) |
| `CreatedAtUtc` | `datetime2` | |
| `UpdatedAtUtc` | `datetime2` | |

**Unique constraint**: `IX_AutomationRule_Code_Tenant` on `(Code, TenantId)`.

### 2.6 `AutomationExecutionLog` (new entity)

Records every automation execution attempt for audit and idempotency.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `bigint` (PK, identity) | Auto-increment |
| `TenantId` | `int` (indexed) | Tenant scope |
| `RuleId` | `int` (FK → AutomationRule) | Which rule fired |
| `RuleCode` | `varchar(50)` | Denormalized for fast queries |
| `TriggerType` | `varchar(20)` | `EVENT`, `TIME`, `METRIC` |
| `TriggerEventId` | `guid?` | OutboxMessage.Id that triggered this |
| `TriggerScheduleId` | `bigint?` | AutomationSchedule.Id that triggered this |
| `EntityType` | `varchar(50)` | `Booking`, `Listing`, `Property`, etc. |
| `EntityId` | `varchar(50)` | PK of the affected entity |
| `Status` | `varchar(20)` | `Scheduled`, `Executed`, `Failed`, `Retried`, `Skipped`, `DeadLettered` |
| `ActionType` | `varchar(30)` | Action executed |
| `ActionResultJson` | `nvarchar(max)` | Action outcome (message ID, task ID, error details) |
| `ExecutedAtUtc` | `datetime2` | When execution occurred |
| `DurationMs` | `int` | Execution time |
| `AttemptCount` | `int` | Number of attempts |
| `ErrorMessage` | `nvarchar(500)` | Last error if failed |
| `IdempotencyKey` | `varchar(200)` | `{RuleCode}:{EntityId}:{TriggerHash}` |
| `CreatedAtUtc` | `datetime2` | |

**Unique constraint**: `IX_AutomationExecLog_Idempotency` on `(TenantId, IdempotencyKey)`.

**Index**: `IX_AutomationExecLog_Status` on `(Status, CreatedAtUtc)` WHERE `Status IN ('Scheduled', 'Failed')`.

- LOG-01: Before executing any action, the engine checks `AutomationExecutionLog` for the idempotency key. If found with `Status = 'Executed'`, skip.
- LOG-02: `IdempotencyKey` format: `{RuleCode}:{EntityType}:{EntityId}:{TriggerDiscriminator}`. The discriminator is `EventId` for events, `ScheduleId` for time triggers, or `MetricDate` for metric triggers.
- LOG-03: Retention: 90 days. Purge job aligned with RA-DATA-001 §2.5.

---

## 3. Booking Lifecycle Automation Requirements

### 3.1 Workflow definitions

Each workflow maps to an `AutomationRule` (platform scope, pre-installed).

#### 3.1.1 Booking confirmed

| Field | Value |
|-------|-------|
| **Code** | `BOOKING_CONFIRMED_NOTIFY` |
| **Trigger** | Event: `booking.confirmed` |
| **Conditions** | `Booking.BookingStatus == 'Confirmed' AND Guest.Phone IS NOT NULL` |
| **Action** | `SEND_WHATSAPP` — booking confirmation message |
| **Template** | `booking.confirmed` / `whatsapp` / `en` |
| **Delay** | None (immediate) |
| **Max executions** | 1 per booking |

| Field | Value |
|-------|-------|
| **Code** | `BOOKING_CONFIRMED_SCHEDULE` |
| **Trigger** | Event: `booking.confirmed` |
| **Conditions** | `Booking.BookingStatus == 'Confirmed'` |
| **Action** | `CREATE_EVENT` — schedule downstream automations (check-in reminder, checkout, review) |
| **Delay** | None |
| **Details** | Creates `AutomationSchedule` rows for: pre-arrival (check-in − 24h), checkout (check-out − 4h), post-stay (check-out + 24h) |

#### 3.1.2 Payment pending escalation

| Field | Value |
|-------|-------|
| **Code** | `PAYMENT_PENDING_REMIND` |
| **Trigger** | Time: booking created + `Config:PaymentReminderHours` (default 4h) |
| **Conditions** | `Booking.AmountReceived < Booking.FinalAmount AND Booking.BookingStatus == 'Confirmed'` |
| **Action** | `SEND_WHATSAPP` — payment reminder |
| **Template** | `payment.reminder` / `whatsapp` / `en` |
| **Max executions** | 1 per booking |
| **Config** | `Automation:PaymentReminderHours` = 4 |

| Field | Value |
|-------|-------|
| **Code** | `PAYMENT_PENDING_ESCALATE` |
| **Trigger** | Time: booking created + `Config:PaymentEscalationHours` (default 24h) |
| **Conditions** | `Booking.AmountReceived < Booking.FinalAmount AND Booking.BookingStatus == 'Confirmed'` |
| **Action** | `ESCALATE` — notify tenant owner |
| **Max executions** | 1 per booking |
| **Config** | `Automation:PaymentEscalationHours` = 24 |

#### 3.1.3 Guest check-in day reminder

| Field | Value |
|-------|-------|
| **Code** | `CHECKIN_REMINDER` |
| **Trigger** | Time: check-in − `Config:CheckinReminderHours` (default 24h) |
| **Conditions** | `Booking.BookingStatus == 'Confirmed' AND Guest.Phone IS NOT NULL` |
| **Action** | `SEND_WHATSAPP` — pre-arrival instructions |
| **Template** | `stay.welcome.due` (existing EventType) |
| **Max executions** | 1 per booking |

#### 3.1.4 Check-out reminder

| Field | Value |
|-------|-------|
| **Code** | `CHECKOUT_REMINDER` |
| **Trigger** | Time: check-out − `Config:CheckoutReminderHours` (default 4h) |
| **Conditions** | `Booking.BookingStatus == 'CheckedIn'` |
| **Action** | `SEND_WHATSAPP` — checkout instructions |
| **Template** | `stay.precheckout.due` (existing EventType) |
| **Max executions** | 1 per booking |

#### 3.1.5 Post-stay review request

| Field | Value |
|-------|-------|
| **Code** | `POSTSTAY_REVIEW_REQUEST` |
| **Trigger** | Time: check-out + `Config:ReviewRequestHours` (default 24h) |
| **Conditions** | `Booking.BookingStatus IN ('CheckedOut') AND Review for this booking does not exist` |
| **Action** | `SEND_WHATSAPP` — review request with link |
| **Template** | `stay.postcheckout.due` (existing EventType) |
| **Max executions** | 1 per booking |

#### 3.1.6 Cancellation alert

| Field | Value |
|-------|-------|
| **Code** | `CANCELLATION_NOTIFY_STAFF` |
| **Trigger** | Event: `booking.cancelled` |
| **Conditions** | `Booking.CheckinDate >= TODAY` (future booking) |
| **Action** | `NOTIFY_ADMIN` — alert staff dashboard |
| **Max executions** | 1 per booking |

#### 3.1.7 No-show detection

| Field | Value |
|-------|-------|
| **Code** | `NOSHOW_FLAG` |
| **Trigger** | Time: check-in + `Config:NoShowGraceHours` (default 18h, i.e. next morning 09:00 for typical 3 PM check-in) |
| **Conditions** | `Booking.BookingStatus == 'Confirmed' AND Booking.CheckedInAtUtc IS NULL` |
| **Action** | `CREATE_TASK` (type: `NoShowReview`, assigned to: `tenant_owner`) + `SEND_WHATSAPP` to guest |
| **Max executions** | 1 per booking |
| **Config** | `Automation:NoShowGraceHours` = 18 |

### 3.2 Delay configuration

All timing offsets are configurable via `IOptions<AutomationTimingSettings>`:

| Config key | Default | Type | Description |
|------------|:-------:|------|-------------|
| `Automation:PaymentReminderHours` | 4 | int | Hours after booking creation for payment reminder |
| `Automation:PaymentEscalationHours` | 24 | int | Hours after booking for payment escalation |
| `Automation:CheckinReminderHours` | 24 | int | Hours before check-in for reminder |
| `Automation:CheckoutReminderHours` | 4 | int | Hours before check-out for reminder |
| `Automation:ReviewRequestHours` | 24 | int | Hours after check-out for review request |
| `Automation:NoShowGraceHours` | 18 | int | Hours after scheduled check-in to flag no-show |

### 3.3 Idempotency rules

- BLA-01: Every booking lifecycle automation has `MaxExecutionsPerBooking = 1`. The execution log's idempotency key prevents duplicates.
- BLA-02: If a booking is cancelled, all pending `AutomationSchedule` rows for that booking are marked `Cancelled` (existing logic in `AutomationSchedulerHostedService`).
- BLA-03: If a booking's dates change (modification), existing schedules are cancelled and new ones created with updated times.

### 3.4 Multi-channel logic

- BLA-04: Primary channel: WhatsApp (if `Guest.Phone` is set).
- BLA-05: Fallback channel: Email (if `Guest.Email` is set and WhatsApp failed or phone is missing).
- BLA-06: Channel selection logic is in `ActionConfigJson`:

```json
{
  "channels": ["whatsapp", "email"],
  "fallbackOnFailure": true,
  "templateKey": "booking.confirmed"
}
```

- BLA-07: The action executor tries channels in order. If the primary fails after max retries, the next channel is attempted.

### 3.5 Retry behavior

- BLA-08: Message actions retry 3 times with exponential backoff (30s, 60s, 120s).
- BLA-09: Task creation actions retry 2 times (database transient failures only).
- BLA-10: After all retries exhausted, the execution is marked `Failed` and the rule's `CooldownMinutes` prevents re-trigger.
- BLA-11: Failed executions are visible in the staff dashboard alerts panel (RA-DASH-001 §2.3).

---

## 4. Staff Task Automation

### 4.1 `StaffTask` (new entity)

Extends the basic `Incident` model with structured task management.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `long` (PK, identity) | Auto-increment |
| `TenantId` | `int` (FK, indexed) | Tenant scope |
| `PropertyId` | `int` (FK) | Property scope |
| `ListingId` | `int?` (FK) | Listing scope (optional) |
| `BookingId` | `int?` (FK) | Related booking (optional) |
| `TaskType` | `varchar(30)` | `Cleaning`, `Maintenance`, `PaymentFollowUp`, `LateCheckout`, `OverbookingResolution`, `NoShowReview`, `General` |
| `Title` | `nvarchar(200)` | Human-readable title |
| `Description` | `nvarchar(1000)` | Details |
| `AssignedToRole` | `varchar(30)` | `staff`, `property_manager`, `tenant_owner` |
| `AssignedToUserId` | `int?` (FK) | Specific user (optional, V2) |
| `Priority` | `varchar(10)` | `LOW`, `MEDIUM`, `HIGH`, `CRITICAL` |
| `Status` | `varchar(20)` | `Pending`, `InProgress`, `Completed`, `Cancelled`, `Escalated` |
| `DueAtUtc` | `datetime2` | When the task should be completed |
| `EscalationAtUtc` | `datetime2` | When to escalate if not completed |
| `EscalationLevel` | `int` | Current escalation level (0 = none, 1 = property manager, 2 = owner, 3 = admin) |
| `CompletedAtUtc` | `datetime2?` | When completed |
| `CompletedByUserId` | `int?` | Who completed it |
| `SourceRuleCode` | `varchar(50)` | Automation rule that created this task |
| `SourceExecutionId` | `bigint?` (FK → AutomationExecutionLog) | Traceability |
| `CreatedAtUtc` | `datetime2` | |
| `UpdatedAtUtc` | `datetime2` | |

**Index**: `IX_StaffTask_Tenant_Status_Due` on `(TenantId, Status, DueAtUtc)`.

- TASK-01: `StaffTask` implements `ITenantOwnedEntity` for tenant isolation.
- TASK-02: The `Incident` table (existing) remains for ad-hoc incident reporting. `StaffTask` is for automated, structured tasks.

### 4.2 Automatic task creation rules

#### 4.2.1 Cleaning before check-in

| Field | Value |
|-------|-------|
| **Code** | `TASK_CLEANING_PRE_CHECKIN` |
| **Trigger** | Time: check-in − `Config:CleaningLeadHours` (default 4h) |
| **Conditions** | `Booking.BookingStatus IN ('Confirmed') AND Listing has check-out on same day or previous day` |
| **Action** | `CREATE_TASK` |
| **Task config** | `TaskType = 'Cleaning'`, `Priority = 'HIGH'`, `AssignedToRole = 'staff'`, `DueAtUtc = checkin - 1h`, `EscalationAtUtc = checkin - 30min` |

#### 4.2.2 Maintenance follow-up

| Field | Value |
|-------|-------|
| **Code** | `TASK_MAINTENANCE_FOLLOWUP` |
| **Trigger** | Event: `incident.created` (when an Incident is logged) |
| **Conditions** | `Incident.Status == 'Open'` |
| **Action** | `CREATE_TASK` |
| **Task config** | `TaskType = 'Maintenance'`, `Priority = 'MEDIUM'`, `AssignedToRole = 'property_manager'`, `DueAtUtc = now + 24h` |

#### 4.2.3 Late checkout handling

| Field | Value |
|-------|-------|
| **Code** | `TASK_LATE_CHECKOUT` |
| **Trigger** | Time: check-out + `Config:LateCheckoutGraceMinutes` (default 60 min) |
| **Conditions** | `Booking.BookingStatus == 'CheckedIn' AND Booking.CheckedOutAtUtc IS NULL` |
| **Action** | `CREATE_TASK` + `SEND_WHATSAPP` to guest |
| **Task config** | `TaskType = 'LateCheckout'`, `Priority = 'HIGH'`, `AssignedToRole = 'staff'` |

#### 4.2.4 Payment follow-up

| Field | Value |
|-------|-------|
| **Code** | `TASK_PAYMENT_FOLLOWUP` |
| **Trigger** | Time: check-in + 2h (if still unpaid) |
| **Conditions** | `Booking.AmountReceived < Booking.FinalAmount AND Booking.BookingStatus == 'CheckedIn'` |
| **Action** | `CREATE_TASK` |
| **Task config** | `TaskType = 'PaymentFollowUp'`, `Priority = 'HIGH'`, `AssignedToRole = 'staff'` |

#### 4.2.5 Overbooking resolution

| Field | Value |
|-------|-------|
| **Code** | `TASK_OVERBOOKING_RESOLVE` |
| **Trigger** | Event: `booking.confirmed` (when overbooking is detected) |
| **Conditions** | Overlapping confirmed bookings exist for the same listing/date |
| **Action** | `CREATE_TASK` |
| **Task config** | `TaskType = 'OverbookingResolution'`, `Priority = 'CRITICAL'`, `AssignedToRole = 'tenant_owner'`, `DueAtUtc = now + 2h` |

### 4.3 Task status lifecycle

```
Pending ──[Staff picks up]──→ InProgress ──[Staff completes]──→ Completed
   │                              │
   ├──[Cancelled by system]──→ Cancelled    (booking cancelled, etc.)
   │
   └──[SLA breached]──→ Escalated ──[Higher role completes]──→ Completed
                            │
                            └──[Re-escalate]──→ Escalated (level+1)
```

### 4.4 SLA rules

| Task type | SLA (hours to complete) | Escalation after SLA | Config key |
|-----------|:-----------------------:|---------------------|------------|
| Cleaning | 2 | → property_manager | `Automation:CleaningSlaHours` |
| Maintenance | 24 | → tenant_owner | `Automation:MaintenanceSlaHours` |
| LateCheckout | 1 | → tenant_owner | `Automation:LateCheckoutSlaHours` |
| PaymentFollowUp | 4 | → tenant_owner | `Automation:PaymentFollowUpSlaHours` |
| OverbookingResolution | 2 | → atlas_admin | `Automation:OverbookingSlaHours` |
| NoShowReview | 12 | → atlas_admin | `Automation:NoShowSlaHours` |

- TASK-03: SLA is evaluated by the escalation engine (section 5) every 15 minutes.

---

## 5. Escalation & SLA Engine

### 5.1 Escalation levels

| Level | Role | Scope | Notification channel |
|:-----:|------|-------|---------------------|
| 0 | Staff | Listing/property level | Staff dashboard alert |
| 1 | Property Manager | Property level | Staff dashboard + WhatsApp |
| 2 | Tenant Owner | Tenant level | WhatsApp + Email |
| 3 | Atlas Admin | Platform level | Admin dashboard + structured log alert |

### 5.2 Escalation triggers

| Trigger | Source | Detection | Action |
|---------|--------|-----------|--------|
| **Task overdue** | `StaffTask.DueAtUtc < NOW() AND Status IN ('Pending', 'InProgress')` | SLA sweep job (every 15 min) | Increment `EscalationLevel`, notify next role |
| **Payment failure** | `settlement.failed` event | Event-based | Create task + notify tenant owner (Level 2) |
| **Sync failure (repeated)** | `sync.failed` count > 3 in 24h | Metric-based | Notify tenant (Level 2) + Atlas admin (Level 3) |
| **Overbooking conflict** | Overlapping bookings detected | Event-based (on `booking.confirmed`) | Create CRITICAL task → tenant owner (Level 2) |
| **Chargeback received** | Payment dispute event | Event-based | Notify tenant owner (Level 2) + Atlas admin (Level 3) |
| **TrustScore critical** | `TrustScore < 0.20` | `trustscore.updated` event | Notify tenant owner + Atlas admin |

### 5.3 Escalation flow

```
Trigger detected
    │
    ▼
[Current escalation level?]
    │
    ├── Level 0 (Staff) ──[SLA breach]──→ Level 1 (Property Manager)
    │                                          │
    │                                          ├──[SLA breach]──→ Level 2 (Tenant Owner)
    │                                          │                       │
    │                                          │                       └──[SLA breach]──→ Level 3 (Atlas Admin)
    │                                          │
    │                                          └──[Resolved]──→ Completed
    │
    └── Direct escalation (critical issues skip to higher levels)
         └── Overbooking, chargeback → Level 2 directly
```

### 5.4 Escalation limits

- ESC-01: Maximum escalation level: 3 (Atlas Admin). No further escalation beyond Level 3.
- ESC-02: At Level 3, if unresolved after `Config:AdminEscalationTimeoutHours` (default 48h), the task is marked `DeadLettered` and appears in the admin "Unresolved" dashboard.
- ESC-03: Each escalation sends a notification to the new assignee role AND retains the original task (does not create a new task).
- ESC-04: Escalation notifications include: original task description, time since creation, all previous escalation timestamps, and a "Resolve" action link.

### 5.5 `EscalationSweepJob` (new hosted service)

| Parameter | Value |
|-----------|-------|
| Polling interval | 15 minutes |
| Batch size | 200 |
| Query | `StaffTask WHERE Status IN ('Pending', 'InProgress') AND EscalationAtUtc <= NOW()` |
| Action | Increment `EscalationLevel`, set new `EscalationAtUtc`, send notification, log to `AutomationExecutionLog` |
| Idempotency | Check execution log before escalating to prevent double-escalation |

---

## 6. Guest Communication Automation

### 6.1 Lifecycle messaging timeline

```
Booking ──→ Confirmation ──→ Pre-arrival ──→ Check-in ──→ Mid-stay ──→ Pre-checkout ──→ Review
                                (−24h)        (day-of)    (+N days)     (−4h)          (+24h)
```

| Stage | Trigger | Template key | Channel | Content |
|-------|---------|-------------|---------|---------|
| **Confirmation** | `booking.confirmed` | `booking.confirmed` | WhatsApp, Email | Booking details, payment link, property address |
| **Pre-arrival** | Check-in − 24h | `stay.welcome.due` | WhatsApp | Check-in time, address, directions, WiFi, house rules link |
| **Check-in day** | Check-in date 09:00 local | `stay.checkin_day` | WhatsApp | "Welcome today! Check-in at {time}." |
| **Mid-stay check** | Check-in + `Config:MidStayDays` (default 2 days, only if stay ≥ 4 nights) | `stay.midstay_check` | WhatsApp | "How's your stay? Need anything?" |
| **Pre-checkout** | Check-out − 4h | `stay.precheckout.due` | WhatsApp | Checkout time, any pending balance, "Leave a review" link |
| **Review request** | Check-out + 24h | `stay.postcheckout.due` | WhatsApp, Email | "Thank you! Please rate your stay." with review link |
| **Upsell (V2)** | Check-in + 1 day | `stay.upsell` | WhatsApp | "Extend your stay?" or local experience offers |

### 6.2 Template personalization

- COM-01: All templates use Handlebars-style placeholders: `{{guestName}}`, `{{propertyName}}`, `{{checkinDate}}`, `{{checkoutDate}}`, `{{totalAmount}}`, `{{paymentLink}}`, `{{reviewLink}}`.
- COM-02: The template engine resolves placeholders from the `BuildPayload()` context (existing in `AutomationSchedulerHostedService`).
- COM-03: Templates are managed per tenant via the existing `MessageTemplate` entity (`TemplateKey`, `EventType`, `Channel`, `ScopeType`, `Language`).
- COM-04: If the tenant has not customized a template, the platform-default template is used (scope = `System`).

### 6.3 Rate limiting

| Limit | Value | Scope | Enforcement |
|-------|:-----:|-------|-------------|
| Messages per guest per day | 3 | Per guest phone number | Count `CommunicationLog` rows for today |
| Messages per tenant per hour | 50 | Per tenant | Count `CommunicationLog` rows in last hour |
| Messages per tenant per day | 500 | Per tenant | Count `CommunicationLog` rows today |
| Platform-wide per minute | 100 | Global | Outbox dispatcher throttle |

- COM-05: If a rate limit is hit, the message is deferred (not dropped). A new `AutomationSchedule` is created with `DueAtUtc = NOW() + 30 minutes`.
- COM-06: Rate limit breach is logged: `automation.ratelimit.hit` with `{tenantId, guestId, limit, window}`.

### 6.4 Quiet hours

- COM-07: No automated messages sent between `Config:QuietHoursStart` (default 22:00) and `Config:QuietHoursEnd` (default 07:00) in the **property's local timezone**.
- COM-08: Messages scheduled during quiet hours are deferred to `QuietHoursEnd`.
- COM-09: Exception: CRITICAL priority messages (overbooking, payment failure) bypass quiet hours.
- COM-10: Timezone is derived from `Property.Timezone` (new field, default `Asia/Kolkata`).

### 6.5 Opt-out logic

- COM-11: Guests can opt out of automated messages by replying "STOP" to any WhatsApp message.
- COM-12: Opt-out is stored as `Guest.CommsOptedOut` (new `bit` field, default `false`).
- COM-13: When `CommsOptedOut = true`, all non-critical automated messages are skipped. Critical messages (booking confirmation, payment receipt) are still sent.
- COM-14: Opt-out is logged in `AuditLog`: `guest.comms.opted_out` with `{guestId, channel}`.

---

## 7. Rate & Restriction Automation (Advanced V1)

This section bridges the automation engine with the pricing intelligence system (RA-AI-001).

### 7.1 Rate automation rules

| Rule code | Trigger | Condition | Action |
|-----------|---------|-----------|--------|
| `RATE_LOW_OCCUPANCY_DISCOUNT` | Metric: forward 3-day occupancy < 20% | `Listing.AutoPriceEnabled == true` | Accept/create PriceSuggestion with discount |
| `RATE_HIGH_OCCUPANCY_UPLIFT` | Metric: forward 7-day occupancy > 85% | `Listing.AutoPriceEnabled == true` | Accept/create PriceSuggestion with uplift |
| `RATE_FESTIVAL_PRESET` | Metric: `FestivalDate` within 7 days | `Listing.AutoPriceEnabled == true` | Apply festival surge % from `FestivalDate.SurgePercent` |
| `RATE_LAST_MINUTE_DISCOUNT` | Metric: check-in within 3 days, available | `Listing.AutoPriceEnabled == true` | Apply last-minute discount |

- RATE-01: Rate automations ONLY execute when `Listing.AutoPriceEnabled = true` (explicit tenant opt-in, PRO plan only).
- RATE-02: All rate automations delegate to the RA-AI-001 pricing engine. They create `PriceSuggestion` rows with `StrategySource = 'RULE_ENGINE'` and `Status = 'AUTO_APPLIED'`.
- RATE-03: Rate automations MUST NOT bypass RA-AI-001 guardrails (max uplift %, floor rate, freeze window, circuit breaker).

### 7.2 Override priority

```
Manual rate set by tenant          → HIGHEST priority (always wins)
Price suggestion accepted by tenant → HIGH priority
Auto-applied by automation engine   → MEDIUM priority
System default (base nightly rate)  → LOWEST priority
```

- RATE-04: If a tenant manually sets a rate for a date, no automation may override it for 24 hours (RA-AI-001 §4.6 SUG-14).
- RATE-05: If a rate automation fires and detects a manual rate within 24h, the automation is `Skipped` with reason `ManualOverrideActive`.

### 7.3 Freeze window

- RATE-06: No automated rate change within `Config:Guardrails:FreezeWindowHours` (default 48h) of check-in (RA-AI-001 §8.2).
- RATE-07: Freeze window check is performed before action execution. If within freeze, execution is `Skipped`.

### 7.4 Max change guardrail

- RATE-08: Automated rate changes respect `Config:Guardrails:MaxDailyChangePercent` (default 20%) and `Config:Guardrails:MaxWeeklyChangePercent` (default 35%) from RA-AI-001 §8.1.
- RATE-09: If the guardrail would be breached, the execution is `Skipped` and logged.

---

## 8. Automation Audit & Observability

### 8.1 Execution log table

See `AutomationExecutionLog` in section 2.6. Status values:

| Status | Description |
|--------|-------------|
| `Scheduled` | Action is queued but not yet executed |
| `Executed` | Action completed successfully |
| `Failed` | Action failed after all retries |
| `Retried` | Transient failure; will retry |
| `Skipped` | Conditions not met, idempotency check hit, or guardrail blocked |
| `DeadLettered` | Failed and exhausted all recovery options |

### 8.2 Retry policy

| Action category | Max retries | Backoff | Timeout |
|----------------|:-----------:|---------|:-------:|
| Message (WhatsApp, SMS, Email) | 3 | Exponential: 30s, 60s, 120s | 30s per attempt |
| Task creation | 2 | Fixed: 5s | 10s |
| Status update | 2 | Fixed: 5s | 10s |
| Rate adjustment | 1 | None | 30s |
| Escalation | 2 | Fixed: 10s | 15s |

- AUD-01: After max retries, the execution transitions to `Failed`.
- AUD-02: `Failed` executions with `ActionType IN ('SEND_WHATSAPP', 'SEND_EMAIL')` are flagged in the staff dashboard alerts.

### 8.3 Dead-letter handling

- AUD-03: `DeadLettered` status is reserved for executions that cannot be retried and require manual intervention.
- AUD-04: Conditions for dead-lettering: entity deleted, tenant suspended, guest opted out after scheduling, template not found.
- AUD-05: Dead-lettered executions appear in the admin "Automation Health" dashboard section.
- AUD-06: Dead-letter retention: 30 days. After that, purged.

### 8.4 Dashboard visibility

The staff and tenant dashboards (RA-DASH-001) include automation-related panels:

| Dashboard | Panel | Data source |
|-----------|-------|-------------|
| Staff | "Automated Tasks" — pending/overdue staff tasks | `StaffTask` WHERE `Status IN ('Pending', 'InProgress')` |
| Staff | "Alerts" — failed automations | `AutomationExecutionLog` WHERE `Status = 'Failed'` and last 24h |
| Tenant | "Automation Activity" — recent executions | `AutomationExecutionLog` grouped by rule, last 7 days |
| Tenant | "Active Rules" — enabled automation rules | `AutomationRule` WHERE `IsActive = true` |
| Admin | "Automation Health" — platform-wide stats | Aggregated `AutomationExecutionLog` |

### 8.5 Automation health metrics

| Metric | Formula | Alert threshold |
|--------|---------|:---------------:|
| Execution success rate (24h) | `Executed / (Executed + Failed + DeadLettered) * 100` | < 95% |
| Average execution latency | `AVG(DurationMs)` for `Executed` status | > 5000ms |
| Pending execution backlog | `COUNT(Status = 'Scheduled' AND CreatedAtUtc < NOW() - 5min)` | > 100 |
| Dead-lettered (24h) | `COUNT(Status = 'DeadLettered')` | > 0 |
| Failed escalations (24h) | `COUNT(ActionType = 'ESCALATE' AND Status = 'Failed')` | > 0 |

### 8.6 Structured log events

| Event | Level | Fields |
|-------|-------|--------|
| `automation.rule.evaluated` | Debug | `{ruleCode, triggerType, entityId, conditionResult}` |
| `automation.action.executed` | Info | `{ruleCode, actionType, entityId, durationMs, result}` |
| `automation.action.failed` | Warn | `{ruleCode, actionType, entityId, error, attemptCount}` |
| `automation.action.skipped` | Info | `{ruleCode, actionType, entityId, reason}` |
| `automation.action.deadlettered` | Error | `{ruleCode, actionType, entityId, reason}` |
| `automation.escalation.triggered` | Info | `{taskId, fromLevel, toLevel, reason}` |
| `automation.ratelimit.hit` | Warn | `{tenantId, limitType, window}` |
| `automation.circuit_breaker.tripped` | Error | `{executionCount, threshold}` |

---

## 9. Tenant-Level Custom Automation Rules

### 9.1 V1 — Predefined templates only

In V1, tenants cannot create arbitrary rules. Instead, they can enable/disable and configure parameters for pre-built rule templates.

| Template | Configurable parameters | Default |
|----------|------------------------|---------|
| Booking confirmation message | Channel (WhatsApp/Email/Both), delay | WhatsApp, immediate |
| Payment reminder | Delay hours, channel | 4h, WhatsApp |
| Check-in reminder | Delay hours before check-in | 24h |
| Check-out reminder | Delay hours before check-out | 4h |
| Review request | Delay hours after check-out, channel | 24h, WhatsApp |
| Cleaning task auto-creation | Lead time hours | 4h |
| No-show detection | Grace hours after check-in | 18h |

- CUS-01: Tenants configure rules via the admin portal "Automations" page (`/automations`).
- CUS-02: Enabling/disabling a rule creates a tenant-scoped `AutomationRule` row (overriding the platform default).
- CUS-03: If no tenant-scoped rule exists, the platform default applies.

### 9.2 V2 — Custom rule builder (future)

- CUS-04: V2 introduces a visual rule builder: "When {trigger} and {conditions} then {actions}."
- CUS-05: V2 custom rules use the same `AutomationRule` entity with `Scope = 'TENANT'`.

### 9.3 Limits

| Limit | Value | Enforcement |
|-------|:-----:|-------------|
| Max active rules per tenant | 20 | Checked on rule enable |
| Max executions per rule per day | 500 | `AutomationExecutionLog` count |
| Max total executions per tenant per day | 2,000 | `AutomationExecutionLog` count |
| Max scheduled items per tenant | 1,000 | `AutomationSchedule` count WHERE `Status = 'Pending'` |

- CUS-06: If a limit is reached, new executions are `Skipped` with reason `TenantLimitExceeded`.
- CUS-07: Limits are configurable via `IOptions<AutomationLimitsSettings>`.

---

## 10. Automation Safety Guardrails

### 10.1 No infinite loops

- SAFE-01: An action MUST NOT produce an event that re-triggers the same rule for the same entity. The `AutomationExecutionLog` idempotency key prevents this.
- SAFE-02: Additionally, the rule evaluator maintains an in-memory "trigger chain" per execution cycle. If a rule has already been evaluated for a given entity in the current cycle, it is skipped.
- SAFE-03: Maximum trigger chain depth: 3. If an action produces an event that triggers a different rule, which triggers another rule, the chain stops at depth 3.

### 10.2 No cascading triggers

- SAFE-04: Events generated by automation actions are tagged with `HeadersJson: {"automationGenerated": true}`.
- SAFE-05: Rules with `TriggerType = 'EVENT'` MUST have a configurable flag `AllowAutomationTrigger` (default `false`). When `false`, the rule ignores events with the `automationGenerated` header.
- SAFE-06: V1 default: only `booking.confirmed` and `booking.cancelled` can trigger automations from automation-generated events (to allow booking status updates to cascade to notifications).

### 10.3 No rate flip-flopping

- SAFE-07: The rate automation rules include a `CooldownMinutes = 360` (6 hours) to prevent rapid price changes.
- SAFE-08: The `RateChangeLog` (RA-AI-001 §8.1.1) is checked before applying rate changes. If more than 2 automation-driven changes in 24 hours for the same date, skip.
- SAFE-09: Combined with RA-AI-001 guardrails (max daily change %, max weekly change %), rate flip-flopping is structurally impossible.

### 10.4 Idempotent action execution

- SAFE-10: Every action execution checks the `AutomationExecutionLog` idempotency key before proceeding.
- SAFE-11: Message actions additionally check `CommunicationLog.IdempotencyKey` (existing mechanism).
- SAFE-12: Task creation checks for existing `StaffTask` with same `BookingId + TaskType + SourceRuleCode` combination.

### 10.5 Execution window limits

- SAFE-13: All time-based triggers have a validity window. If the trigger's `DueAtUtc` is more than `Config:Automation:MaxStaleHours` (default 72 hours) in the past, the schedule is marked `Expired` (not executed).
- SAFE-14: This prevents stale schedules from executing after a prolonged outage (e.g., server was down for 3 days — don't send 3-day-old check-in reminders).

### 10.6 Circuit breaker

- SAFE-15: If more than `Config:Automation:CircuitBreakerThreshold` (default 1,000) executions fail within a 1-hour window (platform-wide), the automation engine pauses for `Config:Automation:CircuitBreakerCooldownMinutes` (default 30 minutes).
- SAFE-16: Circuit breaker state is logged: `automation.circuit_breaker.tripped`.
- SAFE-17: Circuit breaker does NOT affect time-critical automations (payment, overbooking). Only message and rate automations are paused.

---

## 11. Performance & Scale Requirements

### 11.1 Throughput targets

| Metric | Target | Measurement |
|--------|:------:|-------------|
| Max automation executions per minute | 500 | `AutomationExecutionLog` insert rate |
| Max schedules processed per 30s cycle | 50 (existing batch size) | `AutomationSchedulerHostedService` |
| Max event-triggered evaluations per second | 100 | Rule evaluator throughput |
| Max staff tasks created per minute | 200 | `StaffTask` insert rate |

### 11.2 Batch processing model

- PERF-01: The `AutomationSchedulerHostedService` (existing) processes schedules in batches of 50 every 30 seconds.
- PERF-02: The event-triggered rule evaluator processes outbox events inline during outbox dispatch (synchronous consumer).
- PERF-03: The metric-triggered evaluator runs as a separate `MetricAutomationJob` every 4 hours, processing tenants in batches of 500.
- PERF-04: The `EscalationSweepJob` runs every 15 minutes, processing up to 200 overdue tasks per cycle.

### 11.3 Locking strategy

- PERF-05: The `AutomationSchedulerHostedService` uses `UPDLOCK, READPAST` hints when reading due schedules to allow concurrent instances in V2.
- PERF-06: V1 runs a single instance. The lock hint is future-proofing for horizontal scaling.
- PERF-07: `AutomationExecutionLog` insert uses the unique idempotency key as a natural concurrency guard (duplicate key = already processed).

### 11.4 Tenant isolation

- PERF-08: Each automation execution is scoped to a single tenant. No cross-tenant queries in the action executor.
- PERF-09: The rule evaluator loads rules per tenant: first platform defaults, then tenant overrides (merged, tenant wins).
- PERF-10: `StaffTask` has a global query filter on `TenantId` (same pattern as all `ITenantOwnedEntity`).

### 11.5 Execution timeout limits

| Component | Timeout | Consequence |
|-----------|:-------:|-------------|
| Single action execution | 30 seconds | `Failed` after timeout |
| Schedule processing batch | 5 minutes | Log warning, continue with next batch |
| Metric evaluation batch | 30 minutes | Log warning, skip remaining tenants in batch |
| Escalation sweep batch | 5 minutes | Log warning, continue next cycle |

### 11.6 OLTP impact minimization

- PERF-11: Automation queries MUST NOT hold locks for longer than 100ms.
- PERF-12: Automation reads use `NOLOCK` hint where eventual consistency is acceptable (metric evaluations, escalation sweeps).
- PERF-13: Automation writes (execution log, task creation) are batched per `SaveChangesAsync()` call (not row-by-row).
- PERF-14: The automation engine uses a separate `DbContext` scope per batch (short-lived connections).
- PERF-15: Booking creation performance MUST NOT be impacted. Event-triggered automations are asynchronous (via outbox), never synchronous in the booking API call.

---

## 12. Acceptance Criteria & Test Matrix

### 12.1 Given/When/Then acceptance criteria

#### 12.1.1 Booking confirm → message sent

```
GIVEN a booking is created with status 'Lead'
  AND Guest.Phone = '+91-9876543210'
  AND rule BOOKING_CONFIRMED_NOTIFY is active
WHEN the booking status changes to 'Confirmed'
  AND the booking.confirmed event is published to outbox
THEN the automation engine evaluates rule BOOKING_CONFIRMED_NOTIFY
  AND conditions pass (status is Confirmed, phone is not null)
  AND a CommunicationLog entry is created with Channel='whatsapp', EventType='booking.confirmed'
  AND IdempotencyKey prevents duplicate sends
  AND AutomationExecutionLog records Status='Executed'
```

#### 12.1.2 Payment pending → escalation

```
GIVEN a booking was confirmed 24 hours ago
  AND AmountReceived = 0
  AND rule PAYMENT_PENDING_ESCALATE is active with delay = 24h
WHEN the AutomationSchedulerHostedService processes the due schedule
THEN the condition engine verifies AmountReceived < FinalAmount
  AND an escalation notification is sent to the tenant owner
  AND AutomationExecutionLog records ruleCode='PAYMENT_PENDING_ESCALATE', Status='Executed'
```

#### 12.1.3 Cleaning task auto-created

```
GIVEN a booking is confirmed with CheckinDate = tomorrow
  AND there is a check-out today for the same listing
  AND rule TASK_CLEANING_PRE_CHECKIN is active
WHEN the scheduled time (checkin - 4h) arrives
THEN a StaffTask is created with TaskType='Cleaning', Priority='HIGH'
  AND AssignedToRole='staff'
  AND DueAtUtc = checkin - 1h
  AND EscalationAtUtc = checkin - 30min
```

#### 12.1.4 SLA breach → escalation triggered

```
GIVEN a StaffTask with TaskType='Cleaning' created 3 hours ago
  AND Status = 'Pending' (not started)
  AND SLA = 2 hours
  AND EscalationAtUtc has passed
WHEN the EscalationSweepJob runs
THEN EscalationLevel increments from 0 to 1
  AND AssignedToRole changes to 'property_manager'
  AND a notification is sent to the property manager
  AND AutomationExecutionLog records the escalation
```

#### 12.1.5 Retry logic works

```
GIVEN a SEND_WHATSAPP action fails due to provider timeout
  AND AttemptCount = 1 (first attempt)
  AND max retries = 3
WHEN the retry mechanism fires after 30s backoff
THEN the action is re-attempted
  AND if successful, Status = 'Executed'
  AND if all 3 retries fail, Status = 'Failed'
  AND the failure appears in staff dashboard alerts
```

#### 12.1.6 No duplicate message sent

```
GIVEN rule CHECKIN_REMINDER has already executed for Booking #123
  AND AutomationExecutionLog has IdempotencyKey = 'CHECKIN_REMINDER:Booking:123:schedule_456'
WHEN the same schedule is re-processed (e.g., due to worker restart)
THEN the idempotency check finds the existing execution
  AND the action is skipped (Status = 'Skipped')
  AND no duplicate WhatsApp message is sent
```

#### 12.1.7 Rate automation respects freeze window

```
GIVEN Listing L has AutoPriceEnabled = true
  AND a booking exists with CheckinDate = tomorrow
  AND Config:Guardrails:FreezeWindowHours = 48
  AND rule RATE_LOW_OCCUPANCY_DISCOUNT triggers
WHEN the action executor checks the freeze window
THEN the rate change is within the freeze window (< 48h before check-in)
  AND the execution is Skipped with reason 'FreezeWindowActive'
  AND no rate change is applied
```

#### 12.1.8 Manual override blocks automation

```
GIVEN Listing L has a manual rate set 2 hours ago for date D
  AND rule RATE_HIGH_OCCUPANCY_UPLIFT triggers for date D
WHEN the action executor checks for recent manual overrides
THEN a manual rate exists within the 24h recency window
  AND the execution is Skipped with reason 'ManualOverrideActive'
```

### 12.2 Edge case test matrix

| # | Scenario | Expected behavior |
|---|----------|-------------------|
| E1 | Booking cancelled after check-in reminder scheduled | Schedule marked Cancelled. No message sent. |
| E2 | Guest opts out then new booking created | Non-critical messages skipped. Booking confirmation still sent. |
| E3 | Two bookings confirmed simultaneously for same listing | Both get confirmation messages. Overbooking task created for the conflict. |
| E4 | Tenant has 0 active rules (all disabled) | No automations execute. No errors. |
| E5 | Automation engine restarts mid-batch | Unprocessed schedules remain Pending. Processed ones have execution logs. Idempotency prevents re-execution. |
| E6 | Rate limit hit during high booking velocity | Messages deferred (not dropped). New schedules created for later delivery. |
| E7 | Quiet hours: check-in reminder due at 23:00 | Message deferred to 07:00 next day. |
| E8 | Escalation chain: task goes from Level 0 → 3 | Each level gets notification. At Level 3, 48h timeout → DeadLettered. |
| E9 | StaffTask created for cancelled booking | Task marked Cancelled (condition check on booking status). |
| E10 | 100k tenants: metric evaluation job | Processes in batches of 500. Completes within 30 minutes. No OLTP impact. |
| E11 | Automation rule triggers itself (loop) | Idempotency key prevents re-execution. Trigger chain depth limit (3) is a secondary safeguard. |
| E12 | CircuitBreaker trips | Message and rate automations paused. Payment and overbooking automations continue. Auto-recovers after cooldown. |

---

## 13. Definition of Done — Automation Engine V1

### 13.1 Checklist

| # | Criterion | Verification method |
|---|-----------|-------------------|
| 1 | `AutomationRule` entity created with schema from §2.5 | Migration test |
| 2 | `AutomationExecutionLog` entity created with idempotency constraint | Migration test |
| 3 | `StaffTask` entity created with full lifecycle support | Migration test |
| 4 | At least 7 booking lifecycle rules implemented (§3.1) | Unit tests per rule |
| 5 | Booking confirmation → WhatsApp sent successfully | Integration test |
| 6 | Payment reminder sent after configured delay | Integration test with time simulation |
| 7 | Check-in reminder sent 24h before check-in | Integration test |
| 8 | Post-stay review request sent 24h after check-out | Integration test |
| 9 | No-show detection flags booking correctly | Integration test |
| 10 | Cleaning task auto-created before check-in | Integration test |
| 11 | Overbooking resolution task created with CRITICAL priority | Integration test |
| 12 | SLA breach triggers escalation from Level 0 → 1 | Integration test |
| 13 | Escalation chain reaches Level 3 for unresolved tasks | Integration test |
| 14 | Retry logic: failed message retried 3 times with backoff | Unit test |
| 15 | Dead-lettered execution: entity deleted mid-execution | Unit test |
| 16 | Execution log verified: all executions have idempotency key | Query verification |
| 17 | No infinite loop: rule cannot re-trigger itself | Unit test with mock event chain |
| 18 | No cascade: automation-generated events don't trigger non-whitelisted rules | Unit test |
| 19 | Rate automation respects freeze window and max change guardrails | Unit test |
| 20 | Manual override blocks automation for 24h | Unit test |
| 21 | Quiet hours: messages deferred correctly | Unit test |
| 22 | Rate limiting: messages deferred when limit hit | Integration test |
| 23 | Guest opt-out: non-critical messages skipped | Unit test |
| 24 | Execution performance: 500 executions/min under load | Load test |
| 25 | No cross-tenant execution leakage | Security integration test |
| 26 | Staff dashboard shows automated tasks and failed automation alerts | E2E test |
| 27 | Tenant can enable/disable rule templates | E2E test |
| 28 | Circuit breaker trips and recovers correctly | Integration test |

### 13.2 Non-functional requirements

| Requirement | Target | Measurement |
|------------|--------|-------------|
| Automation execution success rate | > 98% | `AutomationExecutionLog` status counts |
| Average execution latency (P95) | < 2 seconds | `DurationMs` in execution log |
| Schedule processing throughput | 50/batch, 30s cycle | `AutomationSchedulerHostedService` metrics |
| Escalation sweep completion | < 2 minutes per cycle | Job duration log |
| Zero duplicate messages | 0 | `CommunicationLog` dedup verification |
| Zero cross-tenant leakage | 0 | Security tests |
| OLTP impact | < 5% increase in avg query latency | Baseline comparison |
| Booking creation latency impact | 0ms (fully async) | API response time unchanged |

---

## Appendix A: Configuration Reference

| Config key | V1 default | Type | Section |
|------------|:----------:|------|:-------:|
| `Automation:Enabled` | `true` | bool | §1.4 |
| `Automation:BookingLifecycleEnabled` | `true` | bool | §1.4 |
| `Automation:StaffTasksEnabled` | `true` | bool | §1.4 |
| `Automation:EscalationEnabled` | `true` | bool | §1.4 |
| `Automation:GuestCommsEnabled` | `true` | bool | §1.4 |
| `Automation:RateAutomationEnabled` | `false` | bool | §1.4 |
| `Automation:TenantCustomRulesEnabled` | `false` | bool | §1.4 |
| `Automation:PaymentReminderHours` | 4 | int | §3.2 |
| `Automation:PaymentEscalationHours` | 24 | int | §3.2 |
| `Automation:CheckinReminderHours` | 24 | int | §3.2 |
| `Automation:CheckoutReminderHours` | 4 | int | §3.2 |
| `Automation:ReviewRequestHours` | 24 | int | §3.2 |
| `Automation:NoShowGraceHours` | 18 | int | §3.2 |
| `Automation:CleaningLeadHours` | 4 | int | §4.2.1 |
| `Automation:LateCheckoutGraceMinutes` | 60 | int | §4.2.3 |
| `Automation:MidStayDays` | 2 | int | §6.1 |
| `Automation:CleaningSlaHours` | 2 | int | §4.4 |
| `Automation:MaintenanceSlaHours` | 24 | int | §4.4 |
| `Automation:LateCheckoutSlaHours` | 1 | int | §4.4 |
| `Automation:PaymentFollowUpSlaHours` | 4 | int | §4.4 |
| `Automation:OverbookingSlaHours` | 2 | int | §4.4 |
| `Automation:NoShowSlaHours` | 12 | int | §4.4 |
| `Automation:AdminEscalationTimeoutHours` | 48 | int | §5.4 |
| `Automation:QuietHoursStart` | `22:00` | TimeOnly | §6.4 |
| `Automation:QuietHoursEnd` | `07:00` | TimeOnly | §6.4 |
| `Automation:MaxStaleHours` | 72 | int | §10.5 |
| `Automation:CircuitBreakerThreshold` | 1000 | int | §10.6 |
| `Automation:CircuitBreakerCooldownMinutes` | 30 | int | §10.6 |
| `Automation:MaxRulesPerTenant` | 20 | int | §9.3 |
| `Automation:MaxExecutionsPerRulePerDay` | 500 | int | §9.3 |
| `Automation:MaxExecutionsPerTenantPerDay` | 2000 | int | §9.3 |
| `Automation:MaxScheduledPerTenant` | 1000 | int | §9.3 |
| `Automation:MaxTriggerChainDepth` | 3 | int | §10.1 |
| `Automation:RateCooldownMinutes` | 360 | int | §10.3 |
| `Automation:GuestMessageLimitPerDay` | 3 | int | §6.3 |
| `Automation:TenantMessageLimitPerHour` | 50 | int | §6.3 |
| `Automation:TenantMessageLimitPerDay` | 500 | int | §6.3 |
| `Automation:PlatformMessageLimitPerMinute` | 100 | int | §6.3 |

---

## Appendix B: Entity Relationship Diagram

```
AutomationRule (platform or tenant scoped)
  │
  ├── Code, TriggerType, TriggerEventType, ConditionsJson, ActionType, ActionConfigJson
  │
  └──── (*) AutomationExecutionLog
              │
              ├── RuleCode, TriggerType, EntityType, EntityId, Status, IdempotencyKey
              │
              ├── → CommunicationLog (for message actions, via IdempotencyKey)
              ├── → StaffTask (for task actions, via SourceExecutionId)
              └── → OutboxMessage (for event actions, via TriggerEventId)

StaffTask (tenant-scoped)
  │
  ├── TaskType, AssignedToRole, Priority, Status, DueAtUtc, EscalationLevel
  │
  ├── → Property (FK)
  ├── → Listing (FK, optional)
  ├── → Booking (FK, optional)
  └── → AutomationExecutionLog (SourceExecutionId, optional)

AutomationSchedule (existing, extended)
  │
  ├── BookingId, EventType, DueAtUtc, Status
  └── Processes → OutboxMessage → Downstream consumers
```

---

*End of RA-AUTO-001*
