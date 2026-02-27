# RA-IC-001: Hybrid Sync (iCal + Channex) Requirements Addendum

**Addendum to:** [RA-001](RA-001-marketplace-commission-boost-ota-payments.md) | [RA-002](RA-002-governance-scale-monetization-control.md) | [RA-006](RA-006-operational-excellence-admin-support.md)

**Purpose:** Define the complete hybrid channel-sync model — iCal polling/export for Airbnb-only tenants and Channex API sync for multi-OTA tenants — covering product modes, functional requirements, architecture abstraction, scheduling at scale, observability, security, onboarding SOPs, acceptance criteria, and phased rollout.

**Audience:** Developer, QA, Support, Platform Ops

**Owner:** Atlas Tech Solutions

**Last updated:** 2026-02-27

**Status:** Draft

---

## Table of contents

1. [Product Modes & Eligibility Rules](#1-product-modes--eligibility-rules)
2. [Functional Requirements — iCal Sync](#2-functional-requirements--ical-sync)
3. [Functional Requirements — Channex Sync](#3-functional-requirements--channex-sync)
4. [Unified "Channel Ingestion" Architecture Requirements](#4-unified-channel-ingestion-architecture-requirements)
5. [Scheduling & Scaling Requirements (100k Tenants)](#5-scheduling--scaling-requirements-100k-tenants)
6. [Observability & Admin Tooling Requirements](#6-observability--admin-tooling-requirements)
7. [Security Requirements](#7-security-requirements)
8. [Onboarding SOP (Factory Process)](#8-onboarding-sop-factory-process)
9. [Acceptance Criteria & Test Matrix](#9-acceptance-criteria--test-matrix)
10. [Rollout Plan (Feature Flags)](#10-rollout-plan-feature-flags)

---

## 1. Product modes & eligibility rules

### 1.1 Sync mode model

Every tenant has a **sync mode** that determines how calendar/booking data flows between Atlas and OTAs.

| Sync mode | Code value | Description | Plan availability |
|-----------|:----------:|-------------|:-----------------:|
| **NONE** | `none` | Manual bookings only. No OTA calendar integration. | All plans |
| **ICAL_BASIC** | `ical_basic` | iCal polling (import) + iCal feed export. Airbnb-only. Near-real-time. | Basic, Premium |
| **CHANNEX_API** | `channex_api` | Full Channex API sync — Airbnb OAuth, Booking.com mapping, rate push, availability push, webhook receive (v2). | Premium only |

### 1.2 Eligibility rules

| Rule ID | Rule | Detail |
|:-------:|------|--------|
| ELG-01 | Default sync mode by plan | Basic plan → `ICAL_BASIC`. Premium plan → `CHANNEX_API`. Both allow explicit `NONE`. |
| ELG-02 | Airbnb-only tenants may use iCal | Tenants listing only on Airbnb are eligible for `ICAL_BASIC` regardless of plan. |
| ELG-03 | Multi-OTA tenants require Channex | Tenants listing on Airbnb **and** Booking.com (or other OTAs) MUST use `CHANNEX_API`. `ICAL_BASIC` does not support Booking.com rate/availability push. |
| ELG-04 | Premium plan unlocks Channex | `CHANNEX_API` is available only to tenants on the Premium plan. Attempting to set it on Basic returns 403 with upgrade prompt. |
| ELG-05 | Downgrade path | If a Premium tenant downgrades to Basic while on `CHANNEX_API`, system pauses Channex sync and offers switch to `ICAL_BASIC`. Channex configs preserved for 90 days. |

### 1.3 Tenant-level selection vs property-level override

**V1 decision: tenant-level sync mode with per-property override.**

| Level | Field | Behaviour |
|-------|-------|-----------|
| **Tenant** | `Tenant.SyncMode` (`varchar(20)`, default `none`) | Sets the default sync mode for all properties owned by this tenant. |
| **Property** | `Property.SyncModeOverride` (`varchar(20)`, nullable) | If non-null, overrides `Tenant.SyncMode` for this specific property. Allows a Premium tenant to use iCal for one property and Channex for another. |

**Effective sync mode resolution:**

```
EffectiveSyncMode(property) =
    property.SyncModeOverride ?? property.Tenant.SyncMode
```

| ID | Requirement |
|----|-------------|
| MOD-01 | `Tenant.SyncMode` MUST be one of `none`, `ical_basic`, `channex_api`. Default: `none`. |
| MOD-02 | `Property.SyncModeOverride` MUST be nullable. When null, inherits from tenant. When set, MUST be one of the three values. |
| MOD-03 | Setting `SyncModeOverride = channex_api` on a property MUST be rejected if tenant is not on Premium plan. |
| MOD-04 | Changing `Tenant.SyncMode` MUST NOT retroactively change any property-level overrides. |
| MOD-05 | `SyncMode` changes MUST be logged to AuditLog: `tenant.syncmode.changed` / `property.syncmode.changed` with `{before, after}`. |

### 1.4 Contractual disclaimer wording requirements

Tenants MUST acknowledge the following before activating any sync mode:

| Sync mode | Disclaimer text (shown in UI + confirmation modal) |
|-----------|---------------------------------------------------|
| `ICAL_BASIC` | "iCal sync is **near real-time, not guaranteed real-time**. Calendar updates are polled every 5–15 minutes. During polling gaps, double-bookings are possible. Atlas provides conflict detection and alerts but cannot prevent all overbookings." |
| `CHANNEX_API` | "Channex API sync provides **faster synchronization** than iCal, but remains subject to OTA processing latency. Booking confirmations depend on Airbnb/Booking.com webhook delivery times. Atlas monitors sync health and alerts on delays, but cannot guarantee zero-latency updates." |

| ID | Requirement |
|----|-------------|
| DIS-01 | Disclaimer MUST be displayed in the sync mode selection wizard and require explicit acknowledgement (checkbox) before activation. |
| DIS-02 | Acknowledgement timestamp and text version MUST be stored: `TenantSyncDisclaimer { TenantId, SyncMode, AcknowledgedAtUtc, DisclaimerVersion }`. |
| DIS-03 | If disclaimer text changes, tenants MUST re-acknowledge on next admin portal login. |

---

## 2. Functional requirements — iCal sync

### 2.A Inbound iCal (import)

#### 2.A.1 Polling schedule strategy

| Parameter | V1 value | Rationale |
|-----------|:--------:|-----------|
| Target poll interval | 5 minutes | Balance between freshness and cost. Upgrade from current 15-min. |
| Stagger strategy | Hash-based: `nextPollUtc = lastPoll + interval + (calendarId % 60)s` | Prevents thundering herd. Distributes polls across the interval window. |
| Backoff if unchanged | If iCal feed body hash matches previous fetch, double interval up to 30 min. Reset on first change. | Reduces load for stable calendars. |
| Minimum interval floor | 2 minutes | Even with manual "sync now", enforce cooldown to prevent abuse. |
| Maximum interval cap | 30 minutes | Even idle calendars re-check every 30 min. |

**Existing implementation reference:** `ICalSyncHostedService` polls every 15 min with no stagger (RA-002 §5.1). This addendum supersedes the interval to 5 min with adaptive backoff.

#### 2.A.2 Change detection: hash / ETag / If-Modified-Since

| Method | Priority | Behaviour |
|--------|:--------:|-----------|
| **Content hash** (SHA-256 of response body) | Primary | Stored as `ListingExternalCalendar.LastContentHash` (`varchar(64)`). If hash matches previous: skip parsing, increment backoff. |
| **ETag** | Secondary | If response includes `ETag` header, store in `ListingExternalCalendar.LastETag`. Send `If-None-Match` on next request. 304 → skip. |
| **If-Modified-Since** | Tertiary | If response includes `Last-Modified`, store as `LastModifiedHeader`. Send `If-Modified-Since`. 304 → skip. |

| ID | Requirement |
|----|-------------|
| ICI-01 | Poller MUST send `If-None-Match` and `If-Modified-Since` headers when stored values exist. |
| ICI-02 | On HTTP 304 response, poller MUST skip parsing, update `LastSyncAtUtc`, clear `LastSyncError`, and apply backoff. |
| ICI-03 | On HTTP 200, poller MUST compute SHA-256 of response body and compare to `LastContentHash`. If identical: skip parsing and apply backoff. |

#### 2.A.3 Parsing rules

| Field | Parsing rule | Fallback |
|-------|-------------|----------|
| `UID` | Extract `UID:` property from each `VEVENT`. Treat as external event identifier. | If missing, generate deterministic UID: `sha256(calendarId + DTSTART + DTEND + SUMMARY)[0:32]`. |
| `DTSTART` | Parse `VALUE=DATE` (all-day, `yyyyMMdd`) and `DATETIME` (`yyyyMMddTHHmmssZ`). | If unparseable: skip event, log warning. |
| `DTEND` | Same parsing as DTSTART. | If missing: `DTEND = DTSTART + 1 day` (all-day convention). |
| `SUMMARY` | Extract for display; sanitize (strip PII patterns: email, phone). | Default: `"Reserved"`. |
| `STATUS` | Map `CONFIRMED` → active block, `TENTATIVE` → active block, `CANCELLED` → skip/remove. | If missing: treat as `CONFIRMED`. |
| Timezone (`TZID`) | If `DTSTART;TZID=X:` present, convert to UTC using IANA tz database. | If no timezone and no `Z` suffix: assume UTC. |

**Existing implementation reference:** `ICalSyncHelper.ParseVEvents()` handles `DTSTART`, `DTEND`, `SUMMARY` with `VALUE=DATE` and basic datetime. This addendum adds UID tracking, timezone handling, and STATUS support.

#### 2.A.4 Idempotency rules (prevent duplicates)

| ID | Requirement |
|----|-------------|
| ICI-04 | Each imported event MUST be uniquely identified by the tuple `(CalendarId, ExternalUID)`. |
| ICI-05 | On each sync cycle, the system MUST perform **full-replace** for that calendar's events: delete all existing `AvailabilityBlock` rows where `Source = 'iCal'` AND `BlockType = calendarId`, then re-insert from parsed feed. This is the current strategy and remains correct for v1. |
| ICI-06 | **V2 enhancement:** switch to **diff-based upsert** keyed on `ExternalUID` — insert new, update changed (date shift), delete removed. Reduces write amplification. |
| ICI-07 | Two different external calendars for the same listing MUST NOT produce duplicate blocks. Each calendar's blocks are scoped by `BlockType = calendarId`. |

#### 2.A.5 "Block vs Booking" representation in Atlas

| iCal event origin | Atlas representation | Entity | Notes |
|-------------------|---------------------|--------|-------|
| OTA booking (Airbnb "Reserved") | `AvailabilityBlock` with `Source = 'iCal'`, `Status = 'Blocked'`, `Inventory = false` | `AvailabilityBlock` | NOT a Booking record — iCal has no booking details (no guest, no amount). |
| OTA manual block (host blocked on Airbnb) | Same as above | `AvailabilityBlock` | Indistinguishable from booking in iCal feed. |
| Atlas booking | `Booking` record (with separate `AvailabilityBlock` via booking flow) | `Booking` + `AvailabilityBlock` | Full booking details. |

| ID | Requirement |
|----|-------------|
| ICI-08 | iCal-imported events MUST be stored as `AvailabilityBlock`, never as `Booking`. iCal provides no structured booking data. |
| ICI-09 | Availability checks MUST query both `Booking` and `AvailabilityBlock` tables to detect conflicts. |
| ICI-10 | Admin UI MUST visually distinguish iCal-sourced blocks (grey, "External Block") from Atlas bookings (blue, "Confirmed Booking"). |

### 2.B Outbound iCal (export)

#### 2.B.1 Feed generation

**Existing implementation:** `ICalController.ExportIcal` at `GET /listings/{listingId}/ical` — already generates VCALENDAR with bookings and manual blocks.

| ID | Requirement |
|----|-------------|
| ICE-01 | Export feed MUST include all confirmed bookings (`BookingStatus != 'Cancelled'` and `!= 'Expired'`) and all active manual blocks for the listing. Already implemented. |
| ICE-02 | Export feed MUST use privacy-safe event titles. Default: `SUMMARY:Reserved`. MUST NOT include guest full name, phone, or email in the SUMMARY or DESCRIPTION. |
| ICE-03 | Each VEVENT MUST have a stable `UID` of format `booking-{id}@atlashomestays.com` or `block-{id}@atlashomestays.com`. Already implemented. |
| ICE-04 | Feed MUST include `DTSTART;VALUE=DATE` and `DTEND;VALUE=DATE` for all-day events (check-in/check-out are dates, not timestamps). Already implemented. |
| ICE-05 | Feed MUST include `PRODID:-//Atlas Homestays//Atlas PMS//EN` and `VERSION:2.0`. Already implemented. |

#### 2.B.2 Cache headers & rate limiting

| Header | Value | Purpose |
|--------|-------|---------|
| `Cache-Control` | `public, max-age=300` | Allow OTAs and CDN to cache for 5 minutes. |
| `ETag` | SHA-256 of response body (first 16 hex chars) | Enable conditional requests. |
| `Last-Modified` | Max of (`LastBookingUpdatedAt`, `LastBlockUpdatedAt`) for the listing | Standard HTTP caching. |

| ID | Requirement |
|----|-------------|
| ICE-06 | Export endpoint MUST return `Cache-Control`, `ETag`, and `Last-Modified` headers. |
| ICE-07 | Export endpoint MUST support `If-None-Match` and `If-Modified-Since` — return 304 when feed unchanged. |
| ICE-08 | Export endpoint MUST be rate-limited: 60 requests/minute per IP, 120 requests/minute per listing. Return 429 with `Retry-After: 60`. |

#### 2.B.3 Feed URL rotation / regeneration

| Scenario | Action |
|----------|--------|
| Feed URL leaked or abused | Tenant can regenerate feed URL via admin portal. Old URL returns 404 after regeneration. |
| URL structure | `/listings/{listingId}/ical?token={feedToken}` — `feedToken` is a 32-char random hex stored on `Listing.ICalFeedToken`. |
| Regeneration | `POST /api/listings/{listingId}/regenerate-ical-token` → generates new random token, invalidates old. |

| ID | Requirement |
|----|-------------|
| ICE-09 | `Listing.ICalFeedToken` (`varchar(64)`, nullable). If non-null, export endpoint MUST validate `?token=` query parameter. If null (v1 default), endpoint is open by listingId. |
| ICE-10 | Regeneration MUST log `listing.ical_token.regenerated` to AuditLog. |
| ICE-11 | Admin portal MUST display the current feed URL and a "Regenerate URL" button with confirmation dialog. |

### 2.C Conflict detection

#### 2.C.1 Overlapping reservation detection

A **conflict** exists when:

```
(existingBlock.StartDate < newEvent.EndDate) AND (existingBlock.EndDate > newEvent.StartDate)
    AND existingBlock.ListingId == newEvent.ListingId
    AND existingBlock is from a DIFFERENT source (e.g., iCal block overlaps Atlas booking, or two different iCal calendars overlap)
```

Same-source overlaps (two events from the same iCal calendar) are NOT conflicts — the OTA's own calendar is self-consistent.

| ID | Requirement |
|----|-------------|
| CON-01 | After each iCal sync cycle, system MUST run conflict detection for the synced listing. |
| CON-02 | After each Atlas booking creation, system MUST check for conflicts against existing iCal blocks. |
| CON-03 | Conflict detection MUST ignore same-calendar overlaps. Only cross-source overlaps are conflicts. |

#### 2.C.2 Conflict states

| State | Code | Description | Transition |
|-------|:----:|-------------|-----------|
| **DETECTED** | `detected` | System found an overlap. Tenant not yet notified or has not acted. | → `acknowledged` (tenant views) or → `resolved` (auto-resolved: one side cancelled). |
| **ACKNOWLEDGED** | `acknowledged` | Tenant has seen the conflict alert. | → `resolved` (tenant resolves manually). |
| **RESOLVED** | `resolved` | Conflict resolved — one booking cancelled, dates changed, or overlap accepted with override. | Terminal state. |

**Data model:**

```
SyncConflict {
    Id              int PK
    TenantId        int FK → Tenant
    ListingId       int FK → Listing
    BlockAId        int FK → AvailabilityBlock (or BookingId)
    BlockASource    varchar(50)     -- 'iCal', 'Atlas', 'Channex'
    BlockBId        int FK → AvailabilityBlock (or BookingId)
    BlockBSource    varchar(50)
    OverlapStart    date
    OverlapEnd      date
    Status          varchar(20)     -- 'detected', 'acknowledged', 'resolved'
    ResolutionNote  varchar(500)    -- nullable, free-text from tenant
    DetectedAtUtc   datetime2
    ResolvedAtUtc   datetime2       -- nullable
}
```

| ID | Requirement |
|----|-------------|
| CON-04 | `SyncConflict` rows MUST be created with `Status = 'detected'` and MUST be idempotent — do not create a duplicate if the same two blocks already have an unresolved conflict. |
| CON-05 | When a conflict is detected, system MUST send alert to tenant via preferred channel (WhatsApp if configured, else email). |
| CON-06 | Admin dashboard (`/platform/sync-conflicts`) MUST show all unresolved conflicts across all tenants with filters by severity, tenant, and age. |

#### 2.C.3 Alert content

| Channel | Content |
|---------|---------|
| WhatsApp | "⚠️ Booking conflict detected for {PropertyName} — {ListingName}. Dates {OverlapStart} to {OverlapEnd} are booked on both Atlas and {OtherSource}. Please resolve immediately in your admin portal." |
| Email | Subject: "Action Required: Booking Conflict — {PropertyName}". Body: Same as WhatsApp + link to admin portal conflict resolution page. |
| Admin dashboard | Row in conflict table with "Resolve" button. |

### 2.D Manual resolution UX

#### 2.D.1 UI actions

| Action | Description | API |
|--------|-------------|-----|
| **Cancel Atlas booking** | Tenant cancels the Atlas-side booking. Refund triggered per existing cancellation policy. | Existing `PUT /api/bookings/{id}/cancel` |
| **Keep Atlas, ignore external** | Tenant acknowledges the external block and will handle it on the OTA side (cancel on Airbnb manually). | `PUT /api/sync-conflicts/{id}/resolve` with `{ resolution: 'keep_atlas' }` |
| **Force re-sync** | Tenant triggers immediate iCal sync for this listing to pull latest state. | Existing `POST /api/external-calendars/{id}/sync` |
| **Add resolution note** | Free-text note explaining what was done. | `PUT /api/sync-conflicts/{id}/resolve` with `{ resolution: '...', note: '...' }` |

| ID | Requirement |
|----|-------------|
| RES-01 | Admin portal conflict list (`/channels/conflicts`) MUST show all unresolved conflicts for the tenant. |
| RES-02 | Each conflict row MUST display: listing name, overlap dates, both sources, detection time, and action buttons. |
| RES-03 | Resolving a conflict MUST set `Status = 'resolved'`, `ResolvedAtUtc = utcnow`, and log `sync.conflict.resolved` with `{conflictId, resolution}` to AuditLog. |
| RES-04 | After manual changes (cancel, date shift), tenant SHOULD trigger re-sync. UI MUST show a "Re-sync now" prompt after resolution. |

---

## 3. Functional requirements — Channex sync

### 3.1 Tenant-level Channex connection lifecycle

| State | Description | Transitions |
|-------|-------------|-------------|
| **DISCONNECTED** | No Channex config. Default state. | → `CONNECTING` (tenant initiates) |
| **CONNECTING** | Tenant has entered API key but test not yet passed. | → `CONNECTED` (test passes) or → `DISCONNECTED` (tenant aborts) |
| **CONNECTED** | Test passed. Sync active. | → `ERROR` (consecutive failures) or → `DISCONNECTED` (tenant disconnects) |
| **ERROR** | 3+ consecutive sync failures. Sync paused. | → `CONNECTED` (manual retry succeeds) or → `DISCONNECTED` (tenant disconnects) |
| **PAUSED** | Tenant subscription suspended (RA-005 §5.2). Configs preserved. | → `CONNECTED` (subscription reactivated) |

| ID | Requirement |
|----|-------------|
| CHX-01 | `ChannelConfig.ConnectionStatus` (`varchar(20)`) MUST reflect the above states. Replaces boolean `IsConnected`. |
| CHX-02 | Transition to `ERROR` MUST trigger alert to tenant and platform admin. |
| CHX-03 | Transition to `PAUSED` MUST happen automatically when `TenantSubscription.Status` becomes `Suspended`. |

### 3.2 Airbnb OAuth connection flow

**Already specified in RA-001 Journey B.** Summary for cross-reference:

| Step | Action | Owner |
|:----:|--------|:-----:|
| 1 | Tenant clicks "Connect Airbnb" in admin portal | Tenant |
| 2 | Redirect to Channex Airbnb OAuth page (`https://app.channex.io/connect/airbnb?group_id={groupId}`) | System |
| 3 | Tenant authorizes on Airbnb via Channex | Tenant |
| 4 | Tenant copies API key + External Property ID back to Atlas | Tenant |
| 5 | Atlas tests connection via `IChannelManagerProvider.TestConnectionAsync` | System |
| 6 | On success: `ChannelConfig` created/updated, `ConnectionStatus = 'connected'` | System |

| ID | Requirement |
|----|-------------|
| CHX-04 | V1: manual copy-paste of API key. V2: Channex callback/redirect to auto-populate. |
| CHX-05 | API key MUST be validated via `TestConnectionAsync` before marking connected. |

### 3.3 Booking.com Hotel ID mapping flow

**Already specified in RA-001 Journey C.** Summary:

| Step | Action | Owner |
|:----:|--------|:-----:|
| 1 | Tenant clicks "Connect Booking.com" | Tenant |
| 2 | Modal: enter Booking.com Hotel ID (numeric) + Channex API key | Tenant |
| 3 | Atlas creates `ChannelConfig` with `Provider = 'channex'`, hotel ID as `ExternalPropertyId` | System |
| 4 | Test connection | System |
| 5 | On success: connected | System |

| ID | Requirement |
|----|-------------|
| CHX-06 | Booking.com Hotel ID MUST be validated as numeric string on submission. |
| CHX-07 | A single Channex API key may be shared across Airbnb and Booking.com for the same property (Channex groups both under one property group). System MUST support multiple `ChannelConfig` rows per property if needed. |

### 3.4 Webhook processing requirements (V2, specified for design)

| Webhook event | Channex event type | Atlas action |
|---------------|-------------------|-------------|
| New booking | `booking_new` | Create `Booking` record (source: `channex`). Create `AvailabilityBlock`. Send confirmation to tenant. |
| Booking modification | `booking_modified` | Update `Booking` dates/amount. Update `AvailabilityBlock`. Notify tenant. |
| Booking cancellation | `booking_cancelled` | Cancel `Booking`. Remove `AvailabilityBlock`. Notify tenant. Process refund if applicable. |
| Availability changed | `availability_changed` | Update local availability cache. |

| ID | Requirement |
|----|-------------|
| CHX-08 | V2 webhook endpoint: `POST /api/webhooks/channex`. Must return 200 within 5 seconds. |
| CHX-09 | Webhook payload MUST be validated (signature if Channex supports, else IP whitelist + payload schema validation). |
| CHX-10 | Webhook processing MUST be idempotent: check `ConsumedWebhook { WebhookId, ReceivedAtUtc }` before processing. Duplicate → 200 with no action. |
| CHX-11 | Heavy processing (booking creation, notifications) MUST go to DB-backed outbox. Webhook handler only writes outbox row + returns 200. |

### 3.5 Rate sync + availability sync behaviour

**Existing implementation:** `ChannelConfigController` exposes `sync-rates` and `sync-availability` endpoints that push to Channex via `IChannelManagerProvider`.

| Sync direction | Trigger | Data |
|----------------|---------|------|
| Atlas → Channex: rates | Tenant updates pricing in Atlas → outbox row → worker pushes to Channex | `RateUpdate { RoomTypeId, RatePlanId, DateFrom, DateTo, Rate }` |
| Atlas → Channex: availability | Booking created/cancelled in Atlas → outbox row → worker pushes to Channex | `AvailabilityUpdate { RoomTypeId, DateFrom, DateTo, Available }` |
| Channex → Atlas: bookings | V2: webhook. V1: N/A (manual entry). | Booking details from Channex payload. |

| ID | Requirement |
|----|-------------|
| CHX-12 | Rate push MUST happen within 2 minutes of a pricing change in Atlas (outbox worker cycle). |
| CHX-13 | Availability push MUST happen within 2 minutes of a booking creation/cancellation in Atlas. |
| CHX-14 | Push failures MUST be retried with exponential backoff (30s, 60s, 120s; max 3 retries). Aligns with RA-002 §7.7. |
| CHX-15 | If all retries fail, `ChannelConfig.LastSyncError` MUST be set and alert triggered. |

### 3.6 Backfill and reconciliation rules

| Scenario | Behaviour |
|----------|-----------|
| **Initial connection** | After first successful `TestConnectionAsync`, system MUST trigger a full availability push for all dates in the next 365 days. |
| **Reconnection after error** | Same as initial: full push. |
| **Daily reconciliation** | Background job (daily at 03:00 UTC) compares Atlas availability for next 90 days against last pushed state. Pushes any drift. |
| **Manual reconciliation** | Admin can trigger `POST /api/channel-configs/{propertyId}/reconcile` — full push for next 365 days. |

| ID | Requirement |
|----|-------------|
| CHX-16 | Initial connection MUST trigger full availability backfill automatically. |
| CHX-17 | Daily reconciliation MUST detect and correct drift silently. Log `channex.reconciliation.completed` with `{propertiesChecked, driftDetected, corrected}`. |
| CHX-18 | Reconciliation MUST respect Channex rate limits (RA-001 §5.A: 60 req/min per API key). |

### 3.7 Feature parity matrix

| Capability | ICAL_BASIC | CHANNEX_API | Notes |
|-----------|:----------:|:-----------:|-------|
| Calendar block sync (inbound) | ✅ | ✅ (v2 via webhook) | iCal: blocks only. Channex: full booking details. |
| Calendar export (outbound) | ✅ | ✅ (via Channex push) | iCal: OTA polls feed. Channex: Atlas pushes. |
| Rate sync to OTA | ❌ | ✅ | iCal has no rate concept. |
| Availability sync to OTA | ❌ (export only) | ✅ (push) | iCal: passive. Channex: active push. |
| Restriction sync (min stay, closed dates) | ❌ | ✅ (v2) | Channex supports restrictions API. |
| Booking details (guest name, amount) | ❌ | ✅ (v2 webhook) | iCal provides no booking metadata. |
| Multi-OTA support | ❌ (Airbnb only) | ✅ (Airbnb + Booking.com + others) | |
| Conflict detection | ✅ | ✅ | Both modes detect overlaps. |
| Real-time-ness | Near real-time (5-15 min lag) | Near real-time (< 2 min for push, webhook latency for receive) | |

---

## 4. Unified "channel ingestion" architecture requirements

### 4.1 Provider-agnostic abstraction

**Existing:** `IChannelManagerProvider` (RA-002 §7.1) covers push operations. This addendum extends the abstraction to include pull and webhook receive.

```
interface IChannelSyncProvider
{
    string ProviderName { get; }

    // Pull: fetch reservations / blocks from external source
    Task<PullResult> PullReservationsAsync(SyncContext ctx, CancellationToken ct);

    // Webhook: process inbound webhook payload
    Task<WebhookResult> ReceiveWebhookAsync(string payload, IDictionary<string, string> headers, CancellationToken ct);

    // Push: send availability to external channel
    Task<ChannelSyncResult> PushAvailabilityAsync(string apiKey, string externalPropertyId, List<AvailabilityUpdate> availability, CancellationToken ct);

    // Push: send rates to external channel (optional — not all providers support)
    Task<ChannelSyncResult> PushRatesAsync(string apiKey, string externalPropertyId, List<RateUpdate> rates, CancellationToken ct);
}
```

**Provider implementations:**

| Provider | `PullReservationsAsync` | `ReceiveWebhookAsync` | `PushAvailabilityAsync` | `PushRatesAsync` |
|----------|:-----------------------:|:---------------------:|:-----------------------:|:----------------:|
| `ICalProvider` | Polls iCal URL, parses VEVENT, returns blocks | N/A (throws `NotSupportedException`) | N/A (export is a separate read endpoint, not a push) | N/A |
| `ChannexProvider` | V2: calls Channex bookings API for backfill | V2: processes webhook payload | Delegates to existing `ChannexAdapter` | Delegates to existing `ChannexAdapter` |

| ID | Requirement |
|----|-------------|
| ARC-01 | `IChannelSyncProvider` MUST be the single abstraction for all sync operations. Existing `IChannelManagerProvider` is subsumed (push methods move here; old interface deprecated). |
| ARC-02 | Provider resolution: `IChannelSyncProviderFactory.GetProvider(syncMode)` returns the correct implementation based on effective sync mode. |
| ARC-03 | `ICalProvider` MUST implement `PullReservationsAsync` by fetching the iCal URL and parsing events. `PushRatesAsync` and `PushAvailabilityAsync` MUST throw `NotSupportedException` (iCal is pull-only, export is passive). |
| ARC-04 | Adding a new provider (e.g., Hostaway) MUST require only: (a) implement `IChannelSyncProvider`, (b) register in DI, (c) add sync mode enum value. No changes to sync worker or scheduler. |

### 4.2 Data model normalization

All provider-specific reservation data is normalized to a common schema before storage:

```
NormalizedReservation {
    ExternalId          string      -- OTA-specific ID (Airbnb confirmation code, Booking.com ID, iCal UID)
    Source              string      -- 'airbnb', 'bookingcom', 'ical', 'atlas'
    Provider            string      -- 'ical_basic', 'channex_api'
    ListingId           int
    CheckIn             date
    CheckOut            date
    GuestName           string?     -- null for iCal
    GuestEmail          string?     -- null for iCal
    GuestPhone          string?     -- null for iCal
    TotalAmount         decimal?    -- null for iCal
    Currency            string?     -- null for iCal
    Status              string      -- 'confirmed', 'cancelled', 'modified'
    RawPayload          string?     -- original iCal event text or Channex JSON (for debugging)
}
```

| ID | Requirement |
|----|-------------|
| ARC-05 | `PullReservationsAsync` MUST return `List<NormalizedReservation>`. Each provider maps its native format to this schema. |
| ARC-06 | For `ICAL_BASIC`: `GuestName`, `GuestEmail`, `GuestPhone`, `TotalAmount` are always null. `ExternalId` = iCal UID. `Source` = `'ical'`. |
| ARC-07 | For `CHANNEX_API`: all fields populated from Channex booking payload. `Source` = OTA name (e.g., `'airbnb'`, `'bookingcom'`). |
| ARC-08 | `RawPayload` MUST be stored for 30 days for debugging/support. Truncated to 10 KB max. |

### 4.3 Provider switching rules

#### Switching from ICAL_BASIC → CHANNEX_API

| Step | Action | System behaviour |
|:----:|--------|-----------------|
| 1 | Tenant upgrades to Premium and selects `CHANNEX_API` | `SyncMode` updated. Disclaimer acknowledged. |
| 2 | **Freeze window begins** | iCal polling continues (read-only). No new iCal-sourced blocks created. Existing blocks preserved. |
| 3 | Tenant completes Channex connection (API key, test) | `ChannelConfig` created. |
| 4 | System performs Channex initial backfill (push current Atlas availability) | Full availability + rate push for next 365 days. |
| 5 | **Cutoff timestamp recorded** | `Property.SyncSwitchCutoffUtc = utcnow`. All iCal blocks with `CreatedAtUtc < cutoff` are marked `Source = 'iCal_legacy'`. |
| 6 | iCal polling disabled for this listing | `ListingExternalCalendar.IsActive = false` for calendars on this property. |
| 7 | Freeze window ends. Channex is now sole sync provider. | New bookings flow through Channex (v2) or are manually entered (v1). |

#### Switching from CHANNEX_API → ICAL_BASIC

| Step | Action | System behaviour |
|:----:|--------|-----------------|
| 1 | Tenant downgrades or selects `ICAL_BASIC` | `SyncMode` updated. |
| 2 | Channex push stops immediately | Worker skips properties with `SyncMode != channex_api`. |
| 3 | Tenant sets up iCal import URLs for each listing | `ListingExternalCalendar` rows created. |
| 4 | First iCal sync runs | Blocks imported. |
| 5 | Channex configs preserved (not deleted) for 90 days | `ChannelConfig.ConnectionStatus = 'paused'`. Allows re-upgrade without re-setup. |

| ID | Requirement |
|----|-------------|
| SWT-01 | Provider switch MUST use a freeze window (steps above) to prevent duplicate blocks during transition. |
| SWT-02 | `Property.SyncSwitchCutoffUtc` (`datetime2`, nullable) MUST be set during iCal→Channex switch. Blocks older than cutoff from iCal source are legacy and excluded from conflict detection. |
| SWT-03 | Switch MUST be logged: `property.sync.provider_switched` with `{from, to, cutoffUtc}`. |
| SWT-04 | Admin portal MUST show a "Switch Sync Mode" wizard that guides tenant through the steps and prevents premature cutover (Channex must test-pass before iCal is disabled). |
| SWT-05 | Rollback: if Channex connection fails within 24 hours of switch, system MUST allow one-click revert to `ICAL_BASIC` with iCal calendars re-enabled. |

### 4.4 Sequence diagrams

#### 4.4.1 iCal import job cycle

```
┌──────────┐     ┌──────────────┐     ┌─────────────┐     ┌─────────┐
│  Timer   │     │ SyncWorker   │     │ ICalProvider │     │ Airbnb  │
│ (5 min)  │     │              │     │              │     │ iCal URL│
└────┬─────┘     └──────┬───────┘     └──────┬───────┘     └────┬────┘
     │  tick              │                    │                  │
     ├───────────────────►│                    │                  │
     │                    │  getNextBatch()    │                  │
     │                    ├───────────────────►│                  │
     │                    │                    │  HTTP GET        │
     │                    │                    │  If-None-Match   │
     │                    │                    ├─────────────────►│
     │                    │                    │  200 + .ics body │
     │                    │                    │◄─────────────────┤
     │                    │                    │                  │
     │                    │                    │  SHA-256 hash    │
     │                    │                    │  compare w/ last │
     │                    │                    │                  │
     │                    │                    │  parse VEVENTs   │
     │                    │                    │  normalize to    │
     │                    │                    │  NormalizedRes[] │
     │                    │  PullResult        │                  │
     │                    │◄───────────────────┤                  │
     │                    │                    │                  │
     │                    │  upsert blocks     │                  │
     │                    │  (full replace)    │                  │
     │                    │                    │                  │
     │                    │  conflict detect   │                  │
     │                    │                    │                  │
     │                    │  update LastSync   │                  │
     │                    │  + ContentHash     │                  │
```

#### 4.4.2 iCal export feed consumption by OTA

```
┌─────────┐     ┌───────────────┐     ┌─────────────┐
│ Airbnb  │     │ Atlas API     │     │ Database    │
│ Poller  │     │ ICalController│     │             │
└────┬────┘     └──────┬────────┘     └──────┬──────┘
     │  GET /listings/  │                     │
     │  {id}/ical?token │                     │
     ├─────────────────►│                     │
     │                  │  validate token     │
     │                  │                     │
     │                  │  query bookings     │
     │                  │  + blocks           │
     │                  ├────────────────────►│
     │                  │  results            │
     │                  │◄────────────────────┤
     │                  │                     │
     │                  │  build VCALENDAR    │
     │                  │  set Cache-Control  │
     │                  │  set ETag           │
     │  200 text/cal    │                     │
     │◄─────────────────┤                     │
     │                  │                     │
     │  (5 min later)   │                     │
     │  GET ...         │                     │
     │  If-None-Match   │                     │
     ├─────────────────►│                     │
     │  304 Not Modified│                     │
     │◄─────────────────┤                     │
```

#### 4.4.3 Channex webhook → booking creation (V2)

```
┌──────────┐     ┌──────────────┐     ┌──────────┐     ┌───────────┐
│ Channex  │     │ Atlas API    │     │ Outbox   │     │ Worker    │
│          │     │ Webhook EP   │     │ (DB)     │     │           │
└────┬─────┘     └──────┬───────┘     └────┬─────┘     └─────┬─────┘
     │  POST /webhooks/  │                  │                  │
     │  channex          │                  │                  │
     ├──────────────────►│                  │                  │
     │                   │                  │                  │
     │                   │  validate sig    │                  │
     │                   │  check consumed  │                  │
     │                   │                  │                  │
     │                   │  write outbox    │                  │
     │                   ├─────────────────►│                  │
     │                   │                  │                  │
     │  200 OK           │                  │                  │
     │◄──────────────────┤                  │                  │
     │                   │                  │                  │
     │                   │                  │  poll outbox     │
     │                   │                  │◄─────────────────┤
     │                   │                  │  message         │
     │                   │                  ├─────────────────►│
     │                   │                  │                  │
     │                   │                  │  parse payload   │
     │                   │                  │  normalize       │
     │                   │                  │  create Booking  │
     │                   │                  │  create Block    │
     │                   │                  │  push avail      │
     │                   │                  │  notify tenant   │
     │                   │                  │                  │
     │                   │                  │  mark consumed   │
     │                   │                  │◄─────────────────┤
```

#### 4.4.4 Tenant switching from iCal to Channex safely

```
┌──────────┐     ┌──────────────┐     ┌──────────┐     ┌──────────┐
│ Tenant   │     │ Atlas API    │     │ SyncWork │     │ Channex  │
│ (Admin)  │     │              │     │          │     │          │
└────┬─────┘     └──────┬───────┘     └────┬─────┘     └────┬─────┘
     │                   │                  │                 │
     │  1. Select        │                  │                 │
     │  CHANNEX_API      │                  │                 │
     ├──────────────────►│                  │                 │
     │                   │                  │                 │
     │                   │  set SyncMode    │                 │
     │                   │  freeze iCal     │                 │
     │                   │  (read-only)     │                 │
     │                   │                  │                 │
     │  2. Enter Channex │                  │                 │
     │  API key + test   │                  │                 │
     ├──────────────────►│                  │                 │
     │                   │  TestConnection  │                 │
     │                   ├─────────────────────────────────► │
     │                   │  OK              │                 │
     │                   │◄─────────────────────────────────┤│
     │                   │                  │                 │
     │  3. Confirm       │                  │                 │
     │  switch           │                  │                 │
     ├──────────────────►│                  │                 │
     │                   │                  │                 │
     │                   │  record cutoff   │                 │
     │                   │  disable iCal    │                 │
     │                   │  calendars       │                 │
     │                   │                  │                 │
     │                   │  trigger backfill│                 │
     │                   ├─────────────────►│                 │
     │                   │                  │  full push      │
     │                   │                  ├────────────────►│
     │                   │                  │  OK             │
     │                   │                  │◄────────────────┤
     │                   │                  │                 │
     │  "Switched to     │                  │                 │
     │   Channex"        │                  │                 │
     │◄──────────────────┤                  │                 │
```

---

## 5. Scheduling & scaling requirements (100k tenants)

### 5.1 Polling scheduler strategy

**Supplements RA-002 §5.1.** Updated targets:

| Scale tier | Tenants | Properties (×3) | iCal URLs (×2 listings × 1 URL) | Base interval | Throughput required |
|:----------:|:-------:|:---------------:|:-------------------------------:|:-------------:|:-------------------:|
| Startup | < 1,000 | 3,000 | 6,000 | 5 min | 20/sec |
| Growth | 1,000–10,000 | 30,000 | 60,000 | 5 min (adaptive to 15 min) | 200/sec |
| Scale | 10,000–100,000 | 300,000 | 600,000 | 10 min base (adaptive 5–30 min) | 1,000/sec |

#### Per-tenant cadence

| Factor | Rule |
|--------|------|
| Tenant plan | Premium tenants polled at base interval. Basic at 1.5× base. |
| Last change recency | If feed changed in last hour: poll at base. If unchanged for > 6 hours: 2× base. If unchanged for > 24 hours: 3× base (capped at 30 min). |
| Check-in proximity | If any listing has a check-in within 48 hours: poll at 0.5× base (more frequent). |

#### Staggering to avoid thundering herd

```
nextPollUtc = lastPollUtc
    + baseInterval
    + TimeSpan.FromSeconds(calendarId % baseInterval.TotalSeconds * 0.1)
```

This distributes polls across 10% of the interval window. For a 5-min base, polls spread over 30 seconds.

| ID | Requirement |
|----|-------------|
| SCH-01 | Scheduler MUST use the stagger formula above. No bulk polling at interval boundaries. |
| SCH-02 | Scheduler MUST implement adaptive backoff: multiply interval by backoff factor based on feed staleness. |
| SCH-03 | Scheduler MUST implement check-in proximity boost: halve interval for listings with upcoming check-ins. |

### 5.2 Cost-control strategy

| Technique | Implementation | Savings estimate |
|-----------|---------------|:----------------:|
| Adaptive backoff | Skip parsing when hash unchanged; increase interval. | 40–60% fewer HTTP requests for stable feeds. |
| Conditional requests | `If-None-Match` / `If-Modified-Since` → 304 responses save bandwidth + parse CPU. | 20–30% bandwidth savings. |
| Check-in priority queue | Prioritize calendars with upcoming check-ins; deprioritize far-future-only calendars. | Better freshness where it matters most. |
| Batch HTTP | Use `HttpClient` connection pooling; limit to 20 concurrent fetches per worker. | Efficient resource use. |
| Off-peak slowdown | Between 01:00–05:00 UTC (low booking activity globally): 2× all intervals. | 15–20% fewer overnight requests. |

| ID | Requirement |
|----|-------------|
| SCH-04 | Worker MUST limit concurrent HTTP fetches to a configurable max (default 20). |
| SCH-05 | Worker MUST implement off-peak interval doubling (configurable time window). |

### 5.3 Concurrency model

| Component | Concurrency strategy |
|-----------|---------------------|
| iCal sync worker | Single `BackgroundService` with configurable concurrency (`MaxParallelFetches`, default 20). Uses `SemaphoreSlim`. |
| Channex push worker | Single `BackgroundService` processing outbox rows. Sequential per API key (rate limit compliance). Parallel across different API keys. |
| Per-tenant lock | `SyncLock` table: `{ TenantId, PropertyId, LockedByWorker, LockedAtUtc, ExpiresAtUtc }`. Worker acquires row-level lock before processing. Skip if locked (another worker/cycle in progress). |

| ID | Requirement |
|----|-------------|
| SCH-06 | iCal sync MUST acquire a per-calendar lock before fetching. If locked (previous cycle still running): skip, log warning. |
| SCH-07 | Channex push MUST acquire a per-property lock before pushing. If locked: re-queue in outbox with 60s delay. |
| SCH-08 | Locks MUST have TTL (default 5 minutes). Expired locks are automatically released. Prevents deadlocks from crashed workers. |

### 5.4 SLA targets

| Sync mode | Metric | Target | Measurement |
|-----------|--------|:------:|-------------|
| `ICAL_BASIC` | Time from OTA calendar change to Atlas block update | Best effort; typically < 10 minutes (p90) | `lastSyncAtUtc - feedLastModified` (estimated). |
| `ICAL_BASIC` | Polling success rate | > 99% of scheduled polls complete without error | `successfulPolls / scheduledPolls` per day. |
| `CHANNEX_API` | Time from Atlas change to Channex push | < 2 minutes (p95) | `channexPushAtUtc - changeDetectedAtUtc`. |
| `CHANNEX_API` | Push success rate | > 99.5% of pushes succeed on first or retry attempt | `successfulPushes / totalPushes` per day. |
| Both | Conflict detection latency | < 1 minute after sync completes | Conflict detection runs inline with sync. |
| Both | Alert delivery latency | < 5 minutes from conflict detection to tenant notification | Outbox-based notification delivery. |

| ID | Requirement |
|----|-------------|
| SLA-01 | SLA targets are internal operational goals, not contractual guarantees. Disclaimers (§1.4) govern tenant expectations. |
| SLA-02 | Platform dashboard MUST display actual p50/p90/p95 sync latency for the last 24 hours, 7 days, 30 days. |

---

## 6. Observability & admin tooling requirements

**Supplements RA-006 §4.** This section adds sync-specific observability.

### 6.1 Per-tenant/property sync dashboard

| Metric | Source | Display | Applicable modes |
|--------|--------|---------|:----------------:|
| Last successful sync time | `ListingExternalCalendar.LastSyncAtUtc` / `ChannelConfig.LastSyncAt` | Relative time ("3 min ago") + absolute | Both |
| Last change detected time | `ListingExternalCalendar.LastChangeDetectedAtUtc` (new field) | Relative + absolute | ICAL_BASIC |
| Error count (last 24h) | Structured log aggregation: `ical.sync.failed` / `channex.push.failed` | Count + trend sparkline | Both |
| Next scheduled poll time | Computed: `LastSyncAtUtc + adaptiveInterval` | Absolute time | ICAL_BASIC |
| Webhook last received | `ConsumedWebhook` table: max `ReceivedAtUtc` for property | Relative time | CHANNEX_API (v2) |
| iCal feed last content hash | `ListingExternalCalendar.LastContentHash` | Truncated hash (first 8 chars) + "changed/unchanged" | ICAL_BASIC |
| Consecutive failure count | Counter on `ListingExternalCalendar.ConsecutiveFailures` / `ChannelConfig.ConsecutiveFailures` (new fields) | Number + colour (green ≤ 1, yellow 2–3, red ≥ 4) | Both |
| Sync mode | `Property.SyncModeOverride ?? Tenant.SyncMode` | Badge: "iCal" / "Channex" / "None" | Both |
| Conflict count (unresolved) | `SyncConflict` count where `Status != 'resolved'` | Number + link to conflict list | Both |

### 6.2 Platform admin views

| View | Route | Content |
|------|-------|---------|
| Sync health overview | `/platform/sync-health` | Aggregated: total active syncs, success rate (24h), stale count, error count. Filterable by sync mode, tenant, property. |
| Stale sync list | `/platform/sync-health?stale=true` | All listings where `lastSync > threshold`. Default threshold: 30 min for iCal, 10 min for Channex push. |
| Conflict list | `/platform/sync-conflicts` | All unresolved `SyncConflict` rows. Sortable by detection time. Filterable by tenant. |
| Sync timeline | `/platform/sync-health/{propertyId}/timeline` | Chronological list of sync events (success, failure, change detected, conflict) for one property. Last 7 days. |

### 6.3 Alert rules

| Alert | Condition | Severity | Notification | Runbook |
|-------|-----------|:--------:|:------------:|---------|
| Stale iCal sync | `LastSyncAtUtc > 30 min ago` AND calendar is active | WARNING | Email to platform ops | Check URL accessibility, check worker health. |
| Stale Channex push | `ChannelConfig.LastSyncAt > 30 min ago` AND connected | WARNING | Email to platform ops | Check outbox queue depth, check Channex API status. |
| Repeated iCal failures | `ConsecutiveFailures >= 5` | WARNING (→ CRITICAL at ≥ 10) | Email to platform ops + tenant | Verify iCal URL. If URL returns 4xx: notify tenant to update. If 5xx: OTA-side issue. |
| Repeated Channex failures | `ConsecutiveFailures >= 3` | WARNING (→ CRITICAL at ≥ 5) | Email to platform ops + tenant | Verify API key. Check Channex status page. |
| Parsing error rate spike | > 10% of parsed events have errors in a 1-hour window | WARNING | Email to platform ops | Check for iCal format changes from OTA. Review parse logs. |
| Duplicate UID anomaly | Same `ExternalUID` appears in two different calendars for the same listing | WARNING | Email to platform ops | Likely misconfiguration: tenant linked same OTA calendar twice. Notify tenant. |
| Unresolved conflict > 24h | `SyncConflict.Status = 'detected'` AND `DetectedAtUtc > 24h ago` | CRITICAL | Email to platform ops + escalation to tenant via WhatsApp | Potential overbooking. Contact tenant directly. |
| Sync worker stopped | No `ical.sync.cycle.completed` log event in last 20 min | CRITICAL | Email to platform ops | Check App Service health. Restart if needed. |

### 6.4 Log events

| Event | Structured log fields | Retention |
|-------|----------------------|:---------:|
| `ical.sync.cycle.started` | `{ calendarsToProcess }` | 30 days |
| `ical.sync.calendar.completed` | `{ calendarId, listingId, tenantId, eventsFound, blocksCreated, durationMs, hashChanged }` | 30 days |
| `ical.sync.calendar.failed` | `{ calendarId, listingId, tenantId, error, httpStatus, consecutiveFailures }` | 90 days |
| `ical.sync.calendar.skipped` | `{ calendarId, reason }` — e.g., "hash unchanged", "locked" | 30 days |
| `ical.sync.cycle.completed` | `{ totalCalendars, succeeded, failed, skipped, durationMs }` | 30 days |
| `channex.push.completed` | `{ propertyId, tenantId, type (rate/avail), syncedCount, durationMs }` | 30 days |
| `channex.push.failed` | `{ propertyId, tenantId, type, error, consecutiveFailures }` | 90 days |
| `channex.webhook.received` | `{ webhookId, eventType, propertyId }` | 90 days |
| `channex.webhook.duplicate` | `{ webhookId }` | 30 days |
| `sync.conflict.detected` | `{ conflictId, listingId, tenantId, overlapStart, overlapEnd, sourceA, sourceB }` | 90 days |
| `sync.conflict.resolved` | `{ conflictId, resolution, resolvedBy }` | 90 days |
| `sync.provider.switched` | `{ propertyId, tenantId, from, to, cutoffUtc }` | Permanent |

### 6.5 Debug bundle export

For support escalations, platform admin can export a **sync debug bundle** for a specific property:

| Bundle content | Source |
|----------------|--------|
| Last 50 sync log events | Structured log query |
| Current `ListingExternalCalendar` rows | DB query |
| Current `ChannelConfig` rows | DB query |
| Last 5 raw iCal feed responses (cached) | `SyncFeedCache` table (new, stores last 5 fetched bodies per calendar, 10 KB each, 7-day retention) |
| Unresolved conflicts | `SyncConflict` query |
| Current availability blocks for listing | `AvailabilityBlock` query |

| ID | Requirement |
|----|-------------|
| OBS-01 | Debug bundle MUST be exportable as JSON via `GET /api/platform/sync-debug/{propertyId}`. Requires `atlas_admin` role. |
| OBS-02 | `SyncFeedCache` table MUST store last 5 raw iCal responses per calendar. Rows older than 7 days auto-purged by background job. |
| OBS-03 | Log retention: 30 days for info-level, 90 days for error-level, permanent for provider switch events. |

---

## 7. Security requirements

### 7.1 iCal security

| Threat | Mitigation | ID |
|--------|------------|:--:|
| Feed URL guessing | `Listing.ICalFeedToken` — 32-char cryptographic random hex appended as `?token=`. Without valid token, endpoint returns 404. | SEC-01 |
| Feed URL leakage | Tenant can regenerate token (§2.B.3). Old token invalidated immediately. | SEC-02 |
| Rate limiting / abuse | 60 req/min per IP, 120 req/min per listing on export endpoint. | SEC-03 |
| Guest PII in feed | Export MUST NOT include guest full name, email, or phone in SUMMARY or DESCRIPTION. Use "Reserved" or "Booking #{id}". | SEC-04 |
| Malicious iCal feed (import) | Parser MUST: (a) limit feed size to 1 MB, (b) limit events to 5,000 per feed, (c) reject non-UTF-8, (d) sanitize SUMMARY/DESCRIPTION (strip HTML, limit length to 500 chars). | SEC-05 |
| SSRF via iCal URL | iCal URL MUST be validated: reject private IPs (10.x, 172.16-31.x, 192.168.x, 127.x, ::1), reject non-HTTP(S) schemes, reject URLs containing `localhost`. | SEC-06 |

### 7.2 Channex security

| Threat | Mitigation | ID |
|--------|------------|:--:|
| API key exposure | V1: stored as plaintext in `ChannelConfig.ApiKey`. V2: encrypt at rest via `IDataProtector` with purpose `"ChannexApiKeys"`. API responses MUST mask key: show only last 4 chars (`****abcd`). | SEC-07 |
| API key in logs | Logger MUST NOT log API key values. Use `[Redacted]` placeholder. | SEC-08 |
| Webhook signature validation | V2: if Channex provides webhook signatures, validate HMAC before processing. If not, use IP whitelist (Channex documented IPs). | SEC-09 |
| Webhook replay attack | `ConsumedWebhook` table with `WebhookId` uniqueness. Duplicate webhook IDs rejected. | SEC-10 |
| Webhook replay with timestamp | V2: if Channex includes timestamp, reject webhooks older than 5 minutes. | SEC-11 |
| Cross-tenant data via Channex | `ChannelConfig` is tenant-scoped via `ITenantOwnedEntity`. EF Core query filter ensures tenant A cannot read tenant B's Channex config. | SEC-12 |

### 7.3 Transport security

| ID | Requirement |
|----|-------------|
| SEC-13 | All iCal import fetches MUST use HTTPS. HTTP URLs MUST be rejected on creation with error "Only HTTPS URLs are supported for iCal import". |
| SEC-14 | All Channex API calls MUST use HTTPS (Channex API enforces this). |
| SEC-15 | iCal export endpoint MUST be served over HTTPS (enforced by Azure App Service + Cloudflare). |

---

## 8. Onboarding SOP (factory process)

### 8.A iCal onboarding (Airbnb-only)

**Target persona:** Basic-plan tenant with 1–3 properties, Airbnb only.

**Estimated time:** 10–15 minutes per property (tenant self-service).

#### Step-by-step

| Step | Action | Who | Time | Screen / URL |
|:----:|--------|:---:|:----:|-------------|
| A1 | Log in to Atlas admin portal | Tenant | 1 min | `/login` |
| A2 | Navigate to Channels page | Tenant | — | `/channels` |
| A3 | Select property, click "Set up iCal Sync" | Tenant | — | `/channels/{propertySlug}` |
| A4 | **Get import URL from Airbnb:** Open Airbnb Host Dashboard → Calendar → Availability Settings → "Export Calendar" → copy the iCal URL | Tenant | 3 min | Airbnb.com |
| A5 | Paste Airbnb iCal URL into Atlas "Import Calendar" field. Name it "Airbnb". Click "Add". | Tenant | 1 min | `/channels/{propertySlug}/ical/import` |
| A6 | Click "Sync Now" to perform first sync. Verify events appear. | Tenant | 1 min | Same page |
| A7 | **Get export URL from Atlas:** Copy the Atlas iCal export URL shown on the page (includes token). | Tenant | 1 min | Same page |
| A8 | **Paste export URL into Airbnb:** Airbnb Host Dashboard → Calendar → Availability Settings → "Import Calendar" → paste Atlas URL → "Import". | Tenant | 2 min | Airbnb.com |
| A9 | **Smoke test:** Create a manual block in Atlas for tomorrow. Wait 5 min. Check Airbnb calendar — block should appear. | Tenant | 5 min | Atlas + Airbnb |
| A10 | **Reverse smoke test:** Create a manual block in Airbnb for day after tomorrow. Click "Sync Now" in Atlas. Verify block appears in Atlas. | Tenant | 3 min | Atlas + Airbnb |

#### Validation checklist

| Check | Expected result | If fail |
|-------|-----------------|---------|
| Import URL is valid HTTPS | URL starts with `https://` | Reject. Show "Only HTTPS URLs are supported." |
| First sync returns > 0 events (if bookings exist) | `SyncedEventCount > 0` | If 0 and tenant has Airbnb bookings: URL may be wrong. Re-check Airbnb export settings. |
| First sync returns 0 errors | `LastSyncError = null` | Display error. Common: URL expired, 403 from Airbnb. |
| Export URL accessible from browser | GET returns `text/calendar` with VCALENDAR content | Check `ICalFeedToken`, check listingId. |
| Smoke test: Atlas block appears on Airbnb within 15 min | Airbnb shows "Imported event" on the blocked date | Airbnb may take up to 4 hours to poll. Re-check export URL in Airbnb settings. |
| Reverse smoke test: Airbnb block appears in Atlas after manual sync | Block visible on Atlas calendar | Check import URL. Re-sync. |

### 8.B Channex onboarding (Premium)

**Target persona:** Premium-plan tenant with multi-OTA setup (Airbnb + Booking.com).

**Estimated time:** 25–40 minutes per property (tenant self-service, may need platform support for Booking.com mapping).

#### Required details from tenant

| Item | Where to get it | Format |
|------|----------------|--------|
| Channex account | Tenant creates at `app.channex.io` | Email + password |
| Channex API key | Channex dashboard → Settings → API Keys | Alphanumeric string |
| Channex Group ID (for Airbnb OAuth) | Channex dashboard → Groups | UUID |
| Booking.com Hotel ID | Booking.com Extranet → Property → General Info | Numeric string |
| Airbnb listing connected in Channex | Channex → Connections → Airbnb → OAuth flow | Completed in Channex |

#### Step-by-step: Airbnb connect

| Step | Action | Who | Time |
|:----:|--------|:---:|:----:|
| B1 | Log in to Atlas admin portal → Channels | Tenant | 1 min |
| B2 | Select property → "Connect Airbnb via Channex" | Tenant | — |
| B3 | Redirected to Channex Airbnb OAuth page (new tab) | Tenant | — |
| B4 | Authorize Airbnb access in Channex | Tenant | 3 min |
| B5 | Copy Channex API key + External Property ID | Tenant | 2 min |
| B6 | Paste into Atlas → Submit | Tenant | 1 min |
| B7 | Click "Test Connection" → verify green badge | Tenant | 1 min |
| B8 | System auto-triggers initial availability + rate push | System | 2 min |

#### Step-by-step: Booking.com connect

| Step | Action | Who | Time |
|:----:|--------|:---:|:----:|
| C1 | In Atlas Channels page → "Connect Booking.com" | Tenant | — |
| C2 | Enter Booking.com Hotel ID + Channex API key | Tenant | 2 min |
| C3 | Test connection → verify green badge | Tenant | 1 min |
| C4 | System auto-triggers initial availability push | System | 2 min |
| C5 | Verify rate mapping in Channex dashboard (room types ↔ rate plans) | Tenant / Support | 10 min |

#### Validation checklist

| Check | Expected result | If fail |
|-------|-----------------|---------|
| Test connection passes | `ChannelConfig.ConnectionStatus = 'connected'` | Invalid API key. Re-check Channex dashboard. |
| Initial availability push succeeds | `LastSyncAt` updated, `LastSyncError = null` | Check Channex rate limits. Retry after 60s. |
| Airbnb calendar reflects Atlas availability within 10 min | Blocked dates match | Check Channex → Airbnb mapping. Verify property mapping in Channex. |
| Booking.com extranet reflects Atlas rates within 10 min | Rates match | Check room type / rate plan mapping in Channex. |
| Rate change in Atlas propagates to OTAs within 5 min | New rate visible on OTA | Check outbox processing. Check `ChannelConfig.LastSyncAt`. |

#### Rollback plan

| Scenario | Action |
|----------|--------|
| Channex connection fails repeatedly | Disconnect Channex in Atlas. Switch to `ICAL_BASIC`. Set up iCal import/export per §8.A. |
| Rate mapping wrong (wrong prices on OTA) | Immediately disconnect in Atlas to stop pushing. Fix mapping in Channex. Reconnect. Verify. |
| Tenant wants to revert to iCal | Use sync switch wizard (§4.3). Channex configs preserved 90 days. |

---

## 9. Acceptance criteria & test matrix

### 9.1 Acceptance criteria (Given/When/Then)

#### AC-ICAL-01: iCal polling — successful sync

**Given** a listing has an active external calendar with a valid iCal URL containing 3 VEVENT blocks,
**When** the iCal sync worker polls this calendar,
**Then** 3 `AvailabilityBlock` rows exist for this listing with `Source = 'iCal'`, `LastSyncAtUtc` is updated, `LastSyncError` is null, and `SyncedEventCount = 3`.

#### AC-ICAL-02: iCal polling — hash unchanged (skip)

**Given** a listing's external calendar was synced 5 minutes ago and the iCal feed body has not changed (same SHA-256 hash),
**When** the sync worker polls again,
**Then** the worker skips parsing, `LastSyncAtUtc` is updated, no `AvailabilityBlock` rows are modified, and the adaptive interval increases.

#### AC-ICAL-03: iCal polling — HTTP 304

**Given** a listing's external calendar has a stored `ETag` from the previous sync,
**When** the sync worker sends `If-None-Match` and receives HTTP 304,
**Then** the worker skips parsing, `LastSyncAtUtc` is updated, `LastSyncError` is null, and no blocks are modified.

#### AC-ICAL-04: iCal polling — network failure

**Given** a listing's external calendar URL returns a network timeout,
**When** the sync worker polls,
**Then** `LastSyncError` is set to the error message, `ConsecutiveFailures` increments, existing blocks are NOT removed, and the worker continues to the next calendar.

#### AC-ICAL-05: Conflict detection

**Given** a listing has an Atlas booking for March 10–12 and an iCal sync imports a block for March 11–13,
**When** conflict detection runs after the iCal sync,
**Then** a `SyncConflict` row is created with `OverlapStart = March 11`, `OverlapEnd = March 12`, `Status = 'detected'`, and a notification is queued for the tenant.

#### AC-ICAL-06: No false conflict for same-calendar overlap

**Given** a single iCal calendar contains two adjacent VEVENTs (March 10–12 and March 12–14),
**When** the sync worker imports both,
**Then** no `SyncConflict` is created (same-source events are not conflicts).

#### AC-ICAL-07: Export feed correctness

**Given** a listing has 2 confirmed bookings and 1 manual block,
**When** an OTA fetches `GET /listings/{id}/ical?token={validToken}`,
**Then** the response is `text/calendar` containing exactly 3 VEVENTs with correct `DTSTART`, `DTEND`, stable UIDs, `SUMMARY: Reserved` (no PII), and valid `Cache-Control` / `ETag` headers.

#### AC-ICAL-08: Export feed — invalid token

**Given** a listing has `ICalFeedToken` set,
**When** a request arrives with an invalid or missing `?token=` parameter,
**Then** the endpoint returns 404 (not 403, to avoid confirming existence).

#### AC-CHX-01: Channex push — rate sync

**Given** a property has an active Channex connection and a tenant changes a nightly rate,
**When** the outbox worker processes the rate change,
**Then** `PushRatesAsync` is called with the correct API key, external property ID, and rate data; `ChannelConfig.LastSyncAt` is updated; `LastSyncError` is null.

#### AC-CHX-02: Channex push — availability sync after booking

**Given** a property has an active Channex connection and a new booking is created in Atlas,
**When** the outbox worker processes the availability change,
**Then** `PushAvailabilityAsync` is called with updated availability (decrement for booked dates); `LastSyncAt` updated.

#### AC-CHX-03: Channex webhook processing (V2)

**Given** a valid Channex webhook arrives with event type `booking_new` for a connected property,
**When** the webhook endpoint processes it,
**Then** an outbox row is created, webhook returns 200 within 5 seconds, and the worker subsequently creates a `Booking` record with `BookingSource = 'channex'` and correct dates/amounts.

#### AC-CHX-04: Channex webhook — duplicate

**Given** a Channex webhook with ID `wh-123` has already been processed,
**When** the same webhook ID arrives again,
**Then** the endpoint returns 200, no outbox row is created, and `channex.webhook.duplicate` is logged.

#### AC-SWT-01: Switching from iCal to Channex

**Given** a tenant on Premium plan has a property using `ICAL_BASIC` with active iCal calendars and imported blocks,
**When** the tenant completes the switch wizard to `CHANNEX_API` and Channex test passes,
**Then** `Property.SyncModeOverride = 'channex_api'`, `SyncSwitchCutoffUtc` is set, iCal calendars are deactivated (`IsActive = false`), iCal blocks older than cutoff are marked legacy, and initial Channex backfill is triggered.

#### AC-SWT-02: Rollback within 24 hours

**Given** a property was switched from `ICAL_BASIC` to `CHANNEX_API` less than 24 hours ago and Channex is failing,
**When** the tenant clicks "Revert to iCal",
**Then** `SyncModeOverride` reverts to `ical_basic`, iCal calendars are reactivated, `SyncSwitchCutoffUtc` is cleared, and a sync cycle runs immediately.

#### AC-ALERT-01: Stale sync alert

**Given** a listing's last successful sync was 35 minutes ago (threshold: 30 min),
**When** the alert evaluation job runs,
**Then** a WARNING alert is generated with the stale calendar details and an email is sent to platform ops.

#### AC-ALERT-02: Unresolved conflict escalation

**Given** a `SyncConflict` with `Status = 'detected'` has existed for > 24 hours,
**When** the alert evaluation job runs,
**Then** a CRITICAL alert is generated and the tenant receives a WhatsApp message.

### 9.2 Test matrix — edge cases

| # | Scenario | Sync mode | Expected behaviour | Priority |
|:-:|----------|:---------:|-------------------|:--------:|
| 1 | **Timezone boundary:** iCal event with `DTSTART;TZID=Asia/Kolkata:20260315T140000` | ICAL | Converted to UTC correctly. Block stored as UTC date. | P1 |
| 2 | **Same-day check-in/out:** `DTSTART=20260401` / `DTEND=20260401` | ICAL | Skipped (`DtEnd <= DtStart`). Logged as warning. | P1 |
| 3 | **Daylight saving transition:** `DTSTART;TZID=America/New_York:20261108T...` (US DST fall-back) | ICAL | Correct UTC conversion despite DST change. No duplicate blocks. | P2 |
| 4 | **Cancellation update:** Previously synced booking disappears from iCal feed | ICAL | Full-replace removes old block. No conflict if Atlas booking was separate. | P1 |
| 5 | **Date modification:** OTA booking moves from March 10–12 to March 12–14 in next sync | ICAL | Old block removed, new block created (full-replace). Conflict detection runs on new dates. | P1 |
| 6 | **Duplicate UID in feed:** Two VEVENTs with same UID but different dates | ICAL | Both imported (full-replace uses DB PK, not UID). Warning logged for duplicate UID. | P2 |
| 7 | **Network failure mid-sync:** HTTP timeout on 3rd of 100 calendars | ICAL | Calendar 3 marked as failed. Calendars 4–100 still processed. | P1 |
| 8 | **Partial Channex webhook payload:** Missing `guest_name` field | CHANNEX | Booking created with `GuestName = null`. Warning logged. | P2 |
| 9 | **Channex rate limit (429):** Push receives 429 response | CHANNEX | Retry after `Retry-After` header (or 60s default). Max 3 retries. | P1 |
| 10 | **Concurrent sync and booking:** iCal sync and manual booking creation happen simultaneously for same listing | Both | Per-listing lock prevents race. One completes first; other retries. Conflict detection runs after both. | P1 |
| 11 | **Feed size > 1 MB:** Large iCal feed from prolific host | ICAL | Rejected with error "Feed exceeds maximum size (1 MB)". Logged. | P2 |
| 12 | **iCal URL returns HTML (not ics):** Broken URL | ICAL | Parse returns 0 events. `LastSyncError` set. Alert after 5 consecutive failures. | P1 |
| 13 | **Provider switch during active booking:** Tenant switches iCal → Channex while having iCal-sourced blocks | Both | Freeze window preserves iCal blocks as legacy. No duplicate blocks. | P1 |
| 14 | **Subscription suspension:** Tenant suspended while Channex connected | CHANNEX | `ConnectionStatus` transitions to `paused`. Pushes stop. Configs preserved. | P1 |
| 15 | **Subscription reactivation:** Tenant pays and reactivates while Channex paused | CHANNEX | `ConnectionStatus` transitions back to `connected`. Full reconciliation push triggered. | P1 |
| 16 | **Export feed with 0 events:** New listing, no bookings, no blocks | ICAL | Valid VCALENDAR with no VEVENT. OTA sees empty calendar. | P2 |
| 17 | **Multiple iCal calendars for one listing:** Airbnb + Google Calendar | ICAL | Each calendar's blocks scoped by `BlockType = calendarId`. No cross-calendar false conflicts. | P1 |
| 18 | **Malicious iCal feed:** Feed contains script injection in SUMMARY | ICAL | SUMMARY sanitized (HTML stripped, length truncated). No XSS in admin UI. | P1 |
| 19 | **Clock skew:** Server time 2 seconds ahead of OTA timestamp | Both | All comparisons use UTC. Tolerance of ±5 seconds on webhook timestamp validation. | P3 |
| 20 | **Leap year date:** Booking on Feb 29, 2028 | Both | Correct date parsing and storage. No off-by-one errors. | P3 |

---

## 10. Rollout plan (feature flags)

### 10.1 Feature flag definitions

| Flag | Type | Default | Description |
|------|:----:|:-------:|-------------|
| `SYNC_MODE_ICAL_ENABLED` | boolean | `true` | Master switch for all iCal sync functionality (import + export). When `false`: import polling stops, export endpoint returns 503. |
| `SYNC_MODE_CHANNEX_ENABLED` | boolean | `true` | Master switch for all Channex sync functionality (push + webhook). When `false`: push worker skips, webhook endpoint returns 503. |
| `ICAL_POLL_INTERVAL_POLICY` | enum | `adaptive` | Values: `fixed_5m`, `fixed_15m`, `adaptive`. Controls polling behaviour. `adaptive` = backoff + check-in boost. |
| `ICAL_FEED_TOKEN_REQUIRED` | boolean | `false` | When `true`: export endpoint requires `?token=` parameter. When `false` (v1 default): endpoint is open by listingId. |
| `SYNC_HEALTH_DASHBOARD_ENABLED` | boolean | `false` | When `true`: `/platform/sync-health` routes are active. When `false`: return 404. |
| `SYNC_SWITCH_WIZARD_ENABLED` | boolean | `false` | When `true`: tenant admin portal shows the "Switch Sync Mode" wizard. When `false`: sync mode can only be set by platform admin. |
| `CHANNEX_WEBHOOKS_ENABLED` | boolean | `false` | When `true`: `POST /api/webhooks/channex` endpoint is active. V2 feature. |
| `SYNC_CONFLICT_DETECTION_ENABLED` | boolean | `true` | When `true`: conflict detection runs after each sync. When `false`: skipped (useful during migration). |

### 10.2 Storage & resolution

| ID | Requirement |
|----|-------------|
| FF-01 | Feature flags MUST be stored in `AppSettings` table (`Key varchar(100)`, `Value varchar(500)`) or `appsettings.json`. |
| FF-02 | Flags MUST be resolvable at runtime without restart. If stored in DB: cache with 1-minute TTL. If in config: use Azure App Service configuration refresh. |
| FF-03 | Flag changes MUST be logged: `platform.feature_flag.changed` with `{key, oldValue, newValue, changedBy}`. |

### 10.3 Phased rollout plan

#### Phase 1: iCal foundation (Week 1–2)

| Step | Action | Flags |
|:----:|--------|-------|
| 1.1 | Deploy enhanced iCal sync (adaptive polling, hash detection, change detection fields) | `SYNC_MODE_ICAL_ENABLED = true`, `ICAL_POLL_INTERVAL_POLICY = fixed_15m` |
| 1.2 | Internal testing: verify polling, export, no regressions | — |
| 1.3 | Switch to adaptive polling for pilot tenants | `ICAL_POLL_INTERVAL_POLICY = adaptive` |
| 1.4 | Enable sync health dashboard for platform admin | `SYNC_HEALTH_DASHBOARD_ENABLED = true` |
| 1.5 | Monitor for 3 days. Verify SLA metrics. | — |

#### Phase 2: Conflict detection + export hardening (Week 3)

| Step | Action | Flags |
|:----:|--------|-------|
| 2.1 | Deploy `SyncConflict` model + detection logic | `SYNC_CONFLICT_DETECTION_ENABLED = true` |
| 2.2 | Deploy export feed enhancements (cache headers, ETag, 304 support) | — |
| 2.3 | Enable feed token for new listings | `ICAL_FEED_TOKEN_REQUIRED = false` (opt-in per listing first) |
| 2.4 | Internal testing of conflict alerts (WhatsApp + email) | — |

#### Phase 3: Channex push hardening (Week 4–5)

| Step | Action | Flags |
|:----:|--------|-------|
| 3.1 | Deploy enhanced Channex push (outbox-based, retry, reconciliation) | `SYNC_MODE_CHANNEX_ENABLED = true` |
| 3.2 | Deploy connection lifecycle states (replacing boolean `IsConnected`) | — |
| 3.3 | Internal testing with test Channex account | — |
| 3.4 | Pilot with 3 Premium tenants | — |
| 3.5 | Monitor push SLA, error rates for 1 week | — |

#### Phase 4: Sync mode selection + switch wizard (Week 6)

| Step | Action | Flags |
|:----:|--------|-------|
| 4.1 | Deploy `Tenant.SyncMode` + `Property.SyncModeOverride` fields | — |
| 4.2 | Deploy sync mode selection in onboarding wizard | `SYNC_SWITCH_WIZARD_ENABLED = true` |
| 4.3 | Deploy provider switch flow (iCal ↔ Channex) with freeze window | — |
| 4.4 | Internal testing of full switch cycle | — |
| 4.5 | Enable for all tenants | — |

#### Phase 5: Channex webhooks — V2 (Week 8+)

| Step | Action | Flags |
|:----:|--------|-------|
| 5.1 | Deploy webhook endpoint + `ConsumedWebhook` table | `CHANNEX_WEBHOOKS_ENABLED = false` |
| 5.2 | Internal testing with Channex sandbox | — |
| 5.3 | Enable for pilot tenants | `CHANNEX_WEBHOOKS_ENABLED = true` (tenant-specific override if needed) |
| 5.4 | GA for all Channex tenants | — |

### 10.4 Data migration

| Migration | Description | Reversible |
|-----------|-------------|:----------:|
| M1: Add `Tenant.SyncMode` | `ALTER TABLE Tenants ADD SyncMode varchar(20) NOT NULL DEFAULT 'none'` | Yes (drop column) |
| M2: Add `Property.SyncModeOverride` | `ALTER TABLE Properties ADD SyncModeOverride varchar(20) NULL` | Yes (drop column) |
| M3: Add `ListingExternalCalendar` fields | Add `LastContentHash`, `LastETag`, `LastModifiedHeader`, `LastChangeDetectedAtUtc`, `ConsecutiveFailures`, `AdaptiveIntervalSeconds` | Yes (drop columns) |
| M4: Add `ChannelConfig.ConnectionStatus` | Add `ConnectionStatus varchar(20) NOT NULL DEFAULT 'disconnected'`. Backfill: `SET ConnectionStatus = CASE WHEN IsConnected = 1 THEN 'connected' ELSE 'disconnected' END`. | Yes (drop column, keep `IsConnected`) |
| M5: Add `ChannelConfig.ConsecutiveFailures` | `ALTER TABLE ChannelConfigs ADD ConsecutiveFailures int NOT NULL DEFAULT 0` | Yes (drop column) |
| M6: Create `SyncConflict` table | New table per §2.C.2 schema. | Yes (drop table) |
| M7: Create `ConsumedWebhook` table | `{ Id int PK, WebhookId varchar(200) UNIQUE, ReceivedAtUtc datetime2 }` | Yes (drop table) |
| M8: Create `TenantSyncDisclaimer` table | Per §1.4 schema. | Yes (drop table) |
| M9: Create `SyncFeedCache` table | `{ Id int PK, CalendarId int FK, FetchedAtUtc datetime2, ResponseBody nvarchar(max), ContentHash varchar(64) }` | Yes (drop table) |
| M10: Add `Listing.ICalFeedToken` | `ALTER TABLE Listings ADD ICalFeedToken varchar(64) NULL` | Yes (drop column) |
| M11: Add `Property.SyncSwitchCutoffUtc` | `ALTER TABLE Properties ADD SyncSwitchCutoffUtc datetime2 NULL` | Yes (drop column) |
| M12: Backfill existing tenants | Tenants with active `ListingExternalCalendar` rows → `SyncMode = 'ical_basic'`. Tenants with active `ChannelConfig` rows → `SyncMode = 'channex_api'`. Others → `SyncMode = 'none'`. | Yes (reset all to `none`) |

**Migration order:** M1 → M2 → M3 → M4 → M5 → M6 → M7 → M8 → M9 → M10 → M11 → M12 (M12 is a data migration, run last).

---

## Appendix A: Requirement ID index

| Prefix | Domain | Count |
|--------|--------|:-----:|
| ELG- | Eligibility rules | 5 |
| MOD- | Sync mode model | 5 |
| DIS- | Disclaimer | 3 |
| ICI- | iCal import | 10 |
| ICE- | iCal export | 11 |
| CON- | Conflict detection | 6 |
| RES- | Conflict resolution | 4 |
| CHX- | Channex sync | 18 |
| ARC- | Architecture abstraction | 8 |
| SWT- | Provider switching | 5 |
| SCH- | Scheduling & scaling | 8 |
| SLA- | SLA targets | 2 |
| OBS- | Observability | 3 |
| SEC- | Security | 15 |
| FF- | Feature flags | 3 |
| **Total** | | **106** |

## Appendix B: Cross-reference to existing docs

| Topic | This doc section | Existing doc | Relationship |
|-------|:----------------:|-------------|:------------:|
| Channex onboarding journey (Airbnb) | §3.2 | RA-001 Journey B | Extends |
| Channex onboarding journey (Booking.com) | §3.3 | RA-001 Journey C | Extends |
| Channex config & rate limits | §3.5 | RA-001 §5.A | Extends |
| iCal polling scale model | §5.1 | RA-002 §5.1 | Supersedes (5 min adaptive replaces 15 min fixed) |
| Channex push scale model | §5.2 | RA-002 §5.2 | Extends |
| Vendor abstraction (`IChannelManagerProvider`) | §4.1 | RA-002 §7.1 | Supersedes (new `IChannelSyncProvider` subsumes old interface) |
| Vendor outage fallback | §3.5 (retry) | RA-002 §7.4 | Aligns |
| Retry/backoff policies | §3.5 | RA-002 §7.7 | Aligns |
| Sync health dashboard | §6.1, §6.2 | RA-006 §4.1 | Extends |
| Sync-related operational playbooks | §2.C, §2.D | RA-002 §9 (incident #3, #4) | Extends |
| OTA sync during subscription states | §3.1 (PAUSED state) | RA-005 §5.2 | Aligns |
