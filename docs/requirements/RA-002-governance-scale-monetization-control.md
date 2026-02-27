# RA-002: Governance, Scale & Control Layer Requirements

**Addendum to:** [RA-001](RA-001-marketplace-commission-boost-ota-payments.md) | [HLD](HLD-marketplace-commission-engine.md) | [LLD](LLD-marketplace-commission-engine.md)

**Purpose:** Define the governance, anti-abuse, financial integrity, reputation, scale, monetization evolution, vendor abstraction, data ownership, operational playbooks, disaster recovery, and launch readiness requirements for the Atlas marketplace.

**Audience:** Developer, QA, Support, Platform Ops

**Owner:** Atlas Tech Solutions

**Last updated:** 2026-02-27

**Status:** Draft

---

## Table of contents

1. [Multi-Tenant Isolation Requirements](#1-multi-tenant-isolation-requirements)
2. [Commission Abuse Prevention & Anti-Gaming](#2-commission-abuse-prevention--anti-gaming)
3. [Financial Integrity Controls](#3-financial-integrity-controls)
4. [Marketplace Reputation & Trust Layer V1](#4-marketplace-reputation--trust-layer-v1)
5. [Scale to 100k Tenants Architecture Requirements](#5-scale-to-100k-tenants-architecture-requirements)
6. [Monetization Evolution Strategy](#6-monetization-evolution-strategy)
7. [Vendor Abstraction Hardening](#7-vendor-abstraction-hardening)
8. [Data Ownership & Exit Strategy](#8-data-ownership--exit-strategy)
9. [Operational Playbooks](#9-operational-playbooks)
10. [Disaster & Worst Case Scenarios](#10-disaster--worst-case-scenarios)
11. [Definition of Done for Marketplace V1](#11-definition-of-done-for-marketplace-v1)

---

## 1. Multi-tenant isolation requirements

### 1.1 Data isolation strategy

Atlas uses a **shared-database, shared-schema, row-level isolation** model. Every tenant-owned entity implements `ITenantOwnedEntity` (property: `TenantId`). EF Core global query filters (`HasQueryFilter`) automatically scope all reads to the resolved tenant.

**Current enforcement (already in codebase):**

- `AppDbContext.ApplyTenantQueryFilter<T>()` applied to all 30+ tenant-owned entity types.
- `SaveChangesAsync` override auto-sets `TenantId` on Added entities, blocks TenantId modification on Updated entities, and validates single-tenant batches.
- `TenantResolutionMiddleware` resolves tenant from `X-Tenant-Slug` header (or dev fallback).

**Requirements (MUST-level):**

| ID | Requirement | Enforcement |
|----|-------------|-------------|
| ISO-01 | Every new entity that holds tenant data MUST implement `ITenantOwnedEntity`. | Code review + compiler error (missing property) |
| ISO-02 | Every tenant-owned DbSet MUST have `ApplyTenantQueryFilter` registered in `OnModelCreating`. | Integration test: `TenantOwnershipTests` verifies filter registration |
| ISO-03 | Cross-tenant writes MUST be rejected with 403 in `SaveChangesAsync`. | Existing enforcement in `AppDbContext` |
| ISO-04 | Background workers processing outbox/schedule rows MUST use `IgnoreQueryFilters()` to read cross-tenant, but MUST read TenantId from the row and scope all downstream operations. | Worker code review + integration test |
| ISO-05 | Marketplace public endpoints (e.g. `GET /marketplace/properties`) MUST use `IgnoreQueryFilters()` but MUST filter by `IsMarketplaceEnabled = true`, `Property.Status = 'Active'`, tenant subscription active. MUST NOT expose TenantId in public DTOs. | Integration test |
| ISO-06 | Platform admin endpoints (e.g. `GET /platform/tenants`) MUST use `IgnoreQueryFilters()` and MUST require Atlas Admin role. | Role-based auth + integration test |

### 1.2 Token storage isolation

| Token type | Storage | Isolation | Access |
|-----------|---------|-----------|--------|
| Razorpay OAuth tokens | `Tenant.RazorpayAccessTokenEncrypted` / `RefreshTokenEncrypted` | Row-level (TenantId on Tenant row itself) | Decrypted only by `RazorpayOAuthService` for that tenant |
| Channex API keys | `ChannelConfig.ApiKey` | Row-level (TenantId + PropertyId) | Read only by channel sync for that property |
| Auth0 JWT | Stateless (header) | Scoped by Auth0 tenant audience | Validated per request |

**Requirements:**

- ISO-07: Razorpay tokens MUST be encrypted at rest via `IDataProtector` (purpose: `"RazorpayOAuthTokens"`). MUST NOT appear in API responses, logs, or error messages.
- ISO-08: Channex API keys SHOULD be encrypted at rest in v2. V1: stored as plaintext in `ChannelConfig.ApiKey` (current behaviour). MUST NOT appear in public API responses.
- ISO-09: Token decryption MUST verify TenantId matches the request context before use.

### 1.3 Cross-tenant access prevention

| Vector | Mitigation |
|--------|-----------|
| API parameter tampering (e.g. `?tenantId=other`) | TenantId resolved from header, never from request body/query. `SaveChangesAsync` override rejects mismatches. |
| Direct DB query bypass | All repository access goes through EF Core with global filters. No raw SQL in controllers. |
| Worker cross-tenant leak | Workers read TenantId from the row being processed. All downstream queries scoped by that TenantId. |
| Marketplace endpoint leak | Public DTOs exclude TenantId, internal IDs. Only slugs and public fields exposed. |

### 1.4 Soft delete vs hard delete rules

Atlas does NOT use soft delete. All deletes are hard deletes.

| Entity | Delete policy | Rationale |
|--------|--------------|-----------|
| Booking | MUST NOT delete if PaymentStatus != 'pending'. Cancel instead. | Financial record integrity |
| Payment | MUST NOT delete ever. | Financial audit requirement |
| CommunicationLog | MUST NOT delete ever. | Audit trail |
| AuditLog | MUST NOT delete ever. | Compliance |
| CommissionPercentSnapshot (booking field) | MUST NOT modify or delete. | Immutability invariant |
| Property | MAY delete if no bookings reference it. | Cleanup for drafts |
| Listing | MAY delete if no bookings reference it. | Cleanup for drafts |
| Guest | MAY delete if no bookings reference them (GDPR future). | Data minimization |
| Tenant | MUST NOT hard delete. Set `IsActive = false` (suspension). | Data export/legal holds |

### 1.5 Tenant suspension logic

**Existing mechanism:** `BillingLockFilter` blocks mutating requests (POST/PUT/PATCH/DELETE) when `TenantSubscription` is locked. Returns HTTP 402 with `TENANT_LOCKED` code, reason, and pay link.

**Subscription status lifecycle:**

```
Trial → Active → PastDue → Suspended → Canceled
                    ↓
               (grace period)
                    ↓
               Suspended (locked)
```

**Lock reasons (existing):** `CreditsExhausted`, `InvoiceOverdue`, `Manual`, `ChargeFailed`.

**Requirements for marketplace integration:**

| ID | Requirement |
|----|-------------|
| SUS-01 | When tenant is Suspended or Canceled: marketplace properties MUST NOT appear in `GET /marketplace/properties`. |
| SUS-02 | When tenant is Suspended: existing confirmed bookings MUST still be honoured (guest experience unaffected). |
| SUS-03 | When tenant is Suspended: `MARKETPLACE_SPLIT` settlement for existing bookings MUST continue (settlement worker must not skip locked tenants). |
| SUS-04 | When tenant is Suspended: new bookings MUST be blocked (BillingLockFilter returns 402). |
| SUS-05 | When tenant is reactivated (pays invoice): marketplace properties MUST reappear within 15 minutes (ranking cache TTL). |

### 1.6 Subscription expiry and commission enforcement

- SUS-06: Commission snapshot on existing bookings MUST be honoured regardless of subscription status. Atlas's commission is earned at booking creation; expiry of the tenant's subscription does not void it.
- SUS-07: If tenant is in `Trial` or `Active` status, commission and marketplace features function normally.
- SUS-08: If tenant is in `PastDue` (grace period), marketplace features continue but admin portal shows a warning banner.

### 1.7 Back-office override rules

| Override | Who | Audit | Constraints |
|----------|-----|-------|-------------|
| Manually lock tenant | Atlas Admin | `AuditLog`: action = `admin.tenant.locked`, reason | Any time; immediate |
| Manually unlock tenant | Atlas Admin | `AuditLog`: action = `admin.tenant.unlocked` | Must resolve underlying issue first |
| Override tenant commission | Atlas Admin | `AuditLog`: action = `admin.commission.override`, old/new | Only forward-looking; existing bookings unaffected |
| Force settlement retry | Atlas Admin | `AuditLog`: action = `admin.settlement.retried` | Resets attempt count, re-queues outbox |
| Mark settlement resolved | Atlas Admin | `AuditLog`: action = `admin.settlement.resolved`, notes | Mandatory notes field |

### 1.8 Acceptance criteria

| ID | Given | When | Then |
|----|-------|------|------|
| AC-ISO-01 | Tenant A creates a property | Tenant B queries properties | Tenant B sees only their own properties |
| AC-ISO-02 | Tenant A is Suspended | Guest searches marketplace | Tenant A's properties do not appear |
| AC-ISO-03 | Tenant A is Suspended, has confirmed MARKETPLACE_SPLIT booking | Settlement worker runs | Settlement proceeds (not blocked by suspension) |
| AC-ISO-04 | Tenant A is Suspended | Tenant A tries to create a booking | 402 TENANT_LOCKED |
| AC-ISO-05 | Tenant A reactivates | Guest searches marketplace within 15 min | Tenant A's marketplace-enabled properties reappear |

### 1.9 Failure scenarios

| Scenario | Expected behaviour |
|----------|-------------------|
| TenantId = 0 (fallback) in production | `BillingLockFilter` skips (no tenant resolved); mutation may proceed. MUST be caught by integration tests that verify tenant resolution is mandatory for all tenant-scoped endpoints. |
| Worker processes outbox row with TenantId for a deleted tenant | Worker MUST log warning, mark outbox row Failed, continue. MUST NOT crash. |
| Concurrent requests from two tenants in same DB transaction | EF Core global filters + auto-set TenantId prevent cross-contamination. No shared transaction across tenants. |

---

## 2. Commission abuse prevention & anti-gaming

### 2.1 Frequent commission flipping

**Threat:** Tenant raises commission before peak hours to boost ranking, lowers it after.

| ID | Guardrail | Implementation |
|----|-----------|----------------|
| AG-01 | Commission change cooldown: 24 hours between changes to `Tenant.DefaultCommissionPercent`. | API rejects with 429 if last change was < 24h ago. `Tenant.CommissionLastChangedAtUtc` column. |
| AG-02 | Property override cooldown: 24 hours between changes to `Property.CommissionPercent`. | Same pattern. `Property.CommissionLastChangedAtUtc` column. |
| AG-03 | Ranking engine ignores commission changes < 1 hour old (uses previous value). | Ranking cache key includes commission timestamp; stale value used for < 1h. |
| AG-04 | Frequency alert: > 3 commission changes in 7 days flags tenant for review. | Structured log alert. No auto-action in v1. |

### 2.2 Minimum boost duration

- AG-05: Once a property override is set, it MUST remain for at least 24 hours before it can be lowered or cleared (cooldown from AG-02).
- AG-06: Lowering commission does NOT immediately lower ranking (1h delay per AG-03).

### 2.3 Artificial commission spikes

**Threat:** Tenant briefly sets 20% commission for ranking spike, then reverts.

- AG-07: Ranking damping: if commission was at level X for < 7 days, the ranking engine uses `DampedCommission = PreviousStableRate + (CurrentRate - PreviousStableRate) * DaysSinceChange / 7`. This linearly ramps the boost over 7 days.
- AG-08: "Stable rate" defined as a rate held for >= 7 consecutive days.

### 2.4 Self-booking

**Threat:** Tenant books their own property to boost recency score.

| ID | Guardrail |
|----|-----------|
| AG-09 | Bookings where `Guest.Email == Tenant.OwnerEmail` or `Guest.Phone == Tenant.OwnerPhone` MUST NOT count toward RecencyScore in ranking. |
| AG-10 | Admin report: flag bookings where guest contact matches tenant contact. No auto-block in v1, but logged for review. |

### 2.5 Fake reviews

| ID | Guardrail |
|----|-----------|
| AG-11 | Reviews MUST require a completed (CheckedOut) booking by the reviewing guest. Enforced at API level. |
| AG-12 | One review per guest per booking. Duplicate attempts return 409. |
| AG-13 | Reviews from guests whose contact matches tenant contact MUST be flagged and excluded from ReviewScore. |
| AG-14 | Review moderation queue (v2): Atlas Admin can flag/remove reviews. V1: reviews are accepted if they meet AG-11 and AG-12. |

### 2.6 Boost misuse

| ID | Guardrail |
|----|-----------|
| AG-15 | Commission weight in ranking is capped at 0.30 (30% of total score). Configurable via `RankingConstants.MaxCommissionWeight`. |
| AG-16 | CommissionBoost forced to 0.0 if `BaseQuality < 0.50` (minimum quality threshold). |
| AG-17 | CommissionBoost forced to 0.0 if property has >= 2 guest complaints in last 30 days (v2; placeholder in v1). |

### 2.7 Commission change audit logging

Every commission change MUST write to `AuditLog`:

```json
{
  "action": "tenant.commission.changed | property.commission.changed",
  "entityType": "Tenant | Property",
  "entityId": "42 | 7",
  "actorUserId": 3,
  "payloadJson": {
    "oldRate": 1.00,
    "newRate": 5.00,
    "cooldownEndsAt": "2026-03-02T10:00:00Z"
  }
}
```

### 2.8 Fraud detection hooks (future)

Not implemented in v1 but architecture MUST support:

- AG-18: Anomaly detection service can subscribe to commission change events (via outbox) and flag patterns.
- AG-19: Booking pattern analysis (same guest across multiple tenant properties).
- AG-20: Review sentiment analysis for spam detection.

---

## 3. Financial integrity controls

### 3.1 Booking snapshot immutability

| Field | Rule | Enforcement |
|-------|------|-------------|
| `CommissionPercentSnapshot` | Write-once at booking creation. No UPDATE. | No PUT/PATCH endpoint modifies this field. Integration test verifies. |
| `CommissionAmount` | Write-once at booking creation. Recalculated only on refund (as a separate refund ledger entry, never overwriting the original). | Same |
| `HostPayoutAmount` | Write-once at booking creation. | Same |
| `PaymentModeSnapshot` | Write-once at booking creation. | Same |

- FIN-01: Any attempt to modify a snapshot field MUST be rejected. There is no API surface that allows it.
- FIN-02: Direct SQL updates to snapshot fields MUST be prohibited by operational policy (documented, not enforced technically).

### 3.2 Ledger consistency checks

| Check | Query | Frequency | Alert if |
|-------|-------|-----------|----------|
| Commission sum matches | `SUM(CommissionAmount) WHERE PaymentModeSnapshot = 'MARKETPLACE_SPLIT' AND BookingStatus = 'Confirmed'` vs. settlement records | Daily | Mismatch > INR 1 |
| Host payout sum matches | `SUM(HostPayoutAmount)` vs. sum of settled transfers | Daily | Mismatch > INR 1 |
| Orphan settlements | Outbox rows with `settlement.requested` where no matching Payment exists | Hourly | Any count > 0 |
| Double settlement | Multiple settled transfers for same BookingId | Continuous (idempotency key) | Any duplicate |

### 3.3 Reconciliation rules

- FIN-03: Daily automated reconciliation MUST compare: (a) Atlas Razorpay account credits for the day vs. (b) sum of `FinalAmount` for confirmed MARKETPLACE_SPLIT bookings that day.
- FIN-04: Settlement transfers sum MUST equal sum of `HostPayoutAmount` for settled bookings.
- FIN-05: Discrepancies > INR 100 MUST generate a Critical alert.

### 3.4 Commission recalculation prevention

- FIN-06: Commission MUST NOT be recalculated after booking creation. If the effective rate was wrong at creation time (bug), the correct remediation is a manual adjustment via refund/credit, not a retroactive recalculation.
- FIN-07: No batch job, migration, or admin tool MUST recalculate existing `CommissionAmount` values.

### 3.5 Manual override logging

| Action | Logged to | Required fields |
|--------|-----------|-----------------|
| Admin retries settlement | `AuditLog` | BookingId, PaymentId, AdminUserId, Reason |
| Admin marks settlement resolved | `AuditLog` | BookingId, PaymentId, AdminUserId, Notes (mandatory), Resolution method |
| Admin adjusts tenant commission | `AuditLog` | TenantId, OldRate, NewRate, AdminUserId |
| Admin issues manual refund | `AuditLog` + `Payment` (negative row) | BookingId, Amount, Reason, AdminUserId |

### 3.6 Refund commission handling

| Scenario | Commission treatment | Host payout treatment |
|----------|---------------------|-----------------------|
| Full refund, HOST_DIRECT | No commission reversal (was reporting-only). | Not applicable (host received payment directly). |
| Full refund, MARKETPLACE_SPLIT, settled | Atlas refunds guest from Atlas account. Commission reversal entry: `CommissionRefunded = CommissionAmount`. Host settlement reversal: `HostPayoutRefunded = HostPayoutAmount`. | Reverse via Razorpay Route reversal or deduct from next settlement. |
| Full refund, MARKETPLACE_SPLIT, not yet settled | Atlas refunds guest. Cancel pending settlement outbox. | No host transfer occurred; nothing to reverse. |
| Partial refund, MARKETPLACE_SPLIT | `RefundedCommission = RefundAmount * CommissionPercentSnapshot / 100`. `RefundedHostShare = RefundAmount - RefundedCommission`. | Reverse `RefundedHostShare` from host. |
| Cancellation (no refund) | Commission earned; no reversal. | No reversal. Booking marked Cancelled. |

### 3.7 Chargeback handling

- FIN-08: If Razorpay notifies a chargeback (via webhook `payment.dispute.created`): mark booking as `Disputed`. Freeze settlement if not yet settled. Alert Atlas Admin.
- FIN-09: Atlas Admin must resolve: either accept chargeback (reverse commission + host payout) or contest (via Razorpay dashboard).
- FIN-10: Chargeback events MUST be logged to AuditLog with full dispute details.
- FIN-11: V1 scope: manual handling only. No automated chargeback processing.

### 3.8 Split settlement failure state machine

(Defined in RA-001 section 4.4; referenced here for completeness.)

```
OrderCreated → PaymentCaptured → SettlementQueued → SettlementInitiated
  → Settled (success)
  → SettlementFailed → SettlementInitiated (auto-retry, attempt < 5)
  → SettlementFailed → ManualReview (attempts exhausted)
  → ManualReview → SettlementInitiated (admin retry)
  → ManualReview → Resolved (admin closes)
```

- FIN-12: Guest payment is NEVER reversed due to settlement failure. Settlement is an Atlas-host concern.
- FIN-13: Max auto-retries: 5. Backoff: exponential (1m, 2m, 4m, 8m, 16m).
- FIN-14: After max retries: outbox row marked Failed, alert generated, displayed in settlement dashboard.

### 3.9 Idempotency rules for payment callbacks

| Callback | Idempotency key | Behaviour on duplicate |
|----------|-----------------|----------------------|
| Razorpay `payment.captured` webhook | `RazorpayPaymentId` | Skip if already processed (existing logic) |
| Razorpay `payment.failed` webhook | `RazorpayPaymentId` | Skip if already processed |
| Settlement transfer | `settlement:{BookingId}:{PaymentId}` | Razorpay Route returns same transfer ID; idempotent |
| Refund | `refund:{BookingId}:{RefundAmount}:{ReasonHash}` | Skip if CommunicationLog/Payment row exists |

### 3.10 Required financial reports

| Report | Audience | Frequency | Content |
|--------|----------|-----------|---------|
| **Tenant payout report** | Tenant (admin portal) | On-demand + monthly PDF | Per-booking: FinalAmount, CommissionPercent, Commission, YourPayout, SettlementStatus, TransferId. Period summary. |
| **Atlas commission report** | Atlas Admin | Daily / monthly | Total commission earned, by tenant, by property, by day. Trends. |
| **Monthly reconciliation report** | Atlas Admin | Monthly | Razorpay credits vs. booking sums, settlement sums vs. host payouts, discrepancies. |
| **Failed settlement report** | Atlas Admin | On-demand (dashboard) | All `SettlementFailed` and `ManualReview` items with LastError, attempt count, linked account status. |

---

## 4. Marketplace reputation & trust layer V1

### 4.1 Review ingestion model

**Existing model:** `Review` entity with `BookingId`, `GuestId`, `ListingId`, `Rating` (1-5), `Title`, `Body`, `HostResponse`, `HostResponseAt`.

**Requirements:**

| ID | Requirement |
|----|-------------|
| REP-01 | Reviews MUST require a completed booking (`BookingStatus = CheckedOut`). |
| REP-02 | One review per guest per booking (unique constraint on `BookingId`). |
| REP-03 | Reviews MUST be submitted within 30 days of checkout. After 30 days, the review endpoint returns 410 Gone. |
| REP-04 | Minimum review content: `Rating` is required (1-5). `Body` is optional but encouraged (UI prompt). |
| REP-05 | Host can respond once per review (`HostResponse`, `HostResponseAt`). No edit after 7 days. |

### 4.2 Review moderation model (V1)

| Rule | Implementation |
|------|---------------|
| Auto-approve | All reviews meeting REP-01 through REP-04 are published immediately. |
| Profanity filter | V1: none. V2: basic keyword filter. |
| Admin removal | Atlas Admin can mark a review as `Hidden` (new boolean field, default false). Hidden reviews excluded from score and display. |
| Self-review detection | Reviews where `Guest.Phone == Tenant.OwnerPhone` or `Guest.Email == Tenant.OwnerEmail` are auto-flagged (AG-13). |
| Dispute | Host can report a review to Atlas Admin. Manual review and potential hide. |

### 4.3 Ranking weight interaction

(From RA-001 section 3.1, repeated for context with trust additions.)

```
RankingScore = (0.30 * BaseQuality)
             + (0.25 * CommissionBoost)
             + (0.25 * ReviewScore)
             + (0.20 * RecencyScore)
```

**Trust-adjusted modifiers:**

| Condition | Effect |
|-----------|--------|
| ReviewCount < 3 | `ReviewScore` dampened: `ReviewScore * (ReviewCount / 3)`. Prevents one 5-star review from maxing the component. |
| CancellationRate > 20% (last 90 days) | Ranking penalty: `RankingScore * 0.85`. |
| Response rate < 50% (last 90 days) | Ranking penalty: `RankingScore * 0.90`. |

### 4.4 Boost suppression for low-quality listings

| Condition | Result |
|-----------|--------|
| `BaseQuality < 0.50` | `CommissionBoost = 0.0` (existing rule AG-16) |
| Average rating < 2.5 (with >= 3 reviews) | `CommissionBoost = 0.0` |
| CancellationRate > 30% | `CommissionBoost = 0.0` |

### 4.5 Minimum quality thresholds for marketplace

A property MUST meet these to appear on the marketplace (in addition to `IsMarketplaceEnabled` and `Status = Active`):

| Threshold | Value | Rationale |
|-----------|-------|-----------|
| At least 1 listing with pricing | Required | Cannot display a bookable property without price |
| At least 1 photo on any listing | Recommended (not blocking in v1) | Photos drive conversion |
| Property description > 50 chars | Recommended (not blocking in v1) | SEO and guest trust |

V1: only the pricing requirement is blocking. Photo and description are `BaseQuality` components that affect ranking but do not prevent listing.

### 4.6 Response rate scoring

```
ResponseRate = BookingsRespondedWithin24h / TotalBookingsLast90Days
```

- "Responded" = booking has at least one `CommunicationLog` entry with `Status = 'Sent'` and `Channel in ('WhatsApp', 'SMS', 'Email')` within 24 hours of booking creation.
- If `TotalBookingsLast90Days = 0`: `ResponseRate = 1.0` (no penalty for new properties).

### 4.7 Cancellation rate scoring

```
CancellationRate = CancelledBookingsLast90Days / TotalBookingsLast90Days
```

- "Cancelled" = `BookingStatus = 'Cancelled'` AND cancellation was host-initiated (not guest-initiated). V1 simplification: all cancellations count (no initiator tracking). V2: differentiate.
- If `TotalBookingsLast90Days = 0`: `CancellationRate = 0.0` (no penalty).

### 4.8 Metrics that impact ranking (summary)

| Metric | Ranking component | Weight | Can suppress boost? |
|--------|------------------|:------:|:-------------------:|
| Commission percentage | CommissionBoost | 0.25 | N/A (it IS the boost) |
| Profile completeness | BaseQuality | 0.30 | Yes (< 0.50 kills boost) |
| Review average | ReviewScore | 0.25 | Yes (< 2.5 kills boost) |
| Booking recency | RecencyScore | 0.20 | No |
| Cancellation rate | Penalty multiplier | (multiplier) | Yes (> 30% kills boost) |
| Response rate | Penalty multiplier | (multiplier) | No |
| Review count | Dampener on ReviewScore | (dampener) | No |

---

## 5. Scale to 100k tenants architecture requirements

### 5.1 iCal polling scaling model

**Current:** `ICalSyncHostedService` polls external calendar URLs stored in `ListingExternalCalendar`.

| Tenants | Properties (est. 3 per tenant) | Listings (est. 2 per property) | iCal URLs (est. 1 per listing) | Polling interval | Polling throughput required |
|:-------:|:-----:|:------:|:------:|:----:|:---:|
| 1,000 | 3,000 | 6,000 | 6,000 | 15 min | 6.7/sec |
| 10,000 | 30,000 | 60,000 | 60,000 | 15 min | 66.7/sec |
| 100,000 | 300,000 | 600,000 | 600,000 | 15 min | 666.7/sec |

**Strategy:**

| Scale tier | Approach |
|-----------|----------|
| < 10k tenants | Single worker with batch HTTP client (connection pooling, 20 concurrent fetches). 15-min cycle. |
| 10k-50k | Partitioned polling: split URLs into N shards by `ListingId % ShardCount`. Run N worker instances (or time-sliced in single worker). Extend cycle to 30 min if needed. |
| > 50k | Dedicated iCal sync microservice (future). Or: push-based approach (Channex webhooks instead of pull). |

- SCA-01: iCal sync MUST be resilient to individual URL failures (timeout 10s, skip and continue).
- SCA-02: iCal sync MUST log per-URL success/failure metrics for monitoring.

### 5.2 Channex webhook scaling model

**V1:** Atlas pushes to Channex (no inbound webhooks). Scaling concern is outbound API call volume.

| Scale | Properties with Channex | Push frequency | API calls/day |
|:-----:|:-----------------------:|:--------------:|:-------------:|
| 1,000 tenants | ~500 | Every 15 min | 48,000 |
| 100,000 tenants | ~50,000 | Every 15 min | 4,800,000 |

**Strategy:**

- SCA-03: Rate-limit Channex API calls to 60/min per API key (assumed limit). Queue pushes and process sequentially per key.
- SCA-04: At > 10k connected properties: implement batched push (group rate changes per property into single API call).
- SCA-05: V2: accept Channex inbound webhooks for booking notifications (reduces polling need).

### 5.3 Payment webhook scaling model

Razorpay webhooks are event-driven (no polling). Volume scales linearly with bookings.

- SCA-06: Webhook endpoint MUST respond within 5 seconds (Razorpay timeout). Heavy processing (settlement) goes to outbox.
- SCA-07: Webhook endpoint MUST be idempotent (existing implementation).
- SCA-08: At > 1000 webhooks/min: consider Azure App Service scale-out (horizontal). V1: single instance sufficient for projected volumes.

### 5.4 DB indexing strategy

**Existing indexes (relevant):** Tenant-scoped composite indexes on most tables.

**New indexes for marketplace scale:**

| Index | Table | Columns | Purpose |
|-------|-------|---------|---------|
| `IX_Properties_Marketplace` | Properties | `(IsMarketplaceEnabled, Status)` INCLUDE `(TenantId, Name, CommissionPercent)` | Fast marketplace listing query |
| `IX_Bookings_SettlementStatus` | Bookings | `(PaymentModeSnapshot, BookingStatus)` | Settlement worker query |
| `IX_Reviews_ListingRating` | Reviews | `(ListingId)` INCLUDE `(Rating)` | Ranking score computation |
| `IX_Tenants_Active` | Tenants | `(IsActive)` | Filter active tenants for marketplace |

- SCA-09: All marketplace queries MUST have covering indexes. Query plans MUST be reviewed for scans vs seeks at 100k tenant scale.

### 5.5 Partitioning strategy

**V1:** No table partitioning. Azure SQL handles up to ~1TB per database efficiently.

**V2 threshold:** If `Bookings` exceeds 50M rows or `OutboxMessage` exceeds 10M rows:

- Consider partitioning `OutboxMessage` by `CreatedAtUtc` (monthly).
- Consider archiving `CommunicationLog` rows older than 1 year to a separate table.
- Consider read replicas for reporting queries.

- SCA-10: V1 design MUST NOT rely on partitioning. All queries MUST work on a single unpartitioned database.

### 5.6 Caching strategy for public search

| Cache layer | TTL | Key | Invalidation |
|-------------|:---:|-----|-------------|
| Ranking scores | 15 min | `ranking:{locationHash}:{sortOrder}` | Time-based expiry |
| Property detail | 5 min | `property:{slug}` | Time-based; immediate on property update (write-through) |
| Marketplace listing count | 15 min | `marketplace:count:{location}` | Time-based |

- SCA-11: V1 uses in-memory cache (`IMemoryCache`). No Redis required.
- SCA-12: At > 10k concurrent search users: consider Cloudflare edge caching for `GET /marketplace/properties` with `Cache-Control: public, max-age=60`.

### 5.7 Ranking computation optimization

- SCA-13: Ranking MUST be computed in batch (not per-request). Background job computes scores every 15 minutes. API reads from cache.
- SCA-14: At 100k properties: ranking computation MUST complete within 60 seconds. Requires: bulk DB queries (not N+1), in-memory scoring, write results to cache.
- SCA-15: If > 100k marketplace properties: precompute scores into a `PropertyRankingScore` table (materialized). V1: in-memory only.

### 5.8 Read vs write separation (future-ready)

V1: single database, single connection string.

**Future-ready design:**

- SCA-16: All read-only queries (marketplace search, reports, analytics) SHOULD use a read-only connection string when available. EF Core supports `DbContext` with read replicas via named connection strings.
- SCA-17: V1 code MUST separate read and write paths logically (e.g. `MarketplaceQueryService` for reads, `BookingService` for writes) even if both use the same DB connection.

### 5.9 Performance targets

| Operation | Target (p95) | At 100k tenants |
|-----------|:------------:|:---------------:|
| Marketplace search (cached) | < 200ms | < 500ms |
| Marketplace search (cache miss) | < 1000ms | < 2000ms |
| Property detail page | < 150ms | < 300ms |
| Booking creation (incl. commission) | < 500ms | < 800ms |
| Commission calculation | < 5ms | < 10ms |
| Settlement transfer (worker) | < 5000ms | Same (Razorpay API bound) |
| Ranking batch recompute (all properties) | < 30s (1k properties) | < 60s (100k properties, with bulk queries) |

---

## 6. Monetization evolution strategy

All parameters below MUST be config-driven (not hardcoded). V1 ships with defaults; future versions change config only.

### 6.1 Default commission

| Config key | V1 default | Type | Location |
|------------|:----------:|------|----------|
| `Commission:FloorPercent` | 1.00 | decimal | `appsettings.json` |
| `Commission:CeilingPercent` | 20.00 | decimal | `appsettings.json` |
| `Commission:SystemDefaultPercent` | 1.00 | decimal | `appsettings.json` (used when `Tenant.DefaultCommissionPercent` is null for legacy tenants) |

- MON-01: Changing `Commission:FloorPercent` from 1% to 2% MUST require: update config, existing tenants with < 2% default auto-adjust (same cascade as RA-001 Journey E).
- MON-02: These values MUST NOT be compiled constants. `CommissionCalculationService` reads from `IOptions<CommissionSettings>`.

### 6.2 Tiered commission plans

**Not v1.** Architecture MUST support:

| Tier | Commission range | Subscription fee | Target |
|------|:----------------:|:----------------:|--------|
| Free | 1% (floor) | INR 0 | Small hosts, trial |
| Pro | 1-5% | INR 999/mo | Mid-size |
| Enterprise | 1-20% + volume discounts | Custom | Large portfolios |

- MON-03: `BillingPlan` already has `Code`, `MonthlyPriceInr`. Add `MaxCommissionPercent` (nullable, default 20) and `MinCommissionPercent` (nullable, default 1) to the plan model in v2.
- MON-04: `CommissionCalculationService` MUST read floor/ceiling from tenant's plan (when tiered plans are live) instead of global config.

### 6.3 Subscription + commission hybrid

- MON-05: Hybrid model: tenant pays subscription AND commission. Both revenue streams are independent and additive. Already architecturally supported (BillingPlan + CommissionAmount on booking).
- MON-06: Future: offer commission discounts for higher subscription tiers (e.g. Pro plan gets 0.5% commission reduction). Implement as `Plan.CommissionDiscountPercent`.

### 6.4 Promotional commission discounts

- MON-07: Support time-limited commission promotions (e.g. "First 3 months at 0% commission for new tenants").
- MON-08: Implement as `TenantCommissionPromotion` (TenantId, DiscountPercent, StartsAtUtc, EndsAtUtc, IsActive).
- MON-09: `CommissionCalculationService` checks for active promotion: `effectiveRate = MAX(floor, effectiveRate - promotion.DiscountPercent)`.
- MON-10: V1: not implemented. Config-driven discount field on Tenant (`CommissionDiscountPercent`, default 0) as a simpler alternative.

### 6.5 City-level commission experiments

- MON-11: Support per-city commission floors/ceilings for experimentation (e.g. "Goa: minimum 2%, Mumbai: minimum 1%").
- MON-12: Implement as `CityCommissionConfig` (City, FloorPercent, CeilingPercent, IsActive). `CommissionCalculationService` checks city-level config before global.
- MON-13: V1: not implemented. Property does not have a City field. V2 prerequisite: add `City` to Property model.

### 6.6 A/B testing ranking weight changes

- MON-14: Support runtime-configurable ranking weights.
- MON-15: Store ranking weights in config: `Ranking:W_Base`, `Ranking:W_Commission`, `Ranking:W_Reviews`, `Ranking:W_Recency`. All read via `IOptions<RankingSettings>`.
- MON-16: V1: change weights via config deploy (requires restart). V2: read from DB config table (hot-reload).
- MON-17: Weights MUST always sum to 1.0. `RankingScoreService` validates on startup.

---

## 7. Vendor abstraction hardening

### 7.1 Channel provider interface

**Existing:** `IChannelManagerProvider` in `Atlas.Api/Services/Channels/`.

```
interface IChannelManagerProvider:
    ProviderName: string
    TestConnectionAsync(apiKey) → ChannelConnectionResult
    PushRatesAsync(apiKey, externalPropertyId, rates[]) → ChannelSyncResult
    PushAvailabilityAsync(apiKey, externalPropertyId, availability[]) → ChannelSyncResult
```

**Hardening requirements:**

| ID | Requirement |
|----|-------------|
| VEN-01 | All Channex-specific types (`ChannexRateUpdate`, `ChannexSyncResult`) MUST remain inside `ChannexAdapter`. The interface uses provider-agnostic types (`RateUpdate`, `ChannelSyncResult`). |
| VEN-02 | `ChannelConfig.Provider` stores the provider name (e.g. `"channex"`). DI registration maps provider name to implementation. |
| VEN-03 | Adding a new channel provider (e.g. `"hostaway"`) MUST require: (a) implement `IChannelManagerProvider`, (b) register in DI, (c) configure `ChannelConfig` rows with `Provider = "hostaway"`. No other code changes. |

### 7.2 Payment provider interface

**Not yet abstracted.** `RazorpayPaymentService` is the only implementation.

**Requirements for abstraction:**

| ID | Requirement |
|----|-------------|
| VEN-04 | Introduce `IPaymentProvider` interface with methods: `CreateOrderAsync`, `VerifyPaymentAsync`, `RefundAsync`, `TransferAsync` (for Route). |
| VEN-05 | `RazorpayPaymentService` becomes the Razorpay implementation of `IPaymentProvider`. |
| VEN-06 | `Tenant.PaymentProvider` (varchar, default `"razorpay"`) determines which `IPaymentProvider` implementation to use. V1: always Razorpay. |
| VEN-07 | Adding Stripe (future) MUST require: (a) implement `IPaymentProvider`, (b) register in DI, (c) `Tenant.PaymentProvider = "stripe"`. No booking flow changes. |

### 7.3 Vendor swap checklist

| Step | Action | Effort |
|------|--------|--------|
| 1 | Implement new provider adapter (e.g. `StripePaymentProvider : IPaymentProvider`) | M |
| 2 | Add provider config to `appsettings.json` | S |
| 3 | Register in DI (`AddScoped<IPaymentProvider, StripePaymentProvider>` conditionally) | S |
| 4 | Migrate tenant config (new API keys, linked accounts) | M |
| 5 | Feature-flag new provider for pilot tenants | S |
| 6 | Update webhook endpoints for new provider | M |
| 7 | Run payment simulation tests | M |
| 8 | Gradual rollout via `Tenant.PaymentProvider` | S |

### 7.4 Vendor outage fallback strategy

| Vendor | Outage impact | Fallback |
|--------|--------------|----------|
| Channex down | Rate/availability push fails | Retry with backoff. iCal sync continues independently. Alert after 3 consecutive failures. No booking impact. |
| Razorpay down | New bookings fail (cannot create orders) | Display "Payments temporarily unavailable" to guest. Existing confirmed bookings unaffected. Settlement worker retries. |
| Auth0 down | Admin portal login fails | Display "Login temporarily unavailable". API endpoints with `[AllowAnonymous]` still work. Guest portal unaffected. |
| Azure SQL down | Everything fails | App Service returns 503. Cloudflare Pages serve cached static assets for guest portal. Manual incident response. |

- VEN-08: All external API calls MUST have timeouts (10s for Channex, 15s for Razorpay, 5s for Auth0).
- VEN-09: All external API calls MUST have circuit-breaker logic: after 5 consecutive failures in 60 seconds, stop calling for 30 seconds, then retry. V1: implement as simple backoff in the worker loop. V2: Polly circuit breaker.

### 7.5 Feature flag isolation

Each vendor integration MUST be independently toggleable:

| Flag | Default | Effect when off |
|------|---------|-----------------|
| `integrations.channex.enabled` | true | Channex push disabled; test connection returns stub result |
| `integrations.razorpay.standard.enabled` | true | HOST_DIRECT checkout disabled |
| `integrations.razorpay.route.enabled` | false | MARKETPLACE_SPLIT disabled |

### 7.6 Webhook resilience

| Requirement | Detail |
|-------------|--------|
| VEN-10 | All webhooks MUST verify cryptographic signatures before processing. |
| VEN-11 | Webhooks MUST return 200 within 5 seconds. Heavy work goes to outbox. |
| VEN-12 | Webhooks MUST be idempotent (check if event already processed via `ConsumedEvent` table or `RazorpayPaymentId` uniqueness). |
| VEN-13 | Webhook endpoints MUST NOT expose internal error details in the response body. Return 200 (acknowledged) even on processing failure; log the error. |

### 7.7 Retry/backoff policies

| Integration | Retry strategy | Max retries | Backoff |
|-------------|---------------|:-----------:|---------|
| Channex push | Exponential | 3 | 30s, 60s, 120s |
| Razorpay order creation | No retry (real-time) | 0 | N/A (return error to user) |
| Razorpay settlement transfer | Exponential | 5 | 1m, 2m, 4m, 8m, 16m |
| Razorpay refund | Exponential | 3 | 30s, 60s, 120s |
| OAuth token refresh | Linear | 3 | 60s, 60s, 60s |

---

## 8. Data ownership & exit strategy

### 8.1 Tenant data export policy

- EXIT-01: Any tenant MUST be able to export all their data. This is a fundamental platform commitment and potential GDPR/regulatory requirement.
- EXIT-02: Export endpoint: `GET /api/tenant/export` returns a ZIP containing JSON files. Admin-only.
- EXIT-03: Export MUST complete within 5 minutes for a tenant with < 10,000 bookings. For larger tenants, async export via email link.

### 8.2 Export contents

| File in ZIP | Content | Format |
|-------------|---------|--------|
| `tenant.json` | Tenant profile, settings, commission config (no tokens) | JSON |
| `properties.json` | All properties with commission overrides, marketplace status | JSON |
| `listings.json` | All listings with pricing, external calendars | JSON |
| `bookings.json` | All bookings with commission snapshots, payment modes, statuses | JSON |
| `payments.json` | All payments with Razorpay IDs, refunds, settlement status | JSON |
| `guests.json` | All guests (name, phone, email) | JSON |
| `reviews.json` | All reviews with ratings, responses | JSON |
| `communication_logs.json` | Notification history | JSON |
| `audit_log.json` | All audit entries for this tenant | JSON |
| `invoices.json` | Billing invoices | JSON |

### 8.3 Commission history export

- EXIT-04: `bookings.json` MUST include `CommissionPercentSnapshot`, `CommissionAmount`, `HostPayoutAmount`, `PaymentModeSnapshot` for every booking.
- EXIT-05: Tenant can generate a CSV "Commission Statement" for any date range: BookingId, Date, Amount, CommissionPercent, Commission, YourPayout, SettlementStatus.

### 8.4 Audit export

- EXIT-06: `audit_log.json` includes all `AuditLog` entries for the tenant: action, timestamp, actor, entity, changes.

### 8.5 API access model (future)

- EXIT-07: V2: expose a tenant API key for programmatic read access to their own data (bookings, payments, properties). Rate-limited. Read-only.
- EXIT-08: V1: no tenant API. Export covers data portability needs.

### 8.6 Vendor lock-in prevention

| Concern | Mitigation |
|---------|-----------|
| Razorpay lock-in | `IPaymentProvider` abstraction. All Razorpay-specific logic contained in `RazorpayPaymentService`. Booking data does not reference Razorpay internals in core fields (Razorpay IDs stored in Payment, not Booking). |
| Channex lock-in | `IChannelManagerProvider` abstraction. All Channex-specific logic in `ChannexAdapter`. `ChannelConfig.Provider` field allows per-property provider selection. |
| Auth0 lock-in | Standard JWT validation. Auth0-specific config in `appsettings.json`. Switching to any OIDC provider requires config change only. |
| Azure SQL lock-in | Standard EF Core. No Azure SQL-specific features used (no elastic pools, no serverless-specific SQL). Portable to any SQL Server or PostgreSQL with EF provider change. |

---

## 9. Operational playbooks

### Top 15 incidents

| # | Incident | Severity | Detection | Playbook |
|---|---------|:--------:|-----------|----------|
| 1 | **Commission mismatch: booking has wrong commission** | High | Tenant reports; reconciliation check | 1. Pull `CommissionPercentSnapshot` from booking. 2. Compare with `Tenant.DefaultCommissionPercent` and `Property.CommissionPercent` at that time (use AuditLog). 3. If snapshot was correct at creation time: explain to tenant. 4. If bug: do NOT recalculate. Issue manual credit/adjustment. Log in AuditLog. |
| 2 | **Razorpay settlement delay: host not paid** | High | Settlement dashboard shows `SettlementQueued` > 1 hour old | 1. Check outbox row status and attempt count. 2. If `Pending` with old `NextAttemptUtc`: worker may be down. Check App Service health. 3. If `Failed`: check `LastError`. If 4xx: verify linked account status in Razorpay. If 5xx: manual retry via admin dashboard. |
| 3 | **OTA sync delay: rates stale on Airbnb** | Medium | `ChannelConfig.LastSyncAt` > 2 hours old | 1. Check `LastSyncError`. 2. Test connection via API. 3. If API key invalid: notify tenant to reconnect. 4. If Channex API down: wait and retry. 5. If our worker down: restart App Service. |
| 4 | **Overbooking conflict: same dates booked on OTA and marketplace** | Critical | Guest or host reports | 1. Identify which booking was first (by `CreatedAt`). 2. If Atlas booking first: contact OTA to cancel (host responsibility). 3. If OTA booking first: cancel Atlas booking, full refund. 4. Root cause: iCal sync delay. Document for tenant. |
| 5 | **Payment mode switch error: tenant can't switch** | Low | Tenant reports | 1. Check `RazorpayLinkedAccountId`. If null: tenant must complete OAuth. 2. If set but KYC not activated: direct to Razorpay dashboard. 3. If token expired: trigger manual refresh. |
| 6 | **Boost abuse report: competitor reports a tenant** | Medium | Support ticket | 1. Pull commission change history from AuditLog for reported tenant. 2. Check for frequent flipping (> 3 changes in 7 days). 3. If suspicious: warn tenant. 4. If repeated: Atlas Admin can manually set commission and lock it for 30 days. |
| 7 | **Guest payment captured but booking not confirmed** | Critical | Guest reports | 1. Find Payment by `RazorpayPaymentId`. 2. If `Status = pending`: Razorpay webhook may not have fired. Call Razorpay API to verify capture. 3. If captured: manually trigger verify flow. 4. If not captured: investigate (card declined after redirect?). |
| 8 | **Razorpay OAuth callback returns error** | Medium | Tenant reports; error log | 1. Check API logs for callback URL. 2. Verify `state` parameter matches expected. 3. Check Razorpay Partner dashboard for app config (redirect URI mismatch). 4. Check if tenant denied consent (expected; explain). |
| 9 | **Ranking appears wrong: low-commission property ranks high** | Low | Tenant reports | 1. Explain ranking formula (quality + reviews + recency + commission). 2. Property may have high BaseQuality + ReviewScore. 3. Commission is only 25% of score. Show tenant their boost estimate. |
| 10 | **Tenant subscription expired, marketplace properties still showing** | Medium | Alert | 1. Check `TenantSubscription.Status`. If `Suspended` or `Canceled`: properties should be hidden. 2. If still showing: ranking cache not expired (15 min TTL). Wait or manually invalidate. 3. If bug: force `IsMarketplaceEnabled = false` on all properties. |
| 11 | **Refund on MARKETPLACE_SPLIT booking: host wants full amount back** | Medium | Tenant reports | 1. Explain: refund goes to guest from Atlas account. Commission reversal = `RefundAmount * CommissionPercent / 100`. Host receives `RefundAmount - CommissionReversal`. 2. If settlement already completed: reverse via Razorpay or deduct from next settlement. |
| 12 | **Duplicate settlement transfer** | Critical | Reconciliation alert | 1. Check idempotency key `settlement:{BookingId}:{PaymentId}`. 2. If truly duplicate (two different transfer IDs): Razorpay Route should prevent this. Contact Razorpay. 3. If same transfer ID returned twice: idempotent (no issue). |
| 13 | **Commission lower than floor (1%)** | Critical | Should not happen | 1. If `CommissionPercentSnapshot < 1.00`: bug in `CommissionCalculationService`. 2. Do NOT retroactively change. 3. Fix the bug. 4. Issue manual credit for the difference. 5. Post-mortem. |
| 14 | **Webhook storm: Razorpay sends thousands of webhooks** | High | Alert: > 100 webhooks/min | 1. Webhooks are idempotent; duplicates are skipped. 2. If App Service overloaded: scale out temporarily. 3. If malicious: verify signatures. Non-signed webhooks are rejected. 4. Contact Razorpay if their system is misbehaving. |
| 15 | **Tenant data export fails** | Low | Tenant reports | 1. Check export endpoint logs. 2. If timeout: tenant has too many bookings. Switch to async export. 3. If error: check DB connectivity. Retry. |

---

## 10. Disaster & worst case scenarios

### 10.1 Channex down for 6 hours

| Impact | Mitigation | Recovery |
|--------|-----------|----------|
| Rate/availability pushes fail | Retry with backoff; queue pushes. iCal sync continues independently (direct URL fetch, no Channex dependency). | After recovery: flush all queued pushes. Alert clears. |
| No booking notifications from OTA | V1: no inbound Channex webhooks; no impact. V2: queue would drain on recovery. | N/A for v1. |
| Tenant cannot test connection | Graceful error: "Channel manager temporarily unavailable." | Auto-recovers. |

**Safeguard:** SCA-03 backoff prevents hammering. Alert after 3 consecutive failures. Status page notification if > 1 hour.

### 10.2 Razorpay outage

| Impact | Mitigation | Recovery |
|--------|-----------|----------|
| New bookings fail (order creation) | Guest portal shows "Payments temporarily unavailable. Please try again later." No booking draft created. | Auto-recovers when Razorpay is back. |
| Pending settlements fail | Settlement worker retries with backoff. Guest booking is already confirmed (not affected). | Worker drains queue on recovery. |
| OAuth callbacks fail | Tenant sees error. Can retry later. | Tenant re-initiates OAuth. |
| Webhooks not delivered | Razorpay queues webhooks for up to 24 hours and retries. | Webhooks arrive after recovery; idempotent processing. |

**Safeguard:** Circuit breaker (VEN-09). Alert if > 5 consecutive Razorpay API failures.

### 10.3 Webhook storm

| Impact | Mitigation | Recovery |
|--------|-----------|----------|
| API overwhelmed by webhook volume | Webhook handler is lightweight (signature verify + outbox write); heavy processing is async. | App Service auto-scale (if configured) or manual scale-out. |
| DB write contention | Outbox writes are small rows. No large transactions in webhook handler. | Inherently resilient. |

**Safeguards:** Signature verification rejects invalid webhooks. `ConsumedEvent` table prevents reprocessing. Rate limiting on webhook endpoint (200 req/min per source IP).

### 10.4 DB corruption event

| Impact | Mitigation | Recovery |
|--------|-----------|----------|
| Data loss or inconsistency | Azure SQL automatic backups (point-in-time restore, 7-35 day retention). | Restore to point-in-time before corruption. Apply transaction log. |
| Commission data integrity loss | Snapshot fields are immutable; no application-level process overwrites them. Corruption requires storage-level failure. | Restore from backup. Run reconciliation checks (section 3.2). |

**Safeguards:** Azure SQL geo-redundant backup. Daily reconciliation report catches discrepancies within 24 hours.

### 10.5 Commission bug affecting payouts

**Scenario:** A code bug in `CommissionCalculationService` causes incorrect `CommissionPercentSnapshot` for a batch of bookings.

| Impact | Mitigation | Recovery |
|--------|-----------|----------|
| Hosts overpaid or underpaid | Commission is snapshotted; error is frozen into bookings. | 1. Identify affected bookings via date range and commission values. 2. Do NOT retroactively change snapshots (FIN-06). 3. Calculate correct amounts. 4. Issue manual credits or debits. 5. Log adjustments in AuditLog. 6. Post-mortem and fix bug. |
| Atlas revenue miscalculated | Same | Same + update reconciliation reports. |

**Safeguards:** Unit tests for all commission paths (RA-001 section 7.2). Integration test for boundary values. Daily reconciliation alert.

### 10.6 Rank manipulation exploit discovered

**Scenario:** A tenant discovers a way to game the ranking algorithm (e.g. commission flipping bypasses cooldown).

| Impact | Mitigation | Recovery |
|--------|-----------|----------|
| Unfair ranking for other tenants | Anti-gaming guardrails (section 2). | 1. Disable ranking engine (`marketplace.ranking = false`; reverts to alphabetical). 2. Investigate exploit. 3. Fix guardrail bypass. 4. Re-enable ranking. 5. Manually penalize exploiting tenant if ToS violation. |

**Safeguards:** Feature flag kill switch. Commission change audit log. Cooldown enforcement. Damping logic.

### 10.7 Required system safeguards (summary)

| Safeguard | Protects against | Implementation |
|-----------|-----------------|----------------|
| Feature flag kill switches | All marketplace features | `appsettings.json` overridable by env var |
| Idempotency keys | Duplicate payments, settlements, refunds | Per-action unique keys checked before processing |
| Snapshot immutability | Commission manipulation, retroactive changes | Write-once fields; no update endpoints |
| Signature verification | Webhook spoofing | HMAC-SHA256 on all inbound webhooks |
| Exponential backoff | External service overload | Configurable per integration |
| Daily reconciliation | Financial discrepancy | Automated check + alert |
| Audit logging | All operational actions | `AuditLog` table + structured logs |
| BillingLockFilter | Suspended tenant abuse | HTTP 402 on mutations |

---

## 11. Definition of Done for marketplace V1

This checklist MUST be fully satisfied before the marketplace is publicly launched.

### Security validation

- [ ] All tenant-owned entities have `ApplyTenantQueryFilter` registered (verified by `TenantOwnershipTests`).
- [ ] Cross-tenant data access is impossible via API (verified by integration test with two tenants).
- [ ] Razorpay OAuth tokens are encrypted at rest (verified by DB inspection: columns are `varbinary`).
- [ ] Tokens never appear in API responses (verified by DTO inspection and integration test).
- [ ] Tokens never appear in logs (verified by log output inspection with test data).
- [ ] Webhook signature verification is enabled and tested for Razorpay.
- [ ] OAuth callback validates `state` parameter (CSRF protection).
- [ ] CommissionPercentSnapshot cannot be modified via any API endpoint (verified by integration test).
- [ ] Rate limiting is active on OAuth and payment endpoints.

### Financial reconciliation validation

- [ ] Commission calculation matches expected values for all test cases in RA-001 section 7.2 (10 cases).
- [ ] `CommissionAmount + HostPayoutAmount = FinalAmount` for every test booking (verified by query).
- [ ] Settlement idempotency key prevents duplicate transfers (verified by integration test).
- [ ] Daily reconciliation report runs without discrepancies on test data.
- [ ] Refund commission reversal correctly computed for full and partial refunds.
- [ ] No booking has `CommissionPercentSnapshot < 1.00` (floor enforcement verified by query).

### Ranking integrity validation

- [ ] Ranking formula produces expected order for a set of test properties with known scores.
- [ ] CommissionBoost is 0.0 for properties with `BaseQuality < 0.50`.
- [ ] Commission weight does not exceed 0.30 of total score.
- [ ] Cooldown enforcement: commission change within 24h is rejected (verified by API test).
- [ ] Damping logic: recent commission change uses ramped value (verified by unit test).
- [ ] Self-booking exclusion from RecencyScore (verified by unit test).
- [ ] ReviewScore dampening for < 3 reviews (verified by unit test).

### Payment routing validation

- [ ] HOST_DIRECT booking uses host's Razorpay keys (verified end-to-end in staging).
- [ ] MARKETPLACE_SPLIT booking uses Atlas's keys (verified end-to-end in staging with test linked account).
- [ ] Settlement outbox event is written on MARKETPLACE_SPLIT booking confirmation.
- [ ] Settlement worker successfully transfers to linked account (staging test).
- [ ] Settlement worker retries on transient failure and stops on permanent failure.
- [ ] Payment mode snapshot is immutable after booking creation.

### Tenant onboarding test cases

- [ ] New tenant signup creates correct defaults (`DefaultCommissionPercent = 1.00`, `PaymentMode = HOST_DIRECT`).
- [ ] Tenant can connect Razorpay OAuth and receive linked account ID (staging).
- [ ] Tenant can set `MARKETPLACE_SPLIT` after OAuth connection.
- [ ] Tenant cannot set `MARKETPLACE_SPLIT` without OAuth connection (400 error).
- [ ] Tenant can connect Airbnb via Channex (staging with test Channex account).
- [ ] Tenant can connect Booking.com via Hotel ID (staging).
- [ ] Property marketplace toggle works (enable/disable, reflected in search results).
- [ ] Property commission override works (slider sets value, validation enforced).

### Manual QA checklist

- [ ] Guest can search marketplace and see ranked results.
- [ ] Guest can view property detail page with correct pricing (no commission shown).
- [ ] Guest can complete a booking and pay (HOST_DIRECT and MARKETPLACE_SPLIT, both tested).
- [ ] Guest receives confirmation notification after booking.
- [ ] Admin portal shows commission settings, payment mode, Razorpay connect button.
- [ ] Admin portal shows property marketplace toggle and boost slider.
- [ ] Admin portal shows booking detail with commission snapshot and settlement status.
- [ ] Settlement dashboard shows correct statuses for test bookings.
- [ ] Commission change warning modal appears and is accurate.
- [ ] Feature flags can be toggled at runtime; marketplace hides/shows accordingly.
- [ ] Suspended tenant's properties do not appear on marketplace.
- [ ] Data export endpoint returns complete, valid ZIP for a test tenant.

---

## Glossary

| Term | Definition |
|------|-----------|
| **Row-level isolation** | Every tenant-owned row has `TenantId`; EF Core global filters scope all queries. |
| **BillingLockFilter** | Action filter returning 402 when tenant subscription is locked. |
| **Commission damping** | Gradual ramp-up of ranking boost over 7 days after a commission change. |
| **Cooldown** | Minimum 24-hour interval between commission changes. |
| **Circuit breaker** | Pattern that stops calling a failing external service temporarily. |
| **Snapshot immutability** | Commission and payment mode fields on a booking are write-once and never modified. |
| **Reconciliation** | Automated comparison of expected vs actual financial totals to detect discrepancies. |
| **Debug bundle** | Downloadable JSON/ZIP with tenant data for support diagnostics. |
| **Kill switch** | Feature flag that instantly disables a marketplace feature. |
