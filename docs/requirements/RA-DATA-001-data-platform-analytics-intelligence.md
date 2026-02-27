# RA-DATA-001 — Data Platform, Analytics & Intelligence Architecture Requirements

| Field | Value |
|-------|-------|
| **ID** | RA-DATA-001 |
| **Title** | Data Platform, Analytics & Intelligence Architecture |
| **Status** | Draft |
| **Author** | Chief Data Architect |
| **Created** | 2026-02-27 |
| **Dependencies** | RA-001 (Marketplace/Commission), RA-002 (Governance/Scale), RA-003 (Growth/Demand/Network), RA-004 (Risk/Fraud/Trust), RA-005 (Subscription/Billing), RA-006 (Operational Excellence), RA-IC-001 (Hybrid Sync), RA-IC-002 (Provider Switching), RA-CHX-002 (OTA Mapping/ARI), RA-AI-001 (Pricing Intelligence) |
| **Stack** | Azure App Service · Azure SQL (OLTP) · Cloudflare Pages |
| **Constraints** | Single developer · No separate analytics DB (V1) · No external BI tools · DB-backed outbox · Scale to 100k tenants · Must not degrade OLTP performance |

---

## Table of Contents

1. [Data Architecture Vision](#1-data-architecture-vision)
2. [Event Model Specification](#2-event-model-specification)
3. [Aggregation & Snapshot Strategy](#3-aggregation--snapshot-strategy)
4. [Marketplace Intelligence Metrics](#4-marketplace-intelligence-metrics)
5. [Ranking & TrustScore Computation Engine](#5-ranking--trustscore-computation-engine)
6. [Pricing Intelligence Data Support](#6-pricing-intelligence-data-support)
7. [Reporting & Dashboard Requirements](#7-reporting--dashboard-requirements)
8. [Performance & Scale Requirements (100k Tenants)](#8-performance--scale-requirements-100k-tenants)
9. [Data Quality & Integrity Controls](#9-data-quality--integrity-controls)
10. [Future Warehouse Readiness](#10-future-warehouse-readiness)
11. [Testing Matrix](#11-testing-matrix)
12. [Definition of Done — Data Platform V1](#12-definition-of-done--data-platform-v1)

---

## 1. Data Architecture Vision

### 1.1 Three-layer data model

Atlas's data architecture is organized into three logical layers that all live within the **single Azure SQL database** in V1. The layers are distinguished by table naming convention, access patterns, and update frequency — not by separate databases.

```
┌─────────────────────────────────────────────────────────────────────┐
│ LAYER 3: Analytics / Intelligence                                   │
│                                                                     │
│  Future warehouse-ready.  V1: SQL views + API-level aggregation.    │
│  Feeds: Admin dashboards, marketplace intelligence, AI/ML (V2).     │
│  Access: Read-only queries, never written by OLTP transactions.     │
│  Tables: vw_* views, exported JSON snapshots (V2).                  │
└─────────────────────────┬───────────────────────────────────────────┘
                          │ reads from
┌─────────────────────────▼───────────────────────────────────────────┐
│ LAYER 2: Aggregation Layer                                          │
│                                                                     │
│  Pre-computed daily snapshots + summary tables.                     │
│  Written by nightly/periodic batch jobs only.                       │
│  Tables: Daily*, *Snapshot, *Summary (naming convention: Snap_*)    │
│  Never written by user-facing API requests.                         │
└─────────────────────────┬───────────────────────────────────────────┘
                          │ reads from
┌─────────────────────────▼───────────────────────────────────────────┐
│ LAYER 1: OLTP (Operational Database)                                │
│                                                                     │
│  Transactional tables. Written by API requests, outbox, workers.    │
│  Tables: Bookings, Payments, Listings, OutboxMessage, AuditLogs,    │
│          ListingDailyRate, AvailabilityBlock, ChannelConfigs, etc.   │
│  All existing AppDbContext DbSets.                                  │
└─────────────────────────────────────────────────────────────────────┘
```

### 1.2 Data classification

| Classification | Layer | Tables (examples) | Written by | Read by |
|---|---|---|---|---|
| **Transactional** | L1 OLTP | `Bookings`, `Payments`, `Listings`, `ListingDailyRate`, `ListingDailyInventory`, `AvailabilityBlock`, `BillingInvoice`, `BillingPayment` | API controllers, outbox workers, sync jobs | API controllers, dashboards |
| **Event-sourced** | L1 OLTP (append-only) | `OutboxMessage`, `AuditLogs`, `ConsumedEvent`, `CommunicationLog` | Domain event publishers, audit interceptors | Outbox dispatcher, reconciliation jobs, admin debug |
| **Snapshot / Aggregated** | L2 Aggregation | `Snap_DailyPropertyPerformance`, `Snap_DailyTenantPerformance`, `Snap_DailyMarketplacePerformance`, `Snap_DailyRevenueMetrics`, `Snap_DailyTrustScore`, `Snap_DailyDemandSignals` | Nightly batch jobs only | Dashboards, reporting API, intelligence engine |
| **Configuration** | L1 OLTP | `BillingPlans`, `MessageTemplate`, `FestivalDate`, `TenantPricingSetting`, `ListingPricingRule` | Admin UI, config API | All layers |
| **Intelligence** | L2 → L3 | `DemandSignal`, `PriceSuggestion`, `BookingVelocityMetric`, `DailyPerformanceSnapshot` (from RA-AI-001) | Pricing engine batch jobs | Dashboard, suggestion inbox, admin analytics |
| **Analytics views** | L3 Analytics | `vw_SuggestionHistory`, `vw_TenantRevenueReport`, `vw_MarketplaceHealth`, `vw_SyncReliability` | Never (read-only views) | Dashboard API, export API |

### 1.3 What stays transactional (L1)

- DM-L1-01: All user-initiated writes (bookings, payments, rate changes, blocks, guest records) MUST go through L1 OLTP with full ACID guarantees.
- DM-L1-02: The `OutboxMessage` table is the authoritative event log. All domain events originate here.
- DM-L1-03: `AuditLog` is append-only and MUST never be updated or deleted by application code.
- DM-L1-04: `ConsumedEvent` tracks idempotency for exactly-once processing of outbox events.

### 1.4 What moves to aggregated tables (L2)

- DM-L2-01: Any metric that requires scanning > 30 days of transactional data across multiple tables MUST be pre-computed in a snapshot table.
- DM-L2-02: Snapshot tables are the **only** source for dashboard rendering. Dashboards MUST NOT query L1 tables for historical aggregations.
- DM-L2-03: Exception: "current state" metrics (e.g., "active listings count", "pending suggestions count") MAY query L1 directly with lightweight indexed queries.
- DM-L2-04: All L2 tables use the naming convention `Snap_*` and are NOT registered in `AppDbContext` tenant query filters (they are accessed via `IgnoreQueryFilters()` with explicit tenant predicates, or as platform-wide aggregates).

### 1.5 What is event-sourced (append-only L1)

| Table | Event source pattern | Guarantees |
|-------|---------------------|------------|
| `OutboxMessage` | Transactional outbox. Events written atomically with business operations. | At-least-once delivery via dispatcher. |
| `AuditLog` | Append-only audit trail. Written on every significant domain action. | Immutable. Sensitive fields redacted. |
| `ConsumedEvent` | Idempotency log. Written when an outbox event is successfully processed. | Prevents duplicate processing. |
| `CommunicationLog` | Notification delivery audit. One row per send attempt. | Idempotent via `IdempotencyKey` unique index. |

- DM-ES-01: Event-sourced tables MUST NEVER have `UPDATE` or `DELETE` operations in application code.
- DM-ES-02: Data retention for event-sourced tables is managed by a periodic purge job (section 8.5) — never by application logic.

### 1.6 What is snapshot-based (materialized L2)

- DM-SN-01: Snapshot tables are **idempotent upserts** — re-running the snapshot job for the same date overwrites the row.
- DM-SN-02: Snapshots MUST carry a `ComputedAtUtc` timestamp so consumers know data freshness.
- DM-SN-03: Snapshots MUST carry a `SchemaVersion` (`int`, default 1) for forward compatibility during format changes.

---

## 2. Event Model Specification

### 2.1 Canonical domain events

Atlas defines a set of well-known domain events that drive the data pipeline. These extend the existing `EventTypes.cs` constants.

| Event type | Topic | Trigger | Entity affected |
|---|---|---|---|
| `booking.created` | `booking.events` | New booking inserted | `Booking` |
| `booking.confirmed` | `booking.events` | Booking status → Confirmed | `Booking` |
| `booking.modified` | `booking.events` | Check-in/out date, amount, or guest changed | `Booking` |
| `booking.cancelled` | `booking.events` | Booking status → Cancelled | `Booking` |
| `commission.calculated` | `billing.events` | Commission amount computed on booking confirm | `Booking` (CommissionAmount field) |
| `settlement.completed` | `billing.events` | Host payout processed | `Payment` |
| `settlement.failed` | `billing.events` | Host payout failed | `Payment` |
| `rate.updated` | `pricing.events` | `ListingDailyRate` row written (any source) | `ListingDailyRate` |
| `availability.updated` | `inventory.events` | `ListingDailyInventory` or `AvailabilityBlock` changed | `ListingDailyInventory` |
| `restriction.updated` | `pricing.events` | `ListingPricingRule` created/modified/deleted | `ListingPricingRule` |
| `subscription.changed` | `billing.events` | Plan change, status change, renewal | `TenantSubscription` |
| `boost.changed` | `marketplace.events` | `Property.CommissionPercent` updated | `Property` |
| `trustscore.updated` | `trust.events` | TrustScore recalculated for a property | Computed (no single table) |
| `sync.completed` | `sync.events` | iCal poll or Channex webhook cycle completed | `ChannelConfig` |
| `sync.failed` | `sync.events` | Sync cycle failed | `ChannelConfig` |
| `suggestion.created` | `pricing.events` | `PriceSuggestion` generated | `PriceSuggestion` |
| `suggestion.accepted` | `pricing.events` | Tenant accepted a suggestion | `PriceSuggestion` |
| `suggestion.auto_applied` | `pricing.events` | Auto-apply wrote rates | `PriceSuggestion` |
| `review.created` | `marketplace.events` | Guest submitted review | `Review` |
| `invoice.generated` | `billing.events` | `BillingInvoice` or `BookingInvoice` created | `BillingInvoice` / `BookingInvoice` |

### 2.2 Required fields per event

Every event written to `OutboxMessage.PayloadJson` MUST include the following canonical envelope:

```json
{
  "eventId": "guid",
  "eventType": "booking.created",
  "schemaVersion": 1,
  "occurredUtc": "2026-02-27T14:30:00Z",
  "tenantId": 42,
  "correlationId": "guid-or-request-id",
  "entityType": "Booking",
  "entityId": "1234",
  "data": { /* event-specific payload */ }
}
```

| Envelope field | Type | Required | Description |
|---|---|---|---|
| `eventId` | `guid` | Yes | Globally unique. Maps to `OutboxMessage.Id`. |
| `eventType` | `string` | Yes | From `EventTypes` constants. |
| `schemaVersion` | `int` | Yes | Payload schema version. Start at 1. |
| `occurredUtc` | `datetime` | Yes | Business timestamp (when the action happened). |
| `tenantId` | `int` | Yes | Tenant scope. |
| `correlationId` | `string` | Yes | Request correlation ID for distributed tracing. |
| `entityType` | `string` | Yes | Table/entity name (e.g., `Booking`, `Payment`). |
| `entityId` | `string` | Yes | Primary key of the affected entity. |
| `data` | `object` | Yes | Event-specific payload (see below). |

#### 2.2.1 Event-specific payloads

**`booking.created` / `booking.confirmed` / `booking.modified` / `booking.cancelled`**

```json
{
  "bookingId": 1234,
  "listingId": 56,
  "propertyId": 78,
  "guestId": 90,
  "checkinDate": "2026-03-15",
  "checkoutDate": "2026-03-18",
  "nights": 3,
  "bookingStatus": "Confirmed",
  "bookingSource": "marketplace_direct",
  "totalAmount": 12000.00,
  "finalAmount": 12360.00,
  "commissionAmount": 123.60,
  "commissionPercent": 1.00,
  "currency": "INR",
  "pricingSource": "Public",
  "rateSourceSnapshot": "Manual",
  "previousStatus": "Lead"
}
```

**`commission.calculated`**

```json
{
  "bookingId": 1234,
  "commissionAmount": 123.60,
  "commissionPercent": 1.00,
  "paymentMode": "MARKETPLACE_SPLIT",
  "effectiveRate": 1.00,
  "baseAmount": 12360.00
}
```

**`settlement.completed` / `settlement.failed`**

```json
{
  "paymentId": 567,
  "bookingId": 1234,
  "amount": 12236.40,
  "method": "BankTransfer",
  "status": "completed",
  "razorpayPaymentId": "pay_xxx",
  "failureReason": null
}
```

**`rate.updated`**

```json
{
  "listingId": 56,
  "date": "2026-03-15",
  "oldRate": 4000.00,
  "newRate": 4400.00,
  "source": "Suggested",
  "suggestionId": 789,
  "currency": "INR"
}
```

**`availability.updated`**

```json
{
  "listingId": 56,
  "date": "2026-03-15",
  "roomsAvailable": 2,
  "source": "BookingCreated",
  "bookingId": 1234
}
```

**`restriction.updated`**

```json
{
  "listingId": 56,
  "ruleId": 12,
  "ruleType": "LOS",
  "action": "Created",
  "minNights": 2,
  "seasonStart": "2026-03-01",
  "seasonEnd": "2026-03-31"
}
```

**`subscription.changed`**

```json
{
  "tenantId": 42,
  "oldPlanCode": "BASIC",
  "newPlanCode": "PRO",
  "oldStatus": "Active",
  "newStatus": "Active",
  "changeType": "Upgrade"
}
```

**`boost.changed`**

```json
{
  "propertyId": 78,
  "oldCommissionPercent": 1.00,
  "newCommissionPercent": 5.00,
  "changedByUserId": 101
}
```

**`trustscore.updated`**

```json
{
  "propertyId": 78,
  "oldScore": 0.82,
  "newScore": 0.78,
  "components": {
    "reviewRating": 0.85,
    "bookingCompletionRate": 0.95,
    "cancellationRateInverted": 0.90,
    "responseTime": 0.80,
    "complaintRatioInverted": 1.00,
    "chargebackRatioInverted": 1.00
  }
}
```

### 2.3 Event storage strategy

- EVT-01: All events are stored in the existing `OutboxMessage` table (append-only pattern).
- EVT-02: The `OutboxMessage` table is the **single source of truth** for all domain events. No separate event store in V1.
- EVT-03: Events are written **atomically** in the same transaction as the business operation (transactional outbox pattern).
- EVT-04: The `OutboxDispatcherHostedService` polls for `Status = 'Pending'` and dispatches to local consumers (no Service Bus in V1).
- EVT-05: After successful dispatch, `Status` transitions to `Published` and `PublishedAtUtc` is set.
- EVT-06: Failed dispatch increments `AttemptCount` and sets `NextAttemptUtc` with exponential backoff (base 30s, max 1h, jitter ±20%).

### 2.4 Idempotency rules

- EVT-07: Every consumer MUST check `ConsumedEvent` before processing. If `(ConsumerName, EventId)` exists, skip.
- EVT-08: After successful processing, insert into `ConsumedEvent` with `PayloadHash = SHA256(PayloadJson)`.
- EVT-09: If the consumer crashes after processing but before recording `ConsumedEvent`, the event will be re-delivered. All consumers MUST be idempotent (upsert semantics, not insert-only).
- EVT-10: The `ConsumedEvent.Status` field tracks: `Processed`, `Failed`, `Skipped` (duplicate detected).

### 2.5 Event retention rules

| Table | Retention | Purge strategy |
|-------|-----------|----------------|
| `OutboxMessage` (Published) | 30 days | Weekly purge job: `DELETE TOP(5000) WHERE Status = 'Published' AND PublishedAtUtc < @cutoff` |
| `OutboxMessage` (Failed) | 90 days | Manual review required. Alert at 48h. |
| `OutboxMessage` (Pending) | N/A | Should not persist > 1 hour. Alert if > 100 pending. |
| `ConsumedEvent` | 30 days | Weekly purge job aligned with OutboxMessage. |
| `AuditLog` | 2 years | Monthly purge (batch 5000). |
| `CommunicationLog` | 1 year | Monthly purge (batch 5000). |

- EVT-11: Purge jobs MUST use `DELETE TOP(N)` batches with `WAITFOR DELAY '00:00:00.100'` between batches to avoid lock escalation.
- EVT-12: Purge jobs log `data.purge.completed` with `{table, rowsDeleted, cutoffDate, durationMs}`.

### 2.6 New event types to register

The following MUST be added to `EventTypes.cs`:

| Constant | Value | Category |
|---|---|---|
| `BookingModified` | `booking.modified` | Booking |
| `CommissionCalculated` | `commission.calculated` | Billing |
| `SettlementCompleted` | `settlement.completed` | Billing |
| `SettlementFailed` | `settlement.failed` | Billing |
| `RateUpdated` | `rate.updated` | Pricing |
| `AvailabilityUpdated` | `availability.updated` | Inventory |
| `RestrictionUpdated` | `restriction.updated` | Pricing |
| `SubscriptionChanged` | `subscription.changed` | Billing |
| `BoostChanged` | `boost.changed` | Marketplace |
| `TrustScoreUpdated` | `trustscore.updated` | Trust |
| `SyncCompleted` | `sync.completed` | Sync |
| `SyncFailed` | `sync.failed` | Sync |
| `SuggestionCreated` | `suggestion.created` | Pricing |
| `SuggestionAccepted` | `suggestion.accepted` | Pricing |
| `SuggestionAutoApplied` | `suggestion.auto_applied` | Pricing |
| `ReviewCreated` | `review.created` | Marketplace |
| `InvoiceGenerated` | `invoice.generated` | Billing |

- EVT-13: Helper methods MUST be added: `IsBillingEvent()`, `IsPricingEvent()`, `IsMarketplaceEvent()`, `IsSyncEvent()`, `IsTrustEvent()`.

---

## 3. Aggregation & Snapshot Strategy

### 3.1 Snapshot table definitions

All snapshot tables share these common columns:

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `bigint` (PK, identity) | Auto-increment |
| `Date` | `date` (indexed) | Calendar date (UTC) |
| `ComputedAtUtc` | `datetime2` | When this snapshot was generated |
| `SchemaVersion` | `int` | Payload format version (default 1) |

#### 3.1.1 `Snap_DailyPropertyPerformance`

Per-property, per-day performance metrics.

| Column | Type | Description |
|--------|------|-------------|
| *common columns* | | |
| `TenantId` | `int` (FK, indexed) | Tenant scope |
| `PropertyId` | `int` (FK, indexed) | Property scope |
| `ListingCount` | `int` | Active listings on this date |
| `TotalRoomsAvailable` | `int` | Sum of available rooms across listings |
| `TotalRoomsSold` | `int` | Sum of rooms booked |
| `OccupancyRate` | `decimal(5,4)` | `RoomsSold / RoomsAvailable` |
| `Revenue` | `decimal(18,2)` | Total revenue from bookings overlapping this date |
| `Adr` | `decimal(18,2)` | `Revenue / RoomsSold` (0 if no sales) |
| `RevPar` | `decimal(18,2)` | `Revenue / RoomsAvailable` |
| `BookingCount` | `int` | Bookings overlapping this date |
| `CancellationCount` | `int` | Cancellations with this date in range |
| `DirectBookingCount` | `int` | Bookings from marketplace sources |
| `OtaBookingCount` | `int` | Bookings from OTA sources |
| `AvgNightlyRate` | `decimal(18,2)` | Average `ListingDailyRate` across listings |
| `Currency` | `varchar(3)` | `INR` |

**Unique constraint**: `IX_SnapDPP_Tenant_Property_Date` on `(TenantId, PropertyId, Date)`.

#### 3.1.2 `Snap_DailyTenantPerformance`

Per-tenant, per-day roll-up of property performance.

| Column | Type | Description |
|--------|------|-------------|
| *common columns* | | |
| `TenantId` | `int` (FK, indexed) | Tenant scope |
| `PropertyCount` | `int` | Active properties |
| `ListingCount` | `int` | Active listings |
| `TotalRoomsAvailable` | `int` | Across all properties |
| `TotalRoomsSold` | `int` | |
| `OccupancyRate` | `decimal(5,4)` | |
| `Revenue` | `decimal(18,2)` | |
| `Adr` | `decimal(18,2)` | |
| `RevPar` | `decimal(18,2)` | |
| `BookingCount` | `int` | |
| `CancellationCount` | `int` | |
| `DirectBookingPercent` | `decimal(5,2)` | |
| `OtaDependencyPercent` | `decimal(5,2)` | |
| `CommissionPaid` | `decimal(18,2)` | Total commission for marketplace bookings |
| `SubscriptionCost` | `decimal(18,2)` | Pro-rated daily subscription cost |
| `NetRevenue` | `decimal(18,2)` | `Revenue - CommissionPaid - SubscriptionCost` |
| `Currency` | `varchar(3)` | `INR` |

**Unique constraint**: `IX_SnapDTP_Tenant_Date` on `(TenantId, Date)`.

#### 3.1.3 `Snap_DailyMarketplacePerformance`

Platform-wide, per-day marketplace metrics. **Not tenant-scoped** — one row per date.

| Column | Type | Description |
|--------|------|-------------|
| *common columns* | | |
| `TotalGmv` | `decimal(18,2)` | Sum of all `FinalAmount` |
| `CommissionRevenue` | `decimal(18,2)` | Sum of all `CommissionAmount` |
| `BoostRevenue` | `decimal(18,2)` | Commission above floor (premium commission) |
| `SubscriptionRevenue` | `decimal(18,2)` | Sum of active subscription invoices pro-rated |
| `TotalRevenue` | `decimal(18,2)` | `CommissionRevenue + SubscriptionRevenue` |
| `ActiveTenants` | `int` | Tenants with ≥ 1 confirmed booking |
| `ActiveListings` | `int` | Listings with `Status = 'Active'` |
| `TotalBookings` | `int` | Bookings created on this date |
| `TotalCancellations` | `int` | Cancellations on this date |
| `DirectBookings` | `int` | Marketplace-sourced bookings |
| `OtaBookings` | `int` | OTA-sourced bookings |
| `ConversionRate` | `decimal(5,4)` | `Bookings / PropertyViews` (from view log) |
| `AvgCommissionPercent` | `decimal(5,2)` | Platform-wide average |
| `Currency` | `varchar(3)` | `INR` |

**Unique constraint**: `IX_SnapDMP_Date` on `(Date)`.

#### 3.1.4 `Snap_DailyRevenueMetrics`

Per-tenant, per-day revenue breakdown by source and type.

| Column | Type | Description |
|--------|------|-------------|
| *common columns* | | |
| `TenantId` | `int` (FK, indexed) | |
| `RevenueSource` | `varchar(30)` | `marketplace_direct`, `ota_channex`, `ota_ical`, `walk_in`, etc. |
| `BookingCount` | `int` | Bookings from this source |
| `Revenue` | `decimal(18,2)` | Total revenue from this source |
| `CommissionAmount` | `decimal(18,2)` | Commission from this source |
| `AvgRate` | `decimal(18,2)` | Average nightly rate for this source |
| `Currency` | `varchar(3)` | `INR` |

**Unique constraint**: `IX_SnapDRM_Tenant_Source_Date` on `(TenantId, RevenueSource, Date)`.

#### 3.1.5 `Snap_DailyTrustScore`

Per-property, per-day TrustScore snapshot for trend analysis.

| Column | Type | Description |
|--------|------|-------------|
| *common columns* | | |
| `TenantId` | `int` (FK, indexed) | |
| `PropertyId` | `int` (FK, indexed) | |
| `TrustScore` | `decimal(5,4)` | Composite score 0.0000–1.0000 |
| `ReviewRating` | `decimal(5,4)` | Component value |
| `BookingCompletionRate` | `decimal(5,4)` | |
| `CancellationRateInverted` | `decimal(5,4)` | |
| `ResponseTime` | `decimal(5,4)` | |
| `ComplaintRatioInverted` | `decimal(5,4)` | |
| `ChargebackRatioInverted` | `decimal(5,4)` | |
| `ReviewCount` | `int` | Total reviews at snapshot time |
| `BookingCount90d` | `int` | Trailing 90-day bookings |
| `TrustMultiplier` | `decimal(3,2)` | Resulting multiplier (RA-004 §2.2) |

**Unique constraint**: `IX_SnapDTS_Tenant_Property_Date` on `(TenantId, PropertyId, Date)`.

#### 3.1.6 `Snap_DailyDemandSignals`

Per-listing, per-day active demand signal summary.

| Column | Type | Description |
|--------|------|-------------|
| *common columns* | | |
| `TenantId` | `int` (FK, indexed) | |
| `ListingId` | `int` (FK, indexed) | |
| `ActiveSignalCount` | `int` | Active `DemandSignal` rows |
| `HighestSeverity` | `varchar(10)` | `HIGH`, `MEDIUM`, `LOW`, or `NONE` |
| `SignalTypesSummary` | `varchar(500)` | Comma-separated list of active signal types |
| `PendingSuggestionCount` | `int` | PENDING `PriceSuggestion` rows |
| `AcceptedSuggestionsToday` | `int` | Accepted today |
| `AutoAppliedToday` | `int` | Auto-applied today |

**Unique constraint**: `IX_SnapDDS_Tenant_Listing_Date` on `(TenantId, ListingId, Date)`.

### 3.2 Snapshot generation schedule

| Snapshot | Schedule | Job name | Estimated duration (100k tenants) |
|----------|----------|----------|:---------------------------------:|
| `Snap_DailyPropertyPerformance` | 02:00 UTC daily | `SnapshotPropertyPerformanceJob` | < 30 min |
| `Snap_DailyTenantPerformance` | 02:30 UTC daily (after property) | `SnapshotTenantPerformanceJob` | < 15 min |
| `Snap_DailyMarketplacePerformance` | 03:00 UTC daily (after tenant) | `SnapshotMarketplacePerformanceJob` | < 5 min |
| `Snap_DailyRevenueMetrics` | 02:15 UTC daily | `SnapshotRevenueMetricsJob` | < 20 min |
| `Snap_DailyTrustScore` | 03:15 UTC daily (after TrustScore recompute) | `SnapshotTrustScoreJob` | < 10 min |
| `Snap_DailyDemandSignals` | 03:30 UTC daily | `SnapshotDemandSignalsJob` | < 10 min |

- AGG-01: All snapshot jobs MUST run sequentially in dependency order within the nightly window (02:00–04:00 UTC).
- AGG-02: Jobs are orchestrated by a single `SnapshotOrchestratorHostedService` that runs each job in sequence and logs completion.
- AGG-03: If a job fails, the orchestrator MUST continue to the next job (not block the pipeline) and emit `data.snapshot.failed` alert.
- AGG-04: Each job is idempotent (upsert on unique constraint). Safe to re-run manually.

### 3.3 Recalculation policy

- AGG-05: Snapshot jobs compute data for **yesterday** (UTC) by default.
- AGG-06: A manual backfill can be triggered for any date range up to 90 days in the past via admin API: `POST /admin/data/backfill?startDate=X&endDate=Y&snapshotType=Z`.
- AGG-07: Backfill runs the same job logic but for the specified date range, processing one date at a time.
- AGG-08: Backfill MUST be throttled: maximum 10 dates per minute to avoid OLTP impact.

### 3.4 Backfill strategy

| Scenario | Backfill approach |
|----------|------------------|
| Bug fix in calculation logic | Admin triggers backfill for affected date range. Old snapshot rows are overwritten (upsert). |
| New snapshot table added | Initial backfill for 90 days of history. Run during low-traffic window. |
| Schema migration adds new column | Backfill all dates where column is NULL. |
| Missed nightly run | Orchestrator detects gap and auto-backfills missed dates on next run. |

- AGG-09: The orchestrator MUST track `LastSuccessfulSnapshotDate` per snapshot type in a `SnapshotJobState` table.
- AGG-10: On startup, the orchestrator checks for gaps between `LastSuccessfulSnapshotDate` and yesterday. If gaps exist, auto-backfills before running today's snapshot.

### 3.5 Late-arriving event handling

"Late-arriving events" are domain events that describe actions on a past date (e.g., a booking cancellation received today for a stay that ended last week).

- AGG-11: Late events are processed normally in L1 OLTP (booking status changes, payment updates).
- AGG-12: The snapshot for the affected past date is NOT automatically recalculated (too expensive to re-scan all past dates on every event).
- AGG-13: Instead, a `SnapshotCorrectionQueue` (new table) records the affected date + tenant + snapshot type.
- AGG-14: A correction job runs hourly, processes queued corrections, and updates only the affected snapshot rows.
- AGG-15: Correction queue retention: 7 days. If a correction is not processed within 7 days, it expires and is handled by the next manual backfill.

#### 3.5.1 `SnapshotCorrectionQueue`

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `bigint` (PK, identity) | |
| `SnapshotType` | `varchar(50)` | e.g., `DailyPropertyPerformance` |
| `TenantId` | `int` | |
| `EntityId` | `int` | PropertyId or ListingId |
| `AffectedDate` | `date` | The date whose snapshot needs recalculation |
| `Reason` | `varchar(100)` | e.g., `booking.cancelled`, `payment.refunded` |
| `QueuedAtUtc` | `datetime2` | |
| `ProcessedAtUtc` | `datetime2?` | NULL until processed |
| `Status` | `varchar(20)` | `Queued`, `Processed`, `Expired` |

**Index**: `IX_SnapCQ_Status` on `(Status, QueuedAtUtc)` WHERE `Status = 'Queued'`.

---

## 4. Marketplace Intelligence Metrics

### 4.1 Tenant-level metric formulas

All monetary formulas use `decimal(18,2)` precision. Percentage formulas use `decimal(5,2)` for display and `decimal(5,4)` for intermediate computation.

#### 4.1.1 GMV (Gross Merchandise Value)

```
GMV = SUM(Booking.FinalAmount)
  WHERE Booking.BookingStatus IN ('Confirmed', 'CheckedIn', 'CheckedOut')
  AND Booking.TenantId = @tenantId
  AND Booking.CheckinDate WITHIN @period
```

- MET-01: GMV includes **all** booking sources (marketplace, OTA, direct, walk-in).
- MET-02: Cancelled bookings are excluded from GMV.
- MET-03: `FinalAmount` is the guest-facing total (includes taxes, convenience fees). This is consistent with RA-001 §4.

#### 4.1.2 Net revenue

```
NetRevenue = GMV - CommissionPaid - SubscriptionCost
```

Where:
- `CommissionPaid = SUM(Booking.CommissionAmount) WHERE PaymentModeSnapshot = 'MARKETPLACE_SPLIT'`
- `SubscriptionCost = SUM(BillingInvoice.TotalInr WHERE Type = 'subscription' AND Status = 'Paid')` prorated to period

- MET-04: For HOST_DIRECT tenants, `CommissionPaid` includes invoiced commission: `SUM(BillingInvoice.TotalInr WHERE Type = 'commission')`.

#### 4.1.3 Direct booking %

```
DirectBookingPercent = (MarketplaceBookings / TotalBookings) * 100
```

Where `MarketplaceBookings` = bookings with `BookingSource LIKE 'marketplace_%'`.

- MET-05: `TotalBookings` includes all confirmed/checked-in/checked-out bookings in the period.
- MET-06: If `TotalBookings = 0`, `DirectBookingPercent = 0` (not NULL or NaN).

#### 4.1.4 OTA dependency %

```
OtaDependencyPercent = 100 - DirectBookingPercent
```

- MET-07: Includes all non-marketplace sources (Airbnb, Booking.com, Agoda, walk-in, direct, etc.).

#### 4.1.5 ADR (Average Daily Rate)

```
ADR = TotalRevenue / NightsSold
```

Where:
- `TotalRevenue = SUM(Booking.AmountReceived)` for active bookings in period
- `NightsSold = SUM(nights)` where `nights = DATEDIFF(day, EffectiveCheckin, EffectiveCheckout)`
- Effective dates are clamped to the report period boundaries

- MET-08: If `NightsSold = 0`, `ADR = 0`.
- MET-09: ADR calculation MUST match the existing `AdminReportsController.GetAnalytics()` logic.

#### 4.1.6 RevPAR (Revenue Per Available Room)

```
RevPAR = TotalRevenue / NightsAvailable
```

Where `NightsAvailable = DaysInPeriod * ActiveListingCount`.

- MET-10: If `NightsAvailable = 0`, `RevPAR = 0`.

#### 4.1.7 Occupancy %

```
OccupancyPercent = (NightsSold / NightsAvailable) * 100
```

- MET-11: Capped at 100% (overbooking scenarios excluded from percentage display).

### 4.2 Marketplace-level (platform) metric formulas

#### 4.2.1 Total GMV

```
TotalGMV = SUM(Booking.FinalAmount)
  WHERE Booking.BookingStatus IN ('Confirmed', 'CheckedIn', 'CheckedOut')
  AND Booking.CheckinDate WITHIN @period
  -- No tenant filter (platform-wide)
```

#### 4.2.2 Commission revenue

```
CommissionRevenue = SUM(Booking.CommissionAmount)
  WHERE Booking.BookingStatus IN ('Confirmed', 'CheckedIn', 'CheckedOut')
  AND Booking.PaymentModeSnapshot = 'MARKETPLACE_SPLIT'
  AND Booking.CheckinDate WITHIN @period
```

#### 4.2.3 Boost revenue

```
BoostRevenue = SUM(
  Booking.CommissionAmount - (Booking.FinalAmount * FloorCommissionPercent / 100)
)
  WHERE Booking.CommissionPercentSnapshot > FloorCommissionPercent
  AND Booking.CheckinDate WITHIN @period
```

Where `FloorCommissionPercent` = `Commission:FloorPercent` config (V1: 1.00).

- MET-12: Boost revenue represents the **premium** above the floor commission that tenants voluntarily paid for ranking benefits.

#### 4.2.4 Active listings

```
ActiveListings = COUNT(Listing)
  WHERE Listing.Status = 'Active'
  -- Snapshot on the date in question
```

#### 4.2.5 Conversion rate

```
ConversionRate = TotalBookings / TotalPropertyViews
```

- MET-13: V1: Property views stored in `PropertyViewDaily` table (RA-003 §8.2). If not yet implemented, conversion rate = NULL.

#### 4.2.6 Churn rate

```
ChurnRate = (CancelledOrSuspendedTenants / ActiveTenantsAtPeriodStart) * 100
```

Where `CancelledOrSuspendedTenants` = tenants whose `TenantSubscription.Status` changed to `Cancelled` or `Suspended` during the period.

### 4.3 Precision and rounding rules

| Data type | Precision | Rounding | Display format |
|-----------|-----------|----------|----------------|
| Monetary (INR) | `decimal(18,2)` | Round half-up to 2 decimal places | `₹12,345.67` (Indian comma formatting) |
| Percentage (display) | `decimal(5,2)` | Round half-up to 2 decimal places | `67.83%` |
| Percentage (intermediate) | `decimal(5,4)` | No rounding until final display | Internal only |
| Rate / score | `decimal(5,4)` | Round half-up to 4 decimal places | `0.7500` |
| Count | `int` | No rounding | `1,234` |

- MET-14: All division operations MUST check for zero denominators. Return 0 (not NULL, NaN, or Infinity).
- MET-15: Currency is always `INR` in V1. Multi-currency support is V2 (column exists for forward compatibility).
- MET-16: Indian number formatting (lakhs/crores) MUST be applied client-side. API returns raw numbers.

---

## 5. Ranking & TrustScore Computation Engine

### 5.1 TrustScore calculation pipeline

The TrustScore for each property is a composite score defined in RA-004 §2.1. The data platform is responsible for efficient computation.

```
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│  Trigger Event    │────→│  Delta Detector  │────→│  Score Computer  │
│  (booking, review,│     │  (checks if      │     │  (recomputes     │
│   cancellation,   │     │   relevant       │     │   affected       │
│   chargeback)     │     │   component      │     │   component      │
│                   │     │   changed)       │     │   only)          │
└──────────────────┘     └──────────────────┘     └───────┬──────────┘
                                                          │
                                                          ▼
                                                  ┌──────────────────┐
                                                  │  Score Cache     │
                                                  │  (PropertyId →   │
                                                  │   TrustScore +   │
                                                  │   components)    │
                                                  │  In-DB table     │
                                                  └───────┬──────────┘
                                                          │
                                              ┌───────────┴──────────┐
                                              ▼                      ▼
                                      ┌──────────────┐     ┌─────────────────┐
                                      │  Ranking     │     │  Snap_Daily     │
                                      │  (live use)  │     │  TrustScore     │
                                      └──────────────┘     │  (daily archive)│
                                                           └─────────────────┘
```

### 5.2 Incremental recalculation strategy

The naive approach — recalculating TrustScore for ALL properties on every event — does not scale to 100k tenants.

- TSC-01: TrustScore MUST use **incremental recomputation**: only affected properties, only changed components.
- TSC-02: Each component has a set of trigger events:

| Component | Trigger events | Recalculation |
|-----------|---------------|---------------|
| ReviewRating | `review.created` | Recompute only for the reviewed property |
| BookingCompletionRate | `booking.confirmed`, `stay.checked_out` | Recompute for the property on the booking |
| CancellationRate | `booking.cancelled` | Recompute for the property on the cancelled booking |
| ResponseTime | (communication log entry) | Recompute for the property associated with the booking |
| ComplaintRatio | (manual flag / refund request) | Recompute for the property |
| ChargebackRatio | (payment dispute event) | Recompute for the property |

- TSC-03: On trigger, only the specific component is recomputed using a 90-day trailing query scoped to the single property.
- TSC-04: The composite TrustScore is recalculated from the cached components (weighted sum). No full DB scan.

### 5.3 `PropertyTrustScoreCache` (new entity)

Live cache of the latest TrustScore per property. Updated incrementally.

| Column | Type | Description |
|--------|------|-------------|
| `PropertyId` | `int` (PK, FK) | One row per property |
| `TenantId` | `int` (FK, indexed) | Tenant scope |
| `TrustScore` | `decimal(5,4)` | Composite score |
| `ReviewRating` | `decimal(5,4)` | Component |
| `BookingCompletionRate` | `decimal(5,4)` | Component |
| `CancellationRateInverted` | `decimal(5,4)` | Component |
| `ResponseTime` | `decimal(5,4)` | Component |
| `ComplaintRatioInverted` | `decimal(5,4)` | Component |
| `ChargebackRatioInverted` | `decimal(5,4)` | Component |
| `ReviewCount` | `int` | For dampener calculation |
| `BookingCount90d` | `int` | Trailing booking count |
| `TrustMultiplier` | `decimal(3,2)` | RA-004 §2.2 |
| `LastComputedAtUtc` | `datetime2` | |
| `LastTriggerEvent` | `varchar(50)` | Event that caused last recomputation |

- TSC-05: This table is NOT tenant-filtered (accessed cross-tenant by the ranking engine via `IgnoreQueryFilters()`).
- TSC-06: Cold-start defaults (RA-004 §2.5) are written when a property is first created.

### 5.4 Frequency of recalculation

| Mode | Frequency | Scope |
|------|-----------|-------|
| **Event-driven** (primary) | On each trigger event | Single property, single component |
| **Batch sweep** (safety net) | Every 15 minutes | Properties not recomputed in last 24h (catch missed events) |
| **Full recalculation** | Nightly at 03:00 UTC | All properties (before daily snapshot) |

- TSC-07: The 15-minute sweep MUST process at most 1000 properties per cycle. If more are stale, the next cycle catches the rest.
- TSC-08: The nightly full recalculation ensures eventual consistency. It is the **authoritative** computation; event-driven updates are best-effort optimization.

### 5.5 Weight update strategy

- TSC-09: Component weights are loaded from `IOptions<TrustScoreSettings>` (not hardcoded).
- TSC-10: Weight changes take effect on the next nightly full recalculation. Event-driven updates use the weights that were active at the time of the last full calculation.
- TSC-11: Weight changes MUST be audit-logged: `trust.weights.changed` with `{oldWeights, newWeights, changedBy}`.

### 5.6 Historical score retention

- TSC-12: `Snap_DailyTrustScore` retains daily snapshots for 2 years.
- TSC-13: `PropertyTrustScoreCache` retains only the current score (one row per property).
- TSC-14: Significant changes (> 0.10 in a single computation) trigger `trustscore.updated` outbox event with full component breakdown.

---

## 6. Pricing Intelligence Data Support

This section defines the data infrastructure that supports the pricing intelligence engine (RA-AI-001).

### 6.1 Demand signal historical storage

- PID-01: `DemandSignal` table (defined in RA-AI-001 §2.1.3) retains signals for 6 months.
- PID-02: Daily snapshot `Snap_DailyDemandSignals` aggregates signal counts per listing for trend analysis.
- PID-03: A monthly aggregation view `vw_MonthlyDemandSignalTrends` summarizes signal frequency by type per listing:

```sql
CREATE VIEW vw_MonthlyDemandSignalTrends AS
SELECT
    TenantId,
    ListingId,
    SignalType,
    DATEADD(MONTH, DATEDIFF(MONTH, 0, DetectedAtUtc), 0) AS MonthStart,
    COUNT(*) AS SignalCount,
    SUM(CASE WHEN Status = 'CONSUMED' THEN 1 ELSE 0 END) AS ConsumedCount,
    SUM(CASE WHEN Status = 'EXPIRED' THEN 1 ELSE 0 END) AS ExpiredCount,
    SUM(CASE WHEN Status = 'SUPPRESSED' THEN 1 ELSE 0 END) AS SuppressedCount
FROM DemandSignal
GROUP BY TenantId, ListingId, SignalType,
    DATEADD(MONTH, DATEDIFF(MONTH, 0, DetectedAtUtc), 0);
```

### 6.2 Suggestion acceptance rate tracking

- PID-04: Acceptance rate is computed from `PriceSuggestion` directly:

```
AcceptanceRate = COUNT(Status = 'ACCEPTED' OR Status = 'AUTO_APPLIED')
              / COUNT(Status IN ('ACCEPTED','REJECTED','EXPIRED','AUTO_APPLIED'))
              * 100
```

- PID-05: `Snap_DailyDemandSignals` includes `AcceptedSuggestionsToday` and `AutoAppliedToday` for trend analysis.
- PID-06: Monthly acceptance rate is queryable via `vw_SuggestionHistory` (RA-AI-001 §2.1.5).

### 6.3 Revenue uplift measurement

To measure the effectiveness of pricing suggestions:

- PID-07: Bookings carry `RateSourceSnapshot` (RA-AI-001 §5.4): `Manual`, `Suggested`, `AutoSuggested`.
- PID-08: Revenue uplift is measured by comparing cohorts:

```
RevenueFromSuggestions = SUM(Booking.FinalAmount WHERE RateSourceSnapshot IN ('Suggested', 'AutoSuggested'))
RevenueFromManual     = SUM(Booking.FinalAmount WHERE RateSourceSnapshot = 'Manual')
```

- PID-09: A/B effectiveness is measured per listing over 30-day windows:

```
UpliftPercent = ((ADR_suggested - ADR_manual) / ADR_manual) * 100
```

Where `ADR_suggested` = ADR for dates where a suggestion was active, `ADR_manual` = ADR for dates with manual pricing.

### 6.4 Before/after performance comparison

- PID-10: When a tenant first enables pricing intelligence, the system snapshots their baseline metrics:

#### `PricingIntelligenceBaseline` (new entity)

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `int` (PK, identity) | |
| `TenantId` | `int` (FK) | |
| `ListingId` | `int` (FK) | |
| `EnabledAtUtc` | `datetime2` | When pricing intelligence was enabled |
| `BaselineAdr` | `decimal(18,2)` | ADR for 30 days before enablement |
| `BaselineRevPar` | `decimal(18,2)` | RevPAR for 30 days before |
| `BaselineOccupancy` | `decimal(5,4)` | Occupancy for 30 days before |
| `BaselineRevenue` | `decimal(18,2)` | Total revenue for 30 days before |

- PID-11: Baseline is computed once and never updated. Used for "before vs. after" comparison in tenant dashboard.
- PID-12: After 30 days, the dashboard shows: "Since enabling Pricing Intelligence: ADR +X%, RevPAR +Y%, Occupancy +Z%".

### 6.5 Experiment tagging support

- PID-13: V1 does not support formal A/B experiments. However, the data model is prepared:
  - `PriceSuggestion.StrategySource` distinguishes `RULE_ENGINE` vs `ML_MODEL` (V2).
  - `ListingDailyRate.Source` distinguishes `Manual` vs `Suggested` vs `AutoSuggested`.
  - `Booking.RateSourceSnapshot` captures the pricing strategy at booking time.
- PID-14: V2 experiment support will add an `ExperimentTag` column to `PriceSuggestion` and `ListingDailyRate` for grouping results by experiment cohort.

---

## 7. Reporting & Dashboard Requirements

### 7.1 Tenant dashboards

#### 7.1.1 Revenue trends

| Metric | Data source | Period options | Display |
|--------|-------------|---------------|---------|
| Monthly revenue trend | `Snap_DailyTenantPerformance` aggregated | 3m, 6m, 12m | Line chart |
| Revenue by source | `Snap_DailyRevenueMetrics` | 7d, 30d, 90d, custom | Stacked bar |
| ADR trend | `Snap_DailyPropertyPerformance` | 3m, 6m, 12m | Line chart |
| RevPAR trend | Same | Same | Line chart |
| Current month vs previous | `Snap_DailyTenantPerformance` | Current + previous month | Comparison cards |

#### 7.1.2 Occupancy heatmap

| Dimension | Description |
|-----------|-------------|
| X-axis | Days of month (1–31) |
| Y-axis | Listings |
| Cell color | Occupancy: Red (0%), Yellow (50%), Green (100%) |
| Data source | `Snap_DailyPropertyPerformance` or live query for current month forward |
| Interaction | Click cell → shows booking details |

- RPT-01: The heatmap for **future dates** MUST use live L1 data (bookings + blocks). Past dates use L2 snapshots.
- RPT-02: Heatmap API MUST return at most 31 days × 50 listings per request (1,550 cells).

#### 7.1.3 Pricing suggestion performance

| Metric | Data source | Display |
|--------|-------------|---------|
| Suggestions generated (period) | `PriceSuggestion` count | Number |
| Acceptance rate | `PriceSuggestion` status distribution | Percentage + donut chart |
| Revenue from suggestions | Bookings with `RateSourceSnapshot` IN suggested | ₹ total + % of total revenue |
| Average uplift | `AVG(PriceSuggestion.ChangePercent)` for accepted | Percentage |
| Before vs. after comparison | `PricingIntelligenceBaseline` vs current | Comparison cards |

#### 7.1.4 Boost ROI report

| Metric | Formula | Display |
|--------|---------|---------|
| Boost spend | `CommissionPaid - (BookingCount * FloorPercent * AvgAmount / 100)` | ₹ total |
| Marketplace views | `PropertyViewDaily` aggregate | Number |
| Marketplace bookings | Bookings with marketplace source | Number |
| Estimated additional revenue | `MarketplaceRevenue - EstimatedOrganicRevenue` | ₹ total |
| ROI ratio | `(AdditionalRevenue - BoostSpend) / BoostSpend` | Ratio or "N/A" |

### 7.2 Admin dashboards

#### 7.2.1 GMV trend

| Metric | Data source | Period | Display |
|--------|-------------|--------|---------|
| Daily GMV | `Snap_DailyMarketplacePerformance.TotalGmv` | 30d, 90d, 12m | Line chart |
| GMV growth | Month-over-month | Last 12 months | Percentage trend |
| GMV by city | `Snap_DailyPropertyPerformance` joined with `Property.City` | Current month | Horizontal bar |
| GMV by source | `Snap_DailyRevenueMetrics` aggregated platform-wide | 30d | Pie chart |

#### 7.2.2 Revenue mix

| Metric | Formula | Display |
|--------|---------|---------|
| Subscription revenue | `SUM(BillingPayment.AmountInr WHERE Status = 'Completed')` | ₹ total + % of total |
| Commission revenue | `Snap_DailyMarketplacePerformance.CommissionRevenue` | ₹ total + % of total |
| Boost revenue | `Snap_DailyMarketplacePerformance.BoostRevenue` | ₹ total + % of total |
| Total platform revenue | Sum of above | ₹ total |
| MRR | `SUM(ActiveKeyCount * PricePerKey)` | ₹ total |
| ARPU | `TotalRevenue / ActiveTenants` | ₹ per tenant |

#### 7.2.3 Sync reliability

| Metric | Data source | Display |
|--------|-------------|---------|
| iCal sync success rate (24h) | Structured logs: `ical.sync.*` | Percentage |
| Channex webhook success rate (24h) | Structured logs: `channex.webhook.*` | Percentage |
| Stale syncs (> threshold) | `ChannelConfig.LastSyncAt` < threshold | Count + list |
| Failed syncs (24h) | `OutboxMessage` with sync events + Failed status | Count + list |
| ARI push success rate | Structured logs: `ari.push.*` | Percentage |

#### 7.2.4 Settlement success rate

| Metric | Data source | Display |
|--------|-------------|---------|
| Settlements completed (period) | `Payment` with method = BankTransfer, status = completed | Count + ₹ total |
| Settlements failed | Same, status = failed | Count + ₹ total |
| Settlement success rate | Completed / Total | Percentage |
| Pending settlements | status = pending > 48h | Count (alert if > 0) |

#### 7.2.5 Fraud indicators

| Indicator | Data source | Threshold | Display |
|-----------|-------------|-----------|---------|
| Commission oscillation | `AuditLog` with `tenant.commission.changed` | > 3 changes / 7 days | Count of flagged tenants |
| Suspicious booking patterns | Self-booking detection (RA-004) | Any match | Count |
| TrustScore drops | `Snap_DailyTrustScore` delta | > 0.15 single-day drop | List |
| Chargeback spike | `Payment` disputes | > 1% of bookings | Percentage |
| Review manipulation | Review timing + pattern detection | Algorithmic | Count |

### 7.3 Refresh SLAs

| Category | Refresh target | Implementation |
|----------|:-------------:|----------------|
| **Real-time** | < 5 seconds | Live L1 queries. Examples: current booking count, pending payments, active listing count. |
| **Near-real-time** | < 15 minutes | Event-driven cache invalidation. Examples: TrustScore after booking event, sync status after webhook. |
| **Daily** | Updated by 04:00 UTC | L2 snapshot tables. Examples: all trend charts, GMV history, revenue breakdown, TrustScore trends. |
| **On-demand** | When admin triggers | Backfill, reconciliation reports, debug bundles. |

- RPT-03: Dashboard API response time MUST be < 2 seconds for all endpoints (P95).
- RPT-04: Real-time queries MUST NOT involve table scans. Use covering indexes.
- RPT-05: Daily dashboard data MUST show `Data as of: {ComputedAtUtc}` timestamp.

---

## 8. Performance & Scale Requirements (100k Tenants)

### 8.1 Indexing strategy

#### 8.1.1 Mandatory indexes on L1 OLTP tables

All existing indexes defined in `AppDbContext.OnModelCreating()` are retained. Additional indexes required for analytics queries:

| Table | Index | Columns | Purpose |
|-------|-------|---------|---------|
| `Bookings` | `IX_Booking_TenantId_CheckinDate_Status` | `(TenantId, CheckinDate, BookingStatus)` | Revenue queries by period |
| `Bookings` | `IX_Booking_TenantId_BookingSource` | `(TenantId, BookingSource)` INCLUDE `(FinalAmount, CommissionAmount)` | Source-based revenue split |
| `Bookings` | `IX_Booking_TenantId_CancelledAtUtc` | `(TenantId, CancelledAtUtc)` WHERE `CancelledAtUtc IS NOT NULL` | Cancellation rate computation |
| `Payments` | `IX_Payment_TenantId_Status_ReceivedOn` | `(TenantId, Status, ReceivedOn)` | Settlement queries |
| `OutboxMessage` | `IX_OutboxMessage_EventType_OccurredUtc` | `(EventType, OccurredUtc)` | Event replay and audit |
| `AuditLogs` | `IX_AuditLog_Action_TimestampUtc` | `(Action, TimestampUtc)` | Fraud signal detection |
| `Reviews` | `IX_Review_ListingId_CreatedAt` | `(ListingId, CreatedAt)` | TrustScore review component |

#### 8.1.2 Mandatory indexes on L2 snapshot tables

| Table | Index | Columns | Purpose |
|-------|-------|---------|---------|
| All `Snap_*` | Primary unique constraint | See section 3.1 | Upsert idempotency |
| `Snap_DailyPropertyPerformance` | `IX_SnapDPP_Date` | `(Date)` INCLUDE `(TotalGmv, BookingCount)` | Platform-wide aggregation |
| `Snap_DailyTenantPerformance` | `IX_SnapDTP_TenantId` | `(TenantId)` | Tenant dashboard |
| `Snap_DailyMarketplacePerformance` | Clustered on `Date` | — | Sequential scan |

### 8.2 Partitioning strategy

- SCL-01: V1 does NOT use SQL Server table partitioning (requires Enterprise edition or Azure SQL Hyperscale).
- SCL-02: Instead, use **logical partitioning** via date-based filtering and tenant-scoped queries.
- SCL-03: V2 consideration: if snapshot tables exceed 100M rows, evaluate Azure SQL Hyperscale with partition functions on `(TenantId, Date)`.
- SCL-04: Archive strategy (section 8.5) keeps active table sizes manageable.

### 8.3 Hot vs cold data separation

| Data category | Hot window | Cold threshold | Storage |
|---|---|---|---|
| Bookings (OLTP) | Current + future 90 days | > 2 years | Archive to `Bookings_Archive` (same schema, no indexes beyond PK) |
| Snapshots (L2) | Last 12 months | > 2 years | Archive to `Snap_Archive_*` tables |
| OutboxMessage | Last 30 days (Published) | > 30 days | Purge (section 2.5) |
| AuditLog | Last 6 months | > 2 years | Purge (section 2.5) |
| DemandSignal | Last 90 days (active) | > 6 months | Purge (RA-AI-001 §2.4) |
| PriceSuggestion | Last 6 months (active) | > 2 years | Archive |

- SCL-05: "Hot" data is in the primary table with full indexing. "Cold" data in archive tables with minimal indexing.
- SCL-06: Archive jobs run monthly on the first Sunday at 04:00 UTC.

### 8.4 Aggregation compute strategy

- SCL-07: All snapshot jobs are implemented as `IHostedService` workers using `PeriodicTimer`.
- SCL-08: Jobs process tenants in batches of 500. Each batch is a single `DbContext` scope (short-lived connection).
- SCL-09: Within each batch, use a single `MERGE` statement per snapshot type for upsert (not row-by-row).
- SCL-10: Snapshot queries MUST use `WITH (NOLOCK)` hint on L1 source tables to avoid blocking OLTP transactions.
- SCL-11: Jobs MUST yield between batches: `await Task.Delay(100)` to prevent monopolizing the connection pool.
- SCL-12: Total nightly snapshot window MUST complete within 2 hours for 100k tenants.

### 8.5 Avoiding heavy cross-tenant joins

- SCL-13: Platform-level metrics (e.g., `Snap_DailyMarketplacePerformance`) MUST be computed by **aggregating L2 tenant snapshots**, NOT by scanning L1 OLTP tables across tenants.

```
Snap_DailyMarketplacePerformance.TotalGmv =
  SUM(Snap_DailyTenantPerformance.Revenue) for same date
```

- SCL-14: The only exception is the initial property-level snapshot, which reads from `Bookings` with a `TenantId` filter (never cross-tenant scan).
- SCL-15: Dashboard APIs MUST always include `TenantId` in WHERE clauses. No full-table scans.
- SCL-16: Admin platform-wide queries MUST use L2 or L3 tables, never L1.

### 8.6 Connection pool management

- SCL-17: Snapshot jobs MUST use a separate connection string with `Max Pool Size = 5` to avoid starving the API connection pool.
- SCL-18: API connection pool: `Max Pool Size = 100` (Azure SQL Basic/Standard tier default).
- SCL-19: All snapshot queries MUST have a `CommandTimeout = 120` seconds (vs API default of 30 seconds).

---

## 9. Data Quality & Integrity Controls

### 9.1 Reconciliation jobs

| Job | Frequency | Scope | Logic |
|-----|-----------|-------|-------|
| **Booking-Payment reconciliation** | Daily at 05:00 UTC | Per tenant | Verify `SUM(Payment.Amount WHERE Status = 'completed')` matches `Booking.AmountReceived` for each booking |
| **Commission reconciliation** | Daily at 05:15 UTC | Platform-wide | Verify `SUM(Booking.CommissionAmount)` matches commission invoice totals and settlement deductions |
| **Snapshot-OLTP reconciliation** | Weekly (Sunday 06:00 UTC) | Sample: 100 random tenants | Compare `Snap_DailyTenantPerformance.Revenue` with live `Booking` query for 7 random dates. Acceptable delta: < 0.01% |
| **ARI-inventory reconciliation** | Daily at 05:30 UTC | Per tenant with Channex | Compare `ListingDailyInventory.RoomsAvailable` with booking-derived availability |
| **TrustScore consistency check** | Daily at 04:00 UTC | All properties | Recompute TrustScore from scratch for 500 random properties and compare with `PropertyTrustScoreCache`. Acceptable delta: < 0.001 |

### 9.2 Missing event detection

- DQI-01: A `MissingEventDetector` job runs every hour.
- DQI-02: For each booking status change in the last 2 hours, verify a corresponding `OutboxMessage` exists.
- DQI-03: Detectable gaps:

| Expected event | Detection query | Action |
|---|---|---|
| `booking.created` | Booking inserted but no outbox row with matching `EntityId` + `EventType` | Emit recovery outbox event |
| `booking.cancelled` | `Booking.CancelledAtUtc` set but no `booking.cancelled` event | Emit recovery event |
| `settlement.completed` | `Payment.Status = 'completed'` but no `settlement.completed` event | Emit recovery event |
| `rate.updated` | `ListingDailyRate` row with recent `UpdatedAtUtc` but no outbox event | Emit recovery event (ARI push) |

- DQI-04: Recovery events are marked with `HeadersJson` containing `{"recovery": true, "detectedBy": "MissingEventDetector"}`.
- DQI-05: Alert: `data.quality.missing_event` with `{eventType, entityId, tenantId, detectedAtUtc}`.

### 9.3 Duplicate event detection

- DQI-06: `ConsumedEvent` unique index on `(TenantId, ConsumerName, EventId)` prevents processing duplicates.
- DQI-07: A monitoring query runs hourly: count `OutboxMessage` rows with same `(TenantId, EntityId, EventType)` within a 5-minute window. If count > 1, flag as potential duplicate.
- DQI-08: Duplicate detection alert: `data.quality.duplicate_event` with `{eventType, entityId, count, windowMinutes}`.

### 9.4 Commission mismatch detection

- DQI-09: For each marketplace booking, verify:

```
Expected = Booking.FinalAmount * Booking.CommissionPercentSnapshot / 100
Actual   = Booking.CommissionAmount
Delta    = ABS(Expected - Actual)
```

- DQI-10: If `Delta > ₹1.00`, flag as commission mismatch.
- DQI-11: Alert: `data.quality.commission_mismatch` with `{bookingId, expected, actual, delta}`.
- DQI-12: Dashboard: Admin sees "Commission Integrity" card with mismatch count and total delta.

### 9.5 Settlement mismatch detection

- DQI-13: For each MARKETPLACE_SPLIT booking with completed settlement:

```
ExpectedHostPayout = Booking.FinalAmount - Booking.CommissionAmount
ActualPayout       = SUM(Payment.Amount WHERE BookingId = X AND Method = 'BankTransfer' AND Status = 'completed')
Delta              = ABS(ExpectedHostPayout - ActualPayout)
```

- DQI-14: If `Delta > ₹1.00`, flag as settlement mismatch.
- DQI-15: Alert: `data.quality.settlement_mismatch` with `{bookingId, expectedPayout, actualPayout, delta}`.

### 9.6 Alerting rules

| Alert | Severity | Threshold | Channel |
|-------|----------|-----------|---------|
| Pending outbox events > 1h old | Critical | > 100 events | Structured log + admin dashboard |
| Failed outbox events > 48h | Critical | > 0 events | Structured log + admin dashboard |
| Missing events detected | Warning | > 0 per hour | Structured log |
| Duplicate events detected | Warning | > 5 per hour | Structured log |
| Commission mismatch | Critical | > 0 | Structured log + admin dashboard |
| Settlement mismatch | Critical | > 0 | Structured log + admin dashboard |
| Snapshot job failed | Warning | Any failure | Structured log + admin dashboard |
| Snapshot staleness > 48h | Critical | Any snapshot type | Structured log + admin dashboard |
| TrustScore consistency drift | Warning | Delta > 0.01 for > 1% of sampled properties | Structured log |
| Booking-Payment reconciliation failure | Warning | Delta > ₹1 for any booking | Structured log |

- DQI-16: All alerts are structured log events (no separate alerting infra in V1).
- DQI-17: Admin dashboard "Data Health" tab aggregates all alerts from the last 24 hours.
- DQI-18: V2: Azure Monitor alerts with email/SMS escalation.

---

## 10. Future Warehouse Readiness

### 10.1 OLTP schema design principles for warehouse compatibility

- FWR-01: All tables MUST have an `Id` column as primary key (identity or GUID). No composite primary keys.
- FWR-02: All tables with temporal relevance MUST have `CreatedAtUtc` and `UpdatedAtUtc` columns (IAuditable interface).
- FWR-03: Soft deletes are preferred over hard deletes for warehouse traceability. Use `IsActive` or `Status` columns.
- FWR-04: Monetary amounts MUST always be paired with a `Currency` column (even though V1 is INR-only).
- FWR-05: Enum-like values MUST be stored as `varchar` (not `int`) for warehouse readability: `BookingStatus = 'Confirmed'` not `2`.
- FWR-06: Foreign keys MUST use meaningful names (not shadow properties) for straightforward star-schema joins.

### 10.2 Event export format (JSON schema)

All events stored in `OutboxMessage.PayloadJson` already follow a canonical JSON schema (section 2.2). For warehouse export:

- FWR-07: Each event MUST be serializable to a self-contained JSON document (no external references required).
- FWR-08: JSON schema version is tracked by `SchemaVersion`. Consumers MUST handle version-specific deserialization.
- FWR-09: V2 export pipeline: Azure Data Factory reads `OutboxMessage` (CDC via `UpdatedAtUtc` watermark) → JSON → Azure Data Lake → Synapse.
- FWR-10: V1 preparation: ensure all `PayloadJson` values are valid JSON (not truncated, not escaped strings). Validation in unit tests.

### 10.3 Data export endpoints

- FWR-11: Admin API MUST expose CSV export for all L2 snapshot tables:

| Endpoint | Data | Auth |
|----------|------|------|
| `GET /admin/data/export/property-performance?startDate=X&endDate=Y` | `Snap_DailyPropertyPerformance` | Admin role |
| `GET /admin/data/export/tenant-performance?startDate=X&endDate=Y` | `Snap_DailyTenantPerformance` | Admin role |
| `GET /admin/data/export/marketplace-performance?startDate=X&endDate=Y` | `Snap_DailyMarketplacePerformance` | Admin role |
| `GET /admin/data/export/revenue-metrics?startDate=X&endDate=Y` | `Snap_DailyRevenueMetrics` | Admin role |
| `GET /admin/data/export/trust-scores?startDate=X&endDate=Y` | `Snap_DailyTrustScore` | Admin role |
| `GET /admin/data/export/events?startDate=X&endDate=Y&eventType=Z` | `OutboxMessage` (Published) | Admin role |

- FWR-12: CSV exports MUST include headers. Maximum 100,000 rows per request. Pagination via `offset` parameter.
- FWR-13: JSON export format is available by adding `Accept: application/json` header (same endpoints).
- FWR-14: Tenant-facing export: `GET /api/data/export/my-performance?startDate=X&endDate=Y` — scoped to requesting tenant.

### 10.4 Soft boundaries for warehouse migration

| Milestone | Trigger | Action |
|-----------|---------|--------|
| L2 snapshot tables > 50M total rows | Monitor monthly | Evaluate Azure Synapse serverless for L3 queries |
| OutboxMessage > 10M rows/month | Monitor monthly | Evaluate CDC export to Data Lake |
| > 3 dashboard queries taking > 5s P95 | Monitor weekly | Evaluate materialized views or read replicas |
| Data team hired (> 1 person) | Business decision | Begin formal warehouse project |

- FWR-15: V1 MUST NOT introduce any warehouse infrastructure. All data stays in Azure SQL.
- FWR-16: The 3-layer architecture (section 1.1) ensures that migrating L3 to a separate warehouse requires NO changes to L1 or L2 — only L3 view definitions change to point at the warehouse.

---

## 11. Testing Matrix

### 11.1 Given/When/Then acceptance criteria

#### 11.1.1 Snapshot generation

```
GIVEN 10 tenants with bookings over the last 30 days
  AND the nightly snapshot job has not run yet for yesterday
WHEN SnapshotPropertyPerformanceJob executes for yesterday's date
THEN Snap_DailyPropertyPerformance rows are created for each property
  AND OccupancyRate = RoomsSold / RoomsAvailable for each property/date
  AND Revenue = SUM(AmountReceived for bookings overlapping the date)
  AND Adr = Revenue / RoomsSold (or 0 if RoomsSold = 0)
  AND RevPar = Revenue / RoomsAvailable (or 0 if RoomsAvailable = 0)
  AND ComputedAtUtc is set to the current time
```

#### 11.1.2 Snapshot idempotency

```
GIVEN Snap_DailyPropertyPerformance already has a row for TenantId=1, PropertyId=5, Date=2026-02-26
WHEN SnapshotPropertyPerformanceJob executes again for the same date
THEN the existing row is overwritten (upsert) with recalculated values
  AND no duplicate row is created
  AND ComputedAtUtc is updated to the new computation time
```

#### 11.1.3 Late event correction

```
GIVEN a booking for TenantId=1, PropertyId=5 with CheckinDate=2026-02-20
  AND Snap_DailyPropertyPerformance for 2026-02-20 was computed on 2026-02-21
WHEN the booking is cancelled on 2026-02-27
THEN a SnapshotCorrectionQueue entry is created for (TenantId=1, PropertyId=5, Date=2026-02-20)
  AND the hourly correction job recomputes the snapshot for that date
  AND the Revenue for 2026-02-20 no longer includes the cancelled booking
```

#### 11.1.4 TrustScore recalculation (incremental)

```
GIVEN PropertyId=10 has TrustScore=0.82 with ReviewRating=0.85
  AND a new 3-star review is submitted for PropertyId=10
WHEN the review.created event is processed
THEN only the ReviewRating component is recomputed (not all 6 components)
  AND the composite TrustScore is recalculated from the new ReviewRating + cached other components
  AND PropertyTrustScoreCache is updated
  AND if the delta > 0.10, a trustscore.updated outbox event is emitted
```

#### 11.1.5 TrustScore full recalculation consistency

```
GIVEN 500 random properties with cached TrustScores
WHEN the nightly full recalculation runs
THEN every property's TrustScore in PropertyTrustScoreCache matches
     the from-scratch computation (delta < 0.001)
```

#### 11.1.6 Commission aggregation

```
GIVEN TenantId=1 has 5 marketplace bookings in February
  AND each booking has CommissionPercentSnapshot = 3.00
  AND FinalAmount for bookings = [10000, 15000, 8000, 12000, 20000]
WHEN Snap_DailyTenantPerformance is computed
THEN CommissionPaid = SUM(FinalAmount * 3.00 / 100) = ₹1,950.00
```

#### 11.1.7 Revenue calculation

```
GIVEN TenantId=1 has 3 confirmed bookings:
  Booking A: CheckinDate=Feb 15, CheckoutDate=Feb 18, FinalAmount=₹12,000 (3 nights)
  Booking B: CheckinDate=Feb 20, CheckoutDate=Feb 22, FinalAmount=₹8,000 (2 nights)
  Booking C: CheckinDate=Feb 25, CheckoutDate=Mar 2, FinalAmount=₹20,000 (5 nights)
  AND report period is Feb 1 – Feb 28
WHEN Snap_DailyTenantPerformance is computed for February
THEN TotalRevenue = ₹12,000 + ₹8,000 + (₹20,000 * 3/5) = ₹32,000
     (Booking C prorated: 3 nights in Feb out of 5 total)
  AND NightsSold = 3 + 2 + 3 = 8
  AND ADR = ₹32,000 / 8 = ₹4,000
```

#### 11.1.8 Duplicate event ingestion

```
GIVEN OutboxMessage with EventId=AAA, EventType=booking.created
  AND ConsumedEvent already has (ConsumerName='SnapshotJob', EventId='AAA')
WHEN the snapshot job receives EventId=AAA again (re-delivery)
THEN the event is skipped (ConsumedEvent check succeeds)
  AND no duplicate processing occurs
  AND ConsumedEvent.Status = 'Skipped' is logged
```

#### 11.1.9 Backfill after bug fix

```
GIVEN Snap_DailyPropertyPerformance has incorrect data for dates Feb 10–Feb 20
     due to a bug in the ADR formula (now fixed)
WHEN admin calls POST /admin/data/backfill?startDate=2026-02-10&endDate=2026-02-20&snapshotType=DailyPropertyPerformance
THEN the job recomputes snapshots for each date in the range
  AND processes at most 10 dates per minute (throttled)
  AND each date's snapshot row is overwritten with corrected values
  AND the admin receives a completion response with {datesProcessed: 11, durationMs: X}
```

### 11.2 Edge case test matrix

| # | Scenario | Expected behavior |
|---|----------|-------------------|
| E1 | Tenant has 0 bookings, 0 listings | Snapshot rows created with all metrics = 0. No errors. |
| E2 | Booking spans month boundary | Prorated to each month based on nights in period. |
| E3 | Booking cancelled same day as creation | `booking.cancelled` event processed. Revenue removed. Cancellation count incremented. |
| E4 | Two snapshot jobs run concurrently for same date | MERGE upsert ensures last-writer-wins. No duplicate rows. |
| E5 | OutboxMessage purge runs during snapshot job | Snapshot reads with `NOLOCK` hint. Published events already consumed. No impact. |
| E6 | TrustScore event-driven update + nightly full recompute race | Nightly recompute is authoritative. Last writer wins on `PropertyTrustScoreCache`. |
| E7 | Platform has 0 tenants (fresh install) | `Snap_DailyMarketplacePerformance` row created with all zeros. No division-by-zero errors. |
| E8 | Snapshot correction queue has 10,000 entries | Correction job processes 1000 per hour. Oldest corrections processed first. |
| E9 | CSV export exceeds 100,000 rows | API returns 100,000 rows with pagination `nextOffset` in response header. |
| E10 | Admin triggers backfill for future dates | API rejects: `startDate must be <= yesterday`. 400 Bad Request. |
| E11 | CommissionAmount = 0 for a marketplace booking | Commission mismatch detector flags if `CommissionPercentSnapshot > 0`. |
| E12 | Cross-tenant data leak test | Dashboard API always includes TenantId filter. Integration test: TenantA cannot see TenantB snapshots. |

---

## 12. Definition of Done — Data Platform V1

### 12.1 Checklist

| # | Criterion | Verification method |
|---|-----------|-------------------|
| 1 | All 6 snapshot tables created with correct schema and unique constraints | Migration test |
| 2 | Nightly snapshot orchestrator completes for 1,000 test tenants within 30 minutes | Load test |
| 3 | `Snap_DailyTenantPerformance.Revenue` matches `SUM(Booking.AmountReceived)` within ₹1 for 100 random tenant-dates | Reconciliation integration test |
| 4 | `Snap_DailyMarketplacePerformance.CommissionRevenue` matches `SUM(Booking.CommissionAmount)` within ₹1 | Reconciliation integration test |
| 5 | GMV from snapshots matches GMV from live booking query within 0.01% | Reconciliation integration test |
| 6 | TrustScore in `PropertyTrustScoreCache` matches full recomputation for 500 random properties (delta < 0.001) | Nightly consistency check |
| 7 | TrustScore stable across 3 consecutive full recalculations (same input = same output) | Determinism test |
| 8 | Incremental TrustScore update on booking event updates only the affected component | Unit test with mock data |
| 9 | All 17 new event types registered in `EventTypes.cs` with helper methods | Unit test |
| 10 | Event payloads match JSON schema (section 2.2) | Schema validation unit test |
| 11 | `ConsumedEvent` prevents duplicate event processing | Integration test: re-deliver same event, assert no side effects |
| 12 | Late event correction queue processes and corrects snapshot | Integration test |
| 13 | Missing event detector identifies and recovers missing outbox events | Integration test with deliberate gap |
| 14 | Commission mismatch detector flags inconsistencies | Unit test with known mismatch |
| 15 | Settlement mismatch detector flags inconsistencies | Unit test with known mismatch |
| 16 | Dashboard API returns correct data from L2 snapshots | Integration test |
| 17 | All dashboard metrics consistent with raw OLTP data (spot-check 10 tenant-dates) | Manual verification script |
| 18 | No cross-tenant data leakage in any dashboard or export API | Security integration test: TenantA queries, TenantB data absent |
| 19 | CSV export produces valid CSV with headers and correct row count | Integration test |
| 20 | Snapshot purge job removes data beyond retention without errors | Integration test |
| 21 | Snapshot job uses separate connection string with limited pool size | Configuration verification |
| 22 | All snapshot queries use `NOLOCK` hint (no OLTP blocking) | Code review + query plan verification |
| 23 | Backfill API correctly recomputes historical snapshots | Integration test for 10-day backfill |
| 24 | Feature flags control all new data features | Integration test with flags off/on |

### 12.2 Non-functional requirements

| Requirement | Target | Measurement |
|------------|--------|-------------|
| Nightly snapshot completion (100k tenants) | < 2 hours | Job completion log |
| Dashboard API response time (P95) | < 2 seconds | Structured log timing |
| Snapshot-OLTP reconciliation accuracy | < 0.01% delta | Weekly reconciliation job |
| TrustScore consistency (nightly check) | < 0.001 delta | Nightly job |
| Commission reconciliation | 0 mismatches > ₹1 | Daily job |
| Settlement reconciliation | 0 mismatches > ₹1 | Daily job |
| Zero cross-tenant data leakage | 0 incidents | Security tests |
| Event processing latency (outbox → consumer) | < 60 seconds P95 | Structured log timing |
| Snapshot correction latency (queue → recompute) | < 2 hours | Correction job log |

---

## Appendix A: Entity Relationship Summary

```
L1 OLTP (existing):
  Tenant (1) ── (*) Property ── (*) Listing ── (*) Booking ── (*) Payment
                                     │
                                     ├── (*) ListingDailyRate
                                     ├── (*) ListingDailyInventory
                                     ├── (*) AvailabilityBlock
                                     └── (*) ListingPricingRule

  Tenant ── (*) OutboxMessage (event source)
  Tenant ── (*) AuditLog (append-only audit)
  Tenant ── (*) ConsumedEvent (idempotency)
  Tenant ── (*) BillingInvoice ── (*) BillingPayment

L2 Aggregation (new):
  Snap_DailyPropertyPerformance   (TenantId, PropertyId, Date)
  Snap_DailyTenantPerformance     (TenantId, Date)
  Snap_DailyMarketplacePerformance (Date)  ← platform-wide
  Snap_DailyRevenueMetrics        (TenantId, RevenueSource, Date)
  Snap_DailyTrustScore            (TenantId, PropertyId, Date)
  Snap_DailyDemandSignals         (TenantId, ListingId, Date)
  SnapshotCorrectionQueue         (SnapshotType, TenantId, EntityId, AffectedDate)
  SnapshotJobState                (SnapshotType → LastSuccessfulDate)

L2 Live Cache (new):
  PropertyTrustScoreCache         (PropertyId → composite TrustScore + components)

L2 Intelligence (from RA-AI-001):
  DailyPerformanceSnapshot        (TenantId, ListingId, Date)
  BookingVelocityMetric           (TenantId, ListingId, WindowDays)
  DemandSignal                    (TenantId, ListingId, SignalType)
  PriceSuggestion                 (TenantId, ListingId, DateRange)
  PricingIntelligenceBaseline     (TenantId, ListingId)

L3 Analytics (views, no tables):
  vw_SuggestionHistory            (RA-AI-001)
  vw_MonthlyDemandSignalTrends    (section 6.1)
  vw_TenantRevenueReport          (joins Snap_DailyTenantPerformance)
  vw_MarketplaceHealth            (joins Snap_DailyMarketplacePerformance)
  vw_SyncReliability              (joins OutboxMessage sync events)
```

---

## Appendix B: Snapshot Job Execution Order

```
02:00 UTC ── SnapshotPropertyPerformanceJob (reads: Bookings, Listings, Payments)
02:15 UTC ── SnapshotRevenueMetricsJob (reads: Bookings)
02:30 UTC ── SnapshotTenantPerformanceJob (reads: Snap_DailyPropertyPerformance)
03:00 UTC ── SnapshotMarketplacePerformanceJob (reads: Snap_DailyTenantPerformance)
03:15 UTC ── SnapshotTrustScoreJob (reads: PropertyTrustScoreCache)
03:30 UTC ── SnapshotDemandSignalsJob (reads: DemandSignal, PriceSuggestion)
03:45 UTC ── SnapshotCorrectionJob (reads: SnapshotCorrectionQueue)
04:00 UTC ── TrustScoreConsistencyCheckJob (reads: PropertyTrustScoreCache, raw data)
05:00 UTC ── BookingPaymentReconciliationJob
05:15 UTC ── CommissionReconciliationJob
05:30 UTC ── ARIInventoryReconciliationJob
```

---

## Appendix C: Structured Log Events

| Event | Level | Fields | Trigger |
|-------|-------|--------|---------|
| `data.snapshot.started` | Info | `{snapshotType, targetDate, batchSize}` | Job start |
| `data.snapshot.completed` | Info | `{snapshotType, targetDate, rowsWritten, durationMs}` | Job success |
| `data.snapshot.failed` | Error | `{snapshotType, targetDate, error, stackTrace}` | Job failure |
| `data.snapshot.gap_detected` | Warn | `{snapshotType, missingDates}` | Orchestrator gap check |
| `data.snapshot.backfill.started` | Info | `{snapshotType, startDate, endDate}` | Admin backfill |
| `data.snapshot.backfill.completed` | Info | `{snapshotType, datesProcessed, durationMs}` | Backfill done |
| `data.correction.queued` | Debug | `{snapshotType, tenantId, entityId, affectedDate, reason}` | Late event |
| `data.correction.processed` | Info | `{snapshotType, tenantId, entityId, affectedDate}` | Correction applied |
| `data.purge.completed` | Info | `{table, rowsDeleted, cutoffDate, durationMs}` | Purge job |
| `data.quality.missing_event` | Warn | `{eventType, entityId, tenantId}` | Missing event detector |
| `data.quality.duplicate_event` | Warn | `{eventType, entityId, count}` | Duplicate detector |
| `data.quality.commission_mismatch` | Error | `{bookingId, expected, actual, delta}` | Commission reconciliation |
| `data.quality.settlement_mismatch` | Error | `{bookingId, expectedPayout, actualPayout, delta}` | Settlement reconciliation |
| `data.quality.snapshot_drift` | Warn | `{snapshotType, tenantId, date, snapshotValue, liveValue, delta}` | Snapshot reconciliation |
| `data.quality.trustscore_drift` | Warn | `{propertyId, cachedScore, computedScore, delta}` | TrustScore consistency |
| `data.export.completed` | Info | `{exportType, rowCount, durationMs, requestedBy}` | CSV/JSON export |

---

## Appendix D: Configuration Reference

| Config key | V1 default | Type | Section |
|------------|:----------:|------|:-------:|
| `DataPlatform:SnapshotBatchSize` | 500 | int | §8.4 |
| `DataPlatform:SnapshotConnectionString` | (separate pool) | string | §8.6 |
| `DataPlatform:SnapshotCommandTimeoutSeconds` | 120 | int | §8.6 |
| `DataPlatform:CorrectionJobIntervalHours` | 1 | int | §3.5 |
| `DataPlatform:CorrectionBatchSize` | 1000 | int | §3.5 |
| `DataPlatform:BackfillMaxDatesPerMinute` | 10 | int | §3.3 |
| `DataPlatform:BackfillMaxDaysBack` | 90 | int | §3.3 |
| `DataPlatform:PurgeOutboxDays` | 30 | int | §2.5 |
| `DataPlatform:PurgeAuditLogDays` | 730 | int | §2.5 |
| `DataPlatform:PurgeCommunicationLogDays` | 365 | int | §2.5 |
| `DataPlatform:PurgeDemandSignalDays` | 180 | int | §2.5 |
| `DataPlatform:PurgeBatchSize` | 5000 | int | §2.5 |
| `DataPlatform:ArchiveBookingDays` | 730 | int | §8.3 |
| `DataPlatform:ArchiveSnapshotDays` | 730 | int | §8.3 |
| `DataPlatform:ExportMaxRows` | 100000 | int | §10.3 |
| `DataPlatform:MissingEventDetectorIntervalHours` | 1 | int | §9.2 |
| `DataPlatform:MissingEventLookbackHours` | 2 | int | §9.2 |
| `DataPlatform:TrustScoreSweepIntervalMinutes` | 15 | int | §5.4 |
| `DataPlatform:TrustScoreSweepBatchSize` | 1000 | int | §5.4 |
| `DataPlatform:ReconciliationSampleSize` | 100 | int | §9.1 |
| `DataPlatform:TrustScoreConsistencySampleSize` | 500 | int | §9.1 |

---

*End of RA-DATA-001*
