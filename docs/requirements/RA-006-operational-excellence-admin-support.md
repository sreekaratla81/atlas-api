# RA-006: Operational Excellence, Admin Tooling & Support Systems

**Addendum to:** [RA-001](RA-001-marketplace-commission-boost-ota-payments.md) | [RA-002](RA-002-governance-scale-monetization-control.md) | [RA-003](RA-003-growth-demand-network-effects.md) | [RA-004](RA-004-risk-fraud-trust-compliance.md) | [RA-005](RA-005-subscription-billing-revenue-control.md)

**Purpose:** Define the internal admin console, safe override controls, support workflows, monitoring dashboards, booking reconciliation, data correction mechanisms, incident management, founder dashboard, and operational automation for the Atlas platform.

**Audience:** Developer, QA, Platform Ops, Support

**Owner:** Atlas Tech Solutions

**Last updated:** 2026-02-27

**Status:** Draft

---

## Table of contents

1. [Internal Admin Console Requirements](#1-internal-admin-console-requirements)
2. [Safe Admin Override Controls](#2-safe-admin-override-controls)
3. [Support Workflow Requirements](#3-support-workflow-requirements)
4. [Sync Health & Monitoring Dashboard](#4-sync-health--monitoring-dashboard)
5. [Booking Reconciliation Engine Requirements](#5-booking-reconciliation-engine-requirements)
6. [Data Correction & Repair Mechanisms](#6-data-correction--repair-mechanisms)
7. [Incident Management Model](#7-incident-management-model)
8. [Operational Metrics for Founder Dashboard](#8-operational-metrics-for-founder-dashboard)
9. [Automation to Reduce Founder Load](#9-automation-to-reduce-founder-load)
10. [Definition of Done — Operational Layer V1](#10-definition-of-done--operational-layer-v1)

---

## 1. Internal admin console requirements

The admin console is a protected section of the atlas-admin-portal accessible only to Atlas team members. It is NOT the tenant admin portal. V1: same SPA, different route group (`/platform/*`), gated by `atlas_admin` Auth0 role.

### 1.1 Search capabilities

| Search type | Input | Results | Endpoint |
|------------|-------|---------|----------|
| **Tenant search** | Name, phone, email, slug, GST number (future), subscription status | Tenant list: Name, Slug, Plan, Status, OwnerPhone, OwnerEmail, CreatedAt | `GET /api/platform/tenants?q={term}&status={status}` |
| **Property search** | Property name, slug, city, tenant name, marketplace status | Property list: Name, Slug, TenantName, City, Status, IsMarketplaceEnabled, CommissionPercent, TrustScore | `GET /api/platform/properties?q={term}&marketplace={bool}` |
| **Booking search** | BookingId, date range, guest phone, payment status, settlement status, booking source | Booking list: Id, TenantName, PropertyName, GuestName, Dates, FinalAmount, CommissionAmount, PaymentStatus, SettlementStatus, BookingSource | `GET /api/platform/bookings?id={id}&from={date}&to={date}&paymentStatus={status}` |
| **Commission history** | TenantId, date range | AuditLog entries: action = `*.commission.*`, with before/after rates | `GET /api/platform/tenants/{id}/commission-history` |
| **Boost history** | PropertyId, date range | AuditLog entries: action = `property.commission.changed`, with boost levels | `GET /api/platform/properties/{id}/boost-history` |
| **OTA sync health** | TenantId or PropertyId | ChannelConfig list: Provider, IsConnected, LastSyncAt, LastSyncError, consecutive failure count | `GET /api/platform/sync-health?tenantId={id}` |
| **Razorpay status** | TenantId | RazorpayLinkedAccountId, KycStatus, TokenExpiresAt, LastOAuthRefreshAt, PaymentMode | `GET /api/platform/tenants/{id}/razorpay-status` |
| **Subscription status** | TenantId | Plan, Status, LockedAtUtc, LockReason, CreditsBalance, CurrentPeriodEnd, NextInvoiceAt, last 5 invoices | `GET /api/platform/tenants/{id}/subscription` |

- ADM-01: All platform endpoints MUST use `IgnoreQueryFilters()` to read cross-tenant data.
- ADM-02: All platform endpoints MUST require the `atlas_admin` role (or more specific role per section 1.2).
- ADM-03: All platform endpoints MUST log access in structured logs: `platform.admin.query` with `{adminUserId, endpoint, parameters}`.

### 1.2 Access levels

| Role | Auth0 role claim | Capabilities | Restrictions |
|------|-----------------|-------------|-------------|
| **Super Admin** | `atlas_super_admin` | Full read + write. All overrides (section 2). User management. Config changes. | None |
| **Finance Admin** | `atlas_finance_admin` | Read all. Settlement retry/resolve. Refund approval. Invoice adjustments. Credit adjustments. | Cannot suspend tenants. Cannot change commission config. Cannot manage users. |
| **Support Agent** | `atlas_support_agent` | Read all. Create support notes. Trigger settlement retry (with Finance Admin approval for amounts > INR 10,000). Send tenant notifications. | Cannot modify commission. Cannot approve refunds > INR 5,000. Cannot suspend. |
| **Read-only Analyst** | `atlas_analyst` | Read all. Export data. Run reconciliation queries. View dashboards. | No write operations. No overrides. |

- ADM-04: Role enforcement MUST use Auth0 role claims validated in a custom authorization policy.
- ADM-05: V1 simplification: `atlas_admin` is the only role (equivalent to Super Admin). Multi-role RBAC is V2. The role structure is defined here for future implementation.
- ADM-06: Every role action MUST include `ActorUserId` in the `AuditLog` entry.

### 1.3 Audit logging for admin actions

Every admin action writes to `AuditLog` (existing model: `Id`, `TenantId`, `ActorUserId`, `Action`, `EntityType`, `EntityId`, `TimestampUtc`, `PayloadJson`).

**For platform-level (cross-tenant) actions, `TenantId = 0` (sentinel for platform scope).**

| Action category | AuditLog.Action pattern | PayloadJson includes |
|----------------|------------------------|---------------------|
| Tenant search/view | `platform.tenant.viewed` | `{tenantId, viewedFields}` |
| Override actions | `admin.override.*` (see section 2) | Before/after state, reason |
| Support notes | `support.note.created` | `{tenantId, bookingId, noteText}` |
| Settlement actions | `admin.settlement.*` | BookingId, PaymentId, action taken |
| Refund actions | `admin.refund.*` | BookingId, amount, reason |
| Suspension actions | `admin.tenant.suspended` / `unlocked` | Reason, trigger |
| Data export | `platform.data.exported` | Entity type, filter criteria, row count |

- ADM-07: AuditLog rows are append-only. No UPDATE or DELETE operations exist for this table.
- ADM-08: AuditLog MUST be queryable by `ActorUserId`, `Action` pattern, `EntityType`, `EntityId`, and date range.
- ADM-09: Admin console MUST display a "My Activity" tab showing the logged-in admin's recent actions.

---

## 2. Safe admin override controls

Every override MUST follow a standard protocol:

1. **Mandatory reason field** — admin must type a reason (min 10 chars) before executing.
2. **Before/after state capture** — `PayloadJson` records the state before and after the change.
3. **Immutable audit log** — `AuditLog` entry with `ActorUserId`, `TimestampUtc`, full payload.
4. **Notification** — tenant is notified of the override via admin portal banner and (optionally) outbox notification.

### 2.1 Commission adjustment

| Field | Detail |
|-------|--------|
| **What** | Override `Tenant.DefaultCommissionPercent` or `Property.CommissionPercent` on behalf of a tenant. |
| **Who** | Super Admin only. |
| **Validation** | Same rules as tenant self-service: floor <= value <= ceiling. Cooldown is bypassed for admin overrides but logged. |
| **Before/after** | `{entityType: 'Tenant|Property', entityId, oldRate, newRate, cooldownBypassed: true|false}` |
| **AuditLog action** | `admin.override.commission` |
| **Notification** | Tenant notified: "Atlas Admin adjusted your commission rate from {old}% to {new}%. Reason: {reason}." |
| **Effect** | Future bookings only. Existing booking snapshots are immutable. |

### 2.2 Boost override

| Field | Detail |
|-------|--------|
| **What** | Force a property's CommissionBoost to a specific value (overriding the formula) or to 0 (suppression). |
| **Who** | Super Admin. |
| **Validation** | Override value: 0.0 to 1.0. Duration: mandatory (1-90 days). After expiry: reverts to formula. |
| **Before/after** | `{propertyId, previousBoost (formula), overrideValue, durationDays, expiresAt}` |
| **AuditLog action** | `admin.override.boost` |
| **Notification** | Tenant notified: "Your property {name} boost has been adjusted by Atlas Admin. Reason: {reason}." |
| **Storage** | New field: `Property.BoostOverrideValue` (decimal?, null = no override), `Property.BoostOverrideExpiresAtUtc` (datetime?). Ranking engine checks: if override is set and not expired, use it instead of formula. |

### 2.3 Refund trigger

| Field | Detail |
|-------|--------|
| **What** | Initiate a refund (full or partial) on a booking. |
| **Who** | Finance Admin or Super Admin. Support Agent for amounts <= INR 5,000. |
| **Validation** | Amount <= original captured amount minus prior refunds. Booking must be in CAPTURED or SETTLED state. Refund reason required (dropdown + free text). |
| **Before/after** | `{bookingId, paymentId, refundAmount, refundType: 'full|partial', reason, previousPaymentStatus, commission reversal amount}` |
| **AuditLog action** | `admin.refund.initiated` |
| **Notification** | Guest notified: "Your refund of INR {amount} has been initiated for booking {ref}. Allow 5-7 business days." Tenant notified: "A refund of INR {amount} has been processed for booking {ref}." |
| **Commission reversal** | Automatic per RA-002 section 3.6 rules. Logged as separate entry. |

### 2.4 Subscription extension

| Field | Detail |
|-------|--------|
| **What** | Extend `CurrentPeriodEndUtc` by N days. Typically used for goodwill gestures or onboarding support. |
| **Who** | Super Admin or Finance Admin. |
| **Validation** | Extension: 1-90 days. Cannot extend a `Canceled` subscription. |
| **Before/after** | `{tenantId, subscriptionId, oldEndDate, newEndDate, extensionDays}` |
| **AuditLog action** | `admin.override.subscription_extension` |
| **Notification** | Tenant notified: "Your subscription has been extended by {days} days. New period end: {date}." |

### 2.5 Grace period extension

| Field | Detail |
|-------|--------|
| **What** | Extend grace period for a tenant whose invoice is overdue. |
| **Who** | Finance Admin or Super Admin. |
| **Validation** | Extension: 1-30 days. Only applicable when tenant is in `PastDue` state or about to be locked. |
| **Before/after** | `{tenantId, subscriptionId, oldGraceDays, newGraceDays, newLockDate}` |
| **AuditLog action** | `admin.override.grace_extension` |
| **Notification** | Tenant notified: "Your payment deadline has been extended to {date}." |
| **Implementation** | Update `TenantSubscription.GracePeriodDays`. Or: directly set `LockedAtUtc` further in the future if already past due. |

### 2.6 Property suspension

| Field | Detail |
|-------|--------|
| **What** | Set a property to HIDDEN or DELISTED state. |
| **Who** | Super Admin. |
| **Validation** | Reason required. Must specify target state (HIDDEN or DELISTED). |
| **Before/after** | `{propertyId, previousState, newState, reason}` |
| **AuditLog action** | `admin.override.property_suspended` |
| **Notification** | Tenant notified: "Your property {name} has been {hidden|delisted} from the marketplace. Reason: {reason}. Contact support for more information." |
| **Reversibility** | Admin can reverse (HIDDEN → ACTIVE, DELISTED → HIDDEN → ACTIVE). Each transition is a separate audit entry. |

### 2.7 Manual payout correction

| Field | Detail |
|-------|--------|
| **What** | Correct a settlement amount when the automated split was wrong (e.g. Razorpay Route transferred incorrect amount). |
| **Who** | Super Admin + Finance Admin (dual acknowledgement in v2; single in v1). |
| **Validation** | BookingId required. Correction amount required. Correction amount + original settled amount must equal the correct HostPayoutAmount. |
| **Before/after** | `{bookingId, paymentId, originalSettledAmount, correctionAmount, newTotalSettled, reason}` |
| **AuditLog action** | `admin.override.payout_correction` |
| **Implementation** | Create a `BillingAdjustment` entry (Type = 'correction', linked to booking). If additional transfer needed: queue a new outbox settlement event for the difference. If overpayment: deduct from tenant's next settlement (logged). |
| **Notification** | Tenant notified: "A payout correction of INR {amount} has been applied to booking {ref}." |

---

## 3. Support workflow requirements

### 3.1 Structured workflows

Each workflow type defines the data the support agent must review, the actions available, and the escalation path.

#### 3.1.1 Booking dispute

| Step | Actor | Action | Data required |
|:----:|-------|--------|---------------|
| 1 | Guest/Host | Reports dispute via email/WhatsApp | BookingId or guest phone |
| 2 | Support Agent | Look up booking in admin console | Booking snapshot, payment status, commission snapshot, settlement status |
| 3 | Support Agent | Review communication log | All CommunicationLog entries for this booking |
| 4 | Support Agent | Review AuditLog | All audit entries for this booking (commission, payment, settlement) |
| 5 | Support Agent | Decide: resolve or escalate | If amount > INR 5,000 or complex: escalate to Finance Admin |
| 6 | Finance Admin | Approve refund or rejection | Refund amount, reason |
| 7 | Support Agent | Execute refund (if approved) | Use refund override (section 2.3) |
| 8 | System | Send notifications | Guest + tenant receive outcome notification |
| 9 | Support Agent | Close case with notes | `support.case.closed` AuditLog with resolution |

#### 3.1.2 Payment failure

| Step | Actor | Action | Data required |
|:----:|-------|--------|---------------|
| 1 | Guest/Tenant | Reports payment issue | BookingId, RazorpayOrderId, or guest phone |
| 2 | Support Agent | Check Payment status | Payment row: Status, RazorpayPaymentId, RazorpayOrderId |
| 3 | Support Agent | Check Razorpay dashboard (external) | Payment captured? Authorized? Failed? |
| 4 | Support Agent | If captured in Razorpay but not in Atlas: trigger webhook replay (section 6.4) | OutboxMessage or webhook replay |
| 5 | Support Agent | If not captured: inform guest to retry | No system action; guest tries again |
| 6 | Support Agent | Close case | `support.case.closed` |

#### 3.1.3 OTA sync mismatch

| Step | Actor | Action | Data required |
|:----:|-------|--------|---------------|
| 1 | Tenant | Reports stale rates on Airbnb/Booking.com | PropertyId, ChannelConfig |
| 2 | Support Agent | Check sync health | `ChannelConfig.LastSyncAt`, `LastSyncError` |
| 3 | Support Agent | If error: check API key validity | Test connection via `POST /api/channel-configs/{id}/test` |
| 4 | Support Agent | If key invalid: guide tenant to re-enter | Instruct tenant via admin portal |
| 5 | Support Agent | If system error: trigger manual sync push | `POST /api/platform/sync/{channelConfigId}/force-push` |
| 6 | Support Agent | Verify rates updated on OTA | Manual check or wait for next sync cycle |
| 7 | Support Agent | Close case | `support.case.closed` |

#### 3.1.4 Commission calculation dispute

| Step | Actor | Action | Data required |
|:----:|-------|--------|---------------|
| 1 | Tenant | Reports incorrect commission on a booking | BookingId |
| 2 | Support Agent | Pull booking details | `CommissionPercentSnapshot`, `CommissionAmount`, `FinalAmount`, `HostPayoutAmount` |
| 3 | Support Agent | Verify calculation | `CommissionAmount == ROUND(FinalAmount * CommissionPercentSnapshot / 100, 2)` |
| 4 | Support Agent | Check tenant's commission rate at booking time | `AuditLog` for `tenant.commission.changed` entries around booking date |
| 5 | Support Agent | If calculation correct: explain to tenant | Show snapshot values |
| 6 | Support Agent | If calculation incorrect (bug): escalate to Super Admin | Follow incident playbook (RA-004 section 6.6) |
| 7 | Super Admin | Issue manual correction | `BillingAdjustment` + credit/debit |

#### 3.1.5 Refund escalation

| Step | Actor | Action |
|:----:|-------|--------|
| 1 | Support Agent | Receives refund request exceeding their authority (> INR 5,000 or post-checkout) |
| 2 | Support Agent | Creates escalation note in AuditLog: `support.refund.escalated` with booking details and recommendation |
| 3 | Finance Admin | Reviews escalation. Approves or rejects with reason. |
| 4 | Support Agent (or Finance Admin) | Executes approved refund via override (section 2.3) |

#### 3.1.6 Boost ranking complaint

| Step | Actor | Action | Data required |
|:----:|-------|--------|---------------|
| 1 | Tenant | "My property should rank higher" or "Competitor is cheating" | PropertyId |
| 2 | Support Agent | Pull ranking score components | BaseQuality, CommissionBoost, ReviewScore, RecencyScore, AvailabilityScore, ConversionRate, TrustScore, TrustMultiplier |
| 3 | Support Agent | Explain ranking formula to tenant | Standard response template |
| 4 | Support Agent | If competitor abuse suspected: check competitor's commission change history | AuditLog for the reported property |
| 5 | Support Agent | If abuse confirmed: escalate to Super Admin for boost override | Section 2.2 |

#### 3.1.7 Tenant onboarding help

| Step | Actor | Action | Data required |
|:----:|-------|--------|---------------|
| 1 | Tenant | Stuck on onboarding step | OnboardingChecklistItem status |
| 2 | Support Agent | Review checklist: which items are Pending? | `GET /api/platform/tenants/{id}/onboarding` |
| 3 | Support Agent | Guide tenant or co-fill data (e.g. create property on behalf) | Admin override with `ActorUserId` logged |
| 4 | Support Agent | Mark onboarding items as complete where applicable | Audit trail |

### 3.2 Required data view per support ticket

When a support agent opens a tenant or booking, the admin console MUST present a unified view:

| Panel | Data source | Content |
|-------|------------|---------|
| **Booking snapshot** | `Booking` + `Guest` + `Listing` + `Property` | All booking fields including CommissionPercentSnapshot, PaymentModeSnapshot, BookingSource, dates, amounts |
| **Commission snapshot** | `Booking` fields | CommissionPercentSnapshot, CommissionAmount, HostPayoutAmount, effective rate at creation |
| **Ledger entries** | `Payment` rows for this BookingId | All payment rows: captures, refunds, chargebacks, settlements with amounts and statuses |
| **OTA sync history** | `ChannelConfig` + structured logs | LastSyncAt, LastSyncError, last 10 sync events from logs |
| **Webhook logs** | Structured logs: `razorpay.webhook.*` | Last 20 webhook events for this tenant, with status and payload summary |
| **Audit log** | `AuditLog` for this entity | All audit entries, newest first, with actor and payload |
| **Tenant context** | `Tenant` + `TenantSubscription` + `EntitlementsSnapshot` | Plan, status, credits, lock status, commission settings |

- SUP-01: All data panels MUST load in a single admin console page per booking/tenant. No navigation to separate pages for each piece of data.
- SUP-02: Sensitive data (Razorpay tokens, API keys) MUST NOT appear in the support view.
- SUP-03: Support agents MUST be able to copy a "debug bundle" (JSON export of all panels) for escalation.

---

## 4. Sync health & monitoring dashboard

### 4.1 Operational health panels

The monitoring dashboard is a page in the admin console (`/platform/health`).

| Panel | Data source | Display | Refresh |
|-------|------------|---------|:-------:|
| **OTA sync success rate** | Structured log: `ota.sync.push` with success/failure | Percentage (last 24h). Sparkline (7 days). | 15 min |
| **Payment webhook success rate** | Structured log: `razorpay.webhook.*` with 200/non-200 | Percentage (last 24h). Sparkline (7 days). | 15 min |
| **Split settlement failure rate** | `Payment` rows where settlement status = Failed / total settlements | Percentage (last 24h). Count. | 15 min |
| **Booking creation error rate** | Structured log: `booking.creation.failed` / `booking.creation.success` | Percentage (last 24h). Sparkline. | 15 min |
| **Ranking computation latency** | Structured log: `ranking.batch.completed` with `{durationMs}` | Last value + p95 (7 days). | On batch completion |
| **Boost abuse alerts** | `AuditLog` where action = `fraud.signal.commission_oscillation` or `fraud.signal.boost_spam` | Count (last 7 days). List of flagged tenants. | 1 hour |
| **Active alerts** | All unresolved alerts from section 4.2 | List with severity, time, and summary | Real-time |
| **Settlement queue depth** | `OutboxMessage` where Topic = `settlement.*` AND Status = 'Pending' | Count | 5 min |

### 4.2 Alert severity levels

| Level | Colour | Response time | Notification method | Examples |
|-------|--------|:------------:|---------------------|---------|
| **CRITICAL** | Red | < 15 min | Email + (V2: SMS/push) | Commission mismatch > INR 100. Settlement failure rate > 20%. Webhook failure rate > 50%. Cross-tenant data access. Payment outage (Razorpay circuit open). |
| **WARNING** | Orange | < 2 hours | Email | Settlement failure rate > 5%. OTA sync failure rate > 30%. Booking anomaly (velocity > 10/hr/property). Grace period expiring for > 10 tenants. |
| **INFO** | Blue | Next business day | Dashboard only (no push notification) | New tenant onboarded. Plan upgrade. Ranking batch completed. Daily reconciliation passed. |

### 4.3 Minimal alerting implementation

| Component | Implementation | Cost |
|-----------|---------------|:----:|
| Log-based alerts | Azure Application Insights alert rules (log query → email) | Free tier (up to 10 rules) |
| Health endpoint | `GET /health` → checks DB, Razorpay reachability, outbox queue depth | Free |
| Uptime check | Cloudflare health check on `/health` (or UptimeRobot free plan) | Free |
| Dashboard | Application Insights workbook OR custom admin console page reading structured logs | Free |

- MON-01: V1: maximum 10 alert rules (Application Insights free tier limit). Prioritise CRITICAL alerts.
- MON-02: No Prometheus, Grafana, ELK, PagerDuty, or OpsGenie. Application Insights + email is sufficient.
- MON-03: Alert email recipient: `ops@atlashomestays.com` (or founder's email in v1).
- MON-04: Each alert rule MUST have a documented runbook reference (link to RA-004 incident playbooks or section 7 of this document).

---

## 5. Booking reconciliation engine requirements

### 5.1 Daily automated checks

A background worker (`ReconciliationWorker`) runs once per day (configurable: `Reconciliation:RunAtUtc`, default `02:00 UTC`).

| Check | Query | Expected result | Alert if |
|-------|-------|-----------------|----------|
| **Commission mismatch** | For each MARKETPLACE_SPLIT booking confirmed yesterday: verify `CommissionAmount == ROUND(FinalAmount * CommissionPercentSnapshot / 100, 2)` | All match | Any mismatch (CRITICAL) |
| **Settlement mismatch** | `SUM(settled transfer amounts)` vs `SUM(HostPayoutAmount)` for settled bookings yesterday | Match within INR 1 | Divergence > INR 1 (CRITICAL) |
| **Duplicate booking** | Bookings where same `ListingId + CheckinDate + CheckoutDate` appears more than once with `BookingStatus IN ('Confirmed', 'CheckedIn')` | Zero duplicates | Any duplicate (WARNING) |
| **Missing payout** | MARKETPLACE_SPLIT bookings confirmed > 72 hours ago with `SettlementStatus NOT IN ('Settled', 'Resolved')` | Zero (all settled or in-progress within SLA) | Any count > 0 (WARNING after 72h, CRITICAL after 7 days) |
| **OTA inventory mismatch** | For each property with Channex connected: compare Atlas availability (from bookings/blocks) with last Channex push | Match | Any mismatch (WARNING) |
| **Zero-amount bookings** | Marketplace bookings with `FinalAmount = 0` or `CommissionAmount = 0` (when rate > 0) | Zero | Any count > 0 (WARNING) |
| **Orphan payments** | `Payment` rows with no matching `Booking` (BookingId FK violation shouldn't be possible, but check for Status inconsistency) | Zero | Any count > 0 (WARNING) |

### 5.2 Reporting format

Reconciliation results are stored as a daily report:

| Field | Type | Example |
|-------|------|---------|
| `ReportDate` | date | 2026-02-27 |
| `CheckName` | string | `commission_mismatch` |
| `Status` | string | `PASS` / `FAIL` / `WARN` |
| `AffectedCount` | int | 0 or count of problematic records |
| `Details` | JSON | Array of `{bookingId, expected, actual, delta}` |
| `GeneratedAtUtc` | datetime | Timestamp |

- REC-01: Reports MUST be stored in a `ReconciliationReport` table (new) for historical tracking. Retained for 1 year.
- REC-02: On any `FAIL` result: CRITICAL alert auto-generated via structured log.
- REC-03: On any `WARN` result: WARNING alert auto-generated.
- REC-04: Admin dashboard shows last 7 days of reconciliation results with pass/fail/warn status per check.
- REC-05: Reconciliation worker MUST complete within 5 minutes (for current scale). Performance target at 100k tenants: < 30 minutes.

---

## 6. Data correction & repair mechanisms

All corrections are idempotent, audit-logged, and reversible where possible.

### 6.1 Recompute commission snapshot (bug fix)

**Context:** `CommissionPercentSnapshot` is write-once. If a bug caused an incorrect value, the snapshot is NEVER overwritten (RA-002 FIN-06). Correction is financial, not data.

| Step | Action |
|:----:|--------|
| 1 | Identify affected bookings by date range and incorrect commission value. |
| 2 | For each booking: compute `CorrectCommission = ROUND(FinalAmount * CorrectRate / 100, 2)`. |
| 3 | Compute `Delta = CorrectCommission - CommissionAmount`. |
| 4 | If `Delta > 0` (Atlas was undercharged): create `BillingAdjustment` debit on tenant for the delta. |
| 5 | If `Delta < 0` (Atlas overcharged): create `BillingAdjustment` credit on tenant for the delta. |
| 6 | Log each adjustment: `admin.correction.commission` with `{bookingId, oldAmount, correctAmount, delta}`. |
| 7 | If settlement already completed: adjustment applied to next settlement or invoiced separately. |

- COR-01: The original `CommissionPercentSnapshot` and `CommissionAmount` fields are NEVER modified. Corrections are additive ledger entries.
- COR-02: Bulk correction tool: admin provides date range + correct rate → system computes all adjustments and presents for approval before executing.

### 6.2 Correct ledger entries

| Scenario | Action |
|----------|--------|
| Duplicate Payment row | Cannot delete (append-only). Create a reversal: `Payment` with `Type = 'correction'`, `Amount = -duplicateAmount`, `Note = 'Reversal of duplicate {paymentId}'`. |
| Incorrect refund amount | Create a correction Payment row: positive (if under-refunded to guest) or negative (if over-refunded). |
| Missing settlement record | Create the missing Payment row: `Type = 'settlement'`, link to booking. Queue new settlement outbox if transfer not yet made. |

- COR-03: Ledger corrections MUST always net to the correct financial position. `SUM(Amount) WHERE BookingId = X` must equal the intended net.
- COR-04: Every correction MUST reference the original entry being corrected (`RelatedPaymentId`).

### 6.3 Retry failed settlements

| Step | Action |
|:----:|--------|
| 1 | Admin views failed settlement in dashboard (SettlementStatus = 'Failed' or 'ManualReview'). |
| 2 | Admin clicks "Retry" with mandatory reason. |
| 3 | System resets attempt count to 0 on the outbox row. Sets `Status = 'Pending'`, `NextAttemptUtc = NOW`. |
| 4 | Settlement worker picks up the row in next cycle and retries the transfer. |
| 5 | If transfer succeeds: `SettlementStatus = 'Settled'`. If fails again: normal retry/backoff. |
| 6 | AuditLog: `admin.settlement.retried` with `{bookingId, paymentId, reason, previousAttemptCount}`. |

- COR-05: Settlement retry MUST be idempotent. If the transfer was already completed on Razorpay's side, Razorpay Route returns the same transfer ID. No double transfer.
- COR-06: Admin can also "Resolve" a failed settlement without retrying (e.g. settled out-of-band). Mandatory notes. `SettlementStatus = 'Resolved'`.

### 6.4 Reprocess OTA webhook / payment webhook

| Scenario | Action |
|----------|--------|
| Razorpay webhook was received but processing failed (exception after signature verification) | Admin triggers replay: `POST /api/platform/webhooks/{webhookLogId}/replay`. System re-reads the stored payload from structured logs and reprocesses. |
| Razorpay webhook was never received (Razorpay delivery failure) | Admin manually fetches payment status from Razorpay API: `POST /api/platform/payments/{razorpayOrderId}/reconcile`. System calls Razorpay `GET /payments?order_id=X`, processes the result as if it were a webhook. |

- COR-07: Webhook replay MUST be idempotent. `ConsumedEvent` table (or `RazorpayPaymentId` uniqueness) prevents duplicate processing.
- COR-08: Manual reconciliation endpoint MUST verify the Razorpay response (not trust cached data).

### 6.5 Replay outbox events

| Scenario | Action |
|----------|--------|
| Outbox event was processed but downstream action failed (e.g. notification not sent) | Admin triggers replay: `POST /api/platform/outbox/{outboxId}/replay`. System resets the outbox row: `Status = 'Pending'`, `AttemptCount = 0`, `NextAttemptUtc = NOW`. |
| Outbox event is stuck in 'Processing' state (worker crash) | Admin triggers reset: same endpoint. Worker's polling query picks up rows with `Status = 'Processing'` and `NextAttemptUtc < NOW - 10min` automatically (stale lock detection). |

- COR-09: Outbox replay MUST check idempotency downstream. For notifications: `CommunicationLog.IdempotencyKey` prevents duplicate sends. For settlements: `RazorpayPaymentId`-based idempotency.
- COR-10: All replay actions MUST log: `admin.outbox.replayed` with `{outboxId, eventType, reason}`.

---

## 7. Incident management model

### 7.1 Severity levels

| Level | Code | Definition | Examples |
|-------|------|-----------|---------|
| **P0** | CRITICAL | Financial impact: money incorrectly charged, lost, or transferred. Data integrity compromised. | Commission bug affecting payouts. Double settlement. Chargeback spike. Data leak. |
| **P1** | HIGH | Booking impact: guests or hosts cannot complete bookings, payments fail, settlements stuck. | Razorpay outage. Booking creation errors. Settlement failure > 20%. |
| **P2** | MEDIUM | Sync/operational delay: OTA rates stale, notifications delayed, ranking stale, non-blocking errors. | Channex outage. Ranking batch timeout. Notification worker crash. |
| **P3** | LOW | Cosmetic or minor: UI glitch, report inaccuracy, non-critical log noise. | Dashboard metric wrong. Email template formatting issue. |

### 7.2 Response time requirements

| Level | Acknowledge | Investigate start | Resolution target | Communication |
|-------|:-----------:|:-----------------:|:-----------------:|:-------------:|
| P0 | 15 min | Immediate | 4 hours | Every 30 min until resolved |
| P1 | 30 min | Within 1 hour | 8 hours | Every 2 hours |
| P2 | 2 hours | Within 4 hours | 24 hours | On resolution |
| P3 | Next business day | Next business day | 1 week | On resolution |

- INC-01: Response times are targets for a single-developer operation. They assume the developer is reachable during business hours (IST 9:00–21:00).
- INC-02: P0 during off-hours: mobile notification (email) expected to wake the developer. No on-call rotation (single person).

### 7.3 Communication protocol

| Audience | P0 | P1 | P2 | P3 |
|----------|----|----|----|----|
| Affected tenants | Within 1 hour (if booking/payment impacted) | Within 4 hours (if impacted) | On resolution | Not notified |
| All tenants | If marketplace-wide impact: status page update | Not notified | Not notified | Not notified |
| Internal (AuditLog) | Immediate: `incident.p0.opened` | `incident.p1.opened` | `incident.p2.opened` | `incident.p3.opened` |

### 7.4 Required audit documentation per incident

| Field | Required for |
|-------|:-----------:|
| `IncidentId` (auto-generated) | All |
| `Severity` (P0-P3) | All |
| `OpenedAtUtc` | All |
| `Description` | All |
| `RootCause` | P0, P1 |
| `AffectedTenants` (count or list) | P0, P1 |
| `AffectedBookings` (count or IDs) | P0 |
| `FinancialImpact` (INR estimate) | P0 |
| `Resolution` | All |
| `ResolvedAtUtc` | All |
| `PostMortem` (document link) | P0 |
| `PreventionActions` | P0, P1 |

- INC-03: V1: incidents are logged as AuditLog entries with `Action = 'incident.{severity}.{opened|resolved}'` and `PayloadJson` containing the fields above.
- INC-04: V2: dedicated `Incident` table for structured tracking and dashboard display.

---

## 8. Operational metrics for founder dashboard

A single-page dashboard at `/platform/dashboard` showing the health of the entire business at a glance.

### 8.1 Marketplace health

| Metric | Formula | Refresh | Display |
|--------|---------|:-------:|---------|
| **GMV** | `SUM(FinalAmount)` for marketplace bookings in period | Daily | INR with trend |
| **Commission revenue** | `SUM(CommissionAmount)` for MARKETPLACE_SPLIT bookings | Daily | INR with trend |
| **Active tenants** | Tenants with `Status IN ('Active', 'Trial')` | Real-time | Count |
| **Marketplace adoption %** | Tenants with >= 1 marketplace-enabled property / Total active tenants | Daily | Percentage |
| **Boost adoption %** | Tenants with `DefaultCommissionPercent > 1.00` / Marketplace-adopted tenants | Daily | Percentage |
| **Average TrustScore** | `AVG(TrustScore)` across all marketplace-active properties | 15 min (batch) | Score (0-1) with colour indicator |

### 8.2 Financial health

| Metric | Formula | Refresh | Display |
|--------|---------|:-------:|---------|
| **MRR** | `SUM(ActiveKeyCount * PricePerKey)` for paid tenants | Daily | INR |
| **Total revenue** | MRR + Commission revenue | Daily | INR |
| **Payment failure %** | Failed payment attempts / Total attempts (last 30 days) | Daily | Percentage (target: < 5%) |
| **Chargeback %** | Chargebacks / Total captured payments (last 30 days) | Daily | Percentage (target: < 1%) |
| **Refund %** | Refund amount / Total captured amount (last 30 days) | Daily | Percentage |
| **Unpaid invoices** | Count of `BillingInvoice WHERE Status = 'Overdue'` | Real-time | Count with total INR |

### 8.3 Operational health

| Metric | Formula | Refresh | Display |
|--------|---------|:-------:|---------|
| **OTA sync failure %** | Failed syncs / Total syncs (last 24h) | 15 min | Percentage (target: < 5%) |
| **Settlement failure %** | Failed settlements / Total settlements (last 7 days) | 15 min | Percentage (target: < 2%) |
| **Webhook success rate** | 200 responses / Total webhooks (last 24h) | 15 min | Percentage (target: > 95%) |
| **Reconciliation status** | Last reconciliation result: all pass / some warn / any fail | Daily | Green/yellow/red indicator |
| **Outbox queue depth** | `OutboxMessage WHERE Status = 'Pending'` | 5 min | Count (target: < 100) |
| **Support ticket volume** | Count of `support.case.*` AuditLog entries in period | Daily | Count with trend |

### 8.4 Dashboard requirements

- DASH-01: All metrics MUST be on a single page without scrolling (above the fold) on a standard desktop display. Use cards with KPI values and small sparklines.
- DASH-02: Each metric card MUST show: current value, trend vs. previous period (up/down arrow + percentage), and target threshold (if applicable).
- DASH-03: Red/yellow/green colour coding for metrics that have targets.
- DASH-04: Date range selector: Today, Last 7 days, Last 30 days, This month, Custom.
- DASH-05: "Refresh" button for on-demand data reload. Auto-refresh every 5 minutes.

---

## 9. Automation to reduce founder load

All automations are rule-based. No ML. Each is gated by a feature flag (default ON unless stated).

### 9.1 Auto-suspend unpaid tenants

| Trigger | `CurrentPeriodEndUtc + GracePeriodDays < NOW` AND `Status = 'PastDue'` AND no payment received |
|---------|---|
| **Action** | Set `TenantSubscription.Status = 'Suspended'`, `LockedAtUtc = NOW`, `LockReason = 'InvoiceOverdue'`. |
| **Worker** | `BillingWorker` (daily run). |
| **Flag** | `Automation:AutoSuspendEnabled` (default: true). |
| **Audit** | `automation.tenant.suspended` with `{tenantId, reason, invoiceId}`. |
| **Notification** | Tenant: "Your account has been suspended due to unpaid invoice. Pay now to restore access." |

### 9.2 Auto-disable boost below TrustScore threshold

| Trigger | Property `TrustScore < 0.40` (computed in ranking batch) |
|---------|---|
| **Action** | `Property.BoostOverrideValue = 0.0`, `Property.BoostOverrideExpiresAtUtc = NOW + 30 days`. CommissionBoost forced to 0 in ranking. |
| **Worker** | Ranking batch job (every 15 min). |
| **Flag** | `Automation:AutoBoostSuppressEnabled` (default: true). |
| **Audit** | `automation.boost.suppressed` with `{propertyId, trustScore}`. |
| **Notification** | Tenant: "Boost has been temporarily disabled for {property} due to quality concerns. Improve your listing quality and response time to restore boost eligibility." |
| **Recovery** | When TrustScore recovers to >= 0.50: `BoostOverrideValue = null` (reverts to formula). |

### 9.3 Auto-retry failed webhooks

| Trigger | `OutboxMessage` with `Status = 'Failed'` AND `AttemptCount < 10` AND `EventType LIKE 'webhook.%'` |
|---------|---|
| **Action** | Reset `Status = 'Pending'`, increment attempt, set `NextAttemptUtc` with exponential backoff (max 1 hour). |
| **Worker** | Outbox materializer (existing, every 30 seconds). |
| **Flag** | `Automation:AutoRetryWebhooksEnabled` (default: true). |
| **Audit** | Structured log: `automation.webhook.retried` with `{outboxId, attemptCount}`. |
| **Escalation** | After 10 attempts: mark `Status = 'Failed'` permanently. Generate WARNING alert. |

### 9.4 Auto-detect abnormal commission flips

| Trigger | Tenant changes `DefaultCommissionPercent` or `Property.CommissionPercent` > 3 times in 7 days (from AuditLog) |
|---------|---|
| **Action** | Generate structured log alert: `fraud.signal.commission_oscillation`. Add to "Boost Abuse Alerts" dashboard panel. No auto-suspension. |
| **Worker** | Daily batch job (or computed on commission change). |
| **Flag** | `Automation:CommissionFlipDetectionEnabled` (default: true). |
| **Audit** | Structured log only (no AuditLog row — this is a signal, not an action). |
| **Escalation** | Admin reviews. May apply commission lock via override (section 2.1). |

### 9.5 Auto-flag suspicious booking velocity

| Trigger | > 10 confirmed bookings for a single property in 1 hour, OR > 5 bookings from same guest phone in 1 day across all properties |
|---------|---|
| **Action** | Generate structured log alert: `fraud.signal.booking_velocity`. Flagged in dashboard. |
| **Worker** | Computed on booking confirmation (inline check). |
| **Flag** | `Automation:BookingVelocityDetectionEnabled` (default: true). |
| **Audit** | Structured log + `fraud.signal.booking_velocity` AuditLog entry with `{propertyId or guestPhone, count, timeWindow}`. |
| **Escalation** | Admin reviews. May cancel fraudulent bookings and rate-limit the source. |

### 9.6 Automation summary

| Automation | Frequency | Action type | Human follow-up needed? |
|-----------|-----------|------------|:-----------------------:|
| Auto-suspend unpaid | Daily | State change | No (unless tenant contacts support) |
| Auto-suppress boost | Every 15 min | Override | No (auto-recovers) |
| Auto-retry webhooks | Every 30 sec | Retry | Only if max retries exceeded |
| Commission flip detection | On change / daily | Alert only | Yes (admin review) |
| Booking velocity detection | On booking | Alert only | Yes (admin review) |

---

## 10. Definition of Done — operational layer V1

This checklist MUST be fully satisfied before the operational layer is considered launch-ready.

### Admin override tested

- [ ] Commission adjustment override: admin changes tenant commission → AuditLog entry with before/after and reason.
- [ ] Refund trigger: admin initiates refund → Payment ledger entry created. Commission reversed per rules. Guest and tenant notified.
- [ ] Subscription extension: admin extends period → new end date reflected. AuditLog created.
- [ ] Grace period extension: admin extends grace → lock date pushed. AuditLog created.
- [ ] Property suspension: admin hides property → property no longer in marketplace search. Tenant notified. AuditLog created.
- [ ] Payout correction: admin creates correction → BillingAdjustment entry. Logged.
- [ ] All overrides require non-empty reason field (validation tested with empty string → rejected).

### Booking reconciliation tested

- [ ] Commission mismatch check: inject a booking with wrong CommissionAmount → reconciliation flags it as FAIL.
- [ ] Settlement mismatch check: mark a settlement as completed with wrong amount → reconciliation flags FAIL.
- [ ] Duplicate booking check: create two confirmed bookings for same listing + dates → flagged as WARN.
- [ ] Missing payout check: MARKETPLACE_SPLIT booking > 72h without settlement → flagged as WARN.
- [ ] OTA inventory mismatch check: simulate stale push → flagged as WARN.
- [ ] Reconciliation results stored in `ReconciliationReport` table. Visible in admin dashboard.
- [ ] CRITICAL results auto-generate email alerts.

### Settlement retry tested

- [ ] Failed settlement in admin dashboard. Admin clicks "Retry" → outbox row reset, worker retries.
- [ ] If transfer already completed on Razorpay: idempotent (same transfer ID returned, no double payout).
- [ ] Admin "Resolve" with notes: settlement marked resolved, AuditLog created.
- [ ] AuditLog captures all retry and resolution actions with reason.

### Audit log immutability verified

- [ ] AuditLog table has no UPDATE/DELETE endpoints or stored procedures.
- [ ] Integration test: attempt to UPDATE an AuditLog row via raw SQL → blocked by application policy (no raw SQL path exists).
- [ ] AuditLog entries are queryable by ActorUserId, Action, EntityType, date range.
- [ ] All admin override actions produce an AuditLog entry (verified for each override type).
- [ ] Platform-level actions use `TenantId = 0` sentinel.

### Incident severity classification tested

- [ ] P0 scenario (simulated commission bug): alert generated within 15 minutes. AuditLog: `incident.p0.opened`.
- [ ] P1 scenario (simulated payment outage): alert generated. Circuit breaker triggers.
- [ ] P2 scenario (simulated OTA outage): alert generated after threshold.
- [ ] P3 scenario: logged but no alert pushed.
- [ ] Incident resolution logged: `incident.{severity}.resolved` with all required fields.

### Support workflow dry run completed

- [ ] Booking dispute: support agent can view unified booking data (all 7 panels). Can create escalation note. Can trigger refund (within authority).
- [ ] Payment failure: support agent can identify the issue, trigger webhook replay or manual reconciliation.
- [ ] OTA sync mismatch: support agent can view sync health, test connection, force push.
- [ ] Commission dispute: support agent can verify calculation, pull audit history, explain to tenant.
- [ ] Onboarding help: support agent can view checklist status, guide or co-fill.
- [ ] Debug bundle export: JSON with all support panels downloadable.

### Dashboard and monitoring verified

- [ ] Founder dashboard shows all 18 metrics (6 marketplace + 6 financial + 6 operational) on a single page.
- [ ] Metrics are colour-coded (green/yellow/red) against targets.
- [ ] Date range selector works.
- [ ] Auto-refresh works (5-minute interval).
- [ ] At least 5 CRITICAL alert rules configured in Application Insights and tested.
- [ ] Health endpoint (`/health`) returns 200 when all dependencies are reachable.

### Automation verified

- [ ] Auto-suspend: unpaid tenant suspended after grace period. No manual intervention required.
- [ ] Auto-suppress boost: property with TrustScore < 0.40 has boost disabled. Recovers when TrustScore >= 0.50.
- [ ] Auto-retry webhooks: failed outbox event retried with backoff. Stops after 10 attempts.
- [ ] Commission flip detection: > 3 changes in 7 days → alert logged.
- [ ] Booking velocity detection: > 10 bookings/hr for a property → alert logged.
- [ ] All automations have feature flags and can be disabled via config.

---

## Glossary

| Term | Definition |
|------|-----------|
| **Admin console** | Internal-only section of the admin portal (`/platform/*`), accessible to Atlas team with `atlas_admin` role. |
| **Override** | A manual admin action that changes a system value, requiring mandatory reason and producing an immutable audit trail. |
| **Debug bundle** | A JSON export of all support data panels for a specific booking or tenant, used for escalation. |
| **Reconciliation** | Daily automated comparison of expected vs actual financial, booking, and inventory data. |
| **Ledger correction** | An additive entry that adjusts the financial position without modifying original records. |
| **Outbox replay** | Resetting a failed or stuck outbox event to `Pending` so the worker reprocesses it. |
| **Webhook replay** | Re-reading a stored webhook payload and reprocessing it through the normal handler. |
| **Founder dashboard** | A single-page view of all business-critical metrics for the Atlas founder/operator. |
| **Feature flag** | A config key that enables/disables a specific automation or feature without code deployment. |
| **Stale lock** | An outbox row stuck in `Processing` status due to a worker crash; detected and recovered automatically. |
