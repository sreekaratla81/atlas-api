# RA-IC-002: Provider Switching, Migration & Sync Consistency Requirements

**Addendum to:** [RA-IC-001](RA-IC-001-hybrid-ical-channex-sync.md) | [RA-001](RA-001-marketplace-commission-boost-ota-payments.md) | [RA-002](RA-002-governance-scale-monetization-control.md) | [RA-005](RA-005-subscription-billing-revenue-control.md)

**Purpose:** Define the complete safety model for provider switching, duplicate booking prevention, cutover orchestration, availability consistency, sync state machine, historical booking integrity, idempotency guarantees, failure recovery, migration monitoring, and acceptance criteria — ensuring zero lost bookings, zero financial corruption, and bounded availability drift during any sync mode transition.

**Audience:** Developer, QA, Platform Ops

**Owner:** Atlas Tech Solutions

**Last updated:** 2026-02-27

**Status:** Draft

---

## Table of contents

1. [Provider Switching Scenarios](#1-provider-switching-scenarios)
2. [Duplicate Booking Prevention Architecture](#2-duplicate-booking-prevention-architecture)
3. [Cutover Strategy Design (Safe Migration Model)](#3-cutover-strategy-design-safe-migration-model)
4. [Availability Consistency Model](#4-availability-consistency-model)
5. [Sync State Machine Specification](#5-sync-state-machine-specification)
6. [Historical Booking Integrity](#6-historical-booking-integrity)
7. [Consistency & Idempotency Requirements](#7-consistency--idempotency-requirements)
8. [Failure Modeling](#8-failure-modeling)
9. [Monitoring & Alerting for Migration](#9-monitoring--alerting-for-migration)
10. [Testing Matrix](#10-testing-matrix)
11. [Definition of Done — Safe Sync Switching](#11-definition-of-done--safe-sync-switching)

---

## 1. Provider switching scenarios

Six scenarios cover every production-relevant sync mode transition. Each scenario specifies the full lifecycle from initiation through verification.

### 1.A iCal → Channex upgrade

**Trigger:** Tenant upgrades from Basic to Premium plan, or Premium tenant manually selects `CHANNEX_API` for a property currently on `ICAL_BASIC`.

**Preconditions:** Tenant on Premium plan. Property has active `ListingExternalCalendar` rows. Existing iCal-sourced `AvailabilityBlock` rows present.

#### Freeze window

| Phase | Duration | System behaviour |
|-------|:--------:|-----------------|
| **Pre-freeze** | — | iCal polling runs normally. Atlas bookings created normally. |
| **Freeze start** | T+0 | `Property.SyncState` transitions to `MIGRATING`. iCal polling continues but enters **read-only mode**: fetches feed, logs result, but does NOT create/update/delete `AvailabilityBlock` rows. Existing blocks frozen in place. |
| **Connection window** | T+0 to T+max 72h | Tenant sets up Channex credentials. System validates via `TestConnectionAsync`. If tenant does not complete within 72 hours: auto-rollback to `ACTIVE_ICAL`, unfreeze. |
| **Cutoff** | T+connect | `Property.SyncSwitchCutoffUtc` recorded. All iCal blocks with `CreatedAtUtc < cutoff` tagged `Source = 'iCal_legacy'`. |
| **Backfill** | T+connect to T+connect+5min | Channex full availability + rate push for next 365 days. |
| **Activation** | T+backfill | `SyncState` → `ACTIVE_CHANNEX`. iCal calendars deactivated (`IsActive = false`). |
| **Monitoring** | T+activation to T+activation+30min | System watches for anomalies (§9). If critical failure detected: auto-rollback. |

| ID | Requirement |
|----|-------------|
| PSW-01 | Freeze window MUST NOT exceed 72 hours. If tenant does not complete Channex setup within 72h, system MUST auto-rollback to `ACTIVE_ICAL` and notify tenant. |
| PSW-02 | During freeze, iCal polling MUST continue reading (for monitoring/hash comparison) but MUST NOT mutate `AvailabilityBlock` rows. |
| PSW-03 | All iCal blocks existing at cutoff MUST be tagged `Source = 'iCal_legacy'` and excluded from active conflict detection. They remain visible in the calendar (greyed out, labelled "Legacy — iCal") for 30 days, then auto-purged. |

#### Booking reconciliation

| Step | Action |
|:----:|--------|
| 1 | Before activating Channex, run reconciliation: compare current Atlas `AvailabilityBlock` rows (iCal-sourced) against latest iCal feed. Log discrepancies. |
| 2 | Compare Atlas `Booking` rows against `AvailabilityBlock` rows. Ensure every confirmed Atlas booking has a corresponding block. |
| 3 | After Channex backfill, compare pushed availability against expected (Atlas bookings + blocks). Log any mismatch. |

| ID | Requirement |
|----|-------------|
| PSW-04 | Pre-switch reconciliation MUST run as a blocking step before cutoff. If reconciliation finds unresolvable discrepancies (e.g., Atlas booking with no corresponding block): HALT switch, alert platform admin. |
| PSW-05 | Reconciliation results MUST be logged: `sync.migration.reconciliation` with `{propertyId, iCalBlockCount, atlasBookingCount, discrepancies}`. |

#### Duplicate prevention

| Risk | Mitigation |
|------|-----------|
| iCal block AND Channex booking for same reservation | Cutoff timestamp ensures clean boundary: blocks before cutoff are legacy (iCal), events after cutoff come from Channex. No overlap window. |
| Channex pushes availability that conflicts with legacy iCal block | Legacy blocks are excluded from availability computation sent to Channex. Only Atlas bookings and Channex-era blocks are considered. |

#### UI warnings

| Warning | When shown | Text |
|---------|-----------|------|
| Migration in progress | During `MIGRATING` state | "Provider switch in progress. iCal sync is paused. Complete Channex setup to continue." |
| Deadline approaching | 48h into freeze with no Channex connection | "Channex setup not yet complete. You have 24 hours before automatic rollback to iCal." |
| Auto-rollback occurred | After 72h timeout | "Channex setup was not completed within 72 hours. Your property has been reverted to iCal sync." |

#### Audit logs

| Event | Fields |
|-------|--------|
| `sync.migration.initiated` | `{ propertyId, tenantId, from: 'ical_basic', to: 'channex_api', initiatedBy }` |
| `sync.migration.freeze_started` | `{ propertyId, frozenBlockCount }` |
| `sync.migration.channex_connected` | `{ propertyId, channelConfigId, testResult }` |
| `sync.migration.cutoff_recorded` | `{ propertyId, cutoffUtc, legacyBlocksTagged }` |
| `sync.migration.backfill_completed` | `{ propertyId, daysPushed, ratesPushed, availPushed }` |
| `sync.migration.activated` | `{ propertyId, newSyncState: 'active_channex' }` |
| `sync.migration.rollback` | `{ propertyId, reason, rolledBackTo: 'active_ical' }` |
| `sync.migration.auto_rollback_timeout` | `{ propertyId, freezeDurationHours: 72 }` |

---

### 1.B Channex → iCal downgrade

**Trigger:** Tenant downgrades from Premium to Basic, or manually selects `ICAL_BASIC` for a property on `CHANNEX_API`.

**Preconditions:** Property has active `ChannelConfig`. May have Channex-sourced `Booking` records (v2).

#### Freeze window

| Phase | Duration | System behaviour |
|-------|:--------:|-----------------|
| **Freeze start** | T+0 | `SyncState` → `MIGRATING`. Channex push stops immediately (worker skips property). Channex webhook processing continues for 30 minutes (drain window — any in-flight webhooks are honoured). |
| **Drain window** | T+0 to T+30min | Outbox rows for this property processed. No new push rows enqueued. Webhooks still processed. |
| **iCal setup window** | T+30min to T+max 72h | Tenant configures iCal import URLs. |
| **Cutoff** | T+ical_configured | Cutoff recorded. Channex-sourced bookings retain `SourceProvider = 'channex'`. |
| **First iCal sync** | T+cutoff+1 cycle | iCal polling runs for new calendars. Blocks imported. |
| **Activation** | T+first_sync | `SyncState` → `ACTIVE_ICAL`. `ChannelConfig.ConnectionStatus = 'paused'` (preserved 90 days). |

| ID | Requirement |
|----|-------------|
| PSW-06 | Channex push MUST stop at freeze start. No rates or availability pushed to Channex after this point. |
| PSW-07 | Channex webhook drain window MUST be 30 minutes. After drain: webhook endpoint returns 200 but discards payload (logged as `channex.webhook.drained`). |
| PSW-08 | `ChannelConfig` rows MUST NOT be deleted. Set `ConnectionStatus = 'paused'`. Retain for 90 days to enable re-upgrade. |
| PSW-09 | If tenant does not configure iCal within 72h: property stays in `MIGRATING` with no active sync. Alert tenant and platform admin. Do NOT auto-rollback to Channex (tenant chose downgrade). |

#### Booking reconciliation

Before cutoff: enumerate all Channex-sourced bookings with future check-in dates. These become **orphan bookings** (no active sync will update them). Flag each with `Booking.SyncOrphanedAtUtc`. Tenant must manage them manually or through OTA directly.

| ID | Requirement |
|----|-------------|
| PSW-10 | Future Channex-sourced bookings MUST be flagged `SyncOrphanedAtUtc = cutoffUtc`. Admin UI shows warning: "This booking was created via Channex. iCal sync cannot update it. Manage directly on the OTA." |

#### Audit logs

| Event | Fields |
|-------|--------|
| `sync.migration.initiated` | `{ propertyId, from: 'channex_api', to: 'ical_basic' }` |
| `sync.migration.channex_push_stopped` | `{ propertyId, pendingOutboxRows }` |
| `sync.migration.drain_completed` | `{ propertyId, webhooksProcessedDuringDrain }` |
| `sync.migration.ical_configured` | `{ propertyId, calendarCount }` |
| `sync.migration.activated` | `{ propertyId, newSyncState: 'active_ical' }` |
| `sync.migration.bookings_orphaned` | `{ propertyId, orphanedBookingCount, futureCheckInCount }` |

---

### 1.C OTA removed from Channex

**Trigger:** Tenant disconnects a specific OTA (e.g., Booking.com) from Channex while keeping others (e.g., Airbnb).

This is NOT a full provider switch — `SyncMode` remains `CHANNEX_API`. It is a **partial channel disconnection**.

| Step | Action |
|:----:|--------|
| 1 | Tenant clicks "Disconnect Booking.com" in admin portal. |
| 2 | System stops pushing rates/availability to Channex for that OTA's room/rate mappings. |
| 3 | Existing bookings from that OTA retain their `SourceProvider`. |
| 4 | Future webhooks for that OTA are still processed (bookings may still arrive from OTA side until Channex fully disconnects). |
| 5 | `ChannelConfig` row for that OTA remains with `ConnectionStatus = 'disconnected'`. |

| ID | Requirement |
|----|-------------|
| PSW-11 | OTA disconnection MUST NOT affect other connected OTAs on the same property. |
| PSW-12 | Disconnection MUST be logged: `sync.ota.disconnected` with `{propertyId, ota, channelConfigId}`. |
| PSW-13 | If ALL OTAs are disconnected from Channex for a property, system MUST prompt tenant: "No OTAs connected. Switch to iCal or manual mode?" |

---

### 1.D OTA added after initial iCal usage

**Trigger:** Tenant on `ICAL_BASIC` wants to add Booking.com (which requires Channex).

This requires a full upgrade from `ICAL_BASIC` → `CHANNEX_API`. Follow Scenario 1.A with these additions:

| ID | Requirement |
|----|-------------|
| PSW-14 | Admin portal MUST detect when tenant attempts to add a non-Airbnb OTA while on `ICAL_BASIC` and present upgrade prompt: "Booking.com requires Premium plan with Channex sync." |
| PSW-15 | If tenant is already Premium but on `ICAL_BASIC`: allow in-place switch to `CHANNEX_API` per Scenario 1.A flow. |
| PSW-16 | If tenant is on Basic: require plan upgrade first. After upgrade completes, guide through Scenario 1.A. |

---

### 1.E Sync temporarily disabled

**Trigger:** Tenant wants to pause all sync (e.g., property temporarily offline, renovation). Or: system pauses sync due to subscription suspension (RA-005 §5.2).

| Cause | Behaviour |
|-------|-----------|
| **Tenant-initiated pause** | `SyncState` → `SUSPENDED`. iCal polling stops. Channex push stops. Existing blocks preserved. Export feed still served (reflects last known state). |
| **Subscription suspension** | Same as above but triggered by billing system. `SyncState` → `SUSPENDED` with `SuspensionReason = 'billing'`. |
| **Resume** (tenant or billing) | `SyncState` → previous state (`ACTIVE_ICAL` or `ACTIVE_CHANNEX`). Immediate sync cycle triggered. |

| ID | Requirement |
|----|-------------|
| PSW-17 | Suspension MUST preserve all configuration. No data deleted. Resume restores exact prior state. |
| PSW-18 | During suspension: iCal export feed MUST still be served (stale data is better than 404 which causes OTA to clear calendar). |
| PSW-19 | On resume: system MUST trigger immediate full sync (iCal poll or Channex reconciliation push) to catch up on missed changes. |
| PSW-20 | Resume after > 7 days of suspension MUST trigger full reconciliation, not incremental sync. |
| PSW-21 | Tenant-initiated pause MUST require confirmation: "Pausing sync means OTAs will not receive updates. Overbookings may result." |

#### Audit logs

| Event | Fields |
|-------|--------|
| `sync.suspended` | `{ propertyId, reason: 'tenant' \| 'billing', previousState }` |
| `sync.resumed` | `{ propertyId, suspendedDurationHours, reconciliationType: 'full' \| 'incremental' }` |

---

### 1.F Token expired / reconnect required

**Trigger:** Channex API key revoked or expired. iCal URL becomes invalid (Airbnb rotates URL, tenant changes Airbnb account).

#### Channex token expiry

| Step | System behaviour |
|:----:|-----------------|
| 1 | Push or test returns 401/403. `ConsecutiveFailures` increments. |
| 2 | After 3 failures: `ConnectionStatus` → `ERROR`. Sync paused. Alert sent to tenant. |
| 3 | Tenant re-enters valid API key. Test passes. |
| 4 | `ConnectionStatus` → `CONNECTED`. Full reconciliation push triggered. |

#### iCal URL invalidation

| Step | System behaviour |
|:----:|-----------------|
| 1 | Fetch returns 404 or 403. `ConsecutiveFailures` increments. |
| 2 | After 5 failures: alert sent to tenant. Calendar marked `IsActive = false` (auto-disable after 10 failures). |
| 3 | Existing blocks are NOT deleted (last known state preserved). |
| 4 | Tenant updates URL. `IsActive = true`. `ConsecutiveFailures = 0`. Immediate sync. |

| ID | Requirement |
|----|-------------|
| PSW-22 | Token/URL invalidation MUST NOT delete existing blocks or bookings. Last known state preserved. |
| PSW-23 | Reconnection MUST trigger full reconciliation (not incremental) since the gap may contain missed changes. |
| PSW-24 | Alert to tenant MUST include specific action: "Your Channex API key is no longer valid. Go to Channels → {Property} → Re-enter API key." / "Your Airbnb iCal URL is no longer accessible. Go to Channels → {Property} → Update import URL." |
| PSW-25 | Auto-disable threshold: iCal calendars auto-disabled after 10 consecutive failures. Channex configs enter ERROR after 3. |

#### Audit logs

| Event | Fields |
|-------|--------|
| `sync.credential.invalid` | `{ propertyId, provider, httpStatus, consecutiveFailures }` |
| `sync.credential.auto_disabled` | `{ propertyId, provider, failureCount }` |
| `sync.credential.reconnected` | `{ propertyId, provider, gapDurationHours }` |

---

## 2. Duplicate booking prevention architecture

### 2.1 Global external reservation ID normalization

Every reservation entering Atlas from any external source is assigned a **Normalized External Reservation ID (NERID)** — a deterministic, globally unique identifier.

```
NERID = lowercase(SHA-256(Provider + ":" + ExternalReservationId + ":" + PropertyId))[:32]
```

| Source | Provider value | ExternalReservationId value | Example NERID input |
|--------|:--------------:|---------------------------|-------------------|
| iCal (Airbnb) | `ical` | UID from VEVENT (e.g., `abc123@airbnb.com`) | `ical:abc123@airbnb.com:42` |
| Channex webhook (Airbnb) | `channex_airbnb` | Channex booking ID (e.g., `ch_bk_9876`) | `channex_airbnb:ch_bk_9876:42` |
| Channex webhook (Booking.com) | `channex_bookingcom` | Channex booking ID | `channex_bookingcom:ch_bk_5432:42` |
| Atlas direct booking | `atlas` | Booking ID (e.g., `1001`) | `atlas:1001:42` |

| ID | Requirement |
|----|-------------|
| DUP-01 | Every `AvailabilityBlock` with external origin MUST have a `NormalizedExternalId` (`varchar(32)`) computed from the NERID formula. |
| DUP-02 | Every `Booking` with external origin MUST have a `NormalizedExternalId` computed the same way. |
| DUP-03 | NERID computation MUST be deterministic: same inputs always produce the same hash. |

### 2.2 Unique constraints

```sql
-- Prevents duplicate blocks from the same external source
CREATE UNIQUE INDEX UX_AvailabilityBlock_ExternalId
ON AvailabilityBlocks (NormalizedExternalId)
WHERE NormalizedExternalId IS NOT NULL;

-- Prevents duplicate bookings from the same external source
CREATE UNIQUE INDEX UX_Booking_ExternalId
ON Bookings (NormalizedExternalId)
WHERE NormalizedExternalId IS NOT NULL;
```

| ID | Requirement |
|----|-------------|
| DUP-04 | Unique filtered index on `NormalizedExternalId` MUST exist on both `AvailabilityBlocks` and `Bookings`. Only applies to non-null values (Atlas-created bookings without external origin have NULL). |
| DUP-05 | On duplicate key violation: log the duplicate, skip insertion, and return success (idempotent). MUST NOT throw an unhandled exception. |
| DUP-06 | `NormalizedExternalId` is NULL for Atlas direct bookings that have no external origin. The unique index `WHERE NormalizedExternalId IS NOT NULL` permits multiple NULLs. |

### 2.3 Cross-provider duplicate detection

A single real-world reservation may appear via BOTH iCal and Channex if a tenant switches providers mid-reservation or has both configured temporarily. These must be detected and merged.

**Cross-provider matching heuristic:**

```
Two records are LIKELY DUPLICATES if:
    record_A.ListingId == record_B.ListingId
    AND record_A.CheckIn == record_B.CheckIn
    AND record_A.CheckOut == record_B.CheckOut
    AND record_A.Provider != record_B.Provider
    AND |record_A.CreatedAtUtc - record_B.CreatedAtUtc| < 24 hours
```

This heuristic is NOT used for automatic merging (risk of false positives). It flags **suspected duplicates** for review.

| ID | Requirement |
|----|-------------|
| DUP-07 | After any sync ingestion, system MUST run the cross-provider matching heuristic against existing blocks/bookings for the same listing within the sync's date range. |
| DUP-08 | Matches MUST be flagged as `SuspectedDuplicate` in a `DuplicateCandidate` table, NOT automatically merged. |
| DUP-09 | Platform admin dashboard MUST show suspected duplicates with "Merge" and "Not a duplicate" actions. |

### 2.4 Hash-based duplicate detection fallback

For iCal sources where UIDs may be absent or unstable:

```
FallbackHash = SHA-256(CalendarId + ":" + DTSTART + ":" + DTEND + ":" + first50CharsOfSummary)[:32]
```

| ID | Requirement |
|----|-------------|
| DUP-10 | If iCal VEVENT lacks a UID, NERID MUST use `FallbackHash` as the `ExternalReservationId` component. |
| DUP-11 | `FallbackHash` MUST be stored on `AvailabilityBlock.FallbackHash` (`varchar(32)`) for debugging. |

### 2.5 Booking merge policy

When a confirmed duplicate is detected (e.g., same reservation appears as both iCal block and Channex booking during provider switch):

| Scenario | Policy | Action |
|----------|--------|--------|
| iCal block + Channex booking (same reservation) | **Channex wins** | Delete iCal block. Keep Channex booking (richer data). |
| Two iCal blocks from different calendars (same reservation) | **Keep one** | Keep the block from the calendar that was most recently synced. Delete the other. |
| Two Channex bookings (same reservation, different webhook IDs) | **Keep first** | Channex webhook idempotency should prevent this. If it occurs: keep earlier `CreatedAtUtc`. Alert platform admin. |
| Atlas direct booking + external block (same dates, same property) | **NOT a duplicate** | These are a conflict (RA-IC-001 §2.C), not a duplicate. Different guests. Do not merge. |

| ID | Requirement |
|----|-------------|
| DUP-12 | Merge MUST be a manual admin action (except during provider switch cutover where iCal→Channex auto-merge is triggered by the cutover process). |
| DUP-13 | Merge MUST log: `sync.duplicate.merged` with `{survivorId, mergedId, mergeReason, mergedBy}`. |
| DUP-14 | Merge MUST NOT alter financial fields (`CommissionPercentSnapshot`, `TotalAmount`, `PaymentStatus`) on the surviving record. |

### 2.6 Duplicate states

| State | Code | Description |
|-------|:----:|-------------|
| **SUSPECTED** | `suspected` | Heuristic match detected. Awaiting review. |
| **CONFIRMED** | `confirmed` | Admin confirmed as duplicate. Merge pending. |
| **MERGED** | `merged` | Records merged. Loser record soft-deleted. |
| **DISMISSED** | `dismissed` | Admin confirmed NOT a duplicate. No action. |

**Data model:**

```
DuplicateCandidate {
    Id              int PK
    TenantId        int FK → Tenant
    RecordAType     varchar(20)     -- 'AvailabilityBlock' or 'Booking'
    RecordAId       int
    RecordAProvider varchar(20)
    RecordBType     varchar(20)
    RecordBId       int
    RecordBProvider varchar(20)
    MatchReason     varchar(100)    -- 'exact_nerid', 'date_heuristic', 'fallback_hash'
    Status          varchar(20)     -- 'suspected', 'confirmed', 'merged', 'dismissed'
    DetectedAtUtc   datetime2
    ResolvedAtUtc   datetime2?
    ResolvedBy      varchar(100)?   -- admin user or 'system_cutover'
}
```

### 2.7 Edge cases

#### OTA changes reservation ID on modification

Some OTAs generate a new reservation ID when a booking is modified (date change, guest change). This breaks NERID-based deduplication.

| ID | Requirement |
|----|-------------|
| DUP-15 | When a Channex `booking_modified` webhook arrives with a different booking ID than the original, the system MUST: (a) find the existing booking by matching `ListingId + original CheckIn + CheckOut`, (b) update the `NormalizedExternalId` to the new value, (c) log `sync.external_id.changed` with `{bookingId, oldExternalId, newExternalId}`. |
| DUP-16 | For iCal: UID changes on modification are handled by full-replace strategy (old block deleted, new block created). No special handling needed. |

#### iCal UID changes unexpectedly

| ID | Requirement |
|----|-------------|
| DUP-17 | If an iCal feed returns completely new UIDs for all events (bulk UID change), the full-replace strategy handles this correctly (all old blocks deleted, all new blocks created). However: if this results in > 50% of events changing UIDs in a single sync, log WARNING `ical.uid.bulk_change` with `{calendarId, changedCount, totalCount}`. |

#### Cancellation arrives before booking event

Possible in webhook-based systems (out-of-order delivery):

| ID | Requirement |
|----|-------------|
| DUP-18 | If a `booking_cancelled` webhook arrives and no matching booking exists: write to outbox with `pending_cancel` status. If corresponding `booking_new` arrives within 30 minutes: process creation then immediate cancellation. If no `booking_new` arrives within 30 minutes: log `sync.orphan_cancel` and discard. |
| DUP-19 | Outbox worker MUST process messages for the same property in chronological order (`CreatedAtUtc ASC`) to minimize out-of-order issues. |

---

## 3. Cutover strategy design (safe migration model)

### 3.1 Six-step cutover protocol

Every provider switch follows this protocol. Steps are sequential and transactional (each step must succeed before the next begins).

#### Step 1 — Freeze inbound sync

| Action | Detail |
|--------|--------|
| Set `SyncState = MIGRATING` | DB update, single row. |
| Outgoing provider: stop writes | iCal poller skips block creation for this property. Channex worker skips push for this property. |
| Incoming provider: not yet active | No configuration yet (or not tested). |
| Missed events | Events arriving during freeze are queued (see Step 2). |

**Duration:** Instantaneous (single DB update). Total freeze window is bounded by the connection window (max 72 hours, §1.A).

#### Step 2 — Perform reconciliation pull

| Action | Detail |
|--------|--------|
| **If leaving iCal:** | Perform one final iCal fetch (read-only). Parse events. Compare against current `AvailabilityBlock` rows. Log discrepancies. Do NOT write blocks. |
| **If leaving Channex:** | Process any remaining outbox rows for this property (drain). Check for pending webhooks in the 30-minute drain window. |
| **Result:** | `ReconciliationReport` with `{ blocksInDb, blocksInFeed, bookingsInDb, discrepancies[] }`. |

| ID | Requirement |
|----|-------------|
| CUT-01 | Reconciliation pull MUST execute within 60 seconds. If timeout: retry once. If second timeout: proceed with warning (log `sync.migration.reconciliation_timeout`). |
| CUT-02 | Reconciliation discrepancies MUST be classified: `missing_in_db` (feed has event, DB doesn't), `missing_in_feed` (DB has block, feed doesn't), `date_mismatch` (same UID, different dates). |

#### Step 3 — Lock write operations

| Action | Detail |
|--------|--------|
| Acquire migration lock | `SyncLock` row: `{ PropertyId, LockedByWorker: 'migration', ExpiresAtUtc: now + 10min }`. |
| Block concurrent writes | Booking creation for this property's listings checks `SyncState`. If `MIGRATING`: allow booking but flag it `CreatedDuringMigration = true`. |
| Block concurrent syncs | iCal worker and Channex worker both check lock and skip. |

| ID | Requirement |
|----|-------------|
| CUT-03 | Migration lock MUST have 10-minute TTL. If cutover does not complete within 10 minutes of lock acquisition: release lock, pause migration, alert platform admin. |
| CUT-04 | Bookings created during migration MUST be flagged (`Booking.CreatedDuringMigration = true`). Post-migration reconciliation MUST verify these bookings are reflected in the new provider's state. |
| CUT-05 | Lock acquisition MUST use `UPDATE ... WHERE LockedByWorker IS NULL OR ExpiresAtUtc < GETUTCDATE()` pattern (DB-level atomic lock). |

#### Step 4 — Compare availability windows

| Action | Detail |
|--------|--------|
| Generate availability snapshot | For each listing on the property: compute booked/blocked dates for next 365 days from `Booking` + `AvailabilityBlock` tables. |
| Store snapshot | `MigrationSnapshot { PropertyId, ListingId, SnapshotJson, CreatedAtUtc }`. JSON array of `{ date, status, source }` entries. |
| This becomes the "before" state | Post-migration comparison uses this snapshot. |

| ID | Requirement |
|----|-------------|
| CUT-06 | Availability snapshot MUST be taken AFTER lock acquisition and BEFORE new provider activation. |
| CUT-07 | Snapshot MUST be retained for 30 days for post-migration auditing. |

#### Step 5 — Activate new provider

| Action | Detail |
|--------|--------|
| **iCal → Channex:** | Set `Property.SyncModeOverride = 'channex_api'`. Record cutoff. Tag legacy blocks. Trigger Channex backfill. |
| **Channex → iCal:** | Set `Property.SyncModeOverride = 'ical_basic'`. Pause Channex config. Activate iCal calendars. |
| **Any → None:** | Set `Property.SyncModeOverride = 'none'`. Pause all configs/calendars. |
| Update `SyncState` | Transition to target state (`ACTIVE_ICAL`, `ACTIVE_CHANNEX`, or `NOT_CONFIGURED`). |
| Release lock | Clear `SyncLock` row. |

| ID | Requirement |
|----|-------------|
| CUT-08 | Activation MUST be a single DB transaction: mode change + state change + lock release + cutoff record. If any fails: rollback entire transaction. |
| CUT-09 | After activation: trigger immediate sync cycle for the new provider (iCal poll or Channex push). |

#### Step 6 — Monitor first 30 minutes

| Check | Frequency | Auto-rollback trigger |
|-------|:---------:|----------------------|
| Sync success | Every 5 min | 3 consecutive failures within 30 min. |
| Availability comparison | At T+15min and T+30min | > 5% of dates have drift vs pre-migration snapshot. |
| Duplicate detection | At T+30min | Any confirmed duplicate detected. |
| Booking creation test | At T+5min | If test booking creation fails (system test, not real booking). |

| ID | Requirement |
|----|-------------|
| CUT-10 | Post-activation monitoring MUST run automatically for 30 minutes. No manual trigger needed. |
| CUT-11 | Auto-rollback MUST be triggered if ANY auto-rollback condition is met. Rollback restores previous `SyncState`, re-enables previous provider configs, clears cutoff timestamp. |
| CUT-12 | Auto-rollback MUST be logged: `sync.migration.auto_rollback` with `{propertyId, reason, monitoringMinute}`. |
| CUT-13 | After 30 minutes with no auto-rollback triggers: migration considered successful. Log `sync.migration.completed` with `{propertyId, totalDurationMinutes}`. |

### 3.2 Missed event queue

Events that arrive during the freeze window (between Step 1 and Step 5):

| Event type | Queuing strategy |
|-----------|-----------------|
| iCal feed changes | Final reconciliation pull (Step 2) captures latest state. No queue needed — full-replace on next sync after activation will catch everything. |
| Channex webhooks (during Channex → iCal switch) | 30-minute drain window processes them. After drain: discard with `channex.webhook.drained` log. |
| Atlas direct bookings | Allowed during migration (`CreatedDuringMigration = true`). Post-migration reconciliation verifies. |
| Channex webhooks (during iCal → Channex switch) | Not expected — Channex not yet connected. If somehow received: reject with 503. |

| ID | Requirement |
|----|-------------|
| CUT-14 | No separate message queue needed. DB-backed outbox + reconciliation pull provide sufficient event capture. |
| CUT-15 | Post-migration: any `CreatedDuringMigration` bookings MUST be verified against the new provider's state within 1 hour. If booking not reflected: trigger manual push/sync for that listing. |

### 3.3 Backlog reprocessing

After activation, the new provider must catch up on any changes that occurred during the freeze:

| Provider | Catch-up mechanism |
|----------|-------------------|
| iCal (new) | First poll after activation does full-replace. All events from feed imported fresh. Automatic catch-up. |
| Channex (new) | Backfill push sends full availability for 365 days. Any changes during freeze are included. Automatic catch-up. |

| ID | Requirement |
|----|-------------|
| CUT-16 | Catch-up MUST happen within the first sync cycle after activation. No manual intervention needed. |
| CUT-17 | Catch-up sync MUST be logged distinctly: `sync.migration.catchup` with `{propertyId, provider, eventsProcessed}`. |

### 3.4 Idempotency during cutover

| Operation | Idempotency guarantee |
|-----------|----------------------|
| Cutoff timestamp recording | `Property.SyncSwitchCutoffUtc` is idempotent — setting it twice to the same value is harmless. |
| Legacy block tagging | `UPDATE WHERE Source = 'iCal' AND CreatedAtUtc < @cutoff` is idempotent — running it twice has the same effect. |
| Channex backfill push | Channex API accepts full availability pushes idempotently — last write wins. |
| iCal full-replace | Idempotent by design — delete all + re-insert. |
| Lock acquisition | Idempotent — `WHERE LockedByWorker IS NULL OR ExpiresAtUtc < now` prevents double-lock. |

| ID | Requirement |
|----|-------------|
| CUT-18 | Every step of the cutover protocol MUST be safely re-runnable. If a step fails midway and is retried, it MUST NOT produce inconsistent state. |

---

## 4. Availability consistency model

### 4.1 Single source of truth rules

| Domain | Authoritative source | Rationale |
|--------|:-------------------:|-----------|
| **Atlas direct bookings** | Atlas DB (`Bookings` table) | Atlas created them. No external authority. |
| **Atlas manual blocks** | Atlas DB (`AvailabilityBlocks` where `Source = 'manual'`) | Host created them in Atlas UI. |
| **OTA reservations** | OTA (via iCal feed or Channex webhook) | OTA owns these bookings. Atlas mirrors the OTA's state. |
| **Rates** (Channex) | Atlas DB (`Listing.NightlyRate` etc.) | Atlas is the rate master. Channex receives pushes. |
| **Availability** (composite) | Atlas DB (union of bookings + blocks) | Atlas computes availability from all sources. Channex/iCal export reflects this computed state. |

| ID | Requirement |
|----|-------------|
| AVC-01 | Atlas DB is the **operational source of truth** for all availability decisions (booking creation, conflict detection, calendar display). |
| AVC-02 | For OTA-sourced reservations: if Atlas DB and OTA disagree, the **OTA is canonical**. Reconciliation MUST update Atlas to match OTA, not the reverse. |
| AVC-03 | For Atlas-sourced data pushed to OTA (rates, availability): Atlas is canonical. If Channex/iCal export disagrees with Atlas DB, re-push from Atlas. |

### 4.2 Reconciliation algorithm

#### Daily reconciliation job (03:00 UTC)

```
FOR EACH property WHERE SyncState IN ('ACTIVE_ICAL', 'ACTIVE_CHANNEX'):

    1. PULL latest external state:
       - ICAL: fetch all active calendars, parse events
       - CHANNEX: call Channex availability API for next 90 days (if available; else skip)

    2. COMPUTE Atlas expected state:
       - All Bookings (confirmed, not cancelled) for next 90 days
       - All AvailabilityBlocks (active) for next 90 days
       - Union → set of blocked dates per listing

    3. COMPARE:
       FOR EACH date in next 90 days:
           external_blocked = date appears in external source
           atlas_blocked = date appears in Atlas DB
           
           IF external_blocked AND NOT atlas_blocked:
               → MISMATCH: "external_only" (OTA has reservation Atlas missed)
               → ACTION: create AvailabilityBlock (iCal) or flag for Channex webhook reprocessing
           
           IF atlas_blocked AND NOT external_blocked:
               → MISMATCH: "atlas_only" (Atlas has block OTA doesn't)
               → ACTION: if block source is iCal/Channex → block may be stale, flag for review
                         if block source is Atlas booking → push to OTA (Channex) or trust export feed (iCal)
           
           IF both blocked: OK (consistent)
           IF neither blocked: OK (consistent)

    4. LOG results:
       sync.reconciliation.completed { propertyId, datesChecked, mismatches, autoHealed, flagged }

    5. AUTO-HEAL (if safe):
       - "external_only" with iCal source: create block automatically (missed during polling gap)
       - "atlas_only" where source is external and block is > 48h old: remove stale block
       - All other mismatches: flag for manual review, do NOT auto-heal
```

| ID | Requirement |
|----|-------------|
| AVC-04 | Daily reconciliation MUST run at 03:00 UTC (configurable). MUST complete within 30 minutes for all properties. |
| AVC-05 | Reconciliation MUST be per-property, not per-tenant. A tenant with 5 properties gets 5 independent reconciliation runs. |
| AVC-06 | Auto-heal is limited to **low-risk mismatches** (missing iCal block, stale external block). All others MUST be flagged as `ReconciliationMismatch` for manual review. |
| AVC-07 | Auto-heal MUST be toggleable: feature flag `SYNC_RECONCILIATION_AUTO_HEAL_ENABLED` (default `true`). When `false`: all mismatches flagged, none auto-healed. |
| AVC-08 | Reconciliation MUST respect rate limits. For iCal: one fetch per calendar (no additional cost). For Channex: one API call per property (within 60 req/min limit). |

#### Manual reconciliation trigger

`POST /api/platform/properties/{propertyId}/reconcile` (requires `atlas_admin` role) runs the same algorithm on demand.

### 4.3 Reconciliation mismatch data model

```
ReconciliationMismatch {
    Id              int PK
    TenantId        int FK
    PropertyId      int FK
    ListingId       int FK
    MismatchDate    date
    MismatchType    varchar(20)     -- 'external_only', 'atlas_only'
    ExternalSource  varchar(20)     -- 'ical', 'channex'
    AtlasRecordId   int?            -- AvailabilityBlock or Booking ID (if atlas_only)
    AutoHealed      bit
    HealAction      varchar(50)?    -- 'block_created', 'block_removed', null
    Status          varchar(20)     -- 'detected', 'healed', 'reviewed', 'dismissed'
    DetectedAtUtc   datetime2
    ReviewedAtUtc   datetime2?
}
```

### 4.4 SLA expectations

| Sync mode | Consistency SLA | Tolerance window | Reconciliation frequency |
|-----------|:---------------:|:----------------:|:------------------------:|
| `ICAL_BASIC` | Atlas mirrors OTA within 15 minutes of poll completion | Up to 30 minutes (poll interval + processing time) | Daily + per-poll check |
| `CHANNEX_API` (push) | OTA mirrors Atlas within 5 minutes of change | Up to 5 minutes (outbox cycle + Channex processing) | Daily + per-push verification |
| `CHANNEX_API` (webhook, v2) | Atlas mirrors OTA within 5 minutes of webhook delivery | Up to 5 minutes (webhook processing + outbox cycle) | Daily + per-webhook check |
| During migration (`MIGRATING`) | No consistency guarantee | Freeze window duration (max 72h). Catch-up sync within 10 minutes of activation. | Post-migration comparison (Step 6). |

| ID | Requirement |
|----|-------------|
| AVC-09 | Availability drift beyond the tolerance window MUST trigger a WARNING alert. |
| AVC-10 | Drift beyond 2× the tolerance window MUST trigger a CRITICAL alert. |
| AVC-11 | SLA measurements MUST be computed from structured logs and displayed on the sync health dashboard. |

---

## 5. Sync state machine specification

### 5.1 States

| State | Code | Description | Entry condition |
|-------|:----:|-------------|----------------|
| **NOT_CONFIGURED** | `not_configured` | No sync configured. Property has no iCal calendars and no Channex config. | Default state for new properties. |
| **ACTIVE_ICAL** | `active_ical` | iCal sync running. At least one active `ListingExternalCalendar`. | iCal calendars configured and first sync successful. |
| **ACTIVE_CHANNEX** | `active_channex` | Channex sync running. At least one `ChannelConfig` with `ConnectionStatus = 'connected'`. | Channex connected and first push successful. |
| **MIGRATING** | `migrating` | Provider switch in progress. Freeze window active. | Tenant or system initiates provider switch. |
| **SUSPENDED** | `suspended` | Sync paused. Configuration preserved. | Tenant pause, billing suspension, or admin action. |
| **ERROR** | `error` | Sync broken. Consecutive failures exceeded threshold. | Auto-transition from ACTIVE_* after failure threshold. |

### 5.2 State storage

`Property.SyncState` (`varchar(20)`, NOT NULL, default `'not_configured'`).

Supplementary fields:

| Field | Type | Purpose |
|-------|------|---------|
| `Property.SyncStateChangedAtUtc` | `datetime2` | When the state last changed. |
| `Property.SyncStatePreviousState` | `varchar(20)` | State before current. Used for resume after SUSPENDED. |
| `Property.SyncMigrationTargetState` | `varchar(20)?` | During MIGRATING: what state we are transitioning TO. |

### 5.3 Allowed transitions

```
                    ┌──────────────────────┐
                    │   NOT_CONFIGURED     │
                    └──────┬───────┬───────┘
                           │       │
              iCal setup   │       │  Channex setup
              complete     │       │  complete
                           ▼       ▼
                ┌──────────────┐ ┌──────────────┐
                │ ACTIVE_ICAL  │ │ACTIVE_CHANNEX│
                └──┬───┬───┬──┘ └──┬───┬───┬──┘
                   │   │   │       │   │   │
        failures   │   │   │switch │   │   │  failures
        exceed     │   │   └──┬────┘   │   │  exceed
        threshold  │   │      │        │   │  threshold
                   │   │      ▼        │   │
                   │   │ ┌──────────┐  │   │
                   │   │ │MIGRATING │  │   │
                   │   │ └────┬─────┘  │   │
                   │   │      │        │   │
                   │   │   complete/   │   │
                   │   │   rollback    │   │
                   │   │      │        │   │
                   ▼   │      │        │   ▼
              ┌────────┴──────┴────────┴────────┐
              │            ERROR                │
              └────────┬────────────────────────┘
                       │ reconnect
                       ▼
              ┌───────────────────┐
              │  (previous ACTIVE) │
              └───────────────────┘

              Any ACTIVE/ERROR ──pause──► SUSPENDED ──resume──► (previous state)
```

**Transition table:**

| From | To | Trigger | Validation |
|------|-----|---------|-----------|
| `NOT_CONFIGURED` | `ACTIVE_ICAL` | First iCal calendar activated and synced | At least one `ListingExternalCalendar.IsActive = true` |
| `NOT_CONFIGURED` | `ACTIVE_CHANNEX` | First Channex config connected | `ChannelConfig.ConnectionStatus = 'connected'` |
| `ACTIVE_ICAL` | `MIGRATING` | Tenant initiates switch to Channex | Tenant on Premium plan |
| `ACTIVE_ICAL` | `SUSPENDED` | Tenant pause or billing suspension | — |
| `ACTIVE_ICAL` | `ERROR` | `ConsecutiveFailures >= 10` across all calendars | All calendars failing |
| `ACTIVE_ICAL` | `NOT_CONFIGURED` | All calendars deleted/deactivated | No active calendars remaining |
| `ACTIVE_CHANNEX` | `MIGRATING` | Tenant initiates switch to iCal | — |
| `ACTIVE_CHANNEX` | `SUSPENDED` | Tenant pause or billing suspension | — |
| `ACTIVE_CHANNEX` | `ERROR` | `ConsecutiveFailures >= 5` on all Channex configs | All configs failing |
| `ACTIVE_CHANNEX` | `NOT_CONFIGURED` | All Channex configs disconnected | No connected configs remaining |
| `MIGRATING` | `ACTIVE_ICAL` | Migration to iCal completed or rollback from Channex migration | — |
| `MIGRATING` | `ACTIVE_CHANNEX` | Migration to Channex completed or rollback from iCal migration | — |
| `MIGRATING` | `NOT_CONFIGURED` | Migration to NONE completed | — |
| `ERROR` | `ACTIVE_ICAL` | Reconnection succeeds (iCal URLs fixed) | At least one successful sync |
| `ERROR` | `ACTIVE_CHANNEX` | Reconnection succeeds (Channex API key fixed) | Successful test connection |
| `ERROR` | `SUSPENDED` | Billing suspension during error state | — |
| `ERROR` | `NOT_CONFIGURED` | All configs removed | — |
| `SUSPENDED` | `ACTIVE_ICAL` | Resume (if previous state was `ACTIVE_ICAL`) | Subscription active + successful sync |
| `SUSPENDED` | `ACTIVE_CHANNEX` | Resume (if previous state was `ACTIVE_CHANNEX`) | Subscription active + successful push |
| `SUSPENDED` | `ERROR` | Resume attempted but sync fails | Previous state's error threshold met |
| `SUSPENDED` | `NOT_CONFIGURED` | All configs removed while suspended | — |

### 5.4 Invalid transitions

| From | To | Reason |
|------|-----|--------|
| `NOT_CONFIGURED` | `MIGRATING` | Nothing to migrate from. Must be in an ACTIVE state first. |
| `NOT_CONFIGURED` | `ERROR` | No sync configured, so no errors possible. |
| `NOT_CONFIGURED` | `SUSPENDED` | Nothing to suspend. |
| `MIGRATING` | `MIGRATING` | Already migrating. Cannot start a second migration. |
| `MIGRATING` | `ERROR` | Migration failures → rollback to previous state, not ERROR. |
| `MIGRATING` | `SUSPENDED` | Cannot suspend during migration. Must complete or rollback first. |
| `ACTIVE_ICAL` | `ACTIVE_CHANNEX` | Must go through MIGRATING. Direct switch forbidden. |
| `ACTIVE_CHANNEX` | `ACTIVE_ICAL` | Must go through MIGRATING. Direct switch forbidden. |
| `SUSPENDED` | `MIGRATING` | Cannot start migration while suspended. Must resume first. |

| ID | Requirement |
|----|-------------|
| SSM-01 | State transitions MUST be validated. Attempts at invalid transitions MUST be rejected with 400 and logged: `sync.state.invalid_transition` with `{propertyId, currentState, attemptedState}`. |
| SSM-02 | Every state transition MUST be logged: `sync.state.changed` with `{propertyId, tenantId, from, to, trigger, triggeredBy}`. |
| SSM-03 | `Property.SyncStatePreviousState` MUST be set on every transition. Used by SUSPENDED → resume to restore correct target. |
| SSM-04 | `SyncState` MUST be checked by all sync workers before processing. Worker MUST skip property if state is not the worker's expected active state. |
| SSM-05 | `SyncState` transitions MUST be atomic: use `UPDATE Properties SET SyncState = @new WHERE Id = @id AND SyncState = @expectedCurrent`. If 0 rows affected: concurrent modification detected, retry or abort. |

---

## 6. Historical booking integrity

### 6.1 Provider tag preservation

Every booking retains a permanent record of which sync provider was active when it was created or ingested.

| Field | Type | Description | Mutability |
|-------|------|-------------|:----------:|
| `Booking.SourceProvider` | `varchar(20)` | Sync provider active at creation time: `'ical_basic'`, `'channex_api'`, `'atlas_direct'`, `'manual'` | **Immutable** after creation |
| `Booking.SourceOta` | `varchar(50)` | OTA name: `'airbnb'`, `'bookingcom'`, `'direct'`, `null` (for manual) | **Immutable** after creation |
| `Booking.NormalizedExternalId` | `varchar(32)` | NERID (§2.1) | Updated only if OTA changes reservation ID (§2.7) |

| ID | Requirement |
|----|-------------|
| HBI-01 | `Booking.SourceProvider` MUST be set at creation time and MUST NEVER be changed during or after a provider switch. |
| HBI-02 | `Booking.SourceOta` MUST be set at creation time and MUST NEVER be changed. |
| HBI-03 | Provider switch MUST NOT retroactively update `SourceProvider` or `SourceOta` on existing bookings. |

### 6.2 Commission snapshot integrity

| Field | Existing | Behaviour during switch |
|-------|:--------:|------------------------|
| `Booking.CommissionPercentSnapshot` | Yes (RA-001) | **Unchanged.** Captured at booking creation. Provider switch does not recalculate. |
| `Booking.PaymentModeSnapshot` | Yes (RA-001) | **Unchanged.** Captured at booking creation. Provider switch does not affect. |
| `Booking.TotalAmount` | Yes | **Unchanged.** Financial amount is locked at creation. |
| `Booking.BookingSource` | Yes | **Unchanged.** Source channel (e.g., `"marketplace"`, `"admin_portal"`, `"ota"`) is immutable. |

| ID | Requirement |
|----|-------------|
| HBI-04 | Provider switch MUST NOT modify ANY field on ANY existing `Booking` row except `SyncOrphanedAtUtc` (for Channex → iCal downgrade orphans, §1.B). |
| HBI-05 | Settlement calculations for existing bookings MUST use the original `CommissionPercentSnapshot` and `PaymentModeSnapshot`, regardless of current sync mode. |
| HBI-06 | If a booking's commission is disputed post-switch: resolution uses `AuditLog` entries showing commission at time of creation. Provider switch is irrelevant. |

### 6.3 Reporting isolation

Reports and analytics MUST support filtering by `SourceProvider`:

| Report | Filter support | Purpose |
|--------|:-------------:|---------|
| Revenue by source | `SourceProvider`, `SourceOta` | "How much revenue came through Airbnb iCal vs Channex?" |
| Booking count by provider | `SourceProvider` | "How many bookings per sync provider?" |
| Commission accuracy | `SourceProvider` | "Commission applied correctly per snapshot?" |
| Sync switch history | `AuditLog` where action contains `sync.migration` | "When did this property switch providers?" |

| ID | Requirement |
|----|-------------|
| HBI-07 | All booking list views, exports, and reports MUST support `SourceProvider` as a filter dimension. |
| HBI-08 | Platform admin booking search MUST include `SourceProvider` and `SourceOta` in results and filters. |

### 6.4 AvailabilityBlock provenance

| Field | Type | Description |
|-------|------|-------------|
| `AvailabilityBlock.SourceProvider` | `varchar(20)` | `'ical_basic'`, `'channex_api'`, `'manual'`, `'atlas_booking'` |
| `AvailabilityBlock.Source` | `varchar(50)` | Existing field: `'iCal'`, `'manual'`, `'Channex'`, etc. |
| `AvailabilityBlock.IsLegacy` | `bit` | `true` if tagged as legacy during provider switch. Excluded from conflict detection. |

| ID | Requirement |
|----|-------------|
| HBI-09 | Legacy blocks (`IsLegacy = true`) MUST be excluded from: (a) conflict detection, (b) availability computation for OTA pushes, (c) active calendar display (shown greyed out in a separate "Legacy" section). |
| HBI-10 | Legacy blocks MUST be auto-purged 30 days after the provider switch cutoff timestamp. |

---

## 7. Consistency & idempotency requirements

### 7.1 Webhook idempotency keys

| Provider | Idempotency key source | Storage |
|----------|:----------------------:|---------|
| Channex (v2) | `WebhookId` from Channex payload (or `event_id` field) | `ConsumedWebhook { WebhookId varchar(200) UNIQUE, ReceivedAtUtc datetime2, PropertyId int, EventType varchar(50) }` |
| Razorpay | `RazorpayPaymentId` (existing implementation) | Existing uniqueness on `Payment.RazorpayPaymentId` |

| ID | Requirement |
|----|-------------|
| IDP-01 | Webhook handler MUST check `ConsumedWebhook` before processing. If `WebhookId` exists: return 200 immediately, log `webhook.duplicate`. |
| IDP-02 | `ConsumedWebhook` insert MUST happen in the same transaction as outbox row insert. If transaction fails: neither is persisted (allows safe retry). |
| IDP-03 | `ConsumedWebhook` rows older than 90 days MAY be purged. Risk of replay after 90 days is accepted (negligible). |

### 7.2 iCal polling idempotency logic

| Mechanism | How it provides idempotency |
|-----------|---------------------------|
| **Full-replace strategy** | Each poll deletes ALL blocks for that calendar and re-inserts from feed. Running the same poll twice produces identical final state. |
| **Content hash** | If feed body hash matches previous poll: skip entirely. No writes. Idempotent by skipping. |
| **Per-calendar lock** | Prevents concurrent polls on the same calendar. No race conditions. |

| ID | Requirement |
|----|-------------|
| IDP-04 | Full-replace MUST be wrapped in a single DB transaction: `DELETE` + `INSERT` are atomic. If insert fails: old blocks remain (transaction rolled back). |
| IDP-05 | If poller crashes mid-cycle (after deleting blocks but before inserting new ones): rollback ensures no data loss. |

### 7.3 Outbox replay safety

The DB-backed outbox (`OutboxMessage` table) provides at-least-once delivery. Workers must handle replays.

| Outbox message type | Replay safety mechanism |
|--------------------|------------------------|
| `channex.push.rates` | Channex API is idempotent for rate pushes (last-write-wins). Safe to replay. |
| `channex.push.availability` | Same as rates: last-write-wins. Safe to replay. |
| `channex.webhook.process` | Worker checks `ConsumedWebhook` before processing. Replay → skip. |
| `sync.notification.send` | Worker checks `NotificationLog` for duplicate `(recipientId, templateId, contextHash)`. Replay within 5 minutes → skip. |
| `sync.reconciliation.run` | Reconciliation is read-compare-write. Idempotent. Safe to replay. |

| ID | Requirement |
|----|-------------|
| IDP-06 | Outbox worker MUST handle all message types idempotently. Replaying any message MUST NOT produce incorrect state. |
| IDP-07 | Outbox worker MUST track `AttemptCount` and `LastAttemptUtc` per message. After `MaxAttempts` (configurable, default 5): move to dead-letter state (`Status = 'Failed'`). Alert platform admin. |
| IDP-08 | Dead-letter messages MUST be reviewable and manually retryable via platform admin dashboard. |

### 7.4 At-least-once vs exactly-once behaviour

| Layer | Delivery guarantee | How |
|-------|:------------------:|-----|
| Webhook ingestion → outbox | **Exactly-once** (within `ConsumedWebhook` TTL) | `ConsumedWebhook` uniqueness + transactional insert. |
| Outbox → worker processing | **At-least-once** | Worker may crash after processing but before marking message as completed. Re-processing is safe due to idempotent handlers. |
| Worker → external API (Channex push) | **At-least-once** | Network failure after Channex accepts but before worker acknowledges. Channex receives duplicate push (idempotent, last-write-wins). |
| iCal polling → DB write | **Exactly-once** (per cycle) | Per-calendar lock + transactional full-replace. |

| ID | Requirement |
|----|-------------|
| IDP-09 | The system guarantees **at-least-once processing** for all sync operations. Exactly-once is achieved at the application level through idempotency mechanisms, not through infrastructure. |
| IDP-10 | **Zero financial duplicates**: even with at-least-once delivery, no duplicate `Booking` records with `PaymentStatus = 'Captured'` may exist for the same external reservation. Enforced by `UX_Booking_ExternalId` unique index (§2.2). |

### 7.5 Retry backoff model

| Operation | Strategy | Intervals | Max retries | Dead-letter |
|-----------|:--------:|-----------|:-----------:|:-----------:|
| iCal fetch failure | Exponential | 5m, 10m, 20m, 30m (capped) | ∞ (adaptive interval increase) | No dead-letter; calendar auto-disabled after 10 consecutive failures |
| Channex push failure | Exponential | 30s, 60s, 120s | 3 per message | Yes — outbox message → `Failed` |
| Channex webhook processing failure | Exponential | 15s, 30s, 60s, 120s, 300s | 5 | Yes — outbox message → `Failed` |
| Reconciliation failure | Fixed | 1 hour | 3 (per day) | Skip and alert |
| Migration step failure | No retry | — | 0 | Halt migration, alert platform admin |

| ID | Requirement |
|----|-------------|
| IDP-11 | Retry intervals MUST use jitter: `actual_delay = base_delay * (1 + random(0, 0.3))` to prevent synchronized retries across multiple failing properties. |
| IDP-12 | Migration steps MUST NOT auto-retry. Any failure halts the cutover process and requires platform admin intervention. |

---

## 8. Failure modeling

### 8.1 Channex outage during migration

**Scenario:** Tenant is migrating iCal → Channex. `TestConnectionAsync` passed. During backfill push (Step 4 of cutover): Channex returns 503 for all requests.

| Phase | Impact | Recovery |
|-------|--------|----------|
| During backfill | Backfill push fails. OTAs do not receive updated availability. | Retry backfill 3 times (30s, 60s, 120s). |
| If retries exhausted | Migration cannot complete. Property stuck in `MIGRATING`. | Auto-rollback to `ACTIVE_ICAL`. iCal calendars re-enabled. Alert: `sync.migration.rollback` with `reason: 'channex_outage'`. |
| Post-rollback | Property resumes iCal polling. Channex config preserved. | Tenant can retry migration later. |

| ID | Requirement |
|----|-------------|
| FLR-01 | Channex outage during migration MUST trigger auto-rollback within 10 minutes of backfill failure. |
| FLR-02 | Rollback MUST restore exact pre-migration state: `SyncState`, `SyncModeOverride`, `ListingExternalCalendar.IsActive`, and clear `SyncSwitchCutoffUtc`. |

### 8.2 iCal feed malformed during switch

**Scenario:** Tenant migrating Channex → iCal. During first iCal sync (Step 4 of Channex→iCal cutover): iCal feed returns garbage HTML instead of valid ICS.

| Phase | Impact | Recovery |
|-------|--------|----------|
| First sync attempt | Parse returns 0 events. `LastSyncError` set. | Do NOT auto-rollback (tenant chose downgrade). |
| Subsequent attempts | ConsecutiveFailures increment. | Alert tenant: "iCal URL appears invalid. Please verify the URL in your Airbnb settings." |
| Resolution | Tenant updates URL. Next sync succeeds. | `SyncState` remains `ACTIVE_ICAL` (transition already happened). Normal retry flow. |

| ID | Requirement |
|----|-------------|
| FLR-03 | Malformed iCal feed after Channex → iCal switch MUST NOT trigger auto-rollback to Channex (tenant deliberately downgraded). |
| FLR-04 | System MUST continue retrying with adaptive backoff. Calendar auto-disabled after 10 failures. |

### 8.3 Partial booking ingestion

**Scenario:** Channex webhook with `booking_new` is processed. `Booking` record created. But `AvailabilityBlock` creation fails (e.g., DB constraint violation).

| Phase | Impact | Recovery |
|-------|--------|----------|
| Booking exists without block | Listing appears available for the booked dates. Potential overbooking. | Transactional guarantee prevents this. |

| ID | Requirement |
|----|-------------|
| FLR-05 | Booking creation and AvailabilityBlock creation MUST be in the same DB transaction. Partial state is impossible (both commit or both rollback). |
| FLR-06 | If transaction fails: outbox message remains `Pending`. Retry creates both atomically. |

### 8.4 Race condition: webhook and manual booking

**Scenario:** A Channex webhook for a new OTA booking and a manual Atlas booking for the same listing and overlapping dates arrive within seconds of each other.

| Timing | Outcome |
|--------|---------|
| Manual booking commits first | AvailabilityBlock created. Webhook processing detects conflict (overlapping dates). Creates SyncConflict. Booking still created (both exist). |
| Webhook commits first | Booking + block created. Manual booking attempt checks availability → dates blocked → booking rejected with "Dates not available". |
| Truly simultaneous | Per-listing lock (RA-IC-001 §5.3 SCH-06/07) prevents true simultaneity. One acquires lock first; other waits or retries. |

| ID | Requirement |
|----|-------------|
| FLR-07 | Booking creation MUST acquire a per-listing lock (using `SyncLock` table) before checking availability and creating the booking. Lock held for duration of the transaction. |
| FLR-08 | Lock wait timeout: 5 seconds. If lock not acquired within 5s: return 409 "Please try again in a moment. Calendar update in progress." |
| FLR-09 | If both bookings are created (lock acquired sequentially and both dates were checked against stale state): conflict detection catches this within 1 minute. Alert tenant. |

### 8.5 Tenant switches provider mid-booking flow

**Scenario:** Guest is on checkout page (Razorpay order created). Meanwhile, tenant initiates provider switch. Booking creation request arrives during `MIGRATING` state.

| ID | Requirement |
|----|-------------|
| FLR-10 | Booking creation MUST be allowed during `MIGRATING` state. The booking is flagged `CreatedDuringMigration = true`. |
| FLR-11 | Provider switch MUST NOT block the booking API. Migration freeze only affects sync workers, not booking endpoints. |
| FLR-12 | Post-migration reconciliation verifies the mid-migration booking is reflected in the new provider's state (§3.1, Step 6). |

### 8.6 Double-switch (rapid back-and-forth)

**Scenario:** Tenant switches iCal → Channex, then immediately requests Channex → iCal before first migration completes.

| ID | Requirement |
|----|-------------|
| FLR-13 | While property is in `MIGRATING` state, a second migration request MUST be rejected: 409 "Migration already in progress. Please wait for completion or rollback." |
| FLR-14 | Tenant CAN trigger manual rollback of current migration. After rollback completes (state returns to ACTIVE_*), tenant can initiate a new switch. |

### 8.7 Worker crash during cutover

**Scenario:** App Service restarts during Step 5 (Activate new provider) of cutover.

| ID | Requirement |
|----|-------------|
| FLR-15 | Activation (Step 5) is a single DB transaction (CUT-08). If worker crashes mid-transaction: DB rolls back. Property remains in `MIGRATING`. |
| FLR-16 | On worker restart: migration recovery job checks for properties in `MIGRATING` state where `SyncStateChangedAtUtc` is > 10 minutes ago. Attempts to resume cutover from last completed step. |
| FLR-17 | Each cutover step records its completion: `PropertyMigrationStep { PropertyId, StepNumber, CompletedAtUtc }`. Recovery job reads this to determine resume point. |

---

## 9. Monitoring & alerting for migration

### 9.1 Migration health dashboard

Route: `/platform/migrations` (requires `atlas_admin` role).

| Panel | Content | Data source |
|-------|---------|-------------|
| **Active migrations** | Properties currently in `MIGRATING` state. Duration. Target provider. | `Properties WHERE SyncState = 'migrating'` |
| **Recent migrations (7d)** | Completed/rolled-back migrations. Duration. Outcome. | `AuditLog WHERE action LIKE 'sync.migration.%'` |
| **Migration success rate** | Completed / (Completed + Rolled back) as percentage. | Computed from AuditLog |
| **Average migration duration** | p50, p90 of successful migration durations. | Computed from AuditLog timestamps |
| **Stuck migrations** | Properties in `MIGRATING` for > 4 hours. | `Properties WHERE SyncState = 'migrating' AND SyncStateChangedAtUtc < now - 4h` |

### 9.2 Alert rules

| Alert | Condition | Severity | Action |
|-------|-----------|:--------:|--------|
| **Abnormal booking spike** | > 3× average booking rate for a property within 1 hour of migration activation | WARNING | Possible duplicate ingestion. Check for dual-source bookings. |
| **Availability mismatch** | Post-migration snapshot comparison shows > 5% date drift | WARNING → CRITICAL at > 10% | Auto-rollback triggered at CRITICAL. Manual investigation at WARNING. |
| **Duplicate booking detected** | `DuplicateCandidate` row created with `MatchReason = 'exact_nerid'` within 1 hour of migration | CRITICAL | Halt processing. Platform admin must review and merge. |
| **Sync stall post-migration** | No successful sync within 15 minutes of activation | WARNING | Check new provider connectivity. May auto-rollback if within 30-min window. |
| **Stuck migration** | Property in `MIGRATING` for > 4 hours | WARNING → CRITICAL at > 24h | Platform admin must investigate. Auto-rollback at 72h timeout. |
| **Rollback occurred** | Any auto-rollback event | WARNING | Review cause. Prepare tenant communication. |
| **Mid-migration booking** | `Booking.CreatedDuringMigration = true` not verified within 1 hour post-migration | WARNING | Trigger manual reconciliation for that listing. |

| ID | Requirement |
|----|-------------|
| MIG-01 | All alerts MUST fire via existing Application Insights alert rules (RA-006 §4.3). No additional infrastructure. |
| MIG-02 | Migration dashboard MUST be a page in the existing admin console SPA, not a separate tool. |
| MIG-03 | Stuck migration alert at > 24h MUST also notify tenant via email: "Your provider switch is taking longer than expected. Please contact support or complete the required steps." |

### 9.3 Minimal viable monitoring

All monitoring uses existing infrastructure:

| Component | Tool | Cost |
|-----------|------|:----:|
| Structured logs | Application Insights (existing) | Free tier |
| Alert rules | Application Insights alerts (≤ 10 rules, existing free tier) | Free |
| Dashboard | Admin console SPA page querying API endpoints | Free |
| Metrics computation | Background job computing metrics from DB + structured logs | Free (App Service compute) |

| ID | Requirement |
|----|-------------|
| MIG-04 | No Prometheus, Grafana, ELK, PagerDuty, or OpsGenie. All monitoring within Application Insights + admin console. |
| MIG-05 | Migration metrics MUST be computable from `AuditLog` + `Property.SyncState` + structured logs only. No additional data stores. |

---

## 10. Testing matrix

All test cases in Given/When/Then format.

### 10.1 Switching providers with historical bookings

#### TC-SW-01: iCal → Channex with 100 historical bookings

**Given** a property on `ACTIVE_ICAL` has 100 historical bookings (80 completed, 15 confirmed future, 5 cancelled) and 20 active iCal-sourced AvailabilityBlocks,
**When** the tenant completes the switch wizard to `CHANNEX_API`,
**Then:**
- All 100 bookings retain original `SourceProvider = 'ical_basic'` and `CommissionPercentSnapshot` unchanged
- All 20 iCal blocks are tagged `IsLegacy = true`
- `SyncSwitchCutoffUtc` is recorded
- Channex backfill pushes availability reflecting the 15 future bookings as blocked dates
- `SyncState = 'active_channex'`
- No `DuplicateCandidate` rows created

#### TC-SW-02: Channex → iCal with active Channex bookings

**Given** a property on `ACTIVE_CHANNEX` has 50 bookings (30 via Channex, 20 via Atlas direct) and 10 future Channex-sourced bookings,
**When** the tenant switches to `ICAL_BASIC`,
**Then:**
- All 50 bookings retain original `SourceProvider` values
- 10 future Channex-sourced bookings are flagged `SyncOrphanedAtUtc`
- `ChannelConfig.ConnectionStatus = 'paused'`
- After iCal setup: first poll imports blocks correctly
- Admin UI shows orphan warnings on the 10 bookings

### 10.2 Switching during high booking velocity

#### TC-SW-03: Switch with bookings arriving every minute

**Given** a property on `ACTIVE_ICAL` is receiving ~1 new iCal event per minute (high season),
**When** migration to `CHANNEX_API` initiates (freeze starts),
**Then:**
- iCal poller continues fetching but does NOT create new blocks during freeze
- Bookings created manually in Atlas during freeze are flagged `CreatedDuringMigration`
- After Channex activation: backfill push includes all Atlas bookings (including mid-migration ones)
- Post-activation monitoring detects no anomalies within 30 minutes

### 10.3 Switching with cancellations in flight

#### TC-SW-04: Cancellation arrives during freeze window

**Given** a property is in `MIGRATING` state (iCal → Channex, freeze active),
**When** an iCal feed update removes a previously synced event (cancellation on Airbnb),
**Then:**
- During freeze: existing iCal block is NOT removed (freeze prevents writes)
- After Channex activation: the block remains as legacy (tagged `IsLegacy`)
- Daily reconciliation (next day) detects the mismatch and flags it
- If tenant manually confirms cancellation: block removed via admin action

#### TC-SW-05: Channex cancellation webhook during Channex → iCal switch drain

**Given** a property is in `MIGRATING` state (Channex → iCal, 30-min drain window active),
**When** a `booking_cancelled` webhook arrives from Channex,
**Then:**
- Within drain window: webhook processed normally. Booking cancelled. Block removed.
- After drain window: webhook acknowledged (200) but discarded. Logged as `channex.webhook.drained`.

### 10.4 Switching with future bookings

#### TC-SW-06: Future bookings spanning months

**Given** a property has bookings for dates 3 months, 6 months, and 11 months in the future,
**When** switching from iCal to Channex,
**Then:**
- Channex backfill pushes availability for full 365 days, correctly showing all three future date ranges as blocked
- No availability drift detected in post-migration comparison

### 10.5 Switching back and forth repeatedly

#### TC-SW-07: Three consecutive switches

**Given** a property on `ACTIVE_ICAL`,
**When** the following sequence occurs:
1. Switch to `CHANNEX_API` (completes successfully)
2. Wait 48 hours
3. Switch back to `ICAL_BASIC` (completes successfully)
4. Wait 48 hours
5. Switch to `CHANNEX_API` again (completes successfully)
**Then:**
- After all three switches: zero duplicate bookings
- All historical bookings retain correct `SourceProvider` values
- `SyncSwitchCutoffUtc` reflects the LAST switch timestamp
- Legacy block count is zero (previous legacy blocks were auto-purged or superseded)
- Commission on all bookings unchanged

#### TC-SW-08: Rapid back-and-forth (same day)

**Given** a property just completed migration to `ACTIVE_CHANNEX` (10 minutes ago),
**When** the tenant requests immediate switch back to `ICAL_BASIC`,
**Then:**
- New migration initiates (previous migration completed, state is ACTIVE_CHANNEX not MIGRATING)
- Channex push stops
- Drain window (30 min) observed
- After iCal activation: first poll imports fresh state
- No duplicates despite rapid switching

### 10.6 Simulated webhook delays

#### TC-SW-09: Webhook arrives 25 minutes after migration start

**Given** a property is migrating from `ACTIVE_CHANNEX` to `ACTIVE_ICAL` (drain window: 30 min),
**When** a Channex `booking_new` webhook arrives 25 minutes after migration start,
**Then:**
- Webhook is within drain window: processed normally
- Booking created with `SourceProvider = 'channex_api'`
- Post-migration: booking flagged as created during transition

#### TC-SW-10: Webhook arrives 35 minutes after migration start

**Given** same setup as TC-SW-09,
**When** webhook arrives 35 minutes after migration start (past drain window),
**Then:**
- Webhook acknowledged (200) but payload discarded
- Logged as `channex.webhook.drained`
- Booking NOT created in Atlas
- If booking exists on OTA: next iCal sync (after iCal activation) will pick it up as a block

### 10.7 Simulated iCal feed corruption

#### TC-SW-11: Feed returns HTML during iCal → Channex switch reconciliation

**Given** a property is migrating iCal → Channex, Step 2 (reconciliation pull) running,
**When** iCal feed returns HTML instead of valid ICS,
**Then:**
- Reconciliation logs: `sync.migration.reconciliation_warning` with `{error: 'parse_failed'}`
- Migration proceeds with WARNING (reconciliation is not strictly blocking for iCal → Channex since we're leaving iCal)
- Existing iCal blocks preserved as-is (no update attempted)
- Channex backfill based on current DB state (which is authoritative)

#### TC-SW-12: Feed corruption after Channex → iCal switch

**Given** a property just switched from Channex to iCal, first poll attempted,
**When** iCal feed is corrupted (returns 0 events from normally full calendar),
**Then:**
- Full-replace would delete all blocks and insert 0 (dangerous!)
- Safety check: if previous sync had > 10 events and current parse returns 0: log CRITICAL `ical.suspicious_empty_feed`, DO NOT execute full-replace, increment `ConsecutiveFailures`
- Existing blocks preserved until a valid feed is obtained

| ID | Requirement |
|----|-------------|
| TST-01 | Safety check MUST prevent full-replace when feed returns 0 events but previous sync had > 10 events. Protects against feed corruption or URL invalidation wiping all blocks. |

### 10.8 Additional critical edge cases

#### TC-SW-13: Subscription suspension during active migration

**Given** a property is in `MIGRATING` state,
**When** the tenant's subscription transitions to `Suspended`,
**Then:**
- Migration is NOT interrupted (SSM-04: MIGRATING → SUSPENDED is an invalid transition)
- Billing lock does NOT apply to migration process (migration is a system operation, not a tenant mutation)
- After migration completes: `SyncState` transitions to target ACTIVE state, then immediately to `SUSPENDED` (billing system re-evaluates)

#### TC-SW-14: Concurrent booking creation during lock acquisition

**Given** two booking requests arrive simultaneously for the same listing during migration Step 3 (lock active),
**When** both attempt to acquire the per-listing lock,
**Then:**
- First request acquires lock, checks availability, creates booking
- Second request waits (up to 5s timeout), then acquires lock, checks availability against updated state
- If dates overlap with first booking: second request rejected
- If dates don't overlap: both succeed

---

## 11. Definition of done — safe sync switching

### 11.1 Mandatory checklist

Every item MUST pass before sync switching is considered production-ready.

| # | Criterion | Verification method | Pass threshold |
|:-:|-----------|-------------------|:--------------:|
| 1 | **No duplicate bookings in 100-switch simulation** | Automated test: 100 sequential iCal↔Channex switches on a single property with 50 active bookings. Count bookings before and after. | `bookings_after == bookings_before` (zero duplicates) |
| 2 | **No lost bookings** | Same simulation as #1. Every booking present before simulation exists after. | `missing_bookings == 0` |
| 3 | **No commission corruption** | Same simulation. Compare `CommissionPercentSnapshot` on every booking before and after. | `corrupted_snapshots == 0` |
| 4 | **No payout corruption** | Same simulation. Compare `PaymentModeSnapshot` and `TotalAmount` on every booking. | `corrupted_payouts == 0` |
| 5 | **No availability drift beyond SLA** | Post-switch: compare pre-migration snapshot with post-migration state. Drift dates ≤ tolerance window. | iCal: ≤ 30 min drift. Channex: ≤ 5 min drift. |
| 6 | **Audit logs complete** | Every state transition in the simulation has a corresponding `AuditLog` entry. | `missing_audit_entries == 0` |
| 7 | **State machine integrity** | No property ends in an unexpected state. All final states are valid per §5.3. | All properties in `ACTIVE_*` or `NOT_CONFIGURED` |
| 8 | **Rollback works** | Simulate 10 migrations that fail at Step 4 (backfill). Verify all roll back cleanly. | `stuck_in_migrating == 0` |
| 9 | **Concurrent booking safety** | During migration: create 10 concurrent bookings for different dates. All succeed or fail predictably. No data corruption. | Zero partial states. |
| 10 | **Legacy block cleanup** | After 30 days post-switch: legacy blocks auto-purged. | `legacy_blocks_remaining == 0` after purge job |
| 11 | **Mid-migration booking reconciliation** | Create 5 bookings during freeze. Post-migration: all 5 reflected in new provider's state. | `unverified_mid_migration_bookings == 0` |
| 12 | **Financial integrity across switch** | For every booking created before, during, and after switch: commission calculation produces correct result. Settlement flow works. | Manual verification: 10 sample bookings per scenario. |
| 13 | **Orphan booking handling** | Channex → iCal switch: all future Channex bookings flagged `SyncOrphanedAtUtc`. UI shows warning. | `unflagged_orphans == 0` |
| 14 | **Alert firing** | Trigger each alert condition (§9.2) in test environment. Verify email received. | All 7 alert types fire correctly. |
| 15 | **Performance** | 100-switch simulation completes within 60 minutes (wall clock). No single switch takes > 5 minutes (excluding connection window). | `max_switch_duration < 5 min` |

### 11.2 Load test parameters

| Parameter | Value |
|-----------|:-----:|
| Simulated properties | 100 (10 tenants × 10 properties) |
| Bookings per property | 50 (mix of historical and future) |
| Switch operations | 100 (random iCal↔Channex per property) |
| Concurrent switches | Up to 5 properties migrating simultaneously |
| Concurrent bookings during migration | 10 per property per switch |
| Simulated external events during freeze | 5 per property (mock webhook / mock iCal change) |

### 11.3 Non-functional requirements

| Requirement | Target |
|-------------|:------:|
| Single switch overhead (DB operations) | < 50 queries |
| Single switch lock duration | < 30 seconds |
| Memory overhead during migration | < 50 MB per property (snapshot + reconciliation) |
| Migration recovery time (after worker crash) | < 5 minutes |

---

## Appendix A: Requirement ID index

| Prefix | Domain | Count |
|--------|--------|:-----:|
| PSW- | Provider switching scenarios | 25 |
| DUP- | Duplicate prevention | 19 |
| CUT- | Cutover strategy | 18 |
| AVC- | Availability consistency | 11 |
| SSM- | Sync state machine | 5 |
| HBI- | Historical booking integrity | 10 |
| IDP- | Idempotency | 12 |
| FLR- | Failure modeling | 17 |
| MIG- | Migration monitoring | 5 |
| TST- | Testing | 1 |
| **Total** | | **123** |

## Appendix B: Cross-reference to RA-IC-001

| This doc section | RA-IC-001 section | Relationship |
|:----------------:|:-----------------:|:------------:|
| §1 Provider switching scenarios | §4.3 Provider switching rules | **Deepens**: IC-001 defined high-level steps; IC-002 adds freeze windows, reconciliation, duplicate prevention, audit logs, and edge cases for all 6 scenarios. |
| §2 Duplicate prevention | §2.A.4 Idempotency rules (ICI-04..07) | **Extends**: IC-001 covered iCal-level deduplication; IC-002 adds cross-provider NERID, unique constraints, merge policy, and edge cases. |
| §3 Cutover strategy | §4.3 + §4.4.4 Sequence diagram | **Deepens**: IC-001 had high-level steps and diagram; IC-002 adds 6-step protocol, lock strategy, snapshot comparison, missed event queue, backlog reprocessing. |
| §4 Availability consistency | §2.C Conflict detection + §3.6 Reconciliation | **Extends**: IC-001 covered conflict detection and daily reconciliation; IC-002 adds consistency model, SoT rules, reconciliation algorithm, auto-heal policy, mismatch data model. |
| §5 Sync state machine | §3.1 Connection lifecycle (CHX-01) | **Supersedes**: IC-001 defined connection lifecycle; IC-002 defines comprehensive property-level state machine with all states, transitions, and validation. |
| §6 Historical booking integrity | §4.2 Data model normalization (ARC-05..08) | **Extends**: IC-001 defined NormalizedReservation; IC-002 adds provider tag preservation, commission snapshot integrity, reporting isolation. |
| §7 Idempotency | §2.A.4 (ICI-04..07), §3.4 (CHX-10..11) | **Deepens**: IC-001 covered per-provider idempotency; IC-002 adds unified model, outbox replay safety, retry backoff, at-least-once guarantees. |
| §8 Failure modeling | §7 (RA-002) Vendor outage fallback | **Extends**: IC-002 models 7 specific failure scenarios with step-by-step recovery procedures. |
| §9 Migration monitoring | §6 Observability & admin tooling | **Extends**: IC-001 covered general sync observability; IC-002 adds migration-specific dashboard and alert rules. |
| §10 Testing matrix | §9 Acceptance criteria & test matrix | **Extends**: IC-001 covered functional tests; IC-002 adds migration-specific Given/When/Then tests and load test parameters. |

## Appendix C: Cross-reference to other RA docs

| Topic | This doc | Other RA doc | Relationship |
|-------|:--------:|-------------|:------------:|
| Sync during subscription states | §1.E (SUSPENDED) | RA-005 §5.2 | Aligns |
| Multi-tenant isolation during migration | §3.1 (lock strategy) | RA-002 §1 (ISO-01..06) | Aligns: migration locks are tenant-scoped |
| Vendor outage fallback | §8.1 (Channex outage) | RA-002 §7.4 | Extends: models outage specifically during migration |
| Retry/backoff policies | §7.5 | RA-002 §7.7 | Aligns: same backoff intervals |
| Operational playbooks | §8 (failure modeling) | RA-002 §9 (incident #3, #4) | Extends: 7 migration-specific failure scenarios |
| Commission snapshot immutability | §6.2 | RA-001 §2 (business rules) | Aligns: commission snapshot is immutable |
| Payment mode snapshot immutability | §6.2 | RA-001 §4, RA-005 §5.2 | Aligns: payment mode captured at booking time |
| DB-based locking | §3.1 (SyncLock) | RA-IC-001 §5.3 (SCH-06..08) | Extends: same SyncLock table, adds migration-specific lock semantics |
| Feature flags | §11 (DoD) | RA-IC-001 §10 (FF-01..03) | Aligns: migration features gated by existing `SYNC_SWITCH_WIZARD_ENABLED` flag |
