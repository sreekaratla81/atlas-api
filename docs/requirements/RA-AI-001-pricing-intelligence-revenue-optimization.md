# RA-AI-001 — Pricing Intelligence & Revenue Optimization Requirements

| Field | Value |
|-------|-------|
| **ID** | RA-AI-001 |
| **Title** | Pricing Intelligence & Revenue Optimization |
| **Status** | Draft |
| **Author** | Revenue Systems Architect |
| **Created** | 2026-02-27 |
| **Dependencies** | RA-001 (Marketplace/Commission), RA-002 (Governance/Scale), RA-003 (Growth/Demand/Network), RA-004 (Risk/Fraud/Trust), RA-005 (Subscription/Billing), RA-006 (Operational Excellence), RA-IC-001 (Hybrid Sync), RA-CHX-002 (OTA Mapping/ARI) |
| **Stack** | Azure App Service · Azure SQL · Cloudflare Pages |
| **Constraints** | Single developer · No ML infra (V1) · No message broker · DB-backed outbox · Scale to 100k tenants |

---

## Table of Contents

1. [Pricing Intelligence Vision (V1 vs V2)](#1-pricing-intelligence-vision-v1-vs-v2)
2. [Revenue Data Model Requirements](#2-revenue-data-model-requirements)
3. [Demand Signal Engine (Rule-Based V1)](#3-demand-signal-engine-rule-based-v1)
4. [Dynamic Pricing Suggestion Engine](#4-dynamic-pricing-suggestion-engine)
5. [Multi-Channel Pricing Interaction](#5-multi-channel-pricing-interaction)
6. [Revenue Health Dashboard](#6-revenue-health-dashboard)
7. [TrustScore Interaction](#7-trustscore-interaction)
8. [Automation Guardrails](#8-automation-guardrails)
9. [Acceptance Criteria & Test Matrix](#9-acceptance-criteria--test-matrix)
10. [Definition of Done — Pricing Intelligence V1](#10-definition-of-done--pricing-intelligence-v1)
11. [Rollout Plan](#11-rollout-plan)

---

## 1. Pricing Intelligence Vision (V1 vs V2)

### 1.1 Strategic intent

Atlas's target market — 0–10 key hosts in India — overwhelmingly prices by gut feeling or stale OTA defaults. Pricing Intelligence provides data-driven suggestions that increase RevPAR without requiring hosts to become revenue managers. V1 is entirely rule-based and config-driven; V2 introduces ML and market data.

### 1.2 V1 scope — Rule-based dynamic pricing suggestions

V1 delivers pricing suggestions computed from the host's own booking data, calendar, and manually configured events. No external data sources. No machine learning. All thresholds are config-driven via `IOptions<PricingIntelligenceSettings>`.

| Capability | V1 | Implementation approach |
|---|---|---|
| **Occupancy-based pricing nudges** | Yes | Rule engine evaluates forward-looking occupancy windows (7d, 14d, 30d) |
| **Weekend uplift templates** | Yes | Friday/Saturday auto-detect with configurable uplift percentage |
| **Event/festival surge logic** | Yes | Manual `FestivalDate` config table; admin or tenant configurable |
| **Low-demand discount templates** | Yes | Gap detection with configurable discount ceiling |
| **Last-minute availability nudge** | Yes | Check-in within X days + still available → suggest discount |
| **Booking velocity detection** | Yes | X bookings in Y days triggers high-demand signal |
| **Suggest-only mode** | Yes | Default; tenant sees suggestion in dashboard, manually applies |
| **Auto-apply mode** | Yes | Opt-in per listing; applies suggestion to `ListingDailyRate` with `Source = 'AutoSuggested'` |
| **Floor rate enforcement** | Yes | Never suggest below `ListingPricing.BaseNightlyRate * FloorMultiplier` |
| **ARI propagation** | Yes | Accepted/auto-applied suggestions trigger ARI push via existing sync pipeline |

### 1.3 V2 scope — ML & market intelligence (future, explicitly out of V1)

| Capability | V2 | Notes |
|---|---|---|
| **ML demand forecasting** | V2 | Time-series model on per-city/per-listing historical occupancy |
| **Competitor scraping integration** | V2 | Scrape public OTA listing pages for comparable pricing |
| **Market-wide elasticity modeling** | V2 | Price–demand curve per city/segment |
| **Cross-property demand signals** | V2 | Platform-wide booking velocity per city used as demand proxy |
| **Automated A/B pricing experiments** | V2 | Controlled experiments measuring conversion impact of price changes |
| **RevPAR optimization goal** | V2 | ML optimizes for target RevPAR rather than occupancy alone |
| **External event API** | V2 | Integrate with PredictHQ or similar for auto-detected events |
| **Weather-based demand adjustment** | V2 | Weather API integration for tourism-sensitive markets |

- PV-01: V1 MUST NOT depend on any external API for demand signals. All inputs come from Atlas's own booking data and manual configuration.
- PV-02: V1 MUST be architected so that V2 ML models can replace rule-based logic without schema changes — the `PriceSuggestion` output format is identical regardless of source.
- PV-03: Every `PriceSuggestion` MUST carry a `StrategySource` field (`RULE_ENGINE` in V1, `ML_MODEL` in V2) for reporting and A/B analysis.

### 1.4 Feature flag governance

| Flag | V1 default | Scope | Description |
|------|:----------:|-------|-------------|
| `PricingIntelligence:Enabled` | `false` | Global | Master kill-switch |
| `PricingIntelligence:SuggestionsEnabled` | `true` | Global | Enable suggestion generation (when master is on) |
| `PricingIntelligence:AutoApplyEnabled` | `false` | Global | Enable auto-apply capability (tenant must also opt in) |
| `PricingIntelligence:DashboardEnabled` | `true` | Global | Show revenue health dashboard |
| `PricingIntelligence:FestivalSurgeEnabled` | `true` | Global | Enable festival/event-based surge suggestions |
| `PricingIntelligence:WeekendUpliftEnabled` | `true` | Global | Enable weekend uplift suggestions |
| `PricingIntelligence:LowDemandDiscountEnabled` | `true` | Global | Enable low-demand discount suggestions |
| `PricingIntelligence:LastMinuteNudgeEnabled` | `true` | Global | Enable last-minute availability discounts |

- PV-04: All feature flags MUST be config-driven (`IOptions<PricingIntelligenceFeatureFlags>`).
- PV-05: Flags MUST be evaluable per-tenant in V2 (tenant-level overrides). V1: global only.

---

## 2. Revenue Data Model Requirements

### 2.1 New entities

#### 2.1.1 `DailyPerformanceSnapshot`

Stores pre-computed daily KPIs per listing. Materialized by a nightly batch job to avoid expensive real-time aggregation.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `long` (PK, identity) | Auto-increment |
| `TenantId` | `int` (FK → Tenant, indexed) | Tenant scope |
| `ListingId` | `int` (FK → Listing, indexed) | Listing scope |
| `Date` | `date` (indexed) | Calendar date (UTC) |
| `OccupancyRate` | `decimal(5,4)` | Fraction of units booked for this date (0.0000–1.0000) |
| `Adr` | `decimal(12,2)` | Average Daily Rate for this date (revenue / rooms sold) |
| `RevPar` | `decimal(12,2)` | Revenue per available room (revenue / rooms available) |
| `Revenue` | `decimal(12,2)` | Total revenue attributed to this date |
| `RoomsSold` | `int` | Units booked |
| `RoomsAvailable` | `int` | Total units available |
| `BookingCount` | `int` | Number of bookings overlapping this date |
| `Currency` | `varchar(3)` | `INR` default |
| `ComputedAtUtc` | `datetime2` | Timestamp of last computation |

**Unique constraint**: `IX_DailyPerformanceSnapshot_Tenant_Listing_Date` on `(TenantId, ListingId, Date)`.

- DM-01: Snapshot MUST be computed nightly at 02:00 UTC (configurable via `PricingIntelligence:SnapshotTimeUtc`).
- DM-02: Snapshot covers the trailing 90 days plus forward 90 days (for occupancy-based signals).
- DM-03: Forward dates use confirmed bookings + blocks to compute projected occupancy.
- DM-04: Snapshot job MUST be idempotent — re-running for the same date overwrites (upsert on unique constraint).
- DM-05: Snapshot MUST implement `ITenantOwnedEntity` for EF Core tenant filtering.

#### 2.1.2 `BookingVelocityMetric`

Tracks booking intake rate per listing over rolling windows. Updated on every booking create/cancel event.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `long` (PK, identity) | Auto-increment |
| `TenantId` | `int` (FK) | Tenant scope |
| `ListingId` | `int` (FK) | Listing scope |
| `WindowStartUtc` | `datetime2` | Start of measurement window |
| `WindowEndUtc` | `datetime2` | End of measurement window |
| `WindowDays` | `int` | Window size in days (3, 7, 14) |
| `BookingsCreated` | `int` | Bookings created in window |
| `BookingsCancelled` | `int` | Bookings cancelled in window |
| `NetBookings` | `int` | Created − Cancelled |
| `ComputedAtUtc` | `datetime2` | Last computation timestamp |

**Unique constraint**: `IX_BookingVelocityMetric_Listing_Window` on `(TenantId, ListingId, WindowDays, WindowStartUtc)`.

- DM-06: Velocity metrics MUST be recomputed on every booking status change (via outbox event: `booking.created`, `booking.cancelled`, `booking.modified`).
- DM-07: Three standard windows: 3-day, 7-day, 14-day. Configurable via `PricingIntelligence:VelocityWindows` (int array).
- DM-08: Velocity computation MUST be lightweight — a simple count query scoped by `TenantId` + `ListingId` + date range.

#### 2.1.3 `DemandSignal`

Records detected demand signals that may trigger pricing suggestions.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `long` (PK, identity) | Auto-increment |
| `TenantId` | `int` (FK) | Tenant scope |
| `ListingId` | `int` (FK) | Listing scope |
| `SignalType` | `varchar(50)` | See signal type enum (section 3) |
| `Severity` | `varchar(10)` | `LOW`, `MEDIUM`, `HIGH` |
| `DetectedAtUtc` | `datetime2` | When signal was detected |
| `ExpiresAtUtc` | `datetime2` | When signal becomes stale |
| `AffectedDateStart` | `date` | Start of affected date range |
| `AffectedDateEnd` | `date` | End of affected date range |
| `Metadata` | `nvarchar(max)` | JSON blob with signal-specific context |
| `Status` | `varchar(20)` | `ACTIVE`, `EXPIRED`, `CONSUMED`, `SUPPRESSED` |
| `ConsumedBySuggestionId` | `long?` (FK → PriceSuggestion) | Links to the suggestion this signal produced |

**Index**: `IX_DemandSignal_Active` on `(TenantId, ListingId, Status)` WHERE `Status = 'ACTIVE'`.

- DM-09: Signals MUST auto-expire. The signal engine marks `EXPIRED` when `ExpiresAtUtc < NOW()`.
- DM-10: A signal transitions to `CONSUMED` when it has produced a `PriceSuggestion`.
- DM-11: A signal transitions to `SUPPRESSED` if a higher-priority conflicting signal overrides it.

#### 2.1.4 `PriceSuggestion`

The core output of the pricing engine — a recommended rate change for a specific date range.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `long` (PK, identity) | Auto-increment |
| `TenantId` | `int` (FK) | Tenant scope |
| `ListingId` | `int` (FK) | Listing scope |
| `DateRangeStart` | `date` | First date the suggestion applies to |
| `DateRangeEnd` | `date` | Last date (inclusive) |
| `CurrentRate` | `decimal(12,2)` | Rate at time of suggestion |
| `SuggestedRate` | `decimal(12,2)` | Recommended rate |
| `ChangePercent` | `decimal(5,2)` | Percentage change from current |
| `ChangeDirection` | `varchar(10)` | `INCREASE`, `DECREASE`, `NONE` |
| `StrategySource` | `varchar(30)` | `RULE_ENGINE` (V1), `ML_MODEL` (V2) |
| `RuleId` | `varchar(50)` | Identifier of the rule that produced the suggestion |
| `DemandSignalId` | `long?` (FK → DemandSignal) | Triggering signal, if any |
| `Confidence` | `decimal(3,2)` | 0.00–1.00 confidence (V1: always 1.00 for rule-based) |
| `Status` | `varchar(20)` | `PENDING`, `ACCEPTED`, `REJECTED`, `AUTO_APPLIED`, `EXPIRED`, `SUPERSEDED` |
| `Currency` | `varchar(3)` | `INR` |
| `Reason` | `nvarchar(500)` | Human-readable explanation for the tenant |
| `CreatedAtUtc` | `datetime2` | Generation timestamp |
| `RespondedAtUtc` | `datetime2?` | When tenant acted on the suggestion |
| `RespondedByUserId` | `int?` | User who accepted/rejected |
| `ExpiresAtUtc` | `datetime2` | Auto-expire if not acted upon |
| `AppliedToRateIds` | `nvarchar(max)` | JSON array of `ListingDailyRate.Id` values written |

**Index**: `IX_PriceSuggestion_Pending` on `(TenantId, ListingId, Status)` WHERE `Status = 'PENDING'`.

- DM-12: Only ONE `PENDING` suggestion per listing per date range at any time. New suggestion for overlapping dates supersedes the previous one (`Status → SUPERSEDED`).
- DM-13: `ExpiresAtUtc` defaults to `DateRangeStart - 1 day` (must act before the dates arrive). Configurable via `PricingIntelligence:SuggestionExpiryBufferDays`.
- DM-14: Accepting a suggestion MUST write `ListingDailyRate` rows with `Source = 'Suggested'` and `Reason` referencing the `PriceSuggestion.Id`.
- DM-15: Auto-applying MUST write `ListingDailyRate` rows with `Source = 'AutoSuggested'`.
- DM-16: `PriceSuggestion` MUST implement `ITenantOwnedEntity`.

#### 2.1.5 `SuggestionHistory` (audit view)

This is a **SQL view** (not a table) joining `PriceSuggestion` with `DemandSignal` and `ListingDailyRate` for audit and reporting.

```sql
CREATE VIEW vw_SuggestionHistory AS
SELECT
    ps.Id AS SuggestionId,
    ps.TenantId,
    ps.ListingId,
    ps.DateRangeStart,
    ps.DateRangeEnd,
    ps.CurrentRate,
    ps.SuggestedRate,
    ps.ChangePercent,
    ps.Status,
    ps.StrategySource,
    ps.RuleId,
    ps.Reason,
    ps.CreatedAtUtc,
    ps.RespondedAtUtc,
    ds.SignalType AS TriggeringSignalType,
    ds.Severity AS TriggeringSignalSeverity
FROM PriceSuggestion ps
LEFT JOIN DemandSignal ds ON ps.DemandSignalId = ds.Id;
```

- DM-17: View MUST be tenant-scoped in application queries (EF Core global filter applies to underlying tables).

#### 2.1.6 `FestivalDate` (configuration table)

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `int` (PK, identity) | Auto-increment |
| `Name` | `nvarchar(200)` | Festival/event name |
| `DateStart` | `date` | First day |
| `DateEnd` | `date` | Last day (inclusive) |
| `Region` | `nvarchar(100)` | Geographic scope (e.g., `India`, `Goa`, `All`) |
| `SurgePercent` | `decimal(5,2)` | Suggested uplift percentage |
| `IsActive` | `bit` | Active/inactive |
| `CreatedByUserId` | `int?` | Admin or tenant who created |
| `Scope` | `varchar(20)` | `PLATFORM` (admin-created) or `TENANT` (tenant-created) |
| `TenantId` | `int?` | NULL for PLATFORM scope |

- DM-18: `PLATFORM` festival dates are visible to all tenants. `TENANT` festival dates are private.
- DM-19: Atlas Admin pre-populates major Indian festivals (Diwali, Holi, Christmas, New Year, regional harvest festivals) as `PLATFORM` scope.
- DM-20: Tenants can add local events (e.g., "Sunburn Festival Goa") as `TENANT` scope.

### 2.2 Computation frequency

| Entity | Computation trigger | Frequency |
|--------|-------------------|-----------|
| `DailyPerformanceSnapshot` | Nightly batch job | Once daily at 02:00 UTC |
| `BookingVelocityMetric` | Booking event (outbox) | On each `booking.created` / `booking.cancelled` / `booking.modified` |
| `DemandSignal` | Signal detection job | Every 4 hours (configurable: `PricingIntelligence:SignalDetectionIntervalHours`) |
| `PriceSuggestion` | Suggestion engine job | Every 6 hours (configurable: `PricingIntelligence:SuggestionEngineIntervalHours`) |
| `FestivalDate` | Manual CRUD | On-demand |

### 2.3 Storage granularity

All performance data is stored at **daily** granularity per listing. No hourly or sub-daily resolution in V1.

- DM-21: Date columns use `date` type (not `datetime2`) for calendar dates.
- DM-22: Monetary values use `decimal(12,2)` (max ₹9,99,99,99,999.99).
- DM-23: Rate/percentage values use `decimal(5,2)` for display percentages, `decimal(5,4)` for fractional rates.

### 2.4 Retention policy

| Entity | Retention | Rationale |
|--------|-----------|-----------|
| `DailyPerformanceSnapshot` | 2 years | Revenue trend analysis |
| `BookingVelocityMetric` | 90 days rolling | Only recent velocity matters |
| `DemandSignal` | 6 months | Debugging and pattern analysis |
| `PriceSuggestion` | 2 years | Audit trail, effectiveness measurement |
| `FestivalDate` | Indefinite (soft delete) | Recurs annually |

- DM-24: A weekly cleanup job MUST purge expired rows beyond retention. Uses `DELETE TOP(1000)` batches to avoid lock escalation.
- DM-25: Purge job MUST be idempotent and log `pricing.data.purged` with `{entity, rowsDeleted, cutoffDate}`.

---

## 3. Demand Signal Engine (Rule-Based V1)

### 3.1 Signal types

| Signal type | Code | Description | Severity default |
|---|---|---|---|
| High booking velocity | `HIGH_VELOCITY` | ≥ X net bookings in Y-day window | `HIGH` |
| Low occupancy window | `LOW_OCCUPANCY` | Forward dates with occupancy < threshold | `MEDIUM` |
| Last-minute availability | `LAST_MINUTE_AVAIL` | Check-in within X days, listing still available | `MEDIUM` |
| Peak weekend occupancy | `PEAK_WEEKEND` | Upcoming weekend ≥ threshold occupancy | `HIGH` |
| Festival/event date | `FESTIVAL_SURGE` | Date falls within active `FestivalDate` range | `HIGH` |
| Repeated cancellations | `CANCEL_CLUSTER` | ≥ X cancellations in Y-day window | `LOW` |
| Long booking gap | `BOOKING_GAP` | No booking created for listing in > Z days | `LOW` |
| Extended vacancy streak | `VACANCY_STREAK` | ≥ X consecutive future days with 0 bookings | `MEDIUM` |

### 3.2 Signal detection logic

Each signal type has a dedicated detection rule evaluated by the signal detection job.

#### 3.2.1 `HIGH_VELOCITY`

```
IF BookingVelocityMetric.NetBookings (7-day window) >= Config:HighVelocityThreshold
THEN emit DemandSignal(HIGH_VELOCITY, HIGH)
     AffectedDateStart = today
     AffectedDateEnd = today + 14 days
     ExpiresAtUtc = today + 3 days
```

| Config key | V1 default | Type |
|------------|:----------:|------|
| `PricingIntelligence:HighVelocityThreshold` | 5 | int |
| `PricingIntelligence:HighVelocityWindowDays` | 7 | int |

#### 3.2.2 `LOW_OCCUPANCY`

```
FOR each 7-day forward window (sliding, 1-day step, up to 30 days ahead):
  IF AVG(DailyPerformanceSnapshot.OccupancyRate) < Config:LowOccupancyThreshold
  THEN emit DemandSignal(LOW_OCCUPANCY, MEDIUM)
       AffectedDateStart = window start
       AffectedDateEnd = window end
       ExpiresAtUtc = AffectedDateStart - 1 day
```

| Config key | V1 default | Type |
|------------|:----------:|------|
| `PricingIntelligence:LowOccupancyThreshold` | 0.30 | decimal |
| `PricingIntelligence:LowOccupancyLookAheadDays` | 30 | int |

#### 3.2.3 `LAST_MINUTE_AVAIL`

```
IF date is within Config:LastMinuteDays of today
AND DailyPerformanceSnapshot.OccupancyRate < 1.0
AND no PENDING suggestion exists for this date
THEN emit DemandSignal(LAST_MINUTE_AVAIL, MEDIUM)
     AffectedDateStart = date
     AffectedDateEnd = date
     ExpiresAtUtc = date (same day)
```

| Config key | V1 default | Type |
|------------|:----------:|------|
| `PricingIntelligence:LastMinuteDays` | 3 | int |

#### 3.2.4 `PEAK_WEEKEND`

```
FOR each upcoming Friday-Saturday pair within 14 days:
  IF projected occupancy >= Config:PeakWeekendThreshold
  THEN emit DemandSignal(PEAK_WEEKEND, HIGH)
       AffectedDateStart = Friday
       AffectedDateEnd = Saturday
       ExpiresAtUtc = Friday - 1 day
```

| Config key | V1 default | Type |
|------------|:----------:|------|
| `PricingIntelligence:PeakWeekendThreshold` | 0.70 | decimal |

#### 3.2.5 `FESTIVAL_SURGE`

```
FOR each active FestivalDate WHERE DateStart within next 30 days:
  IF no existing ACTIVE signal of type FESTIVAL_SURGE for this listing+date range
  THEN emit DemandSignal(FESTIVAL_SURGE, HIGH)
       AffectedDateStart = FestivalDate.DateStart
       AffectedDateEnd = FestivalDate.DateEnd
       ExpiresAtUtc = FestivalDate.DateStart
       Metadata = { "festivalName": Name, "surgePercent": SurgePercent }
```

No threshold config — presence in the `FestivalDate` table is the trigger.

#### 3.2.6 `CANCEL_CLUSTER`

```
IF BookingVelocityMetric.BookingsCancelled (7-day window) >= Config:CancelClusterThreshold
THEN emit DemandSignal(CANCEL_CLUSTER, LOW)
     AffectedDateStart = today
     AffectedDateEnd = today + 7 days
     ExpiresAtUtc = today + 3 days
```

| Config key | V1 default | Type |
|------------|:----------:|------|
| `PricingIntelligence:CancelClusterThreshold` | 3 | int |

#### 3.2.7 `BOOKING_GAP`

```
IF last booking for listing was > Config:BookingGapDays ago
AND listing has Status = 'Active'
THEN emit DemandSignal(BOOKING_GAP, LOW)
     AffectedDateStart = today
     AffectedDateEnd = today + 30 days
     ExpiresAtUtc = today + 7 days
```

| Config key | V1 default | Type |
|------------|:----------:|------|
| `PricingIntelligence:BookingGapDays` | 21 | int |

#### 3.2.8 `VACANCY_STREAK`

```
IF there exists a streak of >= Config:VacancyStreakDays consecutive future days
   WHERE DailyPerformanceSnapshot.RoomsSold = 0
THEN emit DemandSignal(VACANCY_STREAK, MEDIUM)
     AffectedDateStart = streak start
     AffectedDateEnd = streak end
     ExpiresAtUtc = streak start - 1 day
```

| Config key | V1 default | Type |
|------------|:----------:|------|
| `PricingIntelligence:VacancyStreakDays` | 7 | int |

### 3.3 Threshold configuration

All thresholds MUST be loaded via `IOptions<PricingIntelligenceSettings>` and are hot-reloadable (Azure App Service configuration).

- SIG-01: Thresholds MUST be documented with their default values in `appsettings.json`.
- SIG-02: Admin portal MUST provide a "Pricing Intelligence Config" page for platform-level threshold tuning (V2: per-tenant overrides).
- SIG-03: Threshold changes MUST be audit-logged: `pricing.config.changed` with `{key, oldValue, newValue, changedBy}`.

### 3.4 Signal expiration rules

- SIG-04: The signal detection job MUST mark all signals with `ExpiresAtUtc < NOW()` as `Status = 'EXPIRED'` before detecting new signals.
- SIG-05: Expired signals are NOT deleted immediately — they are retained per the 6-month retention policy for analysis.
- SIG-06: A signal that has been `CONSUMED` (produced a suggestion) MUST NOT be re-emitted even if conditions still hold. The suggestion engine handles recurring conditions via suggestion supersession.

### 3.5 Conflict resolution between signals

When multiple signals affect the same date range for the same listing, the suggestion engine must resolve conflicts. Signal priority (highest to lowest):

| Priority | Signal type | Rationale |
|:--------:|---|---|
| 1 | `FESTIVAL_SURGE` | External event — strongest demand indicator |
| 2 | `HIGH_VELOCITY` | Actual booking data confirms demand |
| 3 | `PEAK_WEEKEND` | Structural demand pattern |
| 4 | `LAST_MINUTE_AVAIL` | Time-sensitive |
| 5 | `LOW_OCCUPANCY` | Broad indicator |
| 6 | `VACANCY_STREAK` | Subset of low occupancy |
| 7 | `BOOKING_GAP` | Weakest signal (absence of data) |
| 8 | `CANCEL_CLUSTER` | Informational, may counter other signals |

- SIG-07: For the same date range, the HIGHEST-priority active signal drives the suggestion. Lower-priority signals are marked `SUPPRESSED`.
- SIG-08: Exception: `CANCEL_CLUSTER` never suppresses or is suppressed — it acts as a **modifier** that can reduce the magnitude of an uplift suggestion (see section 4).
- SIG-09: If `FESTIVAL_SURGE` and `HIGH_VELOCITY` overlap, use the HIGHER uplift of the two (they reinforce each other rather than stacking).

### 3.6 Scaling considerations (100k tenants)

- SIG-10: The signal detection job MUST process tenants in batches (configurable batch size, default 500).
- SIG-11: Each batch is a single DB transaction scoped to that batch.
- SIG-12: Job uses `SKIP LOCKED` pattern (or `UPDLOCK, READPAST` in SQL Server) to allow parallel worker instances in V2.
- SIG-13: Total signal detection cycle MUST complete within 1 hour for 100k tenants. Target: ≤ 2ms per listing per signal type.
- SIG-14: Signal detection queries MUST use covering indexes; no table scans on `Booking` or `DailyPerformanceSnapshot`.

---

## 4. Dynamic Pricing Suggestion Engine

### 4.1 Rule definitions

The suggestion engine evaluates active demand signals and produces `PriceSuggestion` records. Each rule maps a signal type to a pricing action.

#### 4.1.1 High-demand rules

| Rule ID | Trigger signal | Condition | Suggested change | Reason template |
|---------|---------------|-----------|-----------------|-----------------|
| `RULE_HIGH_VELOCITY` | `HIGH_VELOCITY` | Active signal | +10% on affected dates | "High booking activity detected — consider increasing rates by {pct}%" |
| `RULE_PEAK_WEEKEND` | `PEAK_WEEKEND` | Active signal | +15% on Fri/Sat | "Strong weekend demand — suggested weekend uplift of {pct}%" |
| `RULE_FESTIVAL_SURGE` | `FESTIVAL_SURGE` | Active signal | +{FestivalDate.SurgePercent}% | "Upcoming {festivalName} — seasonal surge pricing of {pct}%" |

#### 4.1.2 Low-demand rules

| Rule ID | Trigger signal | Condition | Suggested change | Reason template |
|---------|---------------|-----------|-----------------|-----------------|
| `RULE_LOW_OCCUPANCY` | `LOW_OCCUPANCY` | Active signal | −8% on affected dates | "Low occupancy ahead — a small discount could attract bookings" |
| `RULE_LAST_MINUTE` | `LAST_MINUTE_AVAIL` | Active signal | −12% on affected date | "Last-minute availability — a discount may fill this date" |
| `RULE_VACANCY_STREAK` | `VACANCY_STREAK` | Active signal | −10% on streak dates | "Extended vacancy detected — consider a discount to break the gap" |
| `RULE_BOOKING_GAP` | `BOOKING_GAP` | Active signal | −5% on next 30 days | "No recent bookings — a gentle discount may restart activity" |

#### 4.1.3 Modifier rules

| Rule ID | Trigger signal | Effect on other suggestions | Reason template |
|---------|---------------|-----------------------------|-----------------|
| `RULE_CANCEL_DAMPER` | `CANCEL_CLUSTER` | Reduce any uplift suggestion by 50% (but never flip to discount) | "Recent cancellations moderate the suggested increase" |

### 4.2 Suggestion percentage configuration

All percentages are configurable via `IOptions<PricingRuleSettings>`.

| Config key | V1 default | Type | Description |
|------------|:----------:|------|-------------|
| `PricingRules:HighVelocityUpliftPercent` | 10.0 | decimal | Uplift for high booking velocity |
| `PricingRules:PeakWeekendUpliftPercent` | 15.0 | decimal | Uplift for peak weekends |
| `PricingRules:LowOccupancyDiscountPercent` | 8.0 | decimal | Discount for low occupancy |
| `PricingRules:LastMinuteDiscountPercent` | 12.0 | decimal | Discount for last-minute availability |
| `PricingRules:VacancyStreakDiscountPercent` | 10.0 | decimal | Discount for vacancy streaks |
| `PricingRules:BookingGapDiscountPercent` | 5.0 | decimal | Discount for long booking gaps |
| `PricingRules:CancelDamperFactor` | 0.50 | decimal | Factor to reduce uplift when cancellations detected |

### 4.3 Suggestion priority order

When multiple rules could produce suggestions for the same date range:

1. Compute all applicable suggestions.
2. If any are uplift and any are discount, uplift wins (demand signal is stronger than absence signal).
3. Among multiple uplifts, use the highest percentage (signals reinforce).
4. Among multiple discounts, use the most moderate (least aggressive discount to preserve revenue).
5. Apply `CANCEL_DAMPER` modifier last if applicable.

- SUG-01: The engine MUST NOT stack percentages. Only one suggestion per date range.
- SUG-02: If the computed suggestion is identical to the current rate (within ₹1), do not emit a suggestion.

### 4.4 Rate boundaries

| Boundary | Config key | V1 default | Description |
|----------|------------|:----------:|-------------|
| **Max uplift %** | `PricingRules:MaxUpliftPercent` | 30.0 | Suggestion MUST NOT exceed this % increase |
| **Max discount %** | `PricingRules:MaxDiscountPercent` | 20.0 | Suggestion MUST NOT exceed this % decrease |
| **Floor rate multiplier** | `PricingRules:FloorRateMultiplier` | 0.60 | `SuggestedRate >= BaseNightlyRate * FloorMultiplier` |
| **Absolute floor** | `PricingRules:AbsoluteFloorInr` | 500.0 | No suggestion below ₹500/night (configurable) |
| **Ceiling rate multiplier** | `PricingRules:CeilingRateMultiplier` | 3.00 | `SuggestedRate <= BaseNightlyRate * CeilingMultiplier` |

- SUG-03: `SuggestedRate = MAX(AbsoluteFloor, BaseNightlyRate * FloorMultiplier, MIN(CurrentRate * (1 + ChangePercent/100), BaseNightlyRate * CeilingMultiplier))`.
- SUG-04: If `SuggestedRate` hits a boundary, `ChangePercent` MUST be recalculated to reflect the clamped value.
- SUG-05: No negative rate is EVER possible. Validation: `SuggestedRate > 0` is a hard invariant.

### 4.5 Rounding rules

- SUG-06: `SuggestedRate` MUST be rounded to the nearest ₹50 when `BaseNightlyRate < ₹5,000`.
- SUG-07: `SuggestedRate` MUST be rounded to the nearest ₹100 when `BaseNightlyRate >= ₹5,000`.
- SUG-08: Rounding MUST happen after all boundary clamping.
- SUG-09: Rounding direction: standard rounding (round half up).

### 4.6 Suggest-only vs Auto-apply modes

| Mode | Behavior | Tenant opt-in | Rate source written |
|------|----------|:-------------:|:-------------------:|
| **Suggest-only** (default) | `PriceSuggestion` created with `Status = PENDING`. Shown in dashboard. Tenant manually accepts or rejects. | Default for all listings | `Source = 'Suggested'` on accept |
| **Auto-apply** | `PriceSuggestion` created with `Status = AUTO_APPLIED`. `ListingDailyRate` rows written immediately. Tenant can revert. | Explicit opt-in per listing via `Listing.AutoPriceEnabled` (new `bit` column, default `false`) | `Source = 'AutoSuggested'` |

- SUG-10: Auto-apply MUST still create the `PriceSuggestion` record for audit trail.
- SUG-11: Auto-apply MUST respect all guardrails (section 8).
- SUG-12: Auto-apply MUST NOT apply if the listing is currently in a sync migration (`SyncState = MIGRATING`).
- SUG-13: Tenant can revert an auto-applied suggestion by manually setting the rate. This creates a new `ListingDailyRate` with `Source = 'Manual'` and marks the suggestion `Status = REJECTED` with `Reason = 'ManualOverride'`.
- SUG-14: Auto-apply for a date MUST NOT overwrite a rate that was manually set within the last 24 hours (manual always wins within recency window).

### 4.7 Suggestion lifecycle

```
PENDING ──[Tenant accepts]──→ ACCEPTED ──[Rate written]──→ (terminal)
   │                                                         
   ├──[Tenant rejects]──→ REJECTED (terminal)
   │
   ├──[ExpiresAtUtc reached]──→ EXPIRED (terminal)
   │
   ├──[New suggestion for same dates]──→ SUPERSEDED (terminal)
   │
   └──[Auto-apply enabled]──→ AUTO_APPLIED ──[Rate written]──→ (terminal)
                                  │
                                  └──[Tenant reverts]──→ REJECTED (Reason='ManualOverride')
```

- SUG-15: Terminal states are immutable. Once a suggestion reaches a terminal state, no further transitions are allowed.
- SUG-16: All state transitions MUST be audit-logged: `pricing.suggestion.{transition}` with `{suggestionId, listingId, fromStatus, toStatus, userId}`.

### 4.8 Scaling considerations

- SUG-17: Suggestion engine processes listings in batches of 500 (configurable).
- SUG-18: For each listing, the engine evaluates all active `DemandSignal` rows (expected: < 5 per listing) — O(1) per listing.
- SUG-19: Suggestion generation MUST complete within 2 hours for 100k tenants across all listings.
- SUG-20: Suggestion writes use batch `INSERT ... ON CONFLICT UPDATE` pattern (upsert via `MERGE` in SQL Server) for idempotency.

---

## 5. Multi-Channel Pricing Interaction

### 5.1 Price suggestion propagation path

When a price suggestion is accepted (or auto-applied), the updated rate flows through the existing ARI pipeline defined in RA-IC-001 and RA-CHX-002.

```
┌─────────────┐     ┌───────────────────┐     ┌──────────────────┐     ┌─────────────┐
│ PriceSuggest │────→│ ListingDailyRate  │────→│ ARI Outbox Event │────→│ Channel Sync│
│   accepted   │     │   row written     │     │  rate.updated    │     │  (Channex)  │
└─────────────┘     └───────────────────┘     └──────────────────┘     └─────────────┘
                           │
                           ▼
                    ┌──────────────────┐
                    │ Direct Booking   │
                    │ (marketplace)    │
                    │ uses latest rate │
                    └──────────────────┘
```

### 5.2 Channel-specific behavior

| Channel | Propagation | Timing | Notes |
|---------|-------------|--------|-------|
| **Direct bookings (marketplace)** | Immediate | Rate read at booking time from `ListingDailyRate` | No push required |
| **Channex ARI push** | Via outbox | Within debounce window (RA-CHX-002 §6) | Rate update triggers `rate.updated` outbox event |
| **iCal sync** | N/A | iCal does not carry rate data | Rates only visible in Atlas + Channex-connected OTAs |

- MCP-01: A `PriceSuggestion` acceptance MUST trigger the same outbox event (`rate.updated`) as a manual rate change.
- MCP-02: The ARI push pipeline MUST NOT differentiate between manual, suggested, and auto-suggested rate sources — they all follow the same debounce and batching rules.
- MCP-03: If Channex ARI push fails, the suggestion remains `ACCEPTED` / `AUTO_APPLIED` in Atlas. The ARI retry mechanism (RA-CHX-002 §6) handles delivery.

### 5.3 Conflict rules — manual override vs suggestion

| Scenario | Resolution | Rationale |
|----------|-----------|-----------|
| Manual rate set → suggestion generated for same date | Suggestion shows both current (manual) rate and suggested rate. Tenant decides. | Host's explicit intent should not be overridden silently |
| Auto-apply fires → host manually changes rate within 24h | Auto-apply marks suggestion as `REJECTED (ManualOverride)`. Next auto-apply for same date skipped for 24h. | Manual always wins within recency window |
| Suggestion accepted → host changes mind, sets manual rate | `ListingDailyRate` updated with `Source = 'Manual'`. Suggestion status unchanged (was already `ACCEPTED`). | Latest write wins at the rate level |
| Two suggestions overlap date ranges | Later suggestion supersedes earlier. Only one PENDING per date range per listing. | Prevents confusion |

- MCP-04: The `ListingDailyRate.Source` field tracks the provenance of every rate: `Manual`, `Suggested`, `AutoSuggested`, `Channex` (inbound from Channex in bi-directional V2).
- MCP-05: V1: Rate sync is **Atlas → OTA only**. Rates from OTAs are NOT imported. The `Channex` source value is reserved for V2 bi-directional rate sync.

### 5.4 Rate source snapshot on bookings

When a booking is created, the `ListingDailyRate.Source` active at booking time MUST be captured:

| New column on `Booking` | Type | Description |
|---|---|---|
| `RateSourceSnapshot` | `varchar(20)` | `Manual`, `Suggested`, `AutoSuggested` — captured at booking creation |

- MCP-06: `RateSourceSnapshot` is immutable once set (same pattern as `CommissionPercentSnapshot` in RA-001).
- MCP-07: Revenue reports MUST allow filtering/grouping by `RateSourceSnapshot` to measure suggestion effectiveness.

### 5.5 Debounce strategy for ARI traffic control

- MCP-08: Multiple suggestion acceptances for the same listing within a 5-minute window MUST be coalesced into a single ARI push (leveraging RA-CHX-002 §6 debounce).
- MCP-09: Auto-apply batch runs MUST NOT generate more than `Config:MaxAriPushesPerListingPerHour` outbox events per listing per hour.

| Config key | V1 default | Type |
|------------|:----------:|------|
| `PricingIntelligence:MaxAriPushesPerListingPerHour` | 4 | int |

- MCP-10: If the ARI push limit is reached, subsequent rate changes are queued and coalesced into the next allowed push window.

---

## 6. Revenue Health Dashboard

### 6.1 Tenant-facing metrics

The admin portal "Revenue Intelligence" tab MUST display the following metrics for the logged-in tenant.

| Metric | Formula | Period | Display |
|--------|---------|--------|---------|
| **Current month revenue** | `SUM(Booking.AmountReceived)` for check-ins in current month | Current month | ₹ total + trend vs. previous month |
| **Last month revenue** | Same formula, previous month | Previous month | ₹ total |
| **ADR** | `Revenue / NightsSold` | Selectable: 7d / 30d / 90d | ₹ amount |
| **RevPAR** | `Revenue / NightsAvailable` | Same | ₹ amount |
| **Occupancy %** | `NightsSold / NightsAvailable * 100` | Same | Percentage + sparkline |
| **Direct booking %** | `MarketplaceBookings / TotalBookings * 100` | Same | Percentage |
| **OTA dependency %** | `100 - DirectBookingPercent` | Same | Percentage |
| **Boost ROI impact** | From RA-003 §4.6 | Same | Ratio or "N/A" |
| **Suggestion acceptance rate** | `AcceptedSuggestions / TotalSuggestions * 100` | Last 90 days | Percentage |
| **Revenue from suggestions** | `SUM(Revenue)` for bookings with `RateSourceSnapshot IN ('Suggested', 'AutoSuggested')` | Same | ₹ total |
| **Avg. suggested uplift** | `AVG(ChangePercent)` for accepted uplift suggestions | Last 90 days | Percentage |

- DSH-01: All revenue metrics MUST use query-time computation (no stale cache for monetary values), consistent with RA-005 §9.1.
- DSH-02: Trend indicators show current vs. previous period. Arrow + percentage change.
- DSH-03: Period selector options: 7 days, 30 days, 90 days, custom range.

### 6.2 Traffic-light indicators

Each key metric receives a health indicator:

| Metric | Green (healthy) | Amber (moderate) | Red (underperforming) |
|--------|:---------------:|:-----------------:|:---------------------:|
| Occupancy % | ≥ 70% | 40–69% | < 40% |
| ADR | ≥ regional median * 0.9 | 60–89% of regional median | < 60% of regional median |
| RevPAR | ≥ regional median * 0.8 | 50–79% of regional median | < 50% of regional median |
| Direct booking % | ≥ 30% | 15–29% | < 15% |
| Suggestion acceptance rate | ≥ 60% | 30–59% | < 30% |

- DSH-04: V1: "Regional median" is approximated by the platform-wide median across all tenants in the same city. Computed in the nightly batch job.
- DSH-05: Traffic-light thresholds MUST be configurable via `IOptions<RevenueDashboardSettings>`.
- DSH-06: Traffic-light colors are CSS classes applied client-side (no backend rendering of colors).

### 6.3 Suggestion inbox

The dashboard MUST include a "Pricing Suggestions" section:

| Element | Description |
|---------|-------------|
| **Pending suggestions list** | Cards showing: listing name, date range, current rate, suggested rate, change %, reason, expiry countdown |
| **Accept button** | Applies suggestion → writes `ListingDailyRate` → triggers ARI push |
| **Reject button** | Marks suggestion as rejected. Optional: "Why did you reject?" quick-select (Too high / Too low / Not relevant / Other) |
| **Accept all** | Bulk-accept all pending suggestions (with confirmation dialog) |
| **Suggestion history** | Table view of past suggestions with status, actual booking outcome if known |
| **Auto-apply toggle** | Per-listing toggle to enable/disable auto-apply |

- DSH-07: Pending suggestions MUST be sorted by expiry (soonest first), then by magnitude (highest change first).
- DSH-08: Each suggestion card MUST show the reason in plain language (from `PriceSuggestion.Reason`).
- DSH-09: "Accept all" MUST show a summary (N suggestions, average change %) before confirmation.
- DSH-10: Rejection reason is optional but stored in `PriceSuggestion.Metadata` for engine improvement analytics.

### 6.4 Revenue trend charts

| Chart | Data source | Type |
|-------|------------|------|
| Monthly revenue trend | `DailyPerformanceSnapshot` aggregated by month | Line chart (12 months) |
| ADR trend | Same | Line chart |
| Occupancy trend | Same | Line chart |
| Channel mix | `ChannelPerformance` DTO | Pie chart |
| Suggestion impact | Bookings with `RateSourceSnapshot` = `Suggested`/`AutoSuggested` vs `Manual` | Stacked bar |

- DSH-11: Charts MUST use the existing `MonthlyTrend` DTO from `AnalyticsDtos.cs` where applicable.
- DSH-12: New chart data endpoints MUST follow the same pattern as `AdminReportsController.GetAnalyticsTrends()`.

### 6.5 Admin (Atlas platform) dashboard

In addition to tenant-facing metrics, the Atlas Admin dashboard MUST show platform-wide pricing intelligence metrics:

| Metric | Formula | Refresh |
|--------|---------|---------|
| **Tenants with auto-apply enabled** | Count of tenants with ≥ 1 listing `AutoPriceEnabled = true` | Daily |
| **Total suggestions generated (period)** | Count of `PriceSuggestion` rows | Daily |
| **Suggestion acceptance rate (platform)** | Accepted / (Accepted + Rejected + Expired) | Daily |
| **Revenue attributed to suggestions** | `SUM(Revenue)` for bookings with suggested rate source | Daily |
| **Avg. uplift from accepted suggestions** | `AVG(ChangePercent)` WHERE accepted AND increase | Daily |
| **Top demand signals** | Most frequent signal types across platform | Daily |
| **Signal detection latency** | P50, P95 of signal detection job duration | Per-run |
| **Suggestion generation latency** | P50, P95 of suggestion engine job duration | Per-run |

- DSH-13: Platform dashboard MUST be accessible only to Atlas Admin role.
- DSH-14: All metrics MUST show data freshness timestamp.

---

## 7. TrustScore Interaction

### 7.1 Pricing behavior affecting trust

The following pricing behaviors are monitored and feed into the TrustScore system (RA-004 §2):

| Behavior | Detection | Impact on TrustScore | Signal emitted |
|----------|-----------|---------------------|----------------|
| **Frequent last-minute cancellations** | ≥ 3 host-initiated cancellations within 7 days for bookings with check-in < 48h | Increases `CancellationRate` component → TrustScore drops | `trust.signal.cancellation_cluster` |
| **Sudden price spikes** | Rate increased > 50% in a single change (manual or auto-applied) | V1: No direct TrustScore impact. Logged for monitoring. V2: May introduce `PricingStability` component. | `pricing.spike.detected` |
| **Over-aggressive discounts** | Rate decreased > 30% below BaseNightlyRate | V1: No direct TrustScore impact. Guardrails prevent (section 8). | `pricing.aggressive_discount.detected` |
| **Price oscillation** | Rate for same date changed > 3 times in 7 days | V1: No direct TrustScore impact. Logged. V2: May penalize. | `pricing.oscillation.detected` |

- TSI-01: V1 does NOT introduce new TrustScore components for pricing behavior. It logs signals only.
- TSI-02: V2 SHOULD introduce a `PricingStability` component (weight ~0.05, reducing other weights proportionally).

### 7.2 Does TrustScore affect pricing suggestions?

| TrustScore range | Effect on suggestions | Rationale |
|:----------------:|----------------------|-----------|
| ≥ 0.80 | Full suggestion capability. All rules apply. | Healthy property. |
| 0.60–0.79 | Suggestions generated but uplift capped at 50% of normal. | Moderate trust — conservative pricing to avoid guest disappointment. |
| 0.40–0.59 | Only discount suggestions generated. No uplift. | Low trust — focus on occupancy recovery, not revenue maximization. |
| < 0.40 | No suggestions generated. | Very low trust — property has fundamental quality issues to fix first. |

- TSI-03: TrustScore-based suggestion gating MUST be evaluated before the suggestion engine runs for each listing.
- TSI-04: If a listing's TrustScore drops below 0.40 mid-cycle, existing `PENDING` suggestions MUST be auto-expired with `Reason = 'TrustScoreBelowThreshold'`.
- TSI-05: Auto-apply MUST be automatically disabled for listings with TrustScore < 0.60. Re-enabled when TrustScore recovers above 0.60.

### 7.3 Does pricing volatility affect ranking?

- TSI-06: V1: Pricing volatility does NOT directly affect ranking. Ranking uses the commission boost, quality, and trust components defined in RA-003.
- TSI-07: V2 hook: A `PricingVolatility` signal (standard deviation of nightly rate over 30 days / mean rate) can be introduced as a ranking modifier (multiplier 0.95–1.00). Config: `Ranking:PricingVolatilityEnabled` (default `false`).

### 7.4 V2 hooks defined

| Hook | V1 state | V2 intent |
|------|:--------:|-----------|
| `PricingStability` TrustScore component | Logged only | Weight 0.05 in TrustScore |
| `PricingVolatility` ranking modifier | Disabled | Multiplier 0.95–1.00 |
| Price spike guest-facing warning | Not shown | "This property recently changed pricing" badge |
| Pricing behavior in fraud signals | Logged to `fraud.signal.*` | Auto-escalation to admin review |

---

## 8. Automation Guardrails

### 8.1 Rate change limits

| Guardrail | Config key | V1 default | Enforcement |
|-----------|------------|:----------:|-------------|
| **Max daily price change %** | `Guardrails:MaxDailyChangePercent` | 20.0 | Combined: manual + suggested + auto changes for a single date MUST NOT exceed this in a rolling 24-hour window |
| **Max weekly price change %** | `Guardrails:MaxWeeklyChangePercent` | 35.0 | Cumulative absolute change over 7 days for a single date MUST NOT exceed this |
| **Max rate (absolute)** | `Guardrails:MaxRateInr` | 100000.0 | No rate above ₹1,00,000/night |
| **Min rate (absolute)** | `Guardrails:MinRateInr` | 500.0 | No rate below ₹500/night |

- GRD-01: Guardrails apply to ALL rate changes — manual, suggested, auto-suggested, and API-sourced.
- GRD-02: If a manual rate change would breach a guardrail, the UI MUST show a warning but allow the change (host override). Audit log: `pricing.guardrail.overridden`.
- GRD-03: If an auto-applied suggestion would breach a guardrail, it MUST be blocked and the suggestion marked `REJECTED (GuardrailBreach)`. Tenant notified.
- GRD-04: Guardrail breach tracking requires storing a `RateChangeLog` per listing per date:

#### 8.1.1 `RateChangeLog` (new entity)

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `long` (PK, identity) | Auto-increment |
| `TenantId` | `int` (FK) | Tenant scope |
| `ListingId` | `int` (FK) | Listing scope |
| `Date` | `date` | Calendar date affected |
| `OldRate` | `decimal(12,2)` | Previous rate |
| `NewRate` | `decimal(12,2)` | New rate |
| `ChangePercent` | `decimal(5,2)` | Percentage change |
| `Source` | `varchar(20)` | `Manual`, `Suggested`, `AutoSuggested` |
| `ChangedAtUtc` | `datetime2` | Timestamp |
| `ChangedByUserId` | `int?` | User who made the change |
| `SuggestionId` | `long?` (FK → PriceSuggestion) | If change was from a suggestion |
| `GuardrailOverridden` | `bit` | `true` if host manually overrode a guardrail warning |

**Retention**: 90 days rolling.

### 8.2 Freeze window before check-in

- GRD-05: Auto-applied suggestions MUST NOT change rates for dates where check-in is within `Config:FreezeWindowHours` hours.

| Config key | V1 default | Type |
|------------|:----------:|------|
| `Guardrails:FreezeWindowHours` | 48 | int |

- GRD-06: The freeze window applies ONLY to auto-apply. Manual changes and explicit suggestion acceptance are permitted (host override).
- GRD-07: If a guest has already booked for the date, rate changes do not affect the existing booking (booking amount is immutable).

### 8.3 Blackout dates

- GRD-08: Tenants can configure blackout date ranges per listing where NO auto-pricing suggestions are generated or applied.

| New column on `ListingPricingRule` | Type | Description |
|---|---|---|
| N/A — reuse existing `ListingPricingRule` with `RuleType = 'BLACKOUT'` | | `SeasonStart` and `SeasonEnd` define the blackout window |

- GRD-09: During blackout, the suggestion engine MUST skip the listing for the blackout date range.
- GRD-10: Existing `ListingPricingRule` with `RuleType = 'BLACKOUT'` is repurposed. `DiscountPercent` is ignored for blackout rules.

### 8.4 Rate-change velocity circuit breaker

- GRD-11: If more than `Config:CircuitBreakerThreshold` listings across the platform receive auto-applied changes in a single engine run, halt the engine and alert admin.

| Config key | V1 default | Type |
|------------|:----------:|------|
| `Guardrails:CircuitBreakerThreshold` | 5000 | int |

- GRD-12: Circuit breaker prevents runaway auto-pricing due to engine bugs or data anomalies.
- GRD-13: When circuit breaker trips: all remaining suggestions in the batch are created as `PENDING` (not auto-applied), even for listings with auto-apply enabled.
- GRD-14: Admin receives alert: `pricing.circuit_breaker.tripped` with `{autoAppliedCount, threshold, batchId}`.

### 8.5 Audit logging

All guardrail evaluations MUST produce structured logs:

| Event | Fields | Condition |
|-------|--------|-----------|
| `pricing.guardrail.evaluated` | `{listingId, date, guardrailType, currentValue, limit, passed}` | Every auto-apply check |
| `pricing.guardrail.blocked` | `{listingId, date, guardrailType, attemptedRate, limit, suggestionId}` | Auto-apply blocked |
| `pricing.guardrail.overridden` | `{listingId, date, guardrailType, newRate, limit, userId}` | Host manually overrides |
| `pricing.circuit_breaker.tripped` | `{autoAppliedCount, threshold, batchId}` | Circuit breaker activated |

---

## 9. Acceptance Criteria & Test Matrix

### 9.1 Given/When/Then acceptance criteria

#### 9.1.1 High demand detection

```
GIVEN a listing with 6 net bookings in the last 7 days
  AND Config:HighVelocityThreshold = 5
WHEN the signal detection job runs
THEN a DemandSignal with SignalType = 'HIGH_VELOCITY' and Severity = 'HIGH'
     is created for that listing
  AND AffectedDateStart = today
  AND AffectedDateEnd = today + 14 days
  AND Status = 'ACTIVE'
```

#### 9.1.2 Low demand detection

```
GIVEN a listing with average occupancy of 25% over the next 14 days
  AND Config:LowOccupancyThreshold = 0.30
WHEN the signal detection job runs
THEN a DemandSignal with SignalType = 'LOW_OCCUPANCY' and Severity = 'MEDIUM'
     is created for the affected date range
  AND AffectedDateStart and AffectedDateEnd cover the low-occupancy window
```

#### 9.1.3 Suggestion generation (uplift)

```
GIVEN an active HIGH_VELOCITY demand signal for listing L, dates D1–D14
  AND listing L has current rate ₹2,000/night
  AND Config:HighVelocityUpliftPercent = 10
WHEN the suggestion engine runs
THEN a PriceSuggestion is created:
     SuggestedRate = ₹2,200 (₹2,000 * 1.10, rounded to nearest ₹50)
     ChangePercent = 10.00
     ChangeDirection = 'INCREASE'
     Status = 'PENDING'
     RuleId = 'RULE_HIGH_VELOCITY'
     Reason = 'High booking activity detected — consider increasing rates by 10%'
```

#### 9.1.4 Suggestion generation (discount)

```
GIVEN an active LOW_OCCUPANCY demand signal for listing L, dates D15–D21
  AND listing L has current rate ₹3,000/night
  AND Config:LowOccupancyDiscountPercent = 8
  AND BaseNightlyRate = ₹2,500
  AND FloorRateMultiplier = 0.60 (floor = ₹1,500)
WHEN the suggestion engine runs
THEN a PriceSuggestion is created:
     SuggestedRate = ₹2,750 (₹3,000 * 0.92, rounded to nearest ₹50)
     ChangePercent = -8.00
     ChangeDirection = 'DECREASE'
     Status = 'PENDING'
```

#### 9.1.5 Auto-apply mode

```
GIVEN listing L has AutoPriceEnabled = true
  AND a PriceSuggestion is generated with SuggestedRate = ₹2,500
  AND no guardrail breach exists
  AND check-in for affected dates is > 48 hours away
WHEN the suggestion engine runs
THEN PriceSuggestion.Status = 'AUTO_APPLIED'
  AND ListingDailyRate rows are created with NightlyRate = ₹2,500
     AND Source = 'AutoSuggested'
  AND an outbox event 'rate.updated' is enqueued for ARI push
```

#### 9.1.6 Auto-apply blocked by guardrail

```
GIVEN listing L has AutoPriceEnabled = true
  AND cumulative daily rate change for date D already at 18%
  AND Config:MaxDailyChangePercent = 20
  AND new suggestion would add 5% (total 23%)
WHEN the suggestion engine runs
THEN the suggestion is created with Status = 'REJECTED'
  AND Reason = 'GuardrailBreach: MaxDailyChangePercent exceeded'
  AND no ListingDailyRate rows are written
  AND structured log 'pricing.guardrail.blocked' is emitted
```

#### 9.1.7 Festival uplift

```
GIVEN FestivalDate 'Diwali' exists with DateStart = 2026-10-20, DateEnd = 2026-10-22, SurgePercent = 20
  AND listing L has current rate ₹4,000/night for those dates
  AND the signal detection job runs on 2026-10-01
WHEN the suggestion engine processes the FESTIVAL_SURGE signal
THEN a PriceSuggestion is created:
     SuggestedRate = ₹4,800 (₹4,000 * 1.20, rounded to nearest ₹100)
     RuleId = 'RULE_FESTIVAL_SURGE'
     Reason = 'Upcoming Diwali — seasonal surge pricing of 20%'
```

#### 9.1.8 Floor rate enforcement

```
GIVEN listing L has BaseNightlyRate = ₹1,000
  AND FloorRateMultiplier = 0.60 (floor = ₹600)
  AND AbsoluteFloorInr = 500
  AND current rate = ₹800
  AND suggestion engine computes a -15% discount (₹680)
WHEN the suggestion engine applies boundary clamping
THEN SuggestedRate = ₹700 (₹680 rounded to nearest ₹50 = ₹700)
  AND ₹700 >= ₹600 (floor) ✓
  AND ₹700 >= ₹500 (absolute floor) ✓
  AND the suggestion is valid
```

#### 9.1.9 Floor rate enforcement — clamped

```
GIVEN listing L has BaseNightlyRate = ₹800
  AND FloorRateMultiplier = 0.60 (floor = ₹480)
  AND AbsoluteFloorInr = 500
  AND current rate = ₹600
  AND suggestion engine computes a -20% discount (₹480)
WHEN the suggestion engine applies boundary clamping
THEN SuggestedRate = MAX(₹500, ₹480) = ₹500
  AND ChangePercent is recalculated to -16.67%
```

#### 9.1.10 Multi-rule conflict resolution

```
GIVEN listing L has:
  - Active FESTIVAL_SURGE signal (SurgePercent = 20%)
  - Active HIGH_VELOCITY signal (UpliftPercent = 10%)
  - Active CANCEL_CLUSTER signal (damper = 50%)
WHEN the suggestion engine resolves conflicts
THEN the HIGHER uplift wins → 20% (FESTIVAL_SURGE)
  AND CANCEL_CLUSTER damper applies → 20% * 50% = 10% effective uplift
  AND final suggestion = +10%
```

#### 9.1.11 ARI push consistency after suggestion

```
GIVEN listing L is connected to Channex (SyncMode = CHANNEX_API)
  AND a PriceSuggestion is accepted for dates D1–D3
WHEN ListingDailyRate rows are written
THEN an outbox event 'rate.updated' is enqueued
  AND the Channex ARI push worker processes the event within the debounce window
  AND Channex receives the updated rates for D1–D3
```

#### 9.1.12 Reverting to manual pricing

```
GIVEN listing L has an AUTO_APPLIED suggestion for date D
  AND ListingDailyRate for D has Source = 'AutoSuggested', NightlyRate = ₹2,500
WHEN the tenant manually sets the rate for D to ₹2,000
THEN a new ListingDailyRate row is created with Source = 'Manual', NightlyRate = ₹2,000
  AND the PriceSuggestion is NOT retroactively changed (it remains AUTO_APPLIED)
  AND subsequent auto-apply for date D is blocked for 24 hours
  AND an outbox event 'rate.updated' is enqueued with the manual rate
```

### 9.2 Edge case test matrix

| # | Scenario | Expected behavior |
|---|----------|-------------------|
| E1 | Listing has 0 bookings ever | `BOOKING_GAP` signal emitted after gap threshold. No velocity signals (0 < threshold). Suggestions are discount-only. |
| E2 | Listing has 100% occupancy for next 30 days | No `LOW_OCCUPANCY` or `VACANCY_STREAK` signals. `HIGH_VELOCITY` possible if recent bookings meet threshold. Uplift suggestions generated. |
| E3 | Festival date overlaps weekend | `FESTIVAL_SURGE` and `PEAK_WEEKEND` both detected. `FESTIVAL_SURGE` has higher priority. Use festival surge percent (not stacked). |
| E4 | Auto-apply + freeze window | Check-in tomorrow → suggestion blocked by freeze window. Created as `PENDING` instead. |
| E5 | Suggestion generated for date in the past | Engine MUST skip dates <= today. No suggestions for past dates. |
| E6 | Currency is not INR | All rounding rules and absolute floors MUST respect the listing's currency. V1: INR only, validated. |
| E7 | BaseNightlyRate = 0 | No suggestions generated. `SuggestedRate` computation would be invalid. Signal: `pricing.validation.zero_base_rate`. |
| E8 | Tenant on FREE plan | Pricing Intelligence feature gated by plan. FREE plan: dashboard view only, no suggestions. BASIC+: full feature. |
| E9 | Listing in `MIGRATING` sync state | Auto-apply disabled. Suggest-only mode forced. |
| E10 | TrustScore drops from 0.85 to 0.35 mid-cycle | Existing PENDING suggestions auto-expired. No new suggestions until trust recovers above 0.40. |
| E11 | 100k tenants: signal detection job timeout | Batch processing with 500-tenant batches. Job logs progress. If timeout, resumes from last completed batch. |
| E12 | Concurrent manual rate change and auto-apply | Optimistic concurrency on `ListingDailyRate`. If manual change wins (expected), auto-apply detects conflict and skips. |
| E13 | Suggestion expired before tenant acts | `Status → EXPIRED`. No rate change. Signal may re-trigger in next cycle if conditions persist. |
| E14 | Tenant enables then disables auto-apply rapidly | Last state wins. If disabled: pending auto-suggestions revert to `PENDING` (suggest-only). |
| E15 | DailyPerformanceSnapshot job fails | Suggestion engine uses stale data. Staleness > 48h → engine skips listing and emits `pricing.stale_data.skipped`. |

---

## 10. Definition of Done — Pricing Intelligence V1

### 10.1 Checklist

| # | Criterion | Verification method |
|---|-----------|-------------------|
| 1 | Revenue metrics (ADR, RevPAR, Occupancy) match manual calculation on 90-day test dataset | Integration test with known booking data |
| 2 | `DailyPerformanceSnapshot` nightly job completes for 1,000 test listings within 5 minutes | Load test with timing assertion |
| 3 | All 8 demand signal types detected correctly in unit tests (1 test per signal type minimum) | Unit tests with Given/When/Then |
| 4 | Suggestion engine produces correct uplift and discount for each rule | Unit tests covering all 7 rules + modifier |
| 5 | Multi-rule conflict resolution matches priority table | Unit test with overlapping signals |
| 6 | Rate boundaries (floor, ceiling, absolute min/max) enforced in all paths | Unit tests: boundary conditions |
| 7 | Rounding rules produce correct ₹50/₹100 rounding | Unit tests: rounding edge cases |
| 8 | Auto-apply writes `ListingDailyRate` with correct source and triggers ARI outbox event | Integration test |
| 9 | ARI propagation after suggestion acceptance sends correct rate to Channex pipeline | Integration test with mock Channex |
| 10 | No rate update loops: suggestion → rate change → re-suggestion cycle impossible | Verified by: suggestions only generated from `DemandSignal`, not from rate changes directly |
| 11 | No negative rate scenario possible | Unit test: assert `SuggestedRate > 0` for all code paths |
| 12 | Guardrails block auto-apply when daily/weekly limits breached | Integration test |
| 13 | Freeze window prevents auto-apply within 48h of check-in | Unit test |
| 14 | Circuit breaker halts engine at threshold | Integration test with mock data |
| 15 | Audit log of suggestion history complete and queryable | `vw_SuggestionHistory` returns correct data |
| 16 | `RateSourceSnapshot` captured on new bookings | Integration test: create booking after suggestion acceptance |
| 17 | TrustScore gating: no suggestions for TrustScore < 0.40 | Unit test |
| 18 | TrustScore gating: uplift capped at 50% for TrustScore 0.60–0.79 | Unit test |
| 19 | Feature flags disable/enable all pricing intelligence features independently | Integration test with flag combinations |
| 20 | Dashboard shows all tenant-facing metrics with correct values | E2E test (Playwright) |
| 21 | Suggestion inbox: accept, reject, accept-all work correctly | E2E test (Playwright) |
| 22 | Data retention purge job removes expired data correctly | Integration test |
| 23 | No table scans in signal detection queries (verified via query plan) | Query plan analysis on representative data |
| 24 | Suggestion engine completes for 10,000 test listings within 30 minutes | Load test |

### 10.2 Non-functional requirements

| Requirement | Target | Measurement |
|------------|--------|-------------|
| Signal detection latency (P95) | < 500ms per listing | Structured log timing |
| Suggestion generation latency (P95) | < 200ms per listing | Structured log timing |
| Nightly snapshot job duration (10k listings) | < 10 minutes | Job completion log |
| ARI push after suggestion acceptance | Within ARI debounce window (RA-CHX-002) | End-to-end timing |
| Dashboard page load time | < 2 seconds | Lighthouse / browser timing |
| Zero financial duplicates from auto-pricing | 0 | Invariant check in integration tests |

---

## 11. Rollout Plan

### 11.1 Feature flag progression

| Phase | Duration | Flags enabled | Scope |
|-------|----------|--------------|-------|
| **Phase 0: Internal testing** | 2 weeks | All flags ON for Atlas test tenants only | 3–5 internal properties |
| **Phase 1: Dashboard only** | 2 weeks | `DashboardEnabled` = true, `SuggestionsEnabled` = false | All tenants |
| **Phase 2: Suggest-only** | 4 weeks | `SuggestionsEnabled` = true, `AutoApplyEnabled` = false | All BASIC+ tenants |
| **Phase 3: Auto-apply beta** | 4 weeks | `AutoApplyEnabled` = true | Opt-in tenants (must explicitly enable per listing) |
| **Phase 4: GA** | Ongoing | All flags ON | All eligible tenants |

### 11.2 Plan-gated access

| Plan | Dashboard | Demand signals | Suggestions | Auto-apply |
|------|:---------:|:--------------:|:-----------:|:----------:|
| FREE | View only (last-30d summary) | — | — | — |
| BASIC | Full | Visible | Suggest-only | — |
| PRO | Full | Visible + configurable thresholds | Suggest-only + auto-apply | Yes |
| MARKETPLACE_ONLY | Full | Visible | Suggest-only | — |

- RLP-01: Plan gating MUST be enforced server-side. UI hides features but API returns 403 for unauthorized plans.
- RLP-02: Plan upgrade from BASIC → PRO immediately unlocks auto-apply capability (no delay).

### 11.3 Success metrics

| Metric | Target (3 months post-GA) | Measurement |
|--------|:-------------------------:|-------------|
| Suggestion acceptance rate | ≥ 40% | `PriceSuggestion` status counts |
| RevPAR improvement for suggestion-adopters | ≥ +8% vs. non-adopters | Cohort analysis from `DailyPerformanceSnapshot` |
| Auto-apply adoption | ≥ 15% of PRO tenants | `Listing.AutoPriceEnabled` count |
| Support tickets related to pricing | < 2% of total tickets | Ticket tagging |
| Zero negative-rate incidents | 0 | Monitoring alert |
| Zero rate-update loops | 0 | Monitoring alert |
| Engine job SLA met (completion within time) | 99.5% | Job monitoring |

### 11.4 Rollback plan

- RLP-03: Setting `PricingIntelligence:Enabled` to `false` immediately halts all signal detection, suggestion generation, and auto-apply.
- RLP-04: Existing `ListingDailyRate` rows written by auto-apply are NOT rolled back (they represent valid rates). Tenants can manually revert.
- RLP-05: Pending suggestions are auto-expired when the master flag is disabled.
- RLP-06: Dashboard gracefully shows "Pricing Intelligence is currently unavailable" when disabled.

---

## Appendix A: Configuration Reference

All configuration keys with their V1 defaults, collected for operational reference.

| Config key | V1 default | Type | Section |
|------------|:----------:|------|:-------:|
| `PricingIntelligence:Enabled` | `false` | bool | §1.4 |
| `PricingIntelligence:SuggestionsEnabled` | `true` | bool | §1.4 |
| `PricingIntelligence:AutoApplyEnabled` | `false` | bool | §1.4 |
| `PricingIntelligence:DashboardEnabled` | `true` | bool | §1.4 |
| `PricingIntelligence:FestivalSurgeEnabled` | `true` | bool | §1.4 |
| `PricingIntelligence:WeekendUpliftEnabled` | `true` | bool | §1.4 |
| `PricingIntelligence:LowDemandDiscountEnabled` | `true` | bool | §1.4 |
| `PricingIntelligence:LastMinuteNudgeEnabled` | `true` | bool | §1.4 |
| `PricingIntelligence:SnapshotTimeUtc` | `02:00` | TimeOnly | §2.2 |
| `PricingIntelligence:SignalDetectionIntervalHours` | `4` | int | §2.2 |
| `PricingIntelligence:SuggestionEngineIntervalHours` | `6` | int | §2.2 |
| `PricingIntelligence:SuggestionExpiryBufferDays` | `1` | int | §2.1.4 |
| `PricingIntelligence:VelocityWindows` | `[3,7,14]` | int[] | §2.1.2 |
| `PricingIntelligence:HighVelocityThreshold` | `5` | int | §3.2.1 |
| `PricingIntelligence:HighVelocityWindowDays` | `7` | int | §3.2.1 |
| `PricingIntelligence:LowOccupancyThreshold` | `0.30` | decimal | §3.2.2 |
| `PricingIntelligence:LowOccupancyLookAheadDays` | `30` | int | §3.2.2 |
| `PricingIntelligence:LastMinuteDays` | `3` | int | §3.2.3 |
| `PricingIntelligence:PeakWeekendThreshold` | `0.70` | decimal | §3.2.4 |
| `PricingIntelligence:CancelClusterThreshold` | `3` | int | §3.2.6 |
| `PricingIntelligence:BookingGapDays` | `21` | int | §3.2.7 |
| `PricingIntelligence:VacancyStreakDays` | `7` | int | §3.2.8 |
| `PricingIntelligence:MaxAriPushesPerListingPerHour` | `4` | int | §5.5 |
| `PricingRules:HighVelocityUpliftPercent` | `10.0` | decimal | §4.2 |
| `PricingRules:PeakWeekendUpliftPercent` | `15.0` | decimal | §4.2 |
| `PricingRules:LowOccupancyDiscountPercent` | `8.0` | decimal | §4.2 |
| `PricingRules:LastMinuteDiscountPercent` | `12.0` | decimal | §4.2 |
| `PricingRules:VacancyStreakDiscountPercent` | `10.0` | decimal | §4.2 |
| `PricingRules:BookingGapDiscountPercent` | `5.0` | decimal | §4.2 |
| `PricingRules:CancelDamperFactor` | `0.50` | decimal | §4.2 |
| `PricingRules:MaxUpliftPercent` | `30.0` | decimal | §4.4 |
| `PricingRules:MaxDiscountPercent` | `20.0` | decimal | §4.4 |
| `PricingRules:FloorRateMultiplier` | `0.60` | decimal | §4.4 |
| `PricingRules:AbsoluteFloorInr` | `500.0` | decimal | §4.4 |
| `PricingRules:CeilingRateMultiplier` | `3.00` | decimal | §4.4 |
| `Guardrails:MaxDailyChangePercent` | `20.0` | decimal | §8.1 |
| `Guardrails:MaxWeeklyChangePercent` | `35.0` | decimal | §8.1 |
| `Guardrails:MaxRateInr` | `100000.0` | decimal | §8.1 |
| `Guardrails:MinRateInr` | `500.0` | decimal | §8.1 |
| `Guardrails:FreezeWindowHours` | `48` | int | §8.2 |
| `Guardrails:CircuitBreakerThreshold` | `5000` | int | §8.4 |

---

## Appendix B: Entity Relationship Diagram (Text)

```
Tenant (1) ──── (*) Listing
                  │
                  ├──── (1) ListingPricing
                  │         BaseNightlyRate, WeekendNightlyRate, Currency
                  │
                  ├──── (*) ListingDailyRate
                  │         Date, NightlyRate, Source, Reason
                  │
                  ├──── (*) ListingPricingRule
                  │         RuleType (LOS | SEASONAL | BLACKOUT), Priority
                  │
                  ├──── (*) DailyPerformanceSnapshot
                  │         Date, OccupancyRate, Adr, RevPar, Revenue
                  │
                  ├──── (*) BookingVelocityMetric
                  │         WindowDays, NetBookings
                  │
                  ├──── (*) DemandSignal
                  │         SignalType, Severity, Status
                  │         │
                  │         └──── (0..1) PriceSuggestion
                  │                      SuggestedRate, Status, RuleId
                  │                      │
                  │                      └──── (*) ListingDailyRate (written on accept/auto-apply)
                  │
                  └──── (*) RateChangeLog
                            Date, OldRate, NewRate, Source

FestivalDate (platform/tenant scoped)
     Name, DateStart, DateEnd, SurgePercent, Region

Booking ── RateSourceSnapshot (new column)
```

---

## Appendix C: Structured Log Events

| Event | Level | Fields | Trigger |
|-------|-------|--------|---------|
| `pricing.snapshot.completed` | Info | `{tenantCount, listingCount, durationMs}` | Nightly batch |
| `pricing.signal.detected` | Info | `{signalType, severity, listingId, tenantId, affectedDates}` | Signal detection |
| `pricing.signal.expired` | Debug | `{signalId, signalType, listingId}` | Expiration sweep |
| `pricing.suggestion.created` | Info | `{suggestionId, listingId, ruleId, currentRate, suggestedRate, changePct}` | Suggestion engine |
| `pricing.suggestion.accepted` | Info | `{suggestionId, listingId, userId}` | Tenant action |
| `pricing.suggestion.rejected` | Info | `{suggestionId, listingId, userId, reason}` | Tenant action |
| `pricing.suggestion.auto_applied` | Info | `{suggestionId, listingId, rateWritten}` | Auto-apply |
| `pricing.suggestion.expired` | Debug | `{suggestionId, listingId}` | Expiration |
| `pricing.suggestion.superseded` | Debug | `{oldSuggestionId, newSuggestionId, listingId}` | New suggestion |
| `pricing.guardrail.evaluated` | Debug | `{listingId, date, guardrailType, value, limit, passed}` | Guardrail check |
| `pricing.guardrail.blocked` | Warn | `{listingId, date, guardrailType, attemptedRate, limit}` | Block |
| `pricing.guardrail.overridden` | Warn | `{listingId, date, guardrailType, newRate, limit, userId}` | Host override |
| `pricing.circuit_breaker.tripped` | Error | `{autoAppliedCount, threshold, batchId}` | Circuit breaker |
| `pricing.config.changed` | Info | `{key, oldValue, newValue, changedBy}` | Admin config |
| `pricing.stale_data.skipped` | Warn | `{listingId, lastSnapshotAge}` | Stale snapshot |
| `pricing.validation.zero_base_rate` | Warn | `{listingId}` | Invalid base rate |
| `pricing.data.purged` | Info | `{entity, rowsDeleted, cutoffDate}` | Retention cleanup |
| `pricing.spike.detected` | Warn | `{listingId, oldRate, newRate, changePercent}` | Rate spike |
| `pricing.oscillation.detected` | Warn | `{listingId, changeCount7d}` | Oscillation |
| `pricing.aggressive_discount.detected` | Warn | `{listingId, rate, baseRate, discountPercent}` | Deep discount |

---

*End of RA-AI-001*
