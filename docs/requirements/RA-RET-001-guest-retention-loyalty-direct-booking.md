# RA-RET-001 — Guest Retention, Loyalty & Direct Booking Flywheel Requirements

| Field | Value |
|-------|-------|
| **Doc ID** | RA-RET-001 |
| **Title** | Guest Retention, Loyalty & Direct Booking Flywheel |
| **Status** | DRAFT |
| **Author** | Atlas Architecture |
| **Created** | 2026-02-27 |
| **Depends on** | RA-GUEST-001 (Guest Experience Platform), RA-AUTO-001 (Automation Engine), RA-DATA-001 (Data Platform), RA-AI-001 (Pricing Intelligence) |
| **Consumers** | Tenant Hosts, Guests, Atlas Admin, Marketplace Engine |

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Guest Identity Model](#2-guest-identity-model)
3. [Repeat Booking Detection Engine](#3-repeat-booking-detection-engine)
4. [Direct Rebooking Engine](#4-direct-rebooking-engine)
5. [Coupon & Incentive Engine](#5-coupon--incentive-engine)
6. [Referral System (Lean V1)](#6-referral-system-lean-v1)
7. [WhatsApp Retention Campaign Engine](#7-whatsapp-retention-campaign-engine)
8. [Loyalty Scoring Model (Lean V1)](#8-loyalty-scoring-model-lean-v1)
9. [LTV & Retention Analytics](#9-ltv--retention-analytics)
10. [Integration with Marketplace Ranking](#10-integration-with-marketplace-ranking)
11. [Abuse & Fraud Guardrails](#11-abuse--fraud-guardrails)
12. [Data Model (LLR)](#12-data-model-llr)
13. [Acceptance Criteria & Test Matrix](#13-acceptance-criteria--test-matrix)
14. [Definition of Done — Retention Engine V1](#14-definition-of-done--retention-engine-v1)

**Appendix A** — [Event Types (Retention Domain)](#appendix-a--event-types-retention-domain)
**Appendix B** — [Cross-Reference to RA-GUEST-001 Entities](#appendix-b--cross-reference-to-ra-guest-001-entities)

---

## 1. Executive Summary

Every guest who books through an OTA costs the host 15–25 % in commission. Atlas's survival as a marketplace depends on converting OTA-sourced guests into **direct repeat bookers** — a flywheel where better guest experiences, automatic incentives, and WhatsApp-native re-engagement steadily shift revenue from OTAs to the host's own channel.

This addendum defines the retention engine that powers that flywheel: guest identity resolution, repeat detection, coupon and referral mechanics, WhatsApp campaigns, a simple loyalty model, and the analytics to prove it is working — all built lean on the existing DB-backed outbox infrastructure and scaling to 100k tenants.

### 1.1 The Flywheel

```
                     ┌─────────────────────────────────┐
                     │                                 │
  OTA Booking ───►  Great Stay  ───► Post-Stay Nudge ──┤
                     │                                 │
                     │   ┌── Coupon ──────────┐        │
                     │   │   Referral ─────── │ ───►  Direct Rebooking
                     │   │   Re-engagement ── │        │
                     │   └────────────────────┘        │
                     │                                 │
                     └──── Loyalty Tier Upgrade ◄──────┘
```

### 1.2 V1 vs V2 Scope

| Capability | V1 (Rule-based, lean) | V2 (AI-driven) |
|------------|----------------------|-----------------|
| Guest identity | Phone/email dedup within tenant | Cross-tenant marketplace identity, fuzzy match |
| Repeat detection | Exact match on phone or email | ML-based probabilistic matching |
| Coupons | Flat / percent discount, auto-generated | Dynamic personalized offers based on price sensitivity |
| Referrals | Simple code + tracking | Network analysis, influencer detection |
| Campaigns | Template-based, segment-triggered | AI-optimized send time, message personalization |
| Loyalty | Formula-based tiers (Bronze/Silver/Gold) | Behavioural tiers, predictive churn, gamification |
| Analytics | Pre-computed snapshot metrics | Real-time cohort analysis, LTV prediction |

---

## 2. Guest Identity Model

### 2.1 Guest Profile Creation Rules

The existing `Guest` entity (Id, TenantId, Name, Phone, Email, IdProofUrl) is the canonical guest record. Guests are **tenant-scoped** via `ITenantOwnedEntity` — each tenant has its own guest pool.

| Source | Creation Trigger | Data Available | Identity Key |
|--------|-----------------|----------------|-------------|
| **Direct booking** | Guest fills booking form on tenant portal | Name, Phone, Email (all required) | Phone + Email |
| **OTA booking** (Channex sync) | `booking.created` from Channex import | Name, Phone or Email (one may be masked by OTA) | Best available: Phone → Email → ExternalReservationId |
| **OTA booking** (iCal sync) | iCal import | Name only (typically) | Name hash + source + reservation UID |
| **Inquiry / Lead** | Guest fills inquiry form | Name, Phone, Email | Phone + Email |
| **Walk-in** | Staff creates booking manually | Name, Phone (minimum) | Phone |

> **RET-ID-001**: Phone number is the **primary identity key** for Indian hospitality. Every guest MUST have a phone number. Email is secondary. For OTA bookings where the OTA masks the real phone (e.g., Airbnb relay), the system stores the relay number and attempts resolution on check-in.

### 2.2 Unique Identification Strategy

| Priority | Matching Rule | Confidence |
|----------|--------------|-----------|
| 1 | **Exact phone match** (normalized to E.164) within tenant | High |
| 2 | **Exact email match** (lowercased, trimmed) within tenant | High |
| 3 | **Phone + Name fuzzy match** (same phone, name similarity > 85% Levenshtein) | Medium |
| 4 | **Email + Name fuzzy match** (same email, name similarity > 85%) | Medium |
| 5 | **Hashed combination** `SHA256(NormalizedPhone + LowercaseEmail)` for cross-booking correlation | Reference only (V2: cross-tenant marketplace identity) |

**Phone normalization rules:**
- Strip all spaces, dashes, parentheses.
- Prefix `+91` if 10-digit Indian number without country code.
- Store in E.164 format: `+919876543210`.
- Match ignores the `+91` prefix for comparison (handles `09876543210` = `+919876543210`).

**Email normalization rules:**
- Lowercase.
- Trim whitespace.
- Strip Gmail dots (V2: `john.doe@gmail.com` = `johndoe@gmail.com`).

### 2.3 Duplicate Merge Rules

When a new booking is created and the guest matches an existing guest record:

| Scenario | Action |
|----------|--------|
| Exact phone match, same tenant | Reuse existing `Guest.Id`; update Name/Email if new data is richer |
| Exact email match, no phone match, same tenant | Reuse existing `Guest.Id`; update Phone if provided |
| No match | Create new `Guest` record |
| Multiple matches (phone matches one, email matches another) | **V1**: Use phone-matched record (phone is primary key). Log conflict for manual review |
| OTA masked phone, real phone obtained at check-in | Update `Guest.Phone` to real number; check for merge with existing guest |

> **RET-ID-002**: Merge is **always within the same tenant** in V1. Cross-tenant guest sharing is NOT supported. Each tenant maintains its own guest pool. The marketplace may hold a separate anonymized identity index in V2.

**Merge audit:**

Every merge operation creates an `AuditLog` entry with:
```json
{
  "action": "guest.merge",
  "entityType": "Guest",
  "entityId": "survivingGuestId",
  "payload": {
    "mergedGuestId": 456,
    "survivingGuestId": 123,
    "matchType": "PHONE_EXACT",
    "fieldsUpdated": ["Email", "Name"]
  }
}
```

### 2.4 Guest Lifetime Value Tracking

LTV is computed at query time from `Booking` history (not stored on `Guest`). See RA-GUEST-001 §7.5 for computation formulas. This document adds a **periodic snapshot** for trend analysis:

| Metric | Source | Frequency |
|--------|--------|-----------|
| Total revenue | `SUM(Booking.FinalAmount)` completed bookings | Snapshotted monthly |
| Total stays | `COUNT(Booking)` completed | Snapshotted monthly |
| Average booking value | Total revenue / total stays | Computed |
| First booking date | `MIN(Booking.CheckinDate)` | Static after first booking |
| Last booking date | `MAX(Booking.CheckinDate)` | Updated on each booking |
| Days since last stay | `DATEDIFF(day, last checkout, today)` | Computed |
| Direct booking ratio | Direct bookings / total bookings | Snapshotted monthly |

> **RET-ID-003**: `GuestLTVSnapshot` is a monthly snapshot table populated by a scheduled job. See §12.1.9 for schema.

### 2.5 Cross-Property Guest Reuse

| Rule | V1 | V2 |
|------|----|----|
| Same tenant, different property | Guest record is shared. Booking history spans all properties | Same |
| Different tenant | Completely isolated. No sharing | Marketplace guest identity (anonymized) |

### 2.6 Data Privacy Guardrails

| Guardrail | Implementation |
|-----------|---------------|
| Tenant isolation | `ITenantOwnedEntity` global query filter on `Guest` |
| PII access | Only `manager`, `owner` roles can view full guest profile |
| Phone masking in logs | `AuditLog.PayloadJson` stores last 4 digits only |
| No ID storage in V1 | `Guest.IdProofUrl` is unused in V1; no upload endpoint exposed |
| Consent tracking | `GuestConsent` (RA-GUEST-001 §10.7) checked before any outbound message |
| Data export | `GET /api/guests/{id}/export` returns full guest data (RA-GUEST-001 §10.4) |
| Right to erasure | Guest anonymization endpoint (V2 full DPDP compliance) |

---

## 3. Repeat Booking Detection Engine

### 3.1 Detection Logic

When `booking.confirmed` fires for a new booking:

```
1. Extract guest phone (normalized) and email (normalized)
2. Query Guest table:  WHERE TenantId = @tid AND (Phone = @phone OR Email = @email)
3. If match found:
   a. Count completed bookings for matched GuestId
   b. Apply tag based on count (see §3.2)
   c. Set Booking.GuestId = matched Guest.Id (reuse record)
4. If no match: create new Guest; tag as FIRST_TIME
```

**Detection timing:**
- **At booking creation**: Primary detection point. Guest identity resolved before booking record is saved.
- **At check-in**: Secondary. If guest provides real phone (replacing OTA relay), re-run detection. Merge if existing guest found.

### 3.2 Guest Tags

Tags are stored in `GuestTag` (defined in RA-GUEST-001 §7.4). This document defines additional retention-specific tags:

| Tag | Code | Condition | Applied When | Auto-Removed? |
|-----|------|-----------|-------------|---------------|
| **First-Time Guest** | `FIRST_TIME` | 0 prior completed stays with this tenant | Booking creation | Yes — removed when 2nd booking confirmed |
| **Returning Guest** | `RETURNING` | ≥ 1 prior completed stay | 2nd booking confirmed | Never |
| **VIP** | `VIP` | ≥ 5 completed stays OR total revenue > ₹50,000 | On qualifying checkout | Never |
| **High LTV** | `HIGH_LTV` | Total revenue in top 10% of tenant's guests | Monthly LTV snapshot job | Yes — recalculated monthly |
| **Direct Booker** | `DIRECT_BOOKER` | ≥ 1 booking with `BookingSource = 'Direct'` | Booking confirmation | Never |
| **OTA Converted** | `OTA_CONVERTED` | First booking was OTA, any later booking is Direct | Direct booking confirmation | Never |
| **Loyalty Bronze** | `LOYALTY_BRONZE` | LoyaltyScore ≥ 10 | Loyalty recalculation | Yes — replaced by higher tier |
| **Loyalty Silver** | `LOYALTY_SILVER` | LoyaltyScore ≥ 30 | Loyalty recalculation | Yes — replaced by higher tier |
| **Loyalty Gold** | `LOYALTY_GOLD` | LoyaltyScore ≥ 60 | Loyalty recalculation | Yes — replaced by higher tier |
| **Churning** | `CHURNING` | No booking in 9+ months despite ≥ 2 prior stays | Monthly retention job | Yes — removed on new booking |
| **Referred** | `REFERRED` | Guest booked via a referral link | Booking confirmation | Never |
| **Referrer** | `REFERRER` | Guest has ≥ 1 successful referral | Referral conversion | Never |

> **RET-TAG-001**: Tag updates are **idempotent** — applying a tag that already exists is a no-op. Tag changes are logged in `AuditLog` with `EntityType = 'GuestTag'`.

### 3.3 Tag Update Timing

| Trigger Event | Tags Evaluated |
|---------------|---------------|
| `booking.confirmed` | `FIRST_TIME`, `RETURNING`, `DIRECT_BOOKER`, `OTA_CONVERTED`, `REFERRED` |
| `stay.checked_out` | `VIP` (stay count + revenue check) |
| Monthly `LoyaltyRecalcJob` | `LOYALTY_BRONZE/SILVER/GOLD`, `HIGH_LTV`, `CHURNING` |
| Referral conversion | `REFERRER` (on referrer), `REFERRED` (on referee) |

### 3.4 Audit Logging

Every tag change produces an `AuditLog` entry:

```json
{
  "action": "guest_tag.applied",
  "entityType": "GuestTag",
  "entityId": "guestId:123",
  "payload": {
    "tag": "RETURNING",
    "previousTag": "FIRST_TIME",
    "triggerEvent": "booking.confirmed",
    "bookingId": 456
  }
}
```

---

## 4. Direct Rebooking Engine

### 4.1 "Book Again" Link

After checkout, the guest receives a **personalized rebooking link** via the guest portal and WhatsApp messages:

```
https://{{tenant_slug}}.atlashomestays.com/rebook?g={{guest_hash}}&p={{property_id}}&src=retention
```

| Parameter | Purpose |
|-----------|---------|
| `g` (guest hash) | Pre-fills guest name, phone, email on booking form (no re-entry needed) |
| `p` (property ID) | Pre-selects the property they last stayed at |
| `src=retention` | Tracks that this booking came from the retention flow |

> **RET-RB-001**: The `guest_hash` is a short-lived, HMAC-signed token (not the raw guest ID). It expires after 180 days. The booking form decrypts it server-side to pre-fill guest details. This prevents manual URL manipulation.

### 4.2 Personalized Reminders

| Reminder Type | Trigger | Template |
|---------------|---------|----------|
| **Anniversary reminder** | Same month as previous stay, next year | "It's been a year since you stayed at {{property_name}}! Ready for round two? Book direct and save: {{rebook_link}}" |
| **Seasonal reminder** | Host configures season (e.g., "Summer in Goa") | "{{property_name}} is ready for summer! Book your stay: {{rebook_link}}" |
| **Price drop alert** | V2: Rate drops below guest's last booking rate | "Great news! Rates at {{property_name}} have dropped. Book now from ₹{{rate}}/night: {{rebook_link}}" |

> **RET-RB-002**: Anniversary reminders are created as `AutomationSchedule` entries at checkout time with `DueAtUtc` = same month next year. Cancelled if guest re-books before the schedule fires.

### 4.3 Deep-Link Generation

| Component | Logic |
|-----------|-------|
| **Base URL** | `{{tenant_slug}}.atlashomestays.com/rebook` or `Tenant.CustomDomain/rebook` |
| **Guest hash** | `HMAC-SHA256(GuestId + TenantId, serverSecret)` truncated to 16 hex chars |
| **Property ID** | Last-stayed property; or omitted for multi-property tenant portal |
| **Coupon code** | If active coupon exists, auto-appended: `&coupon={{code}}` |
| **Referral code** | If guest has referral code: `&ref={{referral_code}}` |
| **UTM tracking** | `&utm_source=whatsapp&utm_medium=retention&utm_campaign={{campaign_id}}` |

### 4.4 Expiry Rules

| Item | Expiry | After Expiry |
|------|--------|-------------|
| Rebook guest hash | 180 days from generation | Link shows generic booking form (no pre-fill) |
| Coupon code | Configurable, default 180 days | `GuestCoupon.Status = 'EXPIRED'` |
| Referral code | No expiry (permanent per guest) | N/A |

### 4.5 Abuse Prevention

| Risk | Mitigation |
|------|-----------|
| Guest shares rebook link (pre-filled data exposure) | Hash only pre-fills on server-side; no PII in URL. Form requires OTP verification on phone |
| Automated link generation/scraping | HMAC-signed with server secret; rate-limited to 10 link generations per guest per day |
| Coupon stacking via multiple rebook links | Only one active coupon per guest per tenant (see §5) |

### 4.6 Rate Floor Enforcement

> **RET-RB-003**: Direct rebooking discounts (coupons) MUST NOT reduce the effective rate below the **rate floor** defined in RA-AI-001 pricing guardrails. If a coupon would breach the floor, the discount is capped at `BookingAmount - RateFloor`. The guest sees the adjusted discount.

---

## 5. Coupon & Incentive Engine

### 5.1 Coupon Types

The `GuestCoupon` entity is defined in RA-GUEST-001 §7.3. This section extends the model with retention-specific coupon types and rules.

| Type | Code | Description | Example |
|------|------|-------------|---------|
| **Flat discount** | `FLAT` | Fixed INR amount off | ₹500 off |
| **Percentage discount** | `PERCENT` | Percentage off final amount | 10% off |
| **Free add-on** | `FREE_ADDON` | Complimentary add-on service | Free late checkout |
| **Early check-in free** | `FREE_EARLY_CHECKIN` | Waive early check-in fee | — |
| **Late checkout free** | `FREE_LATE_CHECKOUT` | Waive late checkout fee | — |
| **Free night** | `FREE_NIGHT` | One night free on multi-night stay | Book 3 get 1 free |

> **RET-CPN-001**: `FREE_ADDON`, `FREE_EARLY_CHECKIN`, `FREE_LATE_CHECKOUT`, and `FREE_NIGHT` types are flagged on the booking and fulfilled manually by the host in V1. Automated fulfillment in V2.

**Extended `GuestCoupon` fields** (additions to RA-GUEST-001 schema):

| Field | Type | Description |
|-------|------|-------------|
| `CouponType` | `varchar(25)` | `FLAT`, `PERCENT`, `FREE_ADDON`, `FREE_EARLY_CHECKIN`, `FREE_LATE_CHECKOUT`, `FREE_NIGHT` |
| `MinNights` | `int?` | Minimum stay length to apply |
| `BlackoutDatesJson` | `nvarchar(max)` | JSON array of date ranges where coupon cannot be used |
| `PropertyId` | `int?` | NULL = any property; set = specific property only |
| `CampaignId` | `int?` | If coupon was issued as part of a campaign |
| `Source` | `varchar(20)` | `POST_STAY`, `REFERRAL`, `CAMPAIGN`, `MANUAL`, `LOYALTY` |

### 5.2 Coupon Rules

| Rule ID | Rule | Default | Configurable? |
|---------|------|---------|---------------|
| **RET-CPN-R01** | Only for guests with ≥ 1 completed stay (returning) | Yes | Per tenant: allow for first-time too |
| **RET-CPN-R02** | Valid for X days from issuance | 180 days | Per tenant (30–365 days) |
| **RET-CPN-R03** | Minimum stay required | None | Per coupon (1–30 nights) |
| **RET-CPN-R04** | Blackout dates (e.g., festivals, peak season) | None | Per coupon (JSON date ranges) |
| **RET-CPN-R05** | Per-guest limit: max active coupons | 3 | Per tenant (1–10) |
| **RET-CPN-R06** | Max discount cap (for PERCENT type) | ₹2,000 | Per tenant |
| **RET-CPN-R07** | Minimum booking amount for FLAT discount | Discount amount × 2 | Per coupon |
| **RET-CPN-R08** | Only valid for direct bookings (`BookingSource = 'Direct'`) | Yes | Always enforced |
| **RET-CPN-R09** | Cannot combine with other coupons (one coupon per booking) | Yes | Always enforced |
| **RET-CPN-R10** | Rate floor respected (§4.6) | Yes | Always enforced |

### 5.3 Coupon Code Generation

| Strategy | Format | Example |
|----------|--------|---------|
| **Post-stay auto** | `{TenantPrefix}{Discount}-{6-char alphanumeric}` | `ATLAS10-X7K9M2` |
| **Referral reward** | `REF-{6-char alphanumeric}` | `REF-B3N8P1` |
| **Campaign** | `{CampaignCode}-{4-char}` | `SUMMER25-A1B2` |
| **Loyalty tier** | `{Tier}{Discount}-{6-char}` | `GOLD15-M4N7Q2` |
| **Manual** | Host enters custom code | `DIWALI500` |

**Uniqueness**: All codes are unique within a tenant. Collision check on generation; retry with new random suffix on collision.

### 5.4 Auto-Apply vs Manual Apply

| Mode | When | Behaviour |
|------|------|-----------|
| **Auto-apply** | Guest clicks rebook link with `&coupon={{code}}` | Coupon pre-applied on booking form; guest sees discounted price |
| **Manual apply** | Guest types code on booking page | Validates code; applies if valid; shows error if invalid/expired |

### 5.5 Coupon Redemption Tracking

**New entity: `CouponRedemption`**

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` (PK) | |
| `TenantId` | `int` | |
| `CouponId` | `int` (FK → GuestCoupon) | |
| `BookingId` | `int` (FK → Booking) | |
| `GuestId` | `int` (FK → Guest) | |
| `DiscountApplied` | `decimal(10,2)` | Actual discount amount applied |
| `RedeemedAtUtc` | `datetime2` | |

> **RET-CPN-002**: `CouponRedemption` is created within the same DB transaction as the `Booking` creation. If booking creation fails, redemption is rolled back.

### 5.6 Abuse Detection

| Signal | Detection | Action |
|--------|-----------|--------|
| Same guest creates and cancels multiple bookings to test coupons | ≥ 3 cancelled bookings with coupon applied in 30 days | Flag guest; revoke unused coupons; alert host |
| Guest uses coupon on minimum-stay booking then shortens stay | Post-checkout: if actual stay < `MinNights`, flag for review | Alert host; log discrepancy |
| Guest shares coupon code publicly | Coupon redeemed by different guest than `GuestCoupon.GuestId` | Reject redemption (coupons are guest-specific) |

---

## 6. Referral System (Lean V1)

### 6.1 Referral Flow

```
Existing Guest (Referrer) shares referral link
    │
    ▼
New Guest (Referee) opens link → booking form
    │
    ▼
Referee creates booking with referral code attached
    │
    ▼
Booking confirmed → Referral recorded
    │
    ▼
Referee completes stay (checked out)
    │
    ▼
Referral "converted" → rewards issued to both
```

### 6.2 Referral Link & Token

| Component | Design |
|-----------|--------|
| **URL** | `https://{{tenant_slug}}.atlashomestays.com/book?ref={{referral_code}}` |
| **Referral code** | `Guest.ReferralCode` (defined in RA-GUEST-001 §7.7): auto-generated on first completed stay |
| **Format** | `{GuestId base36}-{4 random alphanumeric chars}` e.g., `a3x-k9m2` |
| **Permanent** | No expiry — code is permanent for the guest |
| **Attribution** | `Booking.ReferralCode` (defined in RA-GUEST-001 §7.7) stores the referral code used at booking time |

### 6.3 Reward Model

| Recipient | Reward | Trigger | Default |
|-----------|--------|---------|---------|
| **Referrer** | Coupon for next booking | Referee completes stay | 10% off (configurable per tenant) |
| **Referee** | Discount on first booking | Booking creation with referral code | 5% off (configurable per tenant) |

> **RET-REF-001**: Referee discount is applied immediately at booking (auto-generated `GuestCoupon` with `Source = 'REFERRAL'`). Referrer reward is issued only after referee's stay is completed (`stay.checked_out`), not at booking confirmation. This prevents gaming.

### 6.4 Referral Tracking

**New entity: `Referral`**

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` (PK) | |
| `TenantId` | `int` | |
| `ReferrerGuestId` | `int` (FK → Guest) | Guest who shared the link |
| `RefereeGuestId` | `int` (FK → Guest) | Guest who used the link |
| `ReferralCode` | `varchar(20)` | The code used |
| `BookingId` | `int` (FK → Booking) | Referee's booking |
| `Status` | `varchar(15)` | `PENDING` → `CONVERTED` → `REWARDED` or `CANCELLED` |
| `ReferrerCouponId` | `int?` (FK → GuestCoupon) | Coupon issued to referrer (after conversion) |
| `RefereeCouponId` | `int?` (FK → GuestCoupon) | Coupon applied by referee |
| `ConvertedAtUtc` | `datetime2?` | When referee completed stay |
| `CreatedAtUtc` | `datetime2` | |

**Lifecycle:**

| Status | Transition |
|--------|-----------|
| `PENDING` | Referee booking confirmed; referral recorded |
| `CONVERTED` | Referee checks out → `stay.checked_out`. Referrer reward coupon generated |
| `REWARDED` | Referrer notified of reward (automatic after `CONVERTED`) |
| `CANCELLED` | Referee booking cancelled before checkout |

### 6.5 Fraud Detection

| Fraud Type | Detection Rule | Action |
|------------|---------------|--------|
| **Self-referral** | `ReferrerGuestId` phone or email matches `RefereeGuestId` phone or email | Reject referral; no rewards. Log as `AuditLog` with `action = 'referral.fraud.self_referral'` |
| **Circular referral** | Guest A refers Guest B, Guest B refers Guest A | Second referral rejected |
| **Burst referrals** | > 5 referral bookings from same referrer in 7 days | Flag referrer; hold rewards for manual review |
| **Fake bookings** | Referred booking cancelled within 24h of confirmation repeatedly | After 2 cancelled referred bookings, suspend referral code for 30 days |

### 6.6 Limits

| Limit | Default | Configurable? |
|-------|---------|---------------|
| Max referral rewards per guest per month | 5 | Per tenant (1–20) |
| Max lifetime referral rewards per guest | 50 | Per tenant |
| Max referral discount value | ₹1,000 per referral | Per tenant |
| Referral code activation | After first completed stay | Always (cannot refer before first stay) |

---

## 7. WhatsApp Retention Campaign Engine

### 7.1 Campaign Types

| Type | Code | Trigger | Segment | Example |
|------|------|---------|---------|---------|
| **Seasonal reminder** | `SEASONAL` | Host manually triggers or schedules | All past guests or filtered by last-stay property | "Summer is here! Book {{property_name}} for the perfect getaway" |
| **Anniversary of stay** | `ANNIVERSARY` | Auto: same month as last stay, next year | Guests who stayed in that month | "It's been a year since your stay at {{property_name}}!" |
| **Festival campaign** | `FESTIVAL` | Host triggers for upcoming festival | All past guests or segment | "Diwali special! Book now and get 15% off" |
| **Low occupancy push** | `LOW_OCCUPANCY` | Auto: property occupancy < X% for next 14 days | Guests who stayed at that property + all `RETURNING` | "{{property_name}} has availability this weekend! Book direct: {{rebook_link}}" |
| **Win-back** | `WINBACK` | Auto: guest has `CHURNING` tag | Guests with no booking in 9+ months | "We miss you! Here's ₹500 off your next stay" |
| **Post-review** | `POST_REVIEW` | Auto: 7 days after review submitted | Guests who left 4–5 star review | "Thank you for your review! Share with friends: {{referral_link}}" |

### 7.2 Segmentation Rules

| Segment | Filter Criteria |
|---------|----------------|
| **All past guests** | `Guest` with ≥ 1 completed booking for this tenant |
| **By property** | Guests who stayed at specific `PropertyId` |
| **By tag** | Guests with specific `GuestTag` (e.g., `RETURNING`, `VIP`, `CHURNING`) |
| **By booking source** | Guests with specific `Booking.BookingSource` (e.g., only OTA guests for conversion) |
| **By last stay date** | Guests whose last checkout was within X–Y days ago |
| **By LTV range** | Guests with total revenue in a specified range |
| **By loyalty tier** | Guests with specific loyalty tag (`LOYALTY_BRONZE`, `LOYALTY_SILVER`, `LOYALTY_GOLD`) |
| **Exclude opted-out** | Always: remove guests with `GuestConsent.ConsentStatus = 'REVOKED'` for WhatsApp |

> **RET-CMP-001**: Segments are defined at campaign creation time as a set of filter rules stored as JSON. The system evaluates segments at send time (not at creation time) to capture any new matching guests.

### 7.3 Campaign Scheduler

**New entity: `RetentionCampaign`** (see §12.1.7 for full schema)

| Feature | Design |
|---------|--------|
| **Creation** | Host creates via admin portal (name, type, segment rules, template, scheduled date/time) |
| **Scheduling** | `ScheduledAtUtc` stored on campaign; `CampaignSchedulerJob` polls every 5 minutes |
| **Execution** | At scheduled time, evaluate segment → create one `OutboxMessage` per matching guest |
| **Delivery** | Existing outbox pipeline delivers via WhatsApp → SMS → Email fallback |
| **Status tracking** | Per-guest delivery status in `CampaignDeliveryLog` (see §12.1.8) |

### 7.4 Rate Limiting

| Level | Limit | Enforced By |
|-------|-------|-------------|
| **Per guest** | Max 2 marketing messages per week | Check `CommunicationLog` for last 7 days before sending |
| **Per tenant per day** | Max 500 campaign messages per day | Counter on `RetentionCampaign` |
| **Per guest lifetime** | Max 1 message per campaign | `CampaignDeliveryLog` unique constraint on `(CampaignId, GuestId)` |
| **Quiet hours** | No messages 21:00–08:00 IST | Same as RA-GUEST-001 §3.5 |
| **Cooldown** | No campaign to same guest within 72h of transactional message | Check `CommunicationLog` |

### 7.5 Opt-Out Handling

| Mechanism | Action |
|-----------|--------|
| Guest replies "STOP" to any WhatsApp message | `GuestConsent.ConsentStatus = 'REVOKED'` for WhatsApp channel. Guest excluded from all future campaigns |
| Guest opts out via portal | Same as above |
| Guest opts out from email unsubscribe link | Revokes email consent. WhatsApp/SMS unaffected |
| Host can manually revoke marketing consent | Per-guest toggle in admin portal |

> **RET-CMP-002**: Opt-out from **transactional** messages (booking confirmation, check-in instructions) is NOT supported — those are essential service messages. Opt-out only applies to marketing/retention messages.

### 7.6 Template Approval Rules

| Rule | V1 | V2 |
|------|----|----|
| Template content | Host writes; platform provides defaults | AI-assisted generation |
| WhatsApp template approval | Not applicable (using WhatsApp session messages / business API templates pre-approved) | Formal Meta template approval workflow |
| Compliance check | No profanity/spam auto-check in V1 | Automated content review |
| Platform default templates | Provided for each campaign type | A/B testable variants |

### 7.7 Performance Tracking

| Metric | Source | Definition |
|--------|--------|-----------|
| **Delivery rate** | `CampaignDeliveryLog.Status = 'DELIVERED'` / total sent | % of messages delivered |
| **Click rate** | V2: link click tracking via URL shortener | % of delivered messages where rebook link was clicked |
| **Conversion rate** | Bookings within 14 days of campaign delivery with matching `utm_campaign` | % of delivered messages that resulted in a booking |
| **Revenue generated** | `SUM(Booking.FinalAmount)` from converted bookings | ₹ revenue attributable to campaign |
| **Opt-out rate** | Opt-outs within 24h of campaign send / total sent | % of recipients who opted out |
| **ROI** | Revenue generated / discount cost (if coupon included) | Return on campaign investment |

---

## 8. Loyalty Scoring Model (Lean V1)

### 8.1 LoyaltyScore Formula

```
LoyaltyScore = (StayPoints) + (RevenuePoints) + (ReferralPoints) + (ReviewPoints)
```

| Component | Formula | Max Points |
|-----------|---------|-----------|
| **StayPoints** | `CompletedStays × 5` | 50 (capped at 10 stays) |
| **RevenuePoints** | `FLOOR(TotalRevenue / 5000) × 2` | 40 (capped at ₹1,00,000 total) |
| **ReferralPoints** | `ConvertedReferrals × 3` | 15 (capped at 5 referrals) |
| **ReviewPoints** | `ReviewsSubmitted × 2` (only for ≥ 4-star reviews) | 10 (capped at 5 reviews) |

**Maximum possible score**: 115 (theoretical; practical peak ≈ 80–90 for active guests).

### 8.2 Loyalty Tiers

| Tier | Code | Score Range | Requirements |
|------|------|------------|-------------|
| **None** | — | 0–9 | First-time guest |
| **Bronze** | `BRONZE` | 10–29 | 2+ stays or ₹25,000+ revenue |
| **Silver** | `SILVER` | 30–59 | 4+ stays, good reviewer, or referrer |
| **Gold** | `GOLD` | 60+ | Loyal, high-value, engaged guest |

### 8.3 Benefits Per Tier

| Benefit | Bronze | Silver | Gold |
|---------|--------|--------|------|
| **Direct booking discount** | 5% | 10% | 15% |
| **Auto-generated coupon on checkout** | ✓ | ✓ | ✓ |
| **Priority support (host notified of VIP)** | ✗ | ✓ | ✓ |
| **Early check-in free (when available)** | ✗ | ✗ | ✓ |
| **Late checkout free (when available)** | ✗ | ✗ | ✓ |
| **Welcome amenity flag** | ✗ | ✗ | ✓ (staff task created) |
| **Referral bonus multiplier** | 1× | 1.5× | 2× |

> **RET-LYL-001**: Benefits are **informational** in V1 — the system generates the correct coupon discount and tags the booking, but free add-ons and welcome amenities are flagged for host action rather than auto-fulfilled.

### 8.4 Tier Calculation & Updates

| Trigger | Action |
|---------|--------|
| **Guest checkout** (`stay.checked_out`) | Recalculate `LoyaltyScore` for this guest. Update tier tags if changed |
| **Monthly `LoyaltyRecalcJob`** | Recalculate all guests with ≥ 1 completed booking. Update tier tags. Detect `CHURNING` guests |
| **Referral conversion** | Recalculate referrer's score (referral points changed) |
| **Review submitted** | Recalculate reviewer's score (review points changed) |

> **RET-LYL-002**: Tier **downgrades** are possible if the monthly recalc shows score decrease (unlikely in V1 since all components are additive, but possible if a review is removed or a referral is invalidated).

### 8.5 No Manual Overrides (V1)

Loyalty scores and tiers are **fully automatic** in V1. No host ability to manually set a guest's tier. V2 may introduce host-granted temporary VIP status.

---

## 9. LTV & Retention Analytics

### 9.1 Tenant-Visible Metrics

| Metric | Definition | Display | Refresh |
|--------|-----------|---------|---------|
| **Repeat Guest %** | Guests with ≥ 2 completed stays / total unique guests | % gauge + trend | Daily |
| **Direct Booking %** | Bookings with `BookingSource = 'Direct'` / total bookings (period) | % gauge + trend | Daily |
| **Average LTV** | `AVG(GuestLTVSnapshot.TotalRevenue)` for guests with ≥ 1 stay | ₹ metric | Monthly |
| **Median LTV** | `MEDIAN(TotalRevenue)` | ₹ metric | Monthly |
| **Top 10 Guests by LTV** | Ranked guest list with total revenue, stays, last visit | Table | Monthly |
| **Referral Conversion Rate** | `Referral.Status = 'CONVERTED'` / total referrals | % metric | Real-time |
| **Coupon Redemption Rate** | `CouponRedemption` count / `GuestCoupon` issued count | % metric | Real-time |
| **Retention Rate (3M)** | Guests who stayed in last 3 months and had a prior stay ≥ 3M ago / total guests with prior stay ≥ 3M ago | % metric | Monthly |
| **Retention Rate (6M)** | Same logic, 6-month window | % metric | Monthly |
| **Retention Rate (12M)** | Same logic, 12-month window | % metric | Monthly |
| **OTA → Direct Conversion %** | Guests whose first booking was OTA and any subsequent was Direct / total OTA-first guests with ≥ 2 stays | % metric | Monthly |
| **Churning Guests** | Count of guests with `CHURNING` tag | Count + list | Monthly |
| **Campaign ROI** | Revenue from campaign-attributed bookings / campaign discount cost | ₹ ratio | Per campaign |
| **Loyalty Tier Distribution** | Count of guests per tier | Bar chart | Monthly |

### 9.2 Atlas Management-Level Metrics

| Metric | Definition | Access |
|--------|-----------|--------|
| **Marketplace Repeat %** | % of bookings across all tenants from returning guests | Atlas Admin |
| **OTA → Direct Conversion % (platform)** | Aggregated cross-tenant conversion rate | Atlas Admin |
| **Average Tenant Retention Rate** | Mean of per-tenant retention rates | Atlas Admin |
| **Referral Network Growth** | Total referrals created across platform per month | Atlas Admin |
| **Coupon Economy** | Total discount issued vs. revenue generated from coupon bookings (platform-wide) | Atlas Admin |
| **Loyalty Penetration** | % of guests with ≥ Bronze tier across platform | Atlas Admin |
| **Retention Campaign Effectiveness** | Aggregated campaign delivery/conversion rates | Atlas Admin |

> **RET-ANL-001**: Management metrics use **anonymized aggregates**. No individual guest data is visible at Atlas Admin level. Tenant-level detail is visible only to the owning tenant.

---

## 10. Integration with Marketplace Ranking

### 10.1 Repeat Rate Impact on Ranking

| Signal | Marketplace Impact | Weight |
|--------|-------------------|--------|
| **Repeat guest %** | Higher repeat % → higher search ranking | Low in V1 (5% of ranking score) |
| **Direct booking %** | Not directly used in marketplace ranking (marketplace tracks its own bookings) | None |
| **Guest review avg** | Higher avg → higher ranking | Already defined in marketplace ranking |
| **Referral activity** | Active referral network → minor ranking boost | Negligible in V1 |

### 10.2 Loyalty Impact on TrustScore

Ref: RA-DATA-001 `DailyTrustScore`

| Factor | Impact | V1 Weight |
|--------|--------|-----------|
| **Repeat rate** | Properties with higher repeat rate score higher on "Guest Satisfaction" component | 5% of TrustScore |
| **Average review rating** | Already a TrustScore component | Existing |
| **Incident rate** | Already a TrustScore component (RA-OPS-001) | Existing |

> **RET-MKT-001**: TrustScore integration is **additive** — repeat rate adds a small bonus to the existing TrustScore. No penalty for low repeat rate (new hosts would be unfairly penalized). Minimum threshold: 10 completed bookings before repeat rate affects TrustScore.

### 10.3 Boost Suppression

| Condition | Action |
|-----------|--------|
| Property has ≥ 20 bookings and repeat rate < 5% | Marketplace boost effectiveness reduced by 20% (lower ROI for host) |
| Property has ≥ 20 bookings and average review < 3.0 | Marketplace boost eligibility suspended |
| Property has active fraud flags | Boost eligibility suspended |

> **RET-MKT-002**: Boost suppression is a **V2 feature**. V1 defines the hooks (data points collected) but does not enforce suppression. Flag stored on property profile for future use.

### 10.4 Fake Repeat Safeguards

| Risk | Detection | Action |
|------|-----------|--------|
| Host creates fake bookings to inflate repeat rate | Bookings with same `CreatedBy` user as host + guest phone matches host/staff phone | Exclude from repeat rate calculation; flag for review |
| Same guest, same property, same dates (test bookings) | Detect bookings cancelled within 1h with `AmountReceived = 0` | Exclude from all metrics |
| Suspiciously high repeat rate (>80%) with low revenue | Statistical outlier detection (V2) | Flag for manual review |

---

## 11. Abuse & Fraud Guardrails

### 11.1 Per-Guest Limits

| Limit | Default | Configurable? |
|-------|---------|---------------|
| Max active coupons | 3 | Per tenant (1–10) |
| Max coupons issued per year | 12 | Per tenant |
| Max referral rewards per month | 5 | Per tenant |
| Max referral rewards lifetime | 50 | Per tenant |
| Max campaign messages per week | 2 | Platform-wide |
| Max bookings with coupon per year | 6 | Per tenant |

### 11.2 Per-Tenant Limits

| Limit | Default | Purpose |
|-------|---------|---------|
| Max campaign messages per day | 500 | Prevent spam; protect WhatsApp reputation |
| Max discount value per coupon | ₹5,000 | Prevent excessive discounting |
| Max discount as % of booking | 25% | Rate floor protection |
| Max active campaigns | 5 | Prevent campaign fatigue |
| Max coupons issued per month (all guests) | 200 | Cost control |

### 11.3 Alerting Thresholds

| Alert | Threshold | Recipient | Channel |
|-------|-----------|-----------|---------|
| High coupon issuance rate | > 50 coupons issued in 24h for a tenant | Atlas Admin | Dashboard + internal alert |
| High cancellation after coupon apply | > 3 cancelled coupon bookings by same guest in 30 days | Host + Atlas Admin | Dashboard |
| Referral fraud pattern | Self-referral or circular referral detected | Atlas Admin | Dashboard + AuditLog |
| Campaign opt-out spike | > 10% opt-out rate on a single campaign | Host | Dashboard notification |
| Loyalty score anomaly | Guest reaches Gold tier with < 3 stays (should be impossible with formula) | Atlas Admin | Internal alert |
| Excessive discounting | Tenant's avg effective discount > 20% of booking value over 30 days | Atlas Admin | Dashboard |

### 11.4 Enforcement Actions

| Severity | Action | Automated? |
|----------|--------|-----------|
| **Low** — threshold approach | Dashboard warning to host | Yes |
| **Medium** — threshold breach | Feature temporarily rate-limited (e.g., coupon generation paused for 24h) | Yes |
| **High** — fraud confirmed | Referral code suspended; coupons revoked; Atlas Admin notified | Semi-auto (detection auto, action requires Admin confirmation in V1) |

---

## 12. Data Model (LLR)

### 12.1 Entity Definitions

#### 12.1.1 `Guest` (Existing — Extended)

New fields added to the existing `Guest` entity:

| Field | Type | Description |
|-------|------|-------------|
| `NormalizedPhone` | `varchar(20)` | E.164 normalized phone for matching |
| `NormalizedEmail` | `varchar(200)` | Lowercased, trimmed email |
| `PhoneHash` | `varchar(64)` | `SHA256(NormalizedPhone)` for cross-reference |
| `ReferralCode` | `varchar(20)` | Guest's unique referral code (per RA-GUEST-001 §7.7) |
| `FirstBookingAtUtc` | `datetime2?` | Date of first confirmed booking |
| `LastStayCheckoutUtc` | `datetime2?` | Date of last checkout (denormalized for query performance) |
| `TotalCompletedStays` | `int` | Denormalized stay count (updated on checkout) |
| `TotalRevenue` | `decimal(18,2)` | Denormalized revenue sum (updated on checkout) |
| `IsAnonymized` | `bit` | Flag for DPDP erasure |
| `CreatedAtUtc` | `datetime2` | |
| `UpdatedAtUtc` | `datetime2` | |

**New indexes:**
- `IX_Guest_TenantId_NormalizedPhone` on `(TenantId, NormalizedPhone)` — duplicate detection
- `IX_Guest_TenantId_NormalizedEmail` on `(TenantId, NormalizedEmail)` — duplicate detection
- `IX_Guest_TenantId_ReferralCode` on `(TenantId, ReferralCode)` WHERE `ReferralCode IS NOT NULL` — referral lookup

> **RET-DM-001**: `TotalCompletedStays` and `TotalRevenue` are denormalized counters updated within the same transaction as `stay.checked_out` processing. A nightly reconciliation job verifies they match computed values from `Booking` table.

#### 12.1.2 `GuestTag` (Defined in RA-GUEST-001 — Extended)

See RA-GUEST-001 §7.4 for base schema. Additional retention tags defined in §3.2 of this document. No schema change needed — existing `Tag varchar(30)` accommodates all new tag codes.

#### 12.1.3 `LoyaltyScore`

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` (PK) | |
| `TenantId` | `int` | |
| `GuestId` | `int` (FK → Guest) | |
| `StayPoints` | `int` | Points from stays |
| `RevenuePoints` | `int` | Points from revenue |
| `ReferralPoints` | `int` | Points from referrals |
| `ReviewPoints` | `int` | Points from reviews |
| `TotalScore` | `int` | Sum (computed on write) |
| `CurrentTier` | `varchar(10)` | `BRONZE`, `SILVER`, `GOLD`, or NULL |
| `TierSince` | `datetime2?` | When current tier was achieved |
| `LastCalculatedAtUtc` | `datetime2` | |
| `CreatedAtUtc` | `datetime2` | |
| `UpdatedAtUtc` | `datetime2` | |

**Unique constraint**: `IX_LoyaltyScore_TenantId_GuestId` on `(TenantId, GuestId)`.

#### 12.1.4 `GuestCoupon` (Defined in RA-GUEST-001 — Extended)

See RA-GUEST-001 §7.3 for base schema. Extensions from §5.1:

| Field | Type | Description |
|-------|------|-------------|
| `CouponType` | `varchar(25)` | Extended type codes (§5.1) |
| `MinNights` | `int?` | Minimum stay nights |
| `BlackoutDatesJson` | `nvarchar(max)` | JSON date ranges |
| `PropertyId` | `int?` | Property restriction |
| `CampaignId` | `int?` (FK → RetentionCampaign) | Source campaign |
| `Source` | `varchar(20)` | `POST_STAY`, `REFERRAL`, `CAMPAIGN`, `MANUAL`, `LOYALTY` |

**New indexes:**
- `IX_GuestCoupon_TenantId_GuestId_Status` on `(TenantId, GuestId, Status)` — active coupon lookup
- `IX_GuestCoupon_CouponCode` on `(TenantId, CouponCode)` — redemption lookup

#### 12.1.5 `CouponRedemption`

See §5.5 for schema.

**Indexes:**
- `IX_CouponRedemption_CouponId` on `(CouponId)` — usage tracking
- `IX_CouponRedemption_BookingId` on `(BookingId)` — booking-to-coupon linkage

#### 12.1.6 `Referral`

See §6.4 for schema.

**Indexes:**
- `IX_Referral_TenantId_ReferrerGuestId` on `(TenantId, ReferrerGuestId)` — referrer's referral list
- `IX_Referral_TenantId_ReferralCode` on `(TenantId, ReferralCode)` — code lookup
- `IX_Referral_TenantId_Status` on `(TenantId, Status)` — pending conversions

#### 12.1.7 `RetentionCampaign`

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` (PK) | |
| `TenantId` | `int` | |
| `Name` | `nvarchar(200)` | Campaign name |
| `CampaignType` | `varchar(20)` | `SEASONAL`, `ANNIVERSARY`, `FESTIVAL`, `LOW_OCCUPANCY`, `WINBACK`, `POST_REVIEW` |
| `SegmentRulesJson` | `nvarchar(max)` | JSON filter rules (tag, property, date range, LTV, etc.) |
| `TemplateId` | `int?` (FK → MessageTemplate) | Which template to use |
| `CouponTemplateJson` | `nvarchar(max)` | If campaign includes coupon: `{type, discountValue, validDays, minNights}` |
| `ScheduledAtUtc` | `datetime2` | When to send |
| `Status` | `varchar(15)` | `DRAFT`, `SCHEDULED`, `SENDING`, `SENT`, `CANCELLED` |
| `TotalRecipients` | `int?` | Computed at send time |
| `TotalDelivered` | `int?` | Updated by delivery webhook |
| `TotalConversions` | `int?` | Updated by booking attribution |
| `CreatedByUserId` | `int` | |
| `CreatedAtUtc` | `datetime2` | |
| `UpdatedAtUtc` | `datetime2` | |

**Indexes:**
- `IX_RetentionCampaign_TenantId_Status_ScheduledAtUtc` on `(TenantId, Status, ScheduledAtUtc)` — scheduler poll

#### 12.1.8 `CampaignDeliveryLog`

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `long` (PK) | |
| `TenantId` | `int` | |
| `CampaignId` | `int` (FK → RetentionCampaign) | |
| `GuestId` | `int` (FK → Guest) | |
| `Channel` | `varchar(15)` | `WHATSAPP`, `SMS`, `EMAIL` |
| `Status` | `varchar(15)` | `PENDING`, `SENT`, `DELIVERED`, `FAILED`, `OPTED_OUT` |
| `CommunicationLogId` | `long?` (FK → CommunicationLog) | Links to existing delivery tracking |
| `CouponId` | `int?` (FK → GuestCoupon) | If coupon was generated with this campaign message |
| `ConvertedBookingId` | `int?` (FK → Booking) | If guest booked within attribution window |
| `SentAtUtc` | `datetime2?` | |
| `DeliveredAtUtc` | `datetime2?` | |
| `CreatedAtUtc` | `datetime2` | |

**Unique constraint**: `IX_CampaignDeliveryLog_CampaignId_GuestId` on `(CampaignId, GuestId)` — one message per guest per campaign.

**Indexes:**
- `IX_CampaignDeliveryLog_CampaignId_Status` on `(CampaignId, Status)` — delivery stats

#### 12.1.9 `GuestLTVSnapshot`

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `long` (PK) | |
| `TenantId` | `int` | |
| `GuestId` | `int` (FK → Guest) | |
| `SnapshotMonth` | `date` | First day of the month |
| `TotalStays` | `int` | Cumulative completed stays at snapshot time |
| `TotalRevenue` | `decimal(18,2)` | Cumulative revenue |
| `AvgBookingValue` | `decimal(18,2)` | Total revenue / total stays |
| `DirectBookingRatio` | `decimal(5,4)` | Direct bookings / total bookings |
| `LoyaltyScore` | `int` | Score at snapshot time |
| `LoyaltyTier` | `varchar(10)` | Tier at snapshot time |
| `DaysSinceLastStay` | `int` | Days since last checkout at snapshot time |
| `ReferralCount` | `int` | Converted referrals at snapshot time |
| `AvgReviewRating` | `decimal(3,2)?` | Average review rating |
| `CreatedAtUtc` | `datetime2` | |

**Unique constraint**: `IX_GuestLTVSnapshot_TenantId_GuestId_SnapshotMonth` on `(TenantId, GuestId, SnapshotMonth)`.

**Indexes:**
- `IX_GuestLTVSnapshot_TenantId_SnapshotMonth` on `(TenantId, SnapshotMonth)` — monthly reports

### 12.2 Entity Registration

All new and extended entities in `AppDbContext.OnModelCreating()`:

```
modelBuilder.Entity<LoyaltyScore>().HasQueryFilter(e => e.TenantId == _tenantId);
modelBuilder.Entity<CouponRedemption>().HasQueryFilter(e => e.TenantId == _tenantId);
modelBuilder.Entity<Referral>().HasQueryFilter(e => e.TenantId == _tenantId);
modelBuilder.Entity<RetentionCampaign>().HasQueryFilter(e => e.TenantId == _tenantId);
modelBuilder.Entity<CampaignDeliveryLog>().HasQueryFilter(e => e.TenantId == _tenantId);
modelBuilder.Entity<GuestLTVSnapshot>().HasQueryFilter(e => e.TenantId == _tenantId);
// GuestTag, GuestCoupon, GuestConsent already registered per RA-GUEST-001
```

### 12.3 Indexing Strategy

| Pattern | Rationale |
|---------|-----------|
| All entities have `TenantId` as leading column in composite indexes | Tenant-scoped queries are the norm; supports global query filter |
| `Guest.NormalizedPhone` and `Guest.NormalizedEmail` indexed separately | Fast duplicate detection at booking creation |
| `GuestCoupon.CouponCode` indexed with tenant | Coupon redemption lookup by code |
| `CampaignDeliveryLog` unique on `(CampaignId, GuestId)` | Prevents duplicate campaign sends |
| `GuestLTVSnapshot` unique on `(TenantId, GuestId, SnapshotMonth)` | One snapshot per guest per month |

### 12.4 Tenant Isolation

> **RET-DM-002**: Every entity implements `ITenantOwnedEntity` and has a global EF Core query filter. Integration tests MUST verify no cross-tenant data leakage for all retention entities.

### 12.5 Retention Policy

| Entity | Retention | Cleanup |
|--------|-----------|---------|
| `Guest` | Indefinite (unless anonymized) | Anonymization per §2.6 |
| `GuestTag` | Indefinite | Tags removed only by system recalculation |
| `LoyaltyScore` | Indefinite (one record per guest) | Updated in-place |
| `GuestCoupon` | 1 year after expiry | Soft-delete; hard-delete after 2 years |
| `CouponRedemption` | 2 years | Archive |
| `Referral` | 2 years | Archive |
| `RetentionCampaign` | 2 years | Soft-delete |
| `CampaignDeliveryLog` | 1 year | Archive |
| `GuestLTVSnapshot` | Indefinite (historical trend data) | Roll up to annual granularity after 3 years (V2) |

---

## 13. Acceptance Criteria & Test Matrix

### 13.1 Given/When/Then Tests

#### RET-T01: Repeat guest detected

```
GIVEN Guest G1 exists with NormalizedPhone = '+919876543210', TenantId = T1
AND   G1 has 1 completed booking
WHEN  a new booking B2 is created with guest phone '9876543210' for TenantId = T1
THEN  the system matches the phone to G1 (after normalization)
AND   B2.GuestId = G1.Id (existing guest reused)
AND   GuestTag 'RETURNING' is applied to G1
AND   GuestTag 'FIRST_TIME' is removed from G1
AND   AuditLog records the tag change
```

#### RET-T02: Loyalty tier upgraded

```
GIVEN Guest G1 has:
      - 3 completed stays (StayPoints = 15)
      - TotalRevenue = ₹40,000 (RevenuePoints = 16)
      - 0 referrals (ReferralPoints = 0)
      - 1 review at 4 stars (ReviewPoints = 2)
      - TotalScore = 33 → CurrentTier = SILVER
WHEN  G1 completes stay #4 (revenue += ₹12,000)
THEN  LoyaltyScore recalculated:
      - StayPoints = 20, RevenuePoints = 20, TotalScore = 42
AND   CurrentTier remains SILVER (42 < 60)
WHEN  G1 completes stay #5 (revenue += ₹15,000, total = ₹67,000)
THEN  StayPoints = 25, RevenuePoints = 26, TotalScore = 53
AND   CurrentTier remains SILVER
```

#### RET-T03: Coupon auto-generated post-stay

```
GIVEN Guest G1 checks out from Booking B1
AND   G1's LoyaltyTier = BRONZE (5% discount benefit)
WHEN  stay.checked_out event fires for B1
THEN  a GuestCoupon is created with:
      - GuestId = G1.Id
      - SourceBookingId = B1.Id
      - CouponType = 'PERCENT'
      - DiscountValue = 5
      - Source = 'LOYALTY'
      - ValidUntilUtc = now + 180 days
      - Status = 'ACTIVE'
AND   the coupon is included in the post-stay WhatsApp message
```

#### RET-T04: Referral conversion tracked

```
GIVEN Guest G1 (referrer) has ReferralCode = 'a3x-k9m2'
AND   New Guest G2 (referee) creates Booking B3 via link with ?ref=a3x-k9m2
WHEN  B3 is confirmed (booking.confirmed)
THEN  a Referral record is created:
      - ReferrerGuestId = G1.Id
      - RefereeGuestId = G2.Id
      - Status = 'PENDING'
AND   a GuestCoupon with Source = 'REFERRAL' is applied to B3 (referee discount)
WHEN  G2 checks out from B3
THEN  Referral.Status = 'CONVERTED'
AND   a reward GuestCoupon is generated for G1 (referrer)
AND   G1 is notified via WhatsApp: "Your friend stayed! Here's your reward: {{coupon_code}}"
AND   G1 tagged as 'REFERRER' (if not already)
AND   G2 tagged as 'REFERRED'
```

#### RET-T05: Abuse detection triggered

```
GIVEN Guest G1 has phone '+919876543210'
AND   G1 creates a booking B4 with referral code that resolves to... G1's own referral code
WHEN  the system checks ReferrerGuestId vs RefereeGuestId
THEN  the referral is rejected (self-referral detected)
AND   no Referral record is created
AND   AuditLog records: action = 'referral.fraud.self_referral'
AND   the booking B4 proceeds without referral discount
```

#### RET-T06: WhatsApp campaign sent to correct segment

```
GIVEN RetentionCampaign C1 with:
      - CampaignType = 'WINBACK'
      - SegmentRulesJson = {"tags": ["CHURNING"], "minStays": 2}
      - ScheduledAtUtc = 2026-03-01 09:00 UTC
AND   3 guests match the segment: G1, G2, G3
AND   G3 has GuestConsent.ConsentStatus = 'REVOKED' for WhatsApp
WHEN  CampaignSchedulerJob fires at 09:00 UTC
THEN  CampaignDeliveryLog entries created for G1, G2 (not G3)
AND   OutboxMessages created for G1, G2
AND   G3 is excluded (opted out)
AND   C1.TotalRecipients = 2
```

#### RET-T07: Opt-out respected

```
GIVEN Guest G1 has GuestConsent for WhatsApp with ConsentStatus = 'GRANTED'
WHEN  G1 replies "STOP" to a WhatsApp message
THEN  GuestConsent.ConsentStatus = 'REVOKED', RevokedAtUtc = now
AND   all pending AutomationSchedule entries for G1 with marketing event types are cancelled
AND   G1 is excluded from all future campaigns
AND   transactional messages (booking confirmation, check-in instructions) are still sent via SMS/Email
```

#### RET-T08: LTV correctly calculated

```
GIVEN Guest G1 has 3 completed bookings:
      - B1: FinalAmount = ₹5,000, BookingSource = 'Airbnb'
      - B2: FinalAmount = ₹8,000, BookingSource = 'Direct'
      - B3: FinalAmount = ₹12,000, BookingSource = 'Direct'
WHEN  the monthly GuestLTVSnapshot job runs
THEN  a snapshot is created:
      - TotalStays = 3
      - TotalRevenue = ₹25,000
      - AvgBookingValue = ₹8,333.33
      - DirectBookingRatio = 0.6667
AND   Guest.TotalCompletedStays = 3 (denormalized)
AND   Guest.TotalRevenue = 25000 (denormalized)
```

#### RET-T09: Repeat booking correctly tagged

```
GIVEN Guest G1 completed stay at Property P1 in January 2026
WHEN  G1 creates a new booking for Property P1 in March 2026 (BookingSource = 'Direct')
THEN  G1 tagged as 'RETURNING' (already has it)
AND   G1 tagged as 'DIRECT_BOOKER'
AND   G1 tagged as 'OTA_CONVERTED' (first booking was Airbnb, this is Direct)
AND   the booking confirmation message includes "Welcome back, {{guest_name}}!"
AND   if G1 had an active coupon, it is auto-applied on the booking form via rebook link
```

#### RET-T10: Direct rebooking flow works end-to-end

```
GIVEN Guest G1 checked out 30 days ago from Property P1
AND   G1 has active GuestCoupon C1 with CouponCode = 'ATLAS10-X7K9M2'
WHEN  G1 receives re-engagement WhatsApp with rebook link:
      https://sunset.atlashomestays.com/rebook?g=abc123&p=1&coupon=ATLAS10-X7K9M2
AND   G1 taps the link
THEN  the booking form opens with G1's details pre-filled and P1 pre-selected
AND   coupon ATLAS10-X7K9M2 is auto-applied showing discounted price
WHEN  G1 completes the booking
THEN  Booking created with BookingSource = 'Direct', DiscountAmount = 10% of total
AND   CouponRedemption record created
AND   C1.Status = 'USED', C1.TimesUsed = 1
AND   G1 tagged as 'RETURNING', 'DIRECT_BOOKER', 'OTA_CONVERTED' (if applicable)
```

### 13.2 Edge Case Test Matrix

| ID | Scenario | Expected Behavior |
|----|----------|-------------------|
| EC-01 | Guest books with OTA-masked phone, then provides real phone at check-in | Guest record updated; dedup check runs; merge if existing guest found |
| EC-02 | Two guests with same phone but different tenants | Independent guest records; no cross-tenant matching |
| EC-03 | Guest applies expired coupon | Rejected with error: "This coupon has expired" |
| EC-04 | Guest applies coupon from Tenant A on Tenant B booking | Rejected: coupon not found (tenant-scoped query filter) |
| EC-05 | Guest applies coupon during blackout date | Rejected with error: "This coupon is not valid for the selected dates" |
| EC-06 | Campaign scheduled but tenant deactivated | Campaign status set to `CANCELLED`; no messages sent |
| EC-07 | Same guest receives anniversary + win-back campaign on same week | Per-guest rate limit (2/week) blocks second message; win-back deferred |
| EC-08 | Guest opts out then opts back in | New GuestConsent record with `GRANTED`; guest eligible for campaigns again |
| EC-09 | Loyalty score recalc during checkout transaction | Recalculation happens within the same DB transaction; consistent state guaranteed |
| EC-10 | 1000+ guests in campaign segment | Batch processing: 50 OutboxMessages per batch; no timeout |
| EC-11 | Referrer and referee are in different tenants | Referral rejected: referral codes are tenant-scoped |
| EC-12 | Guest has 3 active coupons, checkout generates a 4th | New coupon NOT created (per-guest limit of 3). Logged as skipped |
| EC-13 | Coupon discount exceeds rate floor | Discount capped at `BookingAmount - RateFloor`; guest sees adjusted discount |
| EC-14 | Guest anonymization requested after referral | Referral record anonymized (ReferrerGuestId points to anonymized guest); conversion stats preserved |

---

## 14. Definition of Done — Retention Engine V1

### 14.1 Functional Checklist

| # | Requirement | Status |
|---|-------------|--------|
| 1 | Guest identity resolution: phone-based dedup working on booking creation | ☐ |
| 2 | Guest identity resolution: email-based dedup working on booking creation | ☐ |
| 3 | Duplicate guest merge (phone match) within same tenant | ☐ |
| 4 | `FIRST_TIME` and `RETURNING` tags applied correctly on booking confirmation | ☐ |
| 5 | `VIP` tag applied on qualifying checkout (5+ stays or ₹50k+ revenue) | ☐ |
| 6 | `DIRECT_BOOKER` and `OTA_CONVERTED` tags applied on direct booking | ☐ |
| 7 | LoyaltyScore calculated on checkout with correct formula | ☐ |
| 8 | Loyalty tiers (Bronze/Silver/Gold) assigned based on score thresholds | ☐ |
| 9 | Tier-appropriate coupon auto-generated on checkout | ☐ |
| 10 | Post-stay coupon delivered via WhatsApp with rebook link | ☐ |
| 11 | Rebook link pre-fills guest data and auto-applies coupon | ☐ |
| 12 | Coupon validation: expiry, min stay, blackout dates, rate floor | ☐ |
| 13 | Coupon redemption tracked in `CouponRedemption` | ☐ |
| 14 | One coupon per booking enforced | ☐ |
| 15 | Referral code generated on first completed stay | ☐ |
| 16 | Referral link works: referee discount applied at booking | ☐ |
| 17 | Referral conversion: referrer reward issued after referee checkout | ☐ |
| 18 | Self-referral fraud detected and blocked | ☐ |
| 19 | Retention campaign creation with segment rules | ☐ |
| 20 | Campaign scheduling and execution via `CampaignSchedulerJob` | ☐ |
| 21 | Campaign delivery respects opt-out, quiet hours, and rate limits | ☐ |
| 22 | Anniversary re-engagement scheduled at checkout, cancelled on re-book | ☐ |
| 23 | Win-back campaign targets `CHURNING` guests correctly | ☐ |
| 24 | `GuestLTVSnapshot` monthly job produces accurate data | ☐ |
| 25 | Tenant retention dashboard shows all §9.1 metrics | ☐ |
| 26 | Guest denormalized counters (`TotalCompletedStays`, `TotalRevenue`) accurate | ☐ |
| 27 | All tag changes logged in `AuditLog` | ☐ |

### 14.2 Non-Functional Checklist

| # | Requirement | Status |
|---|-------------|--------|
| 1 | All entities implement `ITenantOwnedEntity` with global query filter | ☐ |
| 2 | No cross-tenant guest data leakage (integration tests) | ☐ |
| 3 | Phone normalization handles Indian formats (+91, 0-prefix, spaces, dashes) | ☐ |
| 4 | Guest dedup runs in < 50ms (indexed phone/email lookup) | ☐ |
| 5 | Campaign sends batched: 50 per batch, no OLTP blocking | ☐ |
| 6 | Loyalty recalc job completes in < 30 min for 10,000 guests per tenant | ☐ |
| 7 | LTV snapshot job completes in < 60 min for 100k guests platform-wide | ☐ |
| 8 | Coupon validation runs in < 100ms | ☐ |
| 9 | All abuse guardrails verified with test cases | ☐ |
| 10 | PII access restricted by role | ☐ |
| 11 | Audit trail complete for all retention actions | ☐ |
| 12 | Feature flags control retention module activation | ☐ |
| 13 | All acceptance tests (§13) pass | ☐ |
| 14 | EF Core migrations are additive (no data loss) | ☐ |

---

## Appendix A — Event Types (Retention Domain)

New event types to add to `EventTypes.cs`:

| Constant | Value | Trigger |
|----------|-------|---------|
| `RetentionGuestMerged` | `retention.guest.merged` | Two guest records merged |
| `RetentionTagApplied` | `retention.tag.applied` | Guest tag added or changed |
| `RetentionLoyaltyRecalc` | `retention.loyalty.recalculated` | Loyalty score recalculated |
| `RetentionTierUpgrade` | `retention.tier.upgraded` | Guest promoted to higher loyalty tier |
| `RetentionCouponIssued` | `retention.coupon.issued` | Auto-generated coupon created |
| `RetentionCouponRedeemed` | `retention.coupon.redeemed` | Coupon applied to a booking |
| `RetentionCouponExpired` | `retention.coupon.expired` | Coupon expired (batch job) |
| `RetentionReferralCreated` | `retention.referral.created` | New referral recorded |
| `RetentionReferralConverted` | `retention.referral.converted` | Referral converted (referee checkout) |
| `RetentionReferralFraud` | `retention.referral.fraud` | Referral fraud detected |
| `RetentionCampaignSent` | `retention.campaign.sent` | Campaign messages dispatched |
| `RetentionCampaignConverted` | `retention.campaign.converted` | Booking attributed to campaign |
| `RetentionWinbackDue` | `retention.winback.due` | Win-back message due for churning guest |
| `RetentionAnniversaryDue` | `retention.anniversary.due` | Stay anniversary reminder due |

Helper method:

```csharp
public static bool IsRetentionEvent(string eventType) =>
    eventType.StartsWith("retention.", StringComparison.Ordinal);
```

---

## Appendix B — Cross-Reference to RA-GUEST-001 Entities

| RA-GUEST-001 Entity | Used By This Document | Extension |
|---------------------|----------------------|-----------|
| `GuestTag` (§7.4) | §3.2 — additional retention/loyalty tags | No schema change; new tag code values added |
| `GuestCoupon` (§7.3) | §5.1 — extended with `CouponType`, `MinNights`, `BlackoutDatesJson`, `PropertyId`, `CampaignId`, `Source` | New columns added |
| `GuestReview` (§7.2) | §8.1 — review count/avg feeds LoyaltyScore `ReviewPoints` | No change |
| `GuestConsent` (§10.7) | §7.5 — opt-out handling for campaigns | No change |
| `Guest.ReferralCode` (§7.7) | §6.2 — referral link generation | No change |
| `Booking.ReferralCode` (§7.7) | §6.2 — referral attribution on booking | No change |
| `GuestPortalToken` (§8.2) | §4.1 — rebook link uses similar HMAC pattern | No change |
| Guest LTV formulas (§7.5) | §2.4 — formulas reused; `GuestLTVSnapshot` adds persistence | New snapshot entity |

---

*End of RA-RET-001*
