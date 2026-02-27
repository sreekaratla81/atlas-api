# RA-CHX-002: OTA Mapping, Multi-Room Inventory & Rate Plan Requirements (Channex-Aligned)

**Addendum to:** [RA-IC-001](RA-IC-001-hybrid-ical-channex-sync.md) | [RA-IC-002](RA-IC-002-provider-switching-consistency.md) | [RA-001](RA-001-marketplace-commission-boost-ota-payments.md)

**Purpose:** Define the canonical mapping between Atlas PMS concepts and Channex/OTA concepts, rate plan system, restrictions engine, inventory management, ARI update rules, go-live validation, supportability requirements, acceptance criteria, and phased rollout — aligned with the Channex API (https://docs.channex.io/) and scoped for India 0–10 key hosts.

**Audience:** Developer, QA, Support, Platform Ops

**Owner:** Atlas Tech Solutions

**Last updated:** 2026-02-27

**Status:** Draft

---

## Table of contents

1. [OTA Mapping Model (Authoritative Rules)](#1-ota-mapping-model-authoritative-rules)
2. [Airbnb vs Booking.com vs Agoda — OTA-Specific Constraints](#2-airbnb-vs-bookingcom-vs-agoda--ota-specific-constraints)
3. [Rate Plan System Requirements](#3-rate-plan-system-requirements)
4. [Restrictions Engine Requirements](#4-restrictions-engine-requirements)
5. [Inventory Pools & Availability Rules](#5-inventory-pools--availability-rules)
6. [ARI Update Rules](#6-ari-update-rules)
7. [Mapping Validation + "Go Live" Checklist](#7-mapping-validation--go-live-checklist)
8. [Supportability Requirements](#8-supportability-requirements)
9. [Acceptance Criteria + Test Matrix](#9-acceptance-criteria--test-matrix)
10. [Rollout Plan (Lean V1)](#10-rollout-plan-lean-v1)

---

## 1. OTA mapping model (authoritative rules)

### 1.1 Concept mapping: Atlas ↔ Channex ↔ OTA

| Atlas concept | Atlas model | Channex concept | Channex API entity | Airbnb concept | Booking.com concept |
|---------------|------------|----------------|-------------------|---------------|-------------------|
| **Property** | `Property` | Property | `property` (UUID) | Listing group (host account level) | Hotel / Property |
| **Listing** (unit type) | `Listing` | Room Type | `room_type` (UUID) | Listing (single unit typical) | Room Type |
| **Unit count** | `Listing.UnitCount` (new) | `count_of_rooms` | `room_type.count_of_rooms` | Always 1 per listing | Count of physical rooms of this type |
| **Rate plan** | `RatePlan` (new) | Rate Plan | `rate_plan` (UUID) | Implicit (Airbnb handles pricing) | Rate plan (BAR, non-refundable, etc.) |
| **Derived rate** | `RatePlan` with `ParentRatePlanId` | Derived Rate Plan | `rate_plan` with `parent_rate_plan_id` | N/A | Derived rate (e.g., –10% non-refundable) |
| **Nightly rate** | `ListingDailyRate` / `ListingPricing` | Rate (per date) | ARI restriction: `rate` | Smart Pricing or manual | Rate per room per night |
| **Restriction** | `ListingRestriction` (new) | Restriction | ARI restriction fields | Limited (min stay only) | Full (MinLOS, CTA, CTD, stop-sell) |
| **Availability** | Computed from bookings + blocks | Availability (per room type per date) | ARI availability: integer | Binary (available / blocked) | Integer (rooms available) |
| **Booking** | `Booking` | Booking Revision | `booking_revision` | Reservation | Reservation |

### 1.2 Channex hierarchy (from Channex docs)

```
Property (hotel / vacation rental)
  └── Room Type (room category with count_of_rooms)
        ├── Rate Plan (pricing + restrictions)
        │     ├── Occupancy Options (per-room or per-person pricing)
        │     └── Derived Options (increase/decrease by amount/percent)
        └── Rate Plan (derived, with parent_rate_plan_id)
              └── Inherits restrictions + rates from parent
```

**Key Channex behaviours:**
- Availability is tracked at the **Room Type** level (integer count per date).
- Rates and restrictions are tracked at the **Rate Plan** level (per date).
- Rate plans are combined with room types to form "room rates" — the sellable product on OTAs.
- `sell_mode`: `per_room` (one price regardless of guest count) or `per_person` (price varies by occupancy).
- `rate_mode`: `manual` (explicitly set), `derived` (from parent), `auto` (computed from primary occupancy), `cascade` (derived per occupancy option from parent).

### 1.3 V1 supported mapping combinations

| Scenario | Atlas config | Channex mapping | V1 support |
|----------|-------------|----------------|:----------:|
| **Single listing, single unit** (Airbnb vacation rental) | 1 Property → 1 Listing (UnitCount=1) | 1 Property → 1 Room Type (count_of_rooms=1) → 1 Rate Plan | ✅ Primary |
| **Single listing, multi-unit** (3 identical rooms) | 1 Property → 1 Listing (UnitCount=3) | 1 Property → 1 Room Type (count_of_rooms=3) → 1 Rate Plan | ✅ Supported |
| **Multi-listing, single property** (2 room types: Deluxe + Standard) | 1 Property → 2 Listings | 1 Property → 2 Room Types → 1 Rate Plan each | ✅ Supported |
| **Multi-listing, multi-rate-plan** (Deluxe with BAR + non-refundable) | 1 Property → 1 Listing → 2 Rate Plans | 1 Property → 1 Room Type → 2 Rate Plans (one derived) | ✅ Supported |
| **Per-person pricing** | 1 Listing → 1 Rate Plan with per-person options | 1 Room Type → 1 Rate Plan (`sell_mode=per_person`) with occupancy options | ✅ Supported |

### 1.4 V1 explicitly unsupported combinations

| Scenario | Why unsupported | V2 migration path |
|----------|----------------|-------------------|
| **Multi-property in single Channex account with shared inventory** | Adds complexity for pool management. India 0–10 key hosts rarely need this. | Add `ChannexGroupId` concept. Allow shared inventory pools. |
| **Rate plans with `rate_mode=auto`** | Auto-rate requires complex occupancy-derived calculations. Manual and derived cover 95% of India hosts. | Implement auto_rate_settings mapping. |
| **Cascading derived rate plans (3+ levels deep)** | Parent → Child → Grandchild chains are error-prone. V1 limits to 1 level of derivation. | Add depth validation, allow 2-level chains. |
| **Dormitory room kind (`room_kind=dorm`)** | India homestay market is room/villa based. Dormitory adds bed-level inventory logic. | Add `room_kind` support, `capacity` field. |
| **Multi-currency per property** | India hosts price in INR. Mixed-currency adds exchange rate complexity. | Add `RatePlan.Currency` override, FX sync. |
| **Tax sets via Channex** | V1: taxes managed in Atlas pricing. Channex tax_set_id left null. | Map Atlas tax config to Channex tax sets. |

### 1.5 Atlas-to-Channex mapping storage

```
ChannexMapping {
    Id                  int PK
    TenantId            int FK → Tenant
    PropertyId          int FK → Property
    ListingId           int FK → Listing      -- nullable (property-level mapping)
    RatePlanId          int FK → RatePlan      -- nullable (listing-level mapping)
    
    ChannexEntityType   varchar(30)            -- 'property', 'room_type', 'rate_plan'
    ChannexEntityId     varchar(50)            -- Channex UUID
    
    MappingStatus       varchar(20)            -- 'active', 'pending', 'disconnected', 'error'
    LastSyncedAtUtc     datetime2?
    LastSyncError       varchar(500)?
    CreatedAtUtc        datetime2
    UpdatedAtUtc        datetime2
}
```

| ID | Requirement |
|----|-------------|
| MAP-01 | Every Atlas entity connected to Channex MUST have a `ChannexMapping` row linking it to the Channex UUID. |
| MAP-02 | `ChannexMapping` is the single source of truth for "what Atlas entity maps to what Channex entity". All sync operations resolve mapping from this table. |
| MAP-03 | Deleting a mapping in Atlas MUST NOT automatically delete the Channex entity (requires explicit disconnect flow to avoid breaking OTA connections). |
| MAP-04 | `ChannexEntityId` MUST be stored as the Channex-assigned UUID (e.g., `994d1375-dbbd-4072-8724-b2ab32ce781b`). Atlas MUST NOT generate its own UUIDs for Channex entities. |

---

## 2. Airbnb vs Booking.com vs Agoda — OTA-specific constraints

### 2.1 Airbnb constraints

| Aspect | Airbnb behaviour | Atlas enforcement |
|--------|-----------------|-------------------|
| **Listing model** | One listing = one unit. No "room type" concept. | `UnitCount` MUST be 1 for Airbnb-mapped listings. UI prevents setting > 1. |
| **Rate plans** | Airbnb manages its own pricing (Smart Pricing, base price). No rate plan concept exposed to channel managers. | Atlas pushes a single base rate. Channex maps to Airbnb's nightly price. Only 1 rate plan per Airbnb-connected room type. |
| **Restrictions** | Limited: min stay, max stay. No CTA/CTD/stop-sell via most channel managers. | UI disables CTA/CTD for Airbnb-only listings. Show tooltip: "Airbnb does not support Close to Arrival/Departure restrictions." |
| **Availability** | Binary: available (1) or blocked (0). | Availability pushed as 0 or 1 only for Airbnb room types. |
| **Children/infant pricing** | Airbnb includes in nightly rate. | `children_fee` and `infant_fee` set to 0.00 for Airbnb rate plans. |
| **Cancellation policy** | Managed on Airbnb, not via Channex. | Atlas does NOT push cancellation policies. Inform tenant: "Manage cancellation policy directly on Airbnb." |
| **Booking data** | Confirmation code, guest first name (last name partial), dates, amount, OTA commission. | Map `ota_reservation_code` → `Booking.ExternalReservationId`. |

### 2.2 Booking.com constraints

| Aspect | Booking.com behaviour | Atlas enforcement |
|--------|----------------------|-------------------|
| **Hotel ID** | Numeric. Required for Channex mapping. | Validate: numeric string, max 50 chars. |
| **Room types** | Full room-type model: Standard, Deluxe, etc. Multiple room types per hotel. `count_of_rooms` > 1 typical. | Atlas Listing maps 1:1 to Channex Room Type. UnitCount maps to `count_of_rooms`. |
| **Rate plans** | Multiple rate plans per room type. BAR (Best Available Rate) + non-refundable + breakfast included. | Atlas supports 1 base + N derived rate plans per listing. |
| **Restrictions** | Full: MinLOS (arrival + through), MaxLOS, CTA, CTD, stop-sell. Per day-of-week defaults. | All restriction types enabled for Booking.com listings. |
| **Availability** | Integer: count of available rooms per date per room type. | Availability = `UnitCount` – `bookedRooms` – `manualBlocks`. Push as integer. |
| **Children policy** | Age-based. `children_fee`, `infant_fee` per rate plan. | Map `Listing.ChildrenFee` → Channex `children_fee`. |
| **Cancellation policy** | Managed via Booking.com extranet or Channex channel settings. | Not pushed via ARI. Inform tenant: "Set cancellation policy in Booking.com extranet." |
| **Booking data** | Reservation ID, guest full name, credit card details (PCI), amount, OTA commission, taxes breakdown. | Map to `Booking` fields. Credit card: V1 not stored (PCI scope avoidance). |

### 2.3 Agoda constraints (high-level, V2 preparation)

| Aspect | Agoda behaviour | Atlas V1 impact |
|--------|----------------|-----------------|
| **Model** | Similar to Booking.com: property → room types → rate plans. | Same mapping structure works. |
| **Rate plans** | BAR + promotional rates. Agoda-specific derived rates. | V1: single base rate plan sufficient. V2: promotional rate support. |
| **Restrictions** | Standard set: MinLOS, CTA, CTD, stop-sell. | Same restriction engine works. |
| **Inventory** | Integer availability, shared with other OTAs via Channex. | Channex handles cross-OTA inventory distribution. |
| **V1 scope** | Not connected in V1. Data model compatible. | No Agoda-specific code. Channex handles Agoda channel. Atlas just maps correctly. |

| ID | Requirement |
|----|-------------|
| OTA-01 | UI MUST detect which OTAs are connected per property (from `ChannelConfig` data) and enforce OTA-specific constraints in real-time. |
| OTA-02 | When tenant connects Airbnb: `UnitCount` auto-set to 1 (read-only). CTA/CTD restrictions hidden. Single rate plan enforced. |
| OTA-03 | When tenant connects Booking.com: full restriction panel enabled. Multiple rate plans allowed. `UnitCount` editable (1–50). |
| OTA-04 | Before enabling sync for any OTA, system MUST run mapping validation (§7). Invalid configs MUST block sync activation. |
| OTA-05 | Tooltip/help text MUST explain OTA-specific limitations in tenant-facing language, not technical jargon. |

### 2.4 Preventing invalid configurations

| Invalid config | Detection | Prevention |
|----------------|-----------|------------|
| Airbnb listing with UnitCount > 1 | Validation on save | UI sets UnitCount=1 and locks field when Airbnb connected. API returns 422 if > 1. |
| Airbnb listing with CTA/CTD restriction | Validation on restriction save | API ignores CTA/CTD for Airbnb-only listings. UI hides fields. |
| Booking.com listing with no Hotel ID | Pre-flight check (§7) | Go-live validator blocks sync. Error: "Booking.com Hotel ID required." |
| Rate plan with 0 rate and no derived parent | Pre-flight check | Validator warns: "Rate is ₹0. Guests will see free listing." Require confirmation. |
| Room type mapped to Channex but 0 availability for all dates | Pre-flight check | Validator warns: "All dates show 0 availability. OTAs will show property as sold out." |
| Derived rate plan without parent rate plan | Validation on create | API returns 422: "Derived rate plan requires a parent rate plan." |
| Per-person rate plan with occupancy > room type's `occ_adults` | Validation on create | Channex rejects this. Atlas pre-validates: "Occupancy option cannot exceed room type max adults." |

---

## 3. Rate plan system requirements

### 3.1 Data model

```
RatePlan {
    Id                  int PK
    TenantId            int FK → Tenant
    ListingId           int FK → Listing
    
    Title               varchar(255)        -- "Best Available Rate", "Non-Refundable"
    SellMode            varchar(20)         -- 'per_room', 'per_person'
    RateMode            varchar(20)         -- 'manual', 'derived'
    Currency            varchar(3)          -- ISO 4217, default 'INR'
    
    ParentRatePlanId    int? FK → RatePlan  -- for derived plans
    
    ChildrenFee         decimal(18,2)       -- additional fee per child per night
    InfantFee           decimal(18,2)       -- additional fee per infant per night
    MealType            varchar(30)         -- 'none', 'breakfast', 'half_board', etc.
    
    IsActive            bit
    CreatedAtUtc        datetime2
    UpdatedAtUtc        datetime2
}

RatePlanOccupancyOption {
    Id                  int PK
    RatePlanId          int FK → RatePlan
    Occupancy           int                 -- guest count this option applies to
    IsPrimary           bit                 -- true for the base occupancy
    DefaultRate         decimal(18,2)       -- default nightly rate for this occupancy
    
    -- Derived option fields (only if parent rate plan exists)
    DerivedModifier     varchar(30)?        -- 'increase_by_amount', 'increase_by_percent', etc.
    DerivedValue        decimal(18,4)?      -- modifier value
}
```

### 3.2 V1 rate plan support

| Feature | V1 scope | Channex mapping |
|---------|:--------:|----------------|
| **Base rate plan** (per room type) | ✅ Required | `rate_plan` with `rate_mode=manual`, `sell_mode=per_room` |
| **Derived rate plan** (% or amount offset) | ✅ Supported | `rate_plan` with `parent_rate_plan_id`, `rate_mode=derived`, `derived_option` on occupancy options |
| **Per-room pricing** | ✅ Default | `sell_mode=per_room`, single occupancy option at max adults |
| **Per-person pricing** | ✅ Supported | `sell_mode=per_person`, one occupancy option per guest count |
| **Weekend pricing** | ✅ Supported | ARI update with `days: ["sa", "su"]` filter. Stored in `ListingDailyRate` with `DayOfWeek` tag. |
| **Seasonal pricing** | ✅ Supported | ARI date-range update. Atlas stores via `ListingPricingRule` with date ranges. |
| **Extra guest pricing** | ✅ Supported | Per-person sell mode with derived occupancy options. Or: `children_fee` / `infant_fee` on rate plan. |
| **Taxes/fees** | ⚠️ Partial | V1: Atlas computes guest-facing total (rate + tax). Channex `tax_set_id` not used. OTAs show Atlas-pushed rate as inclusive. V2: Map to Channex tax sets for OTA-specific tax display. |

### 3.3 Source of truth policy

| Data | Source of truth | Sync direction | Rationale |
|------|:--------------:|:--------------:|-----------|
| **Nightly rates** | Atlas DB (`ListingDailyRate`) | Atlas → Channex (push) | Tenant manages pricing in Atlas. Atlas is the rate master. |
| **Rate plan structure** (title, sell mode, occupancy options) | Atlas DB (`RatePlan`) | Atlas → Channex (push on create/update) | Atlas creates and manages rate plan config. Channex is the distribution layer. |
| **Derived rate rules** | Atlas DB (`RatePlanOccupancyOption.DerivedModifier`) | Atlas → Channex (push) | Atlas defines the derivation. Channex computes derived values. |
| **Restrictions** (MinLOS, CTA, CTD, stop-sell) | Atlas DB (`ListingRestriction`) | Atlas → Channex (push) | Tenant manages restrictions in Atlas. |
| **Availability** (rooms available per date) | Atlas DB (computed) | Atlas → Channex (push) | Atlas computes from bookings + blocks + UnitCount. |
| **Bookings** | OTA (via Channex) | Channex → Atlas (pull/webhook) | OTA is authoritative for OTA bookings. Atlas mirrors. |

| ID | Requirement |
|----|-------------|
| RAT-01 | Atlas is the **rate master**. Rates MUST flow Atlas → Channex only. If Channex shows a different rate (e.g., from OTA extranet override), daily reconciliation flags it as drift. |
| RAT-02 | Every listing with `SyncMode = channex_api` MUST have at least one `RatePlan` with `RateMode = manual` and `IsActive = true`. |
| RAT-03 | Derived rate plans MUST have exactly one `ParentRatePlanId`. Circular references MUST be prevented (validation on create/update). |
| RAT-04 | Derived rate plan depth MUST be limited to 1 level in V1 (parent → child only, no grandchild). |
| RAT-05 | When a parent rate plan's rate changes, derived rate plan rates are recomputed by Channex (via `inherit_rate = true`). Atlas does NOT need to push derived rates — only the parent rate. |

### 3.4 Validation rules

| Rule | Validation | Error message |
|------|-----------|---------------|
| Rate ≥ minimum floor | `DefaultRate >= 100` (INR) or configurable minimum | "Nightly rate must be at least ₹100." |
| Rate plan title unique per property | `UNIQUE(ListingId, Title)` across active plans | "A rate plan with this name already exists for this listing." |
| Occupancy ≤ room type max adults | `RatePlanOccupancyOption.Occupancy <= Listing.MaxGuests` | "Occupancy cannot exceed maximum guests ({MaxGuests})." |
| Derived plan has parent | If `RateMode = derived`: `ParentRatePlanId IS NOT NULL` | "Derived rate plan requires a parent." |
| No circular derivation | `ParentRatePlanId != Id` AND parent's `ParentRatePlanId IS NULL` (depth 1) | "Circular or multi-level derivation not supported." |
| Currency is INR (V1) | `Currency = 'INR'` for all V1 rate plans | "Only INR is supported in V1." |
| Contradictory rate plans | If two active per-room plans exist for same listing with same occupancy: warn | "Multiple active rate plans with same occupancy. Only one will be used per OTA mapping." |

---

## 4. Restrictions engine requirements

### 4.1 V1 restriction types

| Restriction | Channex field | Type | Default | Description |
|-------------|:------------:|:----:|:-------:|-------------|
| **Minimum stay (arrival)** | `min_stay_arrival` | int (1–30) | 1 | Guest must stay at least N nights if arriving on this date. |
| **Minimum stay (through)** | `min_stay_through` | int (1–30) | 1 | Any stay that includes this date must be at least N nights. |
| **Maximum stay** | `max_stay` | int (0–365) | 0 (unlimited) | Guest cannot stay more than N nights. 0 = no limit. |
| **Closed to arrival** | `closed_to_arrival` | bool | false | No check-in allowed on this date. |
| **Closed to departure** | `closed_to_departure` | bool | false | No check-out allowed on this date. |
| **Stop-sell** | `stop_sell` | bool | false | Date is completely closed. No bookings accepted. |

**Channex note:** Restriction defaults can be set as a single value (all days) or an array of 7 values (Sun–Sat) on the rate plan. Date-specific overrides are pushed via the ARI restrictions endpoint.

### 4.2 Storage model in Atlas

```
ListingRestriction {
    Id                  int PK
    TenantId            int FK → Tenant
    ListingId           int FK → Listing
    RatePlanId          int FK → RatePlan
    
    RuleType            varchar(30)         -- 'min_stay_arrival', 'min_stay_through', 
                                            -- 'max_stay', 'closed_to_arrival', 
                                            -- 'closed_to_departure', 'stop_sell'
    
    -- Scope: exactly one of these is set
    DateFrom            date?               -- specific date range
    DateTo              date?
    DayOfWeekPattern    varchar(14)?        -- e.g., 'sa,su' for weekends, null for all days
    
    IntValue            int?                -- for min_stay, max_stay
    BoolValue           bit?                -- for CTA, CTD, stop_sell
    
    Priority            int                 -- higher priority overrides lower. 
                                            -- Default=0 (rate plan default), 
                                            -- DateRange=10, DateSpecific=20
    
    IsActive            bit
    CreatedAtUtc        datetime2
    UpdatedAtUtc        datetime2
}
```

### 4.3 Mapping to Channex

| Atlas restriction | Channex rate plan default | Channex ARI per-date override |
|-------------------|:------------------------:|:----------------------------:|
| `min_stay_arrival` (DayOfWeek) | `rate_plan.min_stay_arrival = [1,1,1,1,1,2,2]` (weekday=1, weekend=2) | N/A (set on plan) |
| `min_stay_arrival` (DateRange) | N/A | `POST /restrictions` with `date_from`, `date_to`, `min_stay_arrival` |
| `stop_sell` (DateRange) | N/A | `POST /restrictions` with `date_from`, `date_to`, `stop_sell: true` |
| `closed_to_arrival` (DayOfWeek) | `rate_plan.closed_to_arrival = [false,false,false,false,false,false,true]` | N/A |

### 4.4 UI rules and templates

| Template | Description | Applied restrictions |
|----------|-------------|---------------------|
| **Weekend min 2 nights** | Require 2-night minimum for Friday and Saturday arrivals | `min_stay_arrival` with `DayOfWeekPattern = 'fr,sa'`, `IntValue = 2` |
| **Holiday block** | Close specific dates entirely | `stop_sell` with `DateFrom/DateTo`, `BoolValue = true` |
| **Peak season min 3 nights** | Longer minimum stay during peak dates | `min_stay_arrival` with `DateFrom/DateTo`, `IntValue = 3` |
| **No same-day checkout** | Prevent checkout on specific dates | `closed_to_departure` with `DateFrom/DateTo`, `BoolValue = true` |

| ID | Requirement |
|----|-------------|
| RST-01 | Admin portal MUST provide a restriction management UI per listing per rate plan: calendar view with restriction overlay. |
| RST-02 | Restriction templates MUST be selectable from a dropdown. Applying a template creates `ListingRestriction` rows. |
| RST-03 | When multiple restrictions overlap for the same date and type, highest `Priority` wins. If same priority: most specific scope wins (DateSpecific > DateRange > DayOfWeek > PlanDefault). |
| RST-04 | Restriction changes MUST trigger ARI push within 2 minutes (via outbox). |
| RST-05 | Airbnb-connected listings MUST hide CTA/CTD in the UI. If tenant later connects Booking.com: CTA/CTD become available. |

### 4.5 Conflict resolution

| Conflict | Resolution |
|----------|-----------|
| `stop_sell = true` AND `min_stay_arrival = 3` on same date | `stop_sell` takes precedence. Date is closed. MinLOS irrelevant. |
| `closed_to_arrival = true` AND `min_stay_arrival = 2` on same date | Both apply. No arrival on this date AND any stay including this date must be ≥ 2 nights. |
| Weekend template (min 2) AND peak season template (min 3) overlap on a Saturday | Higher value wins: `min_stay_arrival = 3`. Priority-based: peak season (Priority=10) > weekend (Priority=5). |
| `max_stay = 7` AND `min_stay_arrival = 10` on same date | Contradictory. Validation MUST warn: "Maximum stay (7) is less than minimum stay (10). No bookings possible for this date." Auto-set `stop_sell = true` or require tenant to fix. |

| ID | Requirement |
|----|-------------|
| RST-06 | System MUST detect contradictory restrictions (max_stay < min_stay) and warn tenant before saving. |
| RST-07 | If contradictory restrictions are saved despite warning, system MUST auto-set `stop_sell = true` for affected dates and log `restriction.auto_stop_sell` with `{listingId, dates, reason}`. |

---

## 5. Inventory pools & availability rules

### 5.1 Inventory model

| Field | Location | Description |
|-------|---------|-------------|
| `Listing.UnitCount` | New field on `Listing` | Total physical units of this listing type. Default: 1. Range: 1–50. |
| Computed availability (per date) | Derived | `UnitCount - bookedUnits - manualBlocks - safetyBuffer` |
| Safety buffer | `Listing.SafetyBufferUnits` (new, default 0) | Units held back from OTA availability. For hosts who want to keep 1 room for walk-ins. |

**Availability computation:**

```
availableUnits(listingId, date) =
    listing.UnitCount
    - COUNT(bookings WHERE listingId AND date BETWEEN checkin AND checkout-1 AND status IN ('Confirmed','CheckedIn'))
    - COUNT(availabilityBlocks WHERE listingId AND date BETWEEN startDate AND endDate-1 AND source != 'iCal_legacy')
    - listing.SafetyBufferUnits

result = MAX(availableUnits, 0)
```

### 5.2 Safety buffer

| Parameter | Default | Range | Purpose |
|-----------|:-------:|:-----:|---------|
| `SafetyBufferUnits` | 0 | 0 – `UnitCount-1` | Hosts can reserve N units from OTA sale. These units are only bookable via Atlas direct. |

| ID | Requirement |
|----|-------------|
| INV-01 | `Listing.UnitCount` MUST be a positive integer (1–50). Default: 1. |
| INV-02 | `SafetyBufferUnits` MUST be < `UnitCount`. Cannot equal UnitCount (would mean 0 OTA availability always). |
| INV-03 | For Airbnb-mapped listings: `UnitCount` MUST be 1 and `SafetyBufferUnits` MUST be 0 (binary availability). |
| INV-04 | Availability pushed to Channex MUST equal `MAX(UnitCount - booked - blocked - safetyBuffer, 0)`. Never negative. |
| INV-05 | Availability MUST be pushed at the **Room Type** level via `POST /api/v1/availability` (Channex). Per-rate-plan availability is derived by Channex from room-type availability + `availability_offset` / `max_availability` (V1: not used, default = room type availability). |

### 5.3 Overbooking prevention

| Scenario | Behaviour |
|----------|-----------|
| OTA sends booking when Atlas shows availability = 0 | Accept booking (OTA is authoritative for OTA bookings). Set availability to negative internally. Trigger CRITICAL alert: `inventory.overbooked` with `{listingId, date, expectedAvail, actualBookings}`. |
| Two OTA bookings arrive simultaneously for last room | Both accepted by Channex (Channex handles OTA-level overbooking prevention). If both propagate to Atlas: second creates an overbooking conflict. |
| Atlas direct booking + OTA booking race | Per-listing lock (RA-IC-002 §8.4 FLR-07) prevents race for Atlas-side. OTA booking always accepted. |

| ID | Requirement |
|----|-------------|
| INV-06 | Atlas MUST NEVER reject an OTA-sourced booking due to "no availability". OTAs are authoritative for their bookings. Overbooking is handled via conflict resolution, not rejection. |
| INV-07 | When overbooking is detected: (a) availability pushed to 0 for affected dates, (b) `SyncConflict` row created (RA-IC-001 §2.C), (c) CRITICAL alert sent to tenant + platform admin. |
| INV-08 | Atlas direct booking creation MUST check computed availability. If availability = 0: reject with "No rooms available for selected dates." (Atlas-side bookings respect inventory limits.) |

### 5.4 Cutover rules: iCal → Channex inventory

When switching from `ICAL_BASIC` to `CHANNEX_API` (per RA-IC-002 §1.A):

| Step | Inventory action |
|:----:|-----------------|
| 1 | Set `Listing.UnitCount` if not already set (default 1 for Airbnb-only hosts). |
| 2 | Count current bookings + blocks per date for next 365 days. |
| 3 | Compute availability per date. |
| 4 | Push full availability to Channex via `POST /api/v1/availability` with date range. |

| ID | Requirement |
|----|-------------|
| INV-09 | During iCal→Channex cutover, system MUST compute and push availability for ALL dates in the next 365 days in a single batched ARI call. |
| INV-10 | If any date has negative computed availability (overbooking inherited from iCal era): push 0 and create `SyncConflict`. |

---

## 6. ARI update rules

### 6.1 Trigger events

| Event | ARI update type | Content |
|-------|:---------------:|---------|
| Booking created (Atlas or OTA) | Availability | Decrement available rooms for booked dates. |
| Booking cancelled | Availability | Increment available rooms for cancelled dates. |
| Booking modified (date change) | Availability | Decrement new dates, increment old dates. |
| Manual block created | Availability | Decrement for blocked dates. |
| Manual block removed | Availability | Increment for unblocked dates. |
| Nightly rate changed | Rate | Push new rate for affected dates. |
| Restriction changed | Restriction | Push changed restriction values for affected dates. |
| Rate plan created/updated | Rate + Restriction | Full push for the new/updated rate plan (365 days). |
| Daily scheduled full sync | Availability + Rate + Restriction | Full reconciliation push for all active rate plans (next 90 days). |

### 6.2 Debounce rules

Rapid edits (e.g., tenant adjusting rates for 30 consecutive days one click at a time) should NOT produce 30 separate ARI calls.

| Rule | Implementation |
|------|---------------|
| **Per-property debounce** | After a rate/restriction change, wait 30 seconds before pushing. If another change arrives within 30 seconds: reset timer. Max wait: 60 seconds. |
| **Availability changes: no debounce** | Availability changes (booking created/cancelled) push immediately. Freshness is critical. |
| **Batch accumulation** | During the debounce window, accumulate all changes into a single ARI payload per property. |

| ID | Requirement |
|----|-------------|
| ARI-01 | Availability updates MUST push within 30 seconds of the triggering event (no debounce). |
| ARI-02 | Rate/restriction updates MUST push within 60 seconds of the last change (30s debounce + 30s max wait). |
| ARI-03 | All ARI changes MUST flow through the DB-backed outbox. Outbox worker processes per-property batches. |

### 6.3 Batching rules

| Batch type | Frequency | Content | Channex API call |
|-----------|:---------:|---------|-----------------|
| **Availability batch** | Per event (no debounce) | All affected dates for the property | `POST /api/v1/availability` with `values[]` array |
| **Rate batch** | After debounce (30–60s) | All changed rates per rate plan per date range | `POST /api/v1/restrictions` with `values[]` array |
| **Daily full sync** | 03:00 UTC daily | All availability (per room type, 90 days) + all rates/restrictions (per rate plan, 90 days) | 2 API calls per property: availability + restrictions |
| **Seasonal bulk update** | On tenant "Save season" | Rate + restrictions for the full season date range | Single `POST /api/v1/restrictions` with date range |

**Channex best practices alignment:**
- Channex recommends separating availability from rate/restriction updates (availability is prioritized in their queue).
- Channex processes messages FIFO. Batch into as few messages as possible.
- Max message size: 10 MB (not a concern for India 0–10 key hosts).

### 6.4 Idempotency keys

| ARI type | Idempotency strategy |
|----------|---------------------|
| Availability push | Last-write-wins at Channex. Pushing same availability twice is safe. Idempotent by nature. |
| Rate push | Last-write-wins. Safe to re-push. |
| Restriction push | Last-write-wins. Safe to re-push. |

Outbox deduplication:

| Field | Purpose |
|-------|---------|
| `OutboxMessage.IdempotencyKey` | `ari:{propertyId}:{type}:{ratePlanOrRoomTypeId}:{dateFrom}:{dateTo}:{hash}` |
| Dedup logic | If outbox worker finds a newer pending message with same `(propertyId, type, ratePlanOrRoomTypeId)`: skip the older one (superseded). |

| ID | Requirement |
|----|-------------|
| ARI-04 | Outbox worker MUST skip superseded messages: if a newer ARI message exists for the same property + entity + date range, the older message is skipped (logged as `ari.message.superseded`). |
| ARI-05 | ARI pushes are idempotent (last-write-wins on Channex). Retrying a failed push is always safe. |
| ARI-06 | Daily full sync MUST NOT be skippable. Even if no changes detected: push anyway (reconciliation). |

### 6.5 Retry policies

| Failure | Retry strategy | Max retries | Escalation |
|---------|:-------------:|:-----------:|-----------|
| Channex 429 (rate limit) | Wait `Retry-After` header (or 60s default) | 5 | After 5: alert platform admin. Message stays in outbox. |
| Channex 5xx (server error) | Exponential: 30s, 60s, 120s, 300s | 4 | After 4: alert. Retry on next daily sync. |
| Channex 422 (validation) | No retry (fix required) | 0 | Log `ari.push.validation_error` with Channex warning details. Alert tenant. |
| Network timeout (10s) | Retry immediately, then exponential | 3 | After 3: treat as 5xx escalation. |
| Channex 200 with warnings | Partial success. Warnings logged. | 0 (success) | Log all warnings. If warning is for a specific date: flag that date for re-push. |

| ID | Requirement |
|----|-------------|
| ARI-07 | Channex 422 responses MUST parse the `warnings` array and log each warning with `{ratePlanId, date, restriction, warning}`. |
| ARI-08 | Retry window MUST NOT exceed 2 hours. After 2 hours of continuous failure: mark outbox message as `Failed`. Daily sync will re-push. |

---

## 7. Mapping validation + "go live" checklist

### 7.1 Pre-flight validator

Before enabling Channex sync for a property, a **pre-flight validator** runs automatically. All checks must pass.

| # | Check | Severity | Pass condition | Failure message |
|:-:|-------|:--------:|---------------|-----------------|
| 1 | **Room types created** | BLOCKER | At least 1 listing with `ChannexMapping` of type `room_type` | "No room types mapped to Channex. Create at least one listing and map it." |
| 2 | **Room types mapped** | BLOCKER | Every active listing has a `room_type` `ChannexMapping` with `MappingStatus = 'active'` | "Listing '{name}' is not mapped to a Channex room type." |
| 3 | **Rate plan exists** | BLOCKER | At least 1 active `RatePlan` per mapped listing | "Listing '{name}' has no rate plan. Create at least one." |
| 4 | **Rate plan mapped** | BLOCKER | Every active rate plan has a `rate_plan` `ChannexMapping` | "Rate plan '{title}' is not mapped to Channex." |
| 5 | **Rate > 0** | WARNING | Every active rate plan has `DefaultRate > 0` on primary occupancy option | "Rate plan '{title}' has ₹0 rate. OTAs will show free listing." |
| 6 | **Inventory > 0** | WARNING | `Listing.UnitCount >= 1` | "Listing '{name}' has 0 units. No rooms available for sale." |
| 7 | **No restriction conflicts** | WARNING | No dates where `max_stay < min_stay_arrival` | "Restriction conflict on listing '{name}': max stay < min stay on {dates}." |
| 8 | **Test ARI push** | BLOCKER | `POST /api/v1/availability` with 1 date returns 200 | "Test availability push failed: {error}. Check Channex connection." |
| 9 | **Test rate push** | BLOCKER | `POST /api/v1/restrictions` with 1 date + rate returns 200 | "Test rate push failed: {error}. Check rate plan mapping." |
| 10 | **Booking revision feed accessible** | BLOCKER | `GET /api/v1/booking_revisions/feed` returns 200 | "Cannot access booking feed. Check Channex API key permissions." |
| 11 | **OTA-specific constraints met** | BLOCKER | Airbnb: UnitCount=1, single rate plan. Booking.com: Hotel ID present. | OTA-specific error message per §2.4. |

### 7.2 Validator execution

| Trigger | Behaviour |
|---------|-----------|
| Tenant clicks "Enable Channex Sync" | Validator runs. Results displayed inline. BLOCKERs prevent activation. WARNINGs require acknowledgement. |
| Admin triggers go-live from platform console | Same validator. Admin can override WARNINGs but not BLOCKERs. |
| Automated: daily health check | Validator runs for all connected properties. New BLOCKERs → alert (e.g., rate plan deleted while connected). |

| ID | Requirement |
|----|-------------|
| VAL-01 | Pre-flight validator MUST run synchronously when tenant clicks "Enable Sync". Results shown within 10 seconds. |
| VAL-02 | BLOCKER failures MUST prevent sync activation. No override for tenants. Platform admin override requires `atlas_super_admin` role. |
| VAL-03 | WARNING acknowledgements MUST be logged: `sync.golive.warning_acknowledged` with `{propertyId, warningCode, acknowledgedBy}`. |
| VAL-04 | Validator results MUST be logged: `sync.golive.validation` with `{propertyId, passed, blockerCount, warningCount, details[]}`. |

### 7.3 Rollback after go-live failure

If issues emerge within the first 30 minutes after go-live (per RA-IC-002 §3.1 Step 6):

| Condition | Action |
|-----------|--------|
| ARI push failure rate > 50% within first 15 min | Auto-disable sync. Revert `SyncState` to previous. Alert tenant + admin. |
| Booking revision feed returning errors | Auto-disable. Alert. |
| Overbooking detected within 30 min | Do NOT auto-disable (OTA bookings already accepted). Alert tenant for manual resolution. |

| ID | Requirement |
|----|-------------|
| VAL-05 | Post-go-live monitoring MUST run for 30 minutes (RA-IC-002 CUT-10). Auto-rollback on systematic failure. |
| VAL-06 | Rollback MUST restore previous sync state and preserve all data. Channex entities are NOT deleted on rollback. |

---

## 8. Supportability requirements

### 8.1 Sync health dashboard — mapping-specific fields

Supplements RA-IC-001 §6.1 with mapping-specific metrics:

| Metric | Source | Display |
|--------|--------|---------|
| Mapping completeness | `ChannexMapping` table | "3/3 listings mapped, 4/4 rate plans mapped" or "1 listing unmapped" (red) |
| Rate plan sync status | `ChannexMapping.LastSyncedAtUtc` for rate_plan entities | Per plan: "Synced 2 min ago" / "Error: {message}" |
| Last ARI push time | Structured log: `ari.push.completed` | Per property: relative time |
| ARI push success rate (24h) | Structured log aggregation | Percentage + trend |
| Restriction conflict count | Computed from `ListingRestriction` | Count (0 = green, > 0 = yellow) |
| Booking revision feed lag | `LastAcknowledgedRevisionAt` vs `LatestRevisionAt` | "0 unacknowledged" / "5 pending" |

### 8.2 Tenant-facing failure messages

| Technical error | Tenant message | Suggested action |
|----------------|---------------|-----------------|
| Channex 401 (API key invalid) | "Your channel manager connection has expired. Please reconnect." | "Go to Channels → Reconnect." |
| Channex 422 on rate push | "Rate update rejected: {parsed_warning}. Please check your pricing." | "Go to Pricing → {listing} and review." |
| Channex 429 (rate limit) | "Updates are queued due to high volume. They will be applied within 10 minutes." | No action needed. |
| Channex 5xx | "Channel manager is temporarily unavailable. Your updates are queued." | No action needed. Auto-retry. |
| Booking feed error | "We're having trouble receiving bookings from OTAs. Our team is investigating." | Contact support if bookings are missing. |
| Mapping validation failure | "Your channel setup is incomplete: {specific_issue}." | "Go to Channels → {property} → Fix issues." |
| Overbooking detected | "Double booking detected! {property} — {listing} is booked for {dates} on both Atlas and {OTA}." | "Resolve immediately in Channels → Conflicts." |
| Rate drift detected | "Prices on {OTA} don't match Atlas. We'll resend your correct prices." | Auto-resolved by daily sync. |

### 8.3 Debug bundle contents

Extends RA-IC-001 §6.5:

| Content | Source |
|---------|--------|
| All `ChannexMapping` rows for property | DB query |
| All `RatePlan` + `RatePlanOccupancyOption` rows | DB query |
| All `ListingRestriction` rows (active) | DB query |
| Last 20 ARI push log events | Structured log |
| Last 10 booking revision events | Structured log |
| Current computed availability (next 30 days) | Computed |
| Pre-flight validator result (latest) | Cached |
| Channex-side room type + rate plan state (API call) | Live `GET` to Channex |

### 8.4 Top 15 mapping failures and resolutions

| # | Failure | Root cause | Resolution |
|:-:|---------|-----------|------------|
| 1 | "Rate plan not found" on ARI push | Rate plan deleted in Channex but mapping still active in Atlas | Re-create rate plan in Channex via API. Update `ChannexMapping.ChannexEntityId`. |
| 2 | "Room type not found" | Room type deleted in Channex | Re-create room type. Run full mapping validation. |
| 3 | Rates showing ₹0 on OTA | Default rate not pushed after rate plan creation | Trigger manual ARI push: "Sync Rates Now" button. |
| 4 | Availability always 0 | UnitCount=0 or all dates blocked | Check `Listing.UnitCount`. Check for stale `AvailabilityBlock` rows. |
| 5 | "Occupancy exceeds room type" | Rate plan occupancy > room type `occ_adults` | Edit rate plan: reduce occupancy to match room type max adults. |
| 6 | Wrong prices on Booking.com | Rate push succeeded but OTA caches for up to 15 min | Wait. If persists > 30 min: check Channex dashboard for mapping issues. |
| 7 | "Stop sell active" but tenant didn't set it | Contradictory restriction auto-triggered `stop_sell` | Review restrictions for the affected dates. Fix max_stay < min_stay conflict. |
| 8 | Booking not appearing in Atlas | Booking revision not acknowledged. Feed polling lag. | Check booking revision feed. Trigger manual poll. |
| 9 | Duplicate booking from OTA | Channex sent same revision twice | System handles via idempotency (RA-IC-002 §7.1). Check `ConsumedWebhook`. |
| 10 | "Property not found" on Channex API call | API key scoped to different Channex account/group | Verify API key in Channex dashboard. Ensure it has access to the target property. |
| 11 | Rate plan mapped to wrong room type | Initial mapping error | Update `ChannexMapping` to correct `ChannexEntityId`. Re-push rates. |
| 12 | Availability shows 20 but only 2 rooms | `UnitCount` incorrectly set to 20 | Correct `UnitCount`. Push updated availability. |
| 13 | Restrictions not applying on OTA | Airbnb doesn't support CTA/CTD. Tenant set them anyway. | Inform tenant: "Airbnb doesn't support this restriction type." |
| 14 | "Currency mismatch" warning | Atlas pushing INR but Channex property set to USD | Align Channex property currency to INR. Or create rate plan with matching currency. |
| 15 | Derived rate showing wrong price | Derived modifier formula incorrect | Review `DerivedModifier` + `DerivedValue`. Test calculation. Re-push parent rate. |

---

## 9. Acceptance criteria + test matrix

### 9.1 Acceptance criteria (Given/When/Then)

#### AC-MAP-01: Single-unit Airbnb mapping

**Given** a property with 1 listing (`UnitCount=1`), 1 rate plan (`SellMode=per_room`, `RateMode=manual`, `DefaultRate=2500`), connected to Airbnb via Channex,
**When** the go-live validator runs and passes,
**Then:** Channex has 1 room type (`count_of_rooms=1`), 1 rate plan, availability pushed as 0 or 1 per date, rate pushed as 2500 for all future dates, and `ChannexMapping` rows exist for property, room type, and rate plan.

#### AC-MAP-02: Multi-unit same room type (inventory > 1)

**Given** a property with 1 listing (`UnitCount=3`, "Deluxe Room"), 1 rate plan, connected to Booking.com,
**When** 1 booking exists for March 10–12,
**Then:** availability pushed to Channex: March 10=2, March 11=2, all other dates=3. Room type has `count_of_rooms=3`.

#### AC-MAP-03: Multi-room-type mapping

**Given** a property with 2 listings ("Standard" `UnitCount=2` and "Deluxe" `UnitCount=1`), each with 1 rate plan,
**When** go-live validator runs,
**Then:** Channex has 2 room types and 2 rate plans. Availability pushed independently per room type. Rates pushed independently per rate plan.

#### AC-MAP-04: Derived rate plan update

**Given** a listing with 2 rate plans: "BAR" (manual, rate=3000) and "Non-Refundable" (derived, `decrease_by_percent=10`, `inherit_rate=true`),
**When** tenant changes BAR rate from 3000 to 3500,
**Then:** Atlas pushes 3500 to Channex for BAR rate plan. Channex auto-computes Non-Refundable as 3150 (3500 – 10%). Atlas does NOT push 3150 separately.

#### AC-MAP-05: Restriction overlap resolution

**Given** a listing with: weekend template (min_stay_arrival=2, Priority=5, DayOfWeek=fr,sa) and peak season rule (min_stay_arrival=3, Priority=10, DateFrom=Dec 20, DateTo=Jan 5),
**When** ARI push runs for Saturday December 28,
**Then:** min_stay_arrival=3 pushed (peak season Priority=10 beats weekend Priority=5).

#### AC-MAP-06: Inventory safety buffer

**Given** a listing with `UnitCount=5`, `SafetyBufferUnits=1`, 2 bookings on March 15,
**When** availability is computed and pushed,
**Then:** availability = 5 – 2 – 1 = 2 pushed to Channex for March 15.

#### AC-MAP-07: OTA cancellation updates availability

**Given** a listing with `UnitCount=3`, availability=0 on March 20 (3 bookings),
**When** 1 OTA booking is cancelled (via Channex booking revision with status=`cancelled`),
**Then:** availability recomputed as 1 and pushed to Channex within 30 seconds.

#### AC-MAP-08: Webhook duplicate handled

**Given** a booking revision with `system_id=rev-001` already processed and acknowledged,
**When** Channex sends the same revision again,
**Then:** system returns 200, does NOT create a duplicate booking, logs `channex.webhook.duplicate`.

#### AC-MAP-09: ARI push retry with eventual success

**Given** a rate update triggers ARI push that fails with Channex 503,
**When** retried after 30s, 60s, and succeeds on 3rd attempt,
**Then:** rate is pushed successfully. Outbox message marked completed. `ChannexMapping.LastSyncedAtUtc` updated. No alert fired.

#### AC-MAP-10: Validator blocks invalid go-live

**Given** a property with 1 listing mapped but 0 rate plans,
**When** tenant clicks "Enable Channex Sync",
**Then:** validator returns BLOCKER: "Listing '{name}' has no rate plan." Sync NOT activated. UI shows error with link to create rate plan.

### 9.2 Test matrix — edge cases

| # | Scenario | Expected behaviour | Priority |
|:-:|----------|-------------------|:--------:|
| 1 | **UnitCount changed from 3 to 2 while 3 bookings exist** | Overbooking detected. Availability pushed as 0 (not -1). Alert generated. | P1 |
| 2 | **Rate plan deleted while OTA connected** | Channex returns error on next ARI push. Alert tenant. Validator flags BLOCKER on next health check. | P1 |
| 3 | **Derived rate plan parent deleted** | Orphaned derived plan. Validation error on save. If already pushed: Channex shows stale derived rate until parent re-created. | P1 |
| 4 | **Tenant sets rate to ₹50 (below floor)** | Validation rejects: "Rate must be at least ₹100." | P2 |
| 5 | **100 dates of rates pushed in bulk (seasonal update)** | Single ARI message with `date_from` / `date_to`. Processed in < 5 seconds by Channex. | P1 |
| 6 | **Weekend + weekday rate push** | Two entries in `values[]`: weekday range with `days: ["mo","tu","we","th","fr"]` and weekend range with `days: ["sa","su"]`. Channex FIFO: weekend overwrites weekday for Sat/Sun. | P1 |
| 7 | **Stop-sell for Diwali (5 days)** | `stop_sell=true` pushed for date range. Availability also pushed as 0 for those dates. | P1 |
| 8 | **Booking arrives for stop-sold date** | Accept booking (OTA authoritative). Create conflict. Alert tenant. Remove stop-sell for booked dates (auto-heal). | P2 |
| 9 | **Per-person rate plan: 1 adult=2000, 2 adults=3000, 3 adults=3500** | Three occupancy options pushed. Channex maps to OTA per-person pricing. | P2 |
| 10 | **Channex returns 200 with warnings for 3 of 30 dates** | 27 dates succeed. 3 dates logged as warnings. Re-push those 3 on next cycle. | P1 |
| 11 | **Daily full sync detects rate drift** | Atlas rate=3000, Channex shows 2500 (manual override in Channex dashboard). Daily sync re-pushes 3000. Log `ari.drift.detected`. | P1 |
| 12 | **Mapping validation timeout (Channex slow)** | If validator calls exceed 10s: partial result returned. Blocking checks that timed out shown as "Could not verify. Please retry." | P2 |
| 13 | **Concurrent rate change and booking creation** | Rate change debounced. Booking triggers immediate availability push. Both succeed independently. | P1 |
| 14 | **Tenant switches listing from Airbnb to Booking.com** | UI unlocks multi-unit, CTA/CTD restrictions. Validator re-runs. Tenant must confirm new mapping. | P2 |
| 15 | **Zero-rate push intercepted** | If computed rate = 0 and rate plan has no derived modifier: block push. Log `ari.zero_rate.blocked`. Alert tenant. | P1 |

---

## 10. Rollout plan (lean V1)

### 10.1 Feature flags

| Flag | Type | Default | Description |
|------|:----:|:-------:|-------------|
| `CHANNEX_MAPPING_ENABLED` | boolean | `false` | Master switch for room type + rate plan mapping UI and API. |
| `CHANNEX_RATE_PLANS_ENABLED` | boolean | `false` | Enable rate plan creation and management in admin portal. |
| `CHANNEX_RESTRICTIONS_ENABLED` | boolean | `false` | Enable restriction management UI and ARI restriction push. |
| `CHANNEX_MULTI_UNIT_ENABLED` | boolean | `false` | Enable `UnitCount > 1` for listings. When false: all listings locked to UnitCount=1. |
| `CHANNEX_DERIVED_RATES_ENABLED` | boolean | `false` | Enable derived rate plan creation. When false: only manual rate plans. |
| `CHANNEX_ARI_DAILY_SYNC_ENABLED` | boolean | `false` | Enable daily 03:00 UTC full ARI reconciliation push. |
| `CHANNEX_GOLIVE_VALIDATOR_ENABLED` | boolean | `true` | Enable pre-flight validator. When false: go-live proceeds without checks (emergency override). |
| `CHANNEX_BOOKING_REVISION_FEED_ENABLED` | boolean | `false` | Enable polling Channex booking revision feed. V2 feature. |

### 10.2 Phased rollout

#### Phase 1: Mapping foundation (Week 1–2)

| Step | Action | Flags |
|:----:|--------|-------|
| 1.1 | Deploy `ChannexMapping`, `RatePlan`, `RatePlanOccupancyOption` tables | — |
| 1.2 | Deploy mapping CRUD API + admin portal UI (Channels → Property → Mapping tab) | `CHANNEX_MAPPING_ENABLED = true` |
| 1.3 | Internal testing: create room types + rate plans, verify Channex API calls | — |
| 1.4 | Deploy go-live validator | `CHANNEX_GOLIVE_VALIDATOR_ENABLED = true` |

#### Phase 2: Rate plans + ARI push (Week 3–4)

| Step | Action | Flags |
|:----:|--------|-------|
| 2.1 | Deploy rate plan management UI | `CHANNEX_RATE_PLANS_ENABLED = true` |
| 2.2 | Deploy ARI push worker (availability + rates) | — |
| 2.3 | Deploy rate debounce + batching logic | — |
| 2.4 | Internal testing: rate changes propagate to Channex within 60s | — |
| 2.5 | Deploy daily full sync | `CHANNEX_ARI_DAILY_SYNC_ENABLED = true` |

#### Phase 3: Restrictions + multi-unit (Week 5–6)

| Step | Action | Flags |
|:----:|--------|-------|
| 3.1 | Deploy `ListingRestriction` table + restriction management UI | `CHANNEX_RESTRICTIONS_ENABLED = true` |
| 3.2 | Deploy restriction templates (weekend min stay, holiday block) | — |
| 3.3 | Enable multi-unit inventory | `CHANNEX_MULTI_UNIT_ENABLED = true` |
| 3.4 | Pilot with 3 beta tenants (1 Airbnb-only, 1 Booking.com, 1 multi-OTA) | — |

#### Phase 4: Derived rates + booking ingestion (Week 7–8)

| Step | Action | Flags |
|:----:|--------|-------|
| 4.1 | Deploy derived rate plan support | `CHANNEX_DERIVED_RATES_ENABLED = true` |
| 4.2 | Deploy booking revision feed polling | `CHANNEX_BOOKING_REVISION_FEED_ENABLED = true` |
| 4.3 | End-to-end testing: rate change → OTA update → booking from OTA → Atlas | — |
| 4.4 | GA for all Premium tenants | All flags enabled |

### 10.3 Beta tenant criteria

| Criterion | Rationale |
|-----------|-----------|
| Premium plan, active subscription | Channex requires Premium. |
| 1–3 properties, 1–5 listings | Small enough for manual support. |
| At least 1 OTA connected (Airbnb or Booking.com) | Need real OTA traffic to validate. |
| Responsive to support queries (WhatsApp) | Beta requires quick feedback loops. |
| Mix of: 1 Airbnb-only, 1 Booking.com, 1 multi-OTA | Cover all mapping scenarios. |

### 10.4 Success metrics

| Metric | Target (3 months post-GA) | Measurement |
|--------|:-------------------------:|-------------|
| Support tickets per onboarded Channex tenant | < 2 tickets in first 30 days | Support queue tagged `channex_mapping` |
| Sync success rate (ARI push) | > 99% | `successfulPushes / totalPushes` |
| Booking ingestion success rate | > 99.5% | `processedRevisions / receivedRevisions` |
| Mean time from rate change to OTA update | < 2 minutes (p95) | `ariPushCompletedAt - rateChangedAt` |
| Overbooking incidents | < 1 per 100 property-months | `SyncConflict WHERE type = 'overbooking'` |
| Go-live validator pass rate on first attempt | > 70% | First validator run passes without BLOCKERs |
| Tenant self-service completion (no support needed) | > 80% of onboardings | Onboarding wizard completed without support ticket |

### 10.5 Gradual enablement per OTA

| OTA | V1 phase | V1 scope |
|-----|:--------:|----------|
| **Airbnb** | Phase 1+ | Single-unit, single rate plan, limited restrictions (min/max stay). Primary test OTA. |
| **Booking.com** | Phase 2+ | Multi-unit, multiple rate plans, full restrictions. Full feature set. |
| **Agoda** | V2 | Not actively tested in V1. Data model compatible. Channex handles Agoda channel — Atlas just needs correct mapping. |
| **Other OTAs** | V2+ | Channex supports 200+ OTAs. Atlas's provider-agnostic mapping model (via Channex) should work without Atlas-side changes. |

---

## Appendix A: Requirement ID index

| Prefix | Domain | Count |
|--------|--------|:-----:|
| MAP- | OTA mapping model | 4 |
| OTA- | OTA-specific constraints | 5 |
| RAT- | Rate plan system | 5 |
| RST- | Restrictions engine | 7 |
| INV- | Inventory & availability | 10 |
| ARI- | ARI update rules | 8 |
| VAL- | Go-live validation | 6 |
| **Total** | | **45** |

## Appendix B: Channex API endpoint reference

| Operation | Channex endpoint | Atlas usage |
|-----------|-----------------|-------------|
| List room types | `GET /api/v1/room_types?filter[property_id]=X` | Mapping screen: "Reload Inventory" |
| Create room type | `POST /api/v1/room_types` | Auto-create when tenant maps listing |
| Update room type | `PUT /api/v1/room_types/:id` | Sync listing changes (title, occupancy) |
| Delete room type | `DELETE /api/v1/room_types/:id?force=true` | Disconnect listing (with confirmation) |
| List rate plans | `GET /api/v1/rate_plans?filter[property_id]=X` | Mapping screen |
| Create rate plan | `POST /api/v1/rate_plans` | Auto-create when tenant creates rate plan |
| Update rate plan | `PUT /api/v1/rate_plans/:id` | Sync rate plan config changes |
| Delete rate plan | `DELETE /api/v1/rate_plans/:id?force=true` | Disconnect rate plan (with confirmation) |
| Get availability (room type) | `GET /api/v1/availability?filter[property_id]=X&filter[date][gte]=Y&filter[date][lte]=Z` | Reconciliation: compare Atlas vs Channex |
| Update availability | `POST /api/v1/availability` | Push availability on booking/block change |
| Get restrictions (rate plan) | `GET /api/v1/restrictions?filter[property_id]=X&filter[date][gte]=Y&filter[date][lte]=Z&filter[restrictions]=rate,min_stay_arrival,...` | Reconciliation: compare Atlas vs Channex |
| Update restrictions | `POST /api/v1/restrictions` | Push rates + restrictions on change |
| Booking revision feed | `GET /api/v1/booking_revisions/feed` | Poll for new OTA bookings |
| Acknowledge revision | `POST /api/v1/booking_revisions/:id/ack` | Mark revision as processed |

## Appendix C: Cross-reference to existing docs

| Topic | This doc section | Other doc | Relationship |
|-------|:----------------:|-----------|:------------:|
| Channex connection lifecycle | §1.5 (mapping storage) | RA-IC-001 §3.1 | Extends: mapping adds room type + rate plan level |
| Provider switching | §5.4 (iCal→Channex cutover) | RA-IC-002 §1.A, §3 | Aligns: inventory push during cutover |
| Conflict detection | §5.3 (overbooking) | RA-IC-001 §2.C | Extends: inventory-aware conflict detection |
| ARI push retry | §6.5 | RA-002 §7.7 | Aligns: same backoff model |
| Sync health dashboard | §8.1 | RA-IC-001 §6.1, RA-006 §4 | Extends: mapping-specific metrics |
| Go-live validation | §7 | RA-IC-002 §3 (cutover Step 6) | Extends: pre-flight validator + post-go-live monitoring |
| Booking ingestion | §9.1 AC-MAP-08 | RA-IC-001 §3.4, RA-IC-002 §7.1 | Aligns: webhook idempotency |
| Feature flags | §10.1 | RA-IC-001 §10.1 | Extends: mapping-specific flags |
| Daily reconciliation | §6.1 (daily full sync) | RA-IC-001 §3.6 (CHX-17) | Extends: ARI-level reconciliation |
| Existing codebase models | §1.1 | `Listing.cs`, `Property.cs`, `ChannelConfig.cs` | Extends: new `RatePlan`, `ListingRestriction`, `ChannexMapping` tables |
