# RA-GUEST-001 — Guest Experience Platform (Pre-Stay, Stay, Post-Stay) Requirements

| Field | Value |
|-------|-------|
| **Doc ID** | RA-GUEST-001 |
| **Title** | Guest Experience Platform — Pre-Stay, Stay, Post-Stay |
| **Status** | DRAFT |
| **Author** | Atlas Architecture |
| **Created** | 2026-02-27 |
| **Depends on** | RA-AUTO-001 (Automation Engine), RA-OPS-001 (Ops Module), RA-DATA-001 (Data Platform) |
| **Consumers** | Guests, Tenant Hosts, Staff, Atlas Admin |

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Guest Lifecycle Model](#2-guest-lifecycle-model)
3. [Pre-Arrival Automation (High Impact V1)](#3-pre-arrival-automation-high-impact-v1)
4. [Self Check-In System (Lean V1)](#4-self-check-in-system-lean-v1)
5. [In-Stay Experience Engine](#5-in-stay-experience-engine)
6. [Check-Out Workflow](#6-check-out-workflow)
7. [Post-Stay & Retention Engine](#7-post-stay--retention-engine)
8. [Guest Portal (Minimal V1)](#8-guest-portal-minimal-v1)
9. [Communication Engine Requirements](#9-communication-engine-requirements)
10. [Guest Data & Privacy Requirements](#10-guest-data--privacy-requirements)
11. [Guest Experience Metrics](#11-guest-experience-metrics)
12. [Acceptance Criteria & Test Matrix](#12-acceptance-criteria--test-matrix)
13. [Definition of Done — Guest Experience V1](#13-definition-of-done--guest-experience-v1)

**Appendix A** — [Event Types (Guest Domain)](#appendix-a--event-types-guest-domain)
**Appendix B** — [Message Template Catalogue](#appendix-b--message-template-catalogue)
**Appendix C** — [Cross-Reference to Existing Models](#appendix-c--cross-reference-to-existing-models)

---

## 1. Executive Summary

Atlas PMS must turn every guest stay into a **relationship** — not a one-off OTA transaction. This addendum defines a lean guest experience platform that automates the full lifecycle from pre-arrival through post-stay retention, delivered primarily via WhatsApp (the default channel for Indian hospitality), with a lightweight guest portal that requires no app download or account creation.

### 1.1 Strategic Goal

> **Reduce OTA dependency by making Atlas-powered hosts the best guest experience in Indian short-stay hospitality, converting OTA-sourced guests into direct repeat bookers.**

### 1.2 Design Principles

| Principle | Implication |
|-----------|-------------|
| **WhatsApp-first** | Indian guests live on WhatsApp. Every lifecycle touchpoint is WhatsApp by default, SMS fallback, email tertiary |
| **Zero-friction guest experience** | No app download, no account creation. Token-based portal access via link |
| **Host-branded** | All guest-facing messages and the portal show the host/property brand, not "Atlas" |
| **Automation-native** | Every guest touchpoint is driven by `AutomationSchedule` + `OutboxMessage` — no manual intervention required for happy path |
| **Privacy-lean** | Store minimum PII. No ID images in V1. Consent-first WhatsApp messaging |
| **Direct-booking flywheel** | Every post-stay touchpoint nudges the guest toward booking direct next time |

### 1.3 V1 vs V2 Scope

| Capability | V1 (Lean, rule-based) | V2 (AI-assisted, extended) |
|------------|----------------------|---------------------------|
| Pre-arrival messaging | Template-based, time-triggered | Personalized by guest history + AI |
| Check-in | Self check-in via portal link + manual access codes | Smart lock integration, facial recognition |
| In-stay | Issue reporting, satisfaction check, static recommendations | AI concierge, real-time sentiment, dynamic upsell |
| Check-out | Reminder + auto-ops trigger | Automated damage assessment, express checkout |
| Post-stay | Review request, coupon, re-engagement | Predictive retention, loyalty program, referral network |
| Guest portal | Static booking page, token access | Full PWA with chat, itinerary, local experiences |
| Language | English | Multi-language (Hindi, Kannada, etc.) |

---

## 2. Guest Lifecycle Model

### 2.1 Lifecycle Stages

```
PRE_BOOKING ──► BOOKING_CONFIRMED ──► PRE_ARRIVAL ──► CHECK_IN ──► IN_STAY ──► CHECK_OUT ──► POST_STAY ──► REPEAT
     │                                                                                             │
     │                                                                                             ▼
     └─────────────────────────────────────────────────────────────────────────────────────── REPEAT (loop)
```

| Stage | Code | Entry Trigger | Exit Trigger | Duration |
|-------|------|--------------|--------------|----------|
| **Pre-Booking** | `PRE_BOOKING` | Guest inquiry or lead created (`BookingStatus = 'Lead'`) | Booking confirmed or lead expired | Variable |
| **Booking Confirmed** | `BOOKING_CONFIRMED` | `booking.confirmed` event | T-7 days before check-in (or immediately if < 7 days out) | Until pre-arrival window |
| **Pre-Arrival** | `PRE_ARRIVAL` | T-7 days before `CheckinDate` | Guest checks in (`stay.checked_in`) | 1–7 days |
| **Check-In** | `CHECK_IN` | `stay.checked_in` event or guest self check-in | First night completed | Hours |
| **In-Stay** | `IN_STAY` | Day 1 after check-in | Check-out day begins | 1–N nights |
| **Check-Out** | `CHECK_OUT` | Check-out day, T-12h before `CheckOutTime` | `stay.checked_out` event | Hours |
| **Post-Stay** | `POST_STAY` | `stay.checked_out` event | 90 days after checkout (moves to dormant) | 1–90 days |
| **Repeat / Retention** | `REPEAT` | Guest has ≥ 2 completed stays OR responds to re-engagement | Ongoing | Indefinite |

> **GX-LC-001**: The lifecycle stage is a **computed value** derived from booking state and timestamps — not stored as a separate field. API endpoints and automation rules evaluate stage at runtime.

### 2.2 State Transitions & Triggers

| From | To | Trigger Event | System Action |
|------|----|--------------|---------------|
| `PRE_BOOKING` | `BOOKING_CONFIRMED` | `booking.confirmed` | Schedule pre-arrival automation sequence |
| `BOOKING_CONFIRMED` | `PRE_ARRIVAL` | Clock reaches `CheckinDate - 7 days` | Send welcome message |
| `PRE_ARRIVAL` | `CHECK_IN` | `stay.checked_in` event | Send Day-1 satisfaction check schedule |
| `PRE_ARRIVAL` | `CHECK_IN` | Guest taps "I have checked in" on portal | Log check-in time, publish `stay.checked_in` |
| `CHECK_IN` | `IN_STAY` | Day 2 of stay begins (00:00 local) | Enable issue reporting, recommendations |
| `IN_STAY` | `CHECK_OUT` | `CheckoutDate` - 12 hours | Send check-out reminder |
| `CHECK_OUT` | `POST_STAY` | `stay.checked_out` event | Trigger review request schedule, ops tasks |
| `POST_STAY` | `REPEAT` | Guest creates new booking | Tag as returning guest |

### 2.3 Data Required Per Stage

| Stage | Required Data | Source |
|-------|--------------|--------|
| `PRE_BOOKING` | Guest name, phone, inquiry details | `Guest`, lead form |
| `BOOKING_CONFIRMED` | Full booking details, payment status, guest contact | `Booking`, `Guest`, `Payment` |
| `PRE_ARRIVAL` | Property address, check-in time, WiFi, house rules, access instructions, balance due | `Listing`, `Property`, `Booking.PaymentStatus` |
| `CHECK_IN` | Access code, check-in confirmation, emergency contacts | `ListingAccessInfo` (new), `Property.ContactPhone` |
| `IN_STAY` | Local recommendations, add-on options, issue reporting link, emergency number | `PropertyRecommendation` (new), `Listing` |
| `CHECK_OUT` | Check-out time, checklist, damage prompt, invoice link | `Listing.CheckOutTime`, `Booking` |
| `POST_STAY` | Review links, coupon code, direct booking URL | `GuestCoupon` (new), tenant booking URL |
| `REPEAT` | Past stay history, preferences, loyalty status | `Booking` history, `GuestTag` (new) |

---

## 3. Pre-Arrival Automation (High Impact V1)

### 3.1 Message Sequence

| # | Message | Trigger Time | Event Type | Channel | Retry |
|---|---------|-------------|------------|---------|-------|
| 1 | **Booking Confirmation** | Immediately on `booking.confirmed` | `guest.booking_confirmed` | WhatsApp → SMS → Email | Yes (3×) |
| 2 | **Welcome Message** | `CheckinDate` − 7 days (or on confirmation if < 7 days) | `guest.welcome` | WhatsApp | Yes (2×) |
| 3 | **Balance Payment Reminder** | `CheckinDate` − 3 days (only if `PaymentStatus ≠ 'Paid'`) | `guest.payment_reminder` | WhatsApp → SMS | Yes (2×) |
| 4 | **House Rules** | `CheckinDate` − 2 days | `guest.house_rules` | WhatsApp | No |
| 5 | **Check-In Instructions** | `CheckinDate` − 1 day (24h before check-in time) | `guest.checkin_instructions` | WhatsApp → SMS | Yes (2×) |
| 6 | **Location & Directions** | `CheckinDate` − 1 day (with check-in instructions) | `guest.location` | WhatsApp | No |
| 7 | **Early Check-In Upsell** | `CheckinDate` − 1 day (only if property supports it) | `guest.early_checkin_offer` | WhatsApp | No |
| 8 | **ID Verification Request** | `CheckinDate` − 1 day (future-ready, flag only in V1) | `guest.id_verification` | WhatsApp | No |

> **GX-PA-001**: Messages 2–8 are created as `AutomationSchedule` entries when `booking.confirmed` fires. The `AutomationSchedulerHostedService` (existing, 30s poll, batch 50) publishes them as `OutboxMessage` at the scheduled time.

### 3.2 Template Content Specification

#### 3.2.1 Booking Confirmation

```
Hi {{guest_name}}! 🎉

Your stay at {{property_name}} is confirmed!

📅 Check-in: {{checkin_date}} at {{checkin_time}}
📅 Check-out: {{checkout_date}} at {{checkout_time}}
🏠 {{listing_name}} ({{listing_type}})
👤 Guests: {{guest_count}}
💰 Total: ₹{{total_amount}}

{{#if balance_due}}
⚠️ Balance due: ₹{{balance_due}} — we'll send a reminder closer to your stay.
{{/if}}

Your booking details: {{portal_link}}

Questions? Reply here or call {{host_phone}}.

— {{host_name}}
```

#### 3.2.2 Check-In Instructions

```
Hi {{guest_name}},

Your stay at {{property_name}} starts tomorrow! Here's everything you need:

🕐 Check-in time: {{checkin_time}}
📍 Address: {{property_address}}
🗺️ Directions: {{map_link}}

{{#if access_code}}
🔑 Access code: {{access_code}}
{{else}}
🔑 Our staff will meet you at the property.
{{/if}}

📶 WiFi: {{wifi_name}} / {{wifi_password}}

📋 Check-in link: {{checkin_portal_link}}
Please tap "I have checked in" when you arrive.

Need help? Call {{host_phone}}.

— {{host_name}}
```

> **GX-PA-002**: Templates use Handlebars-style placeholders. Variables are resolved from `Booking`, `Guest`, `Listing`, `Property`, and `ListingAccessInfo` entities at send time by the automation pipeline.

### 3.3 Time-Based Trigger Configuration

| Parameter | Default | Configurable? | Stored On |
|-----------|---------|---------------|-----------|
| Welcome message offset | `CheckinDate − 7 days` | Per tenant (1–14 days) | `TenantGuestExperienceConfig` (new) |
| Payment reminder offset | `CheckinDate − 3 days` | Per tenant (1–7 days) | `TenantGuestExperienceConfig` |
| Check-in instructions offset | `CheckinDate − 1 day` | Per tenant (1–3 days) | `TenantGuestExperienceConfig` |
| House rules offset | `CheckinDate − 2 days` | Per tenant (1–5 days) | `TenantGuestExperienceConfig` |

> **GX-PA-003**: If the booking is created within the pre-arrival window (e.g., same-day booking), all due messages are sent immediately in sequence with a 5-minute spacing to avoid flooding.

### 3.4 Channel Priority & Fallback

```
WhatsApp ──(fail after 2 attempts)──► SMS ──(fail after 2 attempts)──► Email
```

| Rule | Description |
|------|-------------|
| **Primary channel** | WhatsApp (via existing provider) |
| **Fallback trigger** | WhatsApp delivery fails after 2 attempts with 15-min retry interval |
| **SMS fallback** | Abbreviated version of message (within 160 chars or concatenated) |
| **Email fallback** | Full HTML version of message |
| **Channel recorded** | `CommunicationLog.Channel` stores which channel actually delivered |

> **GX-PA-004**: Channel priority respects `Guest.CommsOptedOut` (if field exists). If opted out of WhatsApp, SMS is primary. If opted out of all, only email is sent. Opt-out status is tracked per guest per channel.

### 3.5 Quiet Hours

| Rule | Default | Configurable? |
|------|---------|---------------|
| No messages before | 08:00 local time | Per tenant |
| No messages after | 21:00 local time | Per tenant |
| Deferred messages | Queued and sent at next window open | — |
| Exception | P0 alerts (safety, emergency) ignore quiet hours | — |

> **GX-PA-005**: Quiet hours are evaluated by the `AutomationSchedulerHostedService` before publishing. If current time is within quiet hours, `DueAtUtc` is adjusted to the next window opening. Timezone is derived from `Property.Address` (India = IST / UTC+5:30).

### 3.6 Retry Policy

| Scenario | Max Attempts | Interval | Escalation |
|----------|-------------|----------|------------|
| WhatsApp send failure | 3 | 15 min, 30 min, 60 min | Falls back to SMS |
| SMS send failure | 2 | 15 min, 30 min | Falls back to Email |
| Email send failure | 3 | 30 min, 1h, 2h | Log as failed; alert host dashboard |
| All channels exhausted | — | — | `CommunicationLog.Status = 'AllChannelsFailed'`; host alerted via dashboard notification |

### 3.7 Idempotency

> **GX-PA-006**: Each scheduled message has an `IdempotencyKey` composed of `{BookingId}:{EventType}:{SequenceNumber}`. The `CommunicationLog` is checked before sending. If a matching key with `Status = 'Sent'` or `'Delivered'` exists, the message is skipped. This prevents duplicate messages from retry storms or duplicate event processing.

---

## 4. Self Check-In System (Lean V1)

### 4.1 Check-In Flow

```
Guest receives check-in instructions (WhatsApp)
    │
    ▼
Guest opens portal link (token-based, no login)
    │
    ▼
Portal shows: property info, WiFi, access code, house rules
    │
    ▼
Guest taps "I have checked in" button
    │
    ▼
System logs CheckedInAtUtc on Booking
    │
    ▼
stay.checked_in event fires
    │
    ▼
Automation triggers: Day-1 satisfaction check, inventory deduction, room status update
```

### 4.2 Access Code Management (V1: Manual Entry)

| Feature | V1 | V2 |
|---------|----|----|
| Code source | Host manually enters access code per listing or per booking | Smart lock API integration |
| Storage | `ListingAccessInfo.AccessCode` (new entity) | Smart lock provider API |
| Rotation | Manual — host updates when changed | Auto-rotate per booking |
| Display | Shown on guest portal 24h before check-in | OTP or time-limited code |
| Security | Code visible only via authenticated portal link | Code auto-expires after checkout |

**New entity: `ListingAccessInfo`**

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` (PK) | |
| `TenantId` | `int` | |
| `ListingId` | `int` (FK → Listing) | |
| `AccessCode` | `nvarchar(50)` | Door/gate code |
| `AccessInstructions` | `nvarchar(2000)` | Free-form check-in instructions |
| `ParkingInstructions` | `nvarchar(500)` | Parking details |
| `EmergencyContactName` | `nvarchar(100)` | Emergency contact |
| `EmergencyContactPhone` | `varchar(20)` | Emergency phone |
| `MapUrl` | `nvarchar(500)` | Google Maps / location link |
| `UpdatedAtUtc` | `datetime2` | |

> **GX-CI-001**: `ListingAccessInfo` is a 1:1 relationship with `Listing`. Access code is shown on the guest portal only within 24 hours of `CheckinDate`. Before that, a placeholder "Check-in details will be available closer to your stay" is shown.

### 4.3 Check-In Confirmation

| Action | System Response |
|--------|----------------|
| Guest taps "I have checked in" | `Booking.CheckedInAtUtc = DateTime.UtcNow` |
| | `stay.checked_in` OutboxMessage published |
| | Downstream: inventory deduction (RA-OPS-001), room status → `OCCUPIED`, Day-1 check scheduled |
| Guest does NOT check in by `CheckinTime + 2h` | Alert to host via WhatsApp: "Guest {{guest_name}} has not checked in for booking #{{booking_id}}. Check-in was expected at {{checkin_time}}." |

> **GX-CI-002**: The "not checked in" alert is an `AutomationSchedule` entry created at `CheckinDate + CheckinTime + 2h`. If `Booking.CheckedInAtUtc` is already set when the schedule fires, the alert is skipped (idempotency).

### 4.4 Smart Lock Integration Hook (V2-Ready)

V1 exposes an **interface** for future smart lock integration:

```
Interface: IAccessCodeProvider
  - GetAccessCode(listingId, bookingId) → string
  - RevokeAccessCode(listingId, bookingId) → void

V1 implementation: ManualAccessCodeProvider
  - Returns ListingAccessInfo.AccessCode (static per listing)

V2 implementation: SmartLockAccessCodeProvider
  - Calls smart lock API (e.g., August, Yale, Igloohome)
  - Generates time-limited code for booking window
  - Auto-revokes after checkout
```

---

## 5. In-Stay Experience Engine

### 5.1 In-Stay Touchpoints

| # | Touchpoint | Trigger | Channel | Content |
|---|-----------|---------|---------|---------|
| 1 | **Day-1 Satisfaction Check** | `CheckedInAtUtc + 20 hours` | WhatsApp | "Hi {{guest_name}}, how's your stay at {{property_name}} so far? Everything good? 👍 Reply if you need anything!" |
| 2 | **Issue Reporting Link** | Available on guest portal from check-in | Portal | Form: issue type, description, urgency |
| 3 | **Local Recommendations** | Available on guest portal from check-in | Portal | Property-configured list of restaurants, attractions, transport |
| 4 | **Add-On Upsell** | Day 2 of stay (for stays ≥ 3 nights) | WhatsApp | Late checkout, extra cleaning, airport transfer |
| 5 | **Mid-Stay Check** | Midpoint of stay (for stays ≥ 5 nights) | WhatsApp | "Hope you're enjoying your stay! Need anything?" |
| 6 | **Emergency Contact** | Always available on portal | Portal | Host phone, property emergency number, local emergency (112) |

### 5.2 Issue Reporting Flow

```
Guest opens portal → taps "Report Issue"
    │
    ▼
Form: Category (plumbing, electrical, WiFi, cleaning, noise, safety, other)
      Urgency: Not urgent / Urgent / Emergency
      Description (free text, 500 chars max)
    │
    ▼
System creates:
    │
    ├── MaintenanceTicket (if category is maintenance-related)
    │     Severity mapped: Not urgent → LOW, Urgent → HIGH, Emergency → P0
    │     Linked to BookingId
    │
    └── OR Incident (if category is noise, safety, or other)
          IncidentType mapped from category
          Linked to BookingId
    │
    ▼
Host notified via WhatsApp: "Guest {{guest_name}} reported an issue: {{category}} — {{description}}"
    │
    ▼
Guest sees acknowledgment: "We've received your report and our team is on it."
    │
    ▼
When resolved: Guest notified via WhatsApp: "Your reported issue ({{category}}) has been resolved. Thank you for your patience!"
```

> **GX-IS-001**: Issue reports from the guest portal create `MaintenanceTicket` (RA-OPS-001) or `Incident` (RA-OPS-001) entities. The `CreatedBy` is set to `'guest:{{guestId}}'` to distinguish from staff-created entries.

### 5.3 Local Recommendations

**New entity: `PropertyRecommendation`**

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` (PK) | |
| `TenantId` | `int` | |
| `PropertyId` | `int` (FK → Property) | |
| `Category` | `varchar(30)` | `RESTAURANT`, `CAFE`, `ATTRACTION`, `TRANSPORT`, `SHOPPING`, `MEDICAL`, `OTHER` |
| `Name` | `nvarchar(200)` | e.g., "Vidyarthi Bhavan" |
| `Description` | `nvarchar(500)` | Short description |
| `MapUrl` | `nvarchar(500)` | Google Maps link |
| `DistanceKm` | `decimal(5,1)?` | Distance from property |
| `Phone` | `varchar(20)` | Contact phone |
| `SortOrder` | `int` | Display order |
| `IsActive` | `bit` | |
| `CreatedAtUtc` | `datetime2` | |

> **GX-IS-002**: Recommendations are managed by the host via the admin portal. Shown on the guest portal under "Things to Do / Nearby". V2: AI-personalized based on guest preferences.

### 5.4 Add-On Upsell (V1: Simple)

| Add-On | Code | Description | Pricing |
|--------|------|-------------|---------|
| Late checkout | `LATE_CHECKOUT` | Extend checkout by 2–4 hours | ₹ configured per listing |
| Extra cleaning | `EXTRA_CLEANING` | Mid-stay deep clean | ₹ configured per listing |
| Airport transfer | `AIRPORT_TRANSFER` | V2: Referral link to partner | — |
| Early check-in | `EARLY_CHECKIN` | Check in 2–4 hours before standard | ₹ configured per listing |

V1 implementation:
- Add-on options are shown on the guest portal.
- Guest taps "Request" → creates an `OutboxMessage` with `EventType = 'guest.addon_request'`.
- Host receives WhatsApp notification with details.
- Host manually confirms/declines (V1). V2: auto-confirm with payment.

> **GX-IS-003**: Add-on requests are informational in V1 — no payment flow. The host handles pricing and confirmation manually. The request is logged for analytics.

### 5.5 Escalation Rules (In-Stay Issues)

| Trigger | Escalation |
|---------|-----------|
| Guest reports `Emergency` issue | Immediate WhatsApp to host + property manager + Atlas Admin |
| Guest issue not acknowledged within 30 min | Escalate to property manager |
| Guest issue not resolved within severity SLA (RA-OPS-001 §3.2) | Escalate per RA-OPS-001 SLA chain |
| Guest sends negative satisfaction response | Alert host; flag for follow-up |

---

## 6. Check-Out Workflow

### 6.1 Check-Out Message Sequence

| # | Message | Trigger Time | Event Type | Channel |
|---|---------|-------------|------------|---------|
| 1 | **Check-Out Reminder** | `CheckoutDate` morning, 12h before checkout time | `guest.checkout_reminder` | WhatsApp |
| 2 | **Check-Out Checklist** | With reminder (or separately 2h before) | `guest.checkout_checklist` | WhatsApp |
| 3 | **Damage Self-Report Prompt** | With checklist | embedded in checklist message | WhatsApp |
| 4 | **Thank You + Review Request** | `CheckedOutAtUtc + 2 hours` | `guest.review_request` | WhatsApp → Email |

### 6.2 Check-Out Reminder Template

```
Hi {{guest_name}},

Your check-out from {{property_name}} is tomorrow at {{checkout_time}}.

📋 Before you leave:
✅ Return all keys/remotes
✅ Check for personal belongings
✅ Close windows and lock doors
✅ Leave used towels in bathroom
✅ Switch off AC and lights

{{#if damage_to_report}}
⚠️ Any damages to report? Tap here: {{damage_report_link}}
{{/if}}

Thank you for staying with us! 🙏

— {{host_name}}
```

### 6.3 Check-Out Confirmation & Ops Integration

| Event | System Action |
|-------|---------------|
| Host marks booking as checked out (or auto at `CheckoutDate + CheckOutTime + 1h`) | `Booking.CheckedOutAtUtc = DateTime.UtcNow` |
| | `stay.checked_out` OutboxMessage published |
| | **Housekeeping**: Auto-create `TURNOVER` task (RA-OPS-001 §2.8) |
| | **Inventory**: Auto-deduct consumables — NOT on checkout, already done on check-in (RA-OPS-001 §4.3.2) |
| | **Review request**: Schedule at `CheckedOutAtUtc + 2h` |
| | **Guest portal**: Update to post-stay view (invoice, review link) |

> **GX-CO-001**: Auto-checkout fires if the host has not manually checked out the guest by `CheckoutDate + CheckOutTime + 1 hour`. An `AutomationSchedule` entry is created at booking confirmation with `EventType = 'stay.auto_checkout_due'`.

### 6.4 Damage Reporting (Guest-Initiated)

The guest portal includes a "Report Damage" link on the check-out page:
- Guest describes damage in free text (500 chars).
- Creates an `Incident` with `IncidentType = 'DAMAGE'` and `CreatedBy = 'guest:{{guestId}}'`.
- Host notified via WhatsApp.
- V2: Photo upload and cost estimation.

---

## 7. Post-Stay & Retention Engine

### 7.1 Review Request Flow

| Step | Timing | Action |
|------|--------|--------|
| 1 | `CheckedOutAtUtc + 2h` | Send review request via WhatsApp |
| 2 | `CheckedOutAtUtc + 48h` | If no review submitted → send reminder via WhatsApp |
| 3 | `CheckedOutAtUtc + 7 days` | Final reminder via Email (gentler tone) |
| 4 | After 3 attempts | Stop; mark `ReviewRequestStatus = 'Exhausted'` |

#### 7.1.1 Review Request Template

```
Hi {{guest_name}},

Thank you for staying at {{property_name}}! We hope you had a wonderful time. 🙏

Your feedback helps us improve. Would you mind leaving a quick review?

⭐ Rate your stay: {{review_link}}

It takes just 30 seconds. Thank you!

— {{host_name}}
```

> **GX-PS-001**: The review link points to the guest portal review page (token-authenticated). The review is a simple 1–5 star rating with an optional text comment (500 chars). V2: route to OTA review pages if booking source is Airbnb/Booking.com.

### 7.2 Guest Review Model

**New entity: `GuestReview`**

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` (PK) | |
| `TenantId` | `int` | |
| `BookingId` | `int` (FK → Booking) | One review per booking |
| `GuestId` | `int` (FK → Guest) | |
| `PropertyId` | `int` (FK → Property) | Denormalized |
| `OverallRating` | `int` | 1–5 stars |
| `CleanlinessRating` | `int?` | 1–5 (optional V1, expanded V2) |
| `CommunicationRating` | `int?` | 1–5 (optional) |
| `LocationRating` | `int?` | 1–5 (optional) |
| `ValueRating` | `int?` | 1–5 (optional) |
| `Comment` | `nvarchar(2000)` | Free-form text |
| `ReviewSource` | `varchar(20)` | `PORTAL` (guest portal), `MANUAL` (host-entered from OTA) |
| `Status` | `varchar(15)` | `SUBMITTED`, `PUBLISHED`, `FLAGGED`, `HIDDEN` |
| `SubmittedAtUtc` | `datetime2` | |
| `PublishedAtUtc` | `datetime2?` | |

**Unique constraint**: `IX_GuestReview_BookingId` (one review per booking).

### 7.3 Direct Booking Incentive Coupon

> **GX-PS-002**: After a successful stay (rating ≥ 3 stars or no review), the guest receives a direct booking coupon to incentivize bypassing OTAs for their next stay.

**New entity: `GuestCoupon`**

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` (PK) | |
| `TenantId` | `int` | |
| `GuestId` | `int` (FK → Guest) | |
| `SourceBookingId` | `int` (FK → Booking) | Booking that generated this coupon |
| `CouponCode` | `varchar(20)` | Unique code (e.g., `WELCOME10-ABCXYZ`) |
| `DiscountType` | `varchar(10)` | `PERCENT` or `FLAT` |
| `DiscountValue` | `decimal(10,2)` | e.g., 10 (for 10%) or 500 (for ₹500 off) |
| `MinBookingAmount` | `decimal(10,2)?` | Minimum booking value to apply |
| `ValidFromUtc` | `datetime2` | |
| `ValidUntilUtc` | `datetime2` | Expiry (default: 6 months from creation) |
| `MaxUses` | `int` | Default: 1 |
| `TimesUsed` | `int` | Default: 0 |
| `Status` | `varchar(10)` | `ACTIVE`, `USED`, `EXPIRED`, `REVOKED` |
| `CreatedAtUtc` | `datetime2` | |

**Coupon generation rules**:
- Auto-created by automation rule when `stay.checked_out` fires AND stay was ≥ 1 night AND booking was NOT cancelled.
- Default: 10% off next direct booking (configurable per tenant via `TenantGuestExperienceConfig`).
- Coupon code format: `{TenantPrefix}{DiscountValue}-{6-char random}` (e.g., `ATLAS10-X7K9M2`).
- Sent to guest via WhatsApp 24h after checkout (bundled with or after review request).

### 7.4 Repeat Guest Tagging

**New entity: `GuestTag`**

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` (PK) | |
| `TenantId` | `int` | |
| `GuestId` | `int` (FK → Guest) | |
| `Tag` | `varchar(30)` | Tag code |
| `Value` | `nvarchar(200)` | Tag value (if applicable) |
| `AppliedAtUtc` | `datetime2` | |
| `AppliedBy` | `varchar(20)` | `SYSTEM` or `MANUAL` |

**System-applied tags:**

| Tag | Condition | Applied When |
|-----|-----------|-------------|
| `RETURNING_GUEST` | Guest has ≥ 2 completed stays with this tenant | On booking confirmation for 2nd+ stay |
| `VIP` | Guest has ≥ 5 completed stays OR total spend > ₹50,000 | On qualifying booking checkout |
| `DIRECT_BOOKER` | Guest has ≥ 1 booking with `BookingSource = 'Direct'` | On direct booking confirmation |
| `HIGH_RATED` | Guest left ≥ 4-star reviews on all stays | On review submission |
| `ISSUE_REPORTER` | Guest reported ≥ 2 issues across stays | On 2nd issue report |
| `OTA_CONVERTED` | First booking was OTA, subsequent booking is Direct | On direct booking confirmation |

> **GX-PS-003**: Tags are additive and never auto-removed. Manual tags can be added by the host. Tags are visible on the booking detail page and guest profile.

### 7.5 Guest Lifetime Value Tracking

Computed fields on guest (not stored — calculated at query time):

| Metric | Formula |
|--------|---------|
| **Total Stays** | `COUNT(Booking WHERE BookingStatus = 'Completed' AND GuestId = X)` |
| **Total Revenue** | `SUM(Booking.FinalAmount WHERE BookingStatus = 'Completed' AND GuestId = X)` |
| **Average Stay Length** | `AVG(DATEDIFF(day, CheckinDate, CheckoutDate))` |
| **Average Rating Given** | `AVG(GuestReview.OverallRating WHERE GuestId = X)` |
| **First Stay Date** | `MIN(CheckinDate)` |
| **Last Stay Date** | `MAX(CheckinDate)` |
| **Days Since Last Stay** | `DATEDIFF(day, MAX(CheckoutDate), GETUTCDATE())` |
| **Booking Source Mix** | `% Direct vs % OTA` |

### 7.6 WhatsApp Re-Engagement Campaign

| Trigger | Timing | Message |
|---------|--------|---------|
| 30 days after checkout | `CheckedOutAtUtc + 30 days` | "We miss you at {{property_name}}! Planning your next trip? Book direct and save with your exclusive code: {{coupon_code}}" |
| 90 days after checkout | `CheckedOutAtUtc + 90 days` | Gentle re-engagement with seasonal offer (if available) |
| Guest's past check-in anniversary | Same month, next year | "It's been a year since your stay at {{property_name}}! Ready for round two?" |

> **GX-PS-004**: Re-engagement messages respect opt-out and quiet hours. They are created as `AutomationSchedule` entries at checkout time. If the guest books again before the schedule fires, the re-engagement is cancelled.

### 7.7 Direct Booking Referral Link

Each guest receives a unique referral URL:

```
https://{{tenant_slug}}.atlashomestays.com/book?ref={{guest_referral_code}}
```

- `guest_referral_code` is auto-generated on first completed stay: `{GuestId base36}-{4 random chars}`.
- When a new guest books using a referral link, both the referrer and the new guest receive a discount (configurable per tenant).
- V1: Referral link is generated and included in post-stay messages. Tracking is basic (referral code stored on new booking).
- V2: Full referral program with dashboards, tiers, and payouts.

**New field on `Booking`:** `ReferralCode varchar(20)?` — the referral code used at booking time.

**New field on `Guest`:** `ReferralCode varchar(20)?` — this guest's unique referral code.

---

## 8. Guest Portal (Minimal V1)

### 8.1 Portal Content Per Booking

| Section | Content | Available When |
|---------|---------|---------------|
| **Booking Details** | Property name, dates, guests, amount, payment status | Always |
| **Check-In Instructions** | Access code, directions, map link, WiFi, parking | 24h before check-in |
| **House Rules** | Property-specific rules | After confirmation |
| **Contact Host** | Host phone (tap-to-call), WhatsApp link | Always |
| **Local Recommendations** | Nearby restaurants, attractions, medical | After check-in |
| **Add-Ons** | Late checkout, extra cleaning (request buttons) | During stay |
| **Report Issue** | Issue reporting form | After check-in |
| **Check-In Button** | "I have checked in" | Check-in day only, until checked in |
| **Check-Out Checklist** | Items to check before leaving | Check-out day |
| **Damage Report** | Report damage form | Check-out day |
| **Download Invoice** | PDF invoice (if applicable) | After checkout |
| **Leave Review** | Star rating + comment form | After checkout |
| **Coupon** | Direct booking discount code | After checkout |

### 8.2 Token-Based Secure Access

| Feature | Design |
|---------|--------|
| **URL format** | `https://{{tenant_slug}}.atlashomestays.com/guest/{{booking_token}}` |
| **Token generation** | `booking_token = HMAC-SHA256(BookingId + GuestId + TenantId, secret)` truncated to 32 hex chars |
| **Token storage** | `GuestPortalToken` entity (see §9.1) |
| **No login required** | Token in URL is the authentication — guest clicks link from WhatsApp |
| **Token expiry** | 30 days after `CheckoutDate` (portal becomes read-only for invoice/review after that, then 404) |
| **Rate limiting** | Max 60 requests per token per hour (prevent abuse) |
| **PII protection** | Portal shows guest's own data only. No access to other bookings or guests |

**New entity: `GuestPortalToken`**

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` (PK) | |
| `TenantId` | `int` | |
| `BookingId` | `int` (FK → Booking) | |
| `GuestId` | `int` (FK → Guest) | |
| `Token` | `varchar(64)` | HMAC-generated token |
| `IsActive` | `bit` | Active until expiry |
| `ExpiresAtUtc` | `datetime2` | `CheckoutDate + 30 days` |
| `LastAccessedAtUtc` | `datetime2?` | |
| `AccessCount` | `int` | Total page views |
| `CreatedAtUtc` | `datetime2` | |

**Unique constraint**: `IX_GuestPortalToken_Token` (unique token lookup).

> **GX-GP-001**: Token is generated when `booking.confirmed` fires and included in the first WhatsApp message. The portal URL is the single entry point for all guest interactions. No separate mobile app required.

### 8.3 Portal UI Requirements

| Requirement | Specification |
|-------------|--------------|
| **Mobile-first** | Designed for 360px width (typical Indian phone). No horizontal scroll |
| **Load time** | < 2 seconds on 3G connection (target < 200KB initial payload) |
| **Offline tolerance** | Static content (house rules, recommendations) cached via service worker. Dynamic content (check-in status) requires connectivity |
| **Branding** | Shows property/host name and logo (from `TenantProfile`). No "Powered by Atlas" in V1 (white-label) |
| **Language** | English V1. Multi-language V2 |
| **Accessibility** | Large tap targets (min 44px), high contrast text, screen reader compatible |
| **No app install prompt** | Never prompt for app install. Pure web page |
| **Dark mode** | Respects device preference (V2) |

### 8.4 Portal Hosting

| Aspect | Design |
|--------|--------|
| **Framework** | React + Vite (same stack as admin portal, separate build) |
| **Hosting** | Cloudflare Pages (same as admin portal, separate project) |
| **API calls** | To `atlas-api` with token-based auth header |
| **Domain** | `{{tenant_slug}}.atlashomestays.com/guest/{{token}}` or `guest.atlashomestays.com/{{token}}` |
| **CDN** | Cloudflare edge caching for static assets |

---

## 9. Communication Engine Requirements

### 9.1 Integration with Existing Notification System

The guest experience platform builds **entirely** on the existing notification infrastructure:

| Component | Existing | Guest Experience Usage |
|-----------|----------|----------------------|
| `OutboxMessage` | Transactional outbox with at-least-once delivery | All guest messages start as `OutboxMessage` entries |
| `AutomationSchedule` | Time-based scheduling with poll + publish | All time-triggered messages (pre-arrival, post-stay) are scheduled here |
| `AutomationSchedulerHostedService` | 30s poll, batch 50, max 5 attempts | Publishes guest messages at scheduled time |
| `OutboxDispatcherHostedService` | Dispatches to Service Bus or inline handler | Routes to WhatsApp/SMS/Email provider |
| `CommunicationLog` | Delivery tracking with idempotency | Tracks every guest message attempt and status |
| `MessageTemplate` | Template with variables, channel, event type | All guest message templates stored here |

> **GX-CE-001**: No new messaging infrastructure is required. The guest experience module **only adds new event types, templates, and automation schedule entries** to the existing pipeline.

### 9.2 Template Management

| Feature | V1 | V2 |
|---------|----|----|
| **Template source** | Platform provides defaults; tenant can override via admin portal | Visual template editor with preview |
| **Template scope** | `ScopeType = 'TENANT'` (tenant-wide) or `'PROPERTY'` (property-specific, via `ScopeId = PropertyId`) | Room-type-level |
| **Variable substitution** | Handlebars-style: `{{guest_name}}`, `{{property_name}}`, `{{checkin_date}}`, etc. | AI-generated personalized sections |
| **Template versioning** | `TemplateVersion` integer; only `IsActive = true` version is used | A/B testing with conversion tracking |
| **Template validation** | All variables must resolve at send time; unresolved → `{{variable_name}}` left as-is with warning log | Pre-send preview with sample data |

**Standard variables available in all guest templates:**

| Variable | Source | Example |
|----------|--------|---------|
| `{{guest_name}}` | `Guest.Name` | "Priya Sharma" |
| `{{guest_phone}}` | `Guest.Phone` | "+91 98765 43210" |
| `{{property_name}}` | `Property.Name` | "Sunset Villa" |
| `{{property_address}}` | `Property.Address` | "123 MG Road, Bangalore" |
| `{{listing_name}}` | `Listing.Name` | "Deluxe Room 201" |
| `{{listing_type}}` | `Listing.Type` | "Private Room" |
| `{{checkin_date}}` | `Booking.CheckinDate` formatted | "15 Mar 2026" |
| `{{checkout_date}}` | `Booking.CheckoutDate` formatted | "18 Mar 2026" |
| `{{checkin_time}}` | `Listing.CheckInTime` | "2:00 PM" |
| `{{checkout_time}}` | `Listing.CheckOutTime` | "11:00 AM" |
| `{{guest_count}}` | `Booking.GuestsPlanned` | "2" |
| `{{total_amount}}` | `Booking.FinalAmount` formatted | "₹8,500" |
| `{{balance_due}}` | `Booking.FinalAmount - Booking.AmountReceived` | "₹3,000" |
| `{{booking_id}}` | `Booking.Id` | "12345" |
| `{{portal_link}}` | Generated portal URL | `https://sunset.atlas.../guest/abc123` |
| `{{checkin_portal_link}}` | Portal URL with check-in anchor | `https://sunset.atlas.../guest/abc123#checkin` |
| `{{review_link}}` | Portal URL with review anchor | `https://sunset.atlas.../guest/abc123#review` |
| `{{damage_report_link}}` | Portal URL with damage anchor | `https://sunset.atlas.../guest/abc123#damage` |
| `{{wifi_name}}` | `Listing.WifiName` | "SunsetVilla_5G" |
| `{{wifi_password}}` | `Listing.WifiPassword` | "welcome2026" |
| `{{access_code}}` | `ListingAccessInfo.AccessCode` | "4521" |
| `{{map_link}}` | `ListingAccessInfo.MapUrl` | Google Maps link |
| `{{host_name}}` | `TenantProfile.DisplayName` or `Tenant.OwnerName` | "Raj Properties" |
| `{{host_phone}}` | `Property.ContactPhone` | "+91 98765 00000" |
| `{{coupon_code}}` | `GuestCoupon.CouponCode` | "ATLAS10-X7K9M2" |
| `{{guest_referral_code}}` | `Guest.ReferralCode` | "a3x-k9m2" |

### 9.3 Message Delivery Status Tracking

| Status | Meaning | Stored In |
|--------|---------|-----------|
| `PENDING` | Message created, not yet sent | `CommunicationLog.Status` |
| `SENT` | Sent to provider API successfully | `CommunicationLog.Status` |
| `DELIVERED` | Provider confirmed delivery (WhatsApp blue tick) | `CommunicationLog.Status` |
| `READ` | Provider confirmed read (WhatsApp double blue tick) | `CommunicationLog.Status` |
| `FAILED` | Send failed (provider error) | `CommunicationLog.Status` |
| `BOUNCED` | Email bounced | `CommunicationLog.Status` |
| `OPTED_OUT` | Guest opted out of this channel | `CommunicationLog.Status` |
| `ALL_CHANNELS_FAILED` | All fallback channels exhausted | `CommunicationLog.Status` |

> **GX-CE-002**: Delivery status updates are received via webhook from WhatsApp/SMS provider. A dedicated endpoint `/api/webhooks/message-status` updates `CommunicationLog` entries matched by `ProviderMessageId`.

### 9.4 Message History & Dashboard

| View | Audience | Content |
|------|----------|---------|
| **Per-Booking Timeline** | Host (manager, owner) | All messages sent for a booking, with status, timestamp, channel |
| **Per-Guest History** | Host (manager, owner) | All messages across all bookings for a guest |
| **Failed Messages** | Host (manager, owner) | Messages with `FAILED` or `ALL_CHANNELS_FAILED` status |
| **Communication Summary** | Dashboard widget | Messages sent today, delivery rate, failure rate |

### 9.5 Multi-Language Support (V2-Ready)

| Aspect | V1 | V2 |
|--------|----|----|
| Template language | English only | `MessageTemplate.Language` field already exists; add Hindi, Kannada, etc. |
| Language selection | N/A | Guest preference on portal; auto-detect from phone locale |
| Translation | N/A | Manual translation of templates; AI-assisted translation V3 |

---

## 10. Guest Data & Privacy Requirements

### 10.1 Guest Data Inventory

| Data Field | Source | Purpose | Sensitivity |
|------------|--------|---------|-------------|
| `Guest.Name` | Booking form / OTA sync | Communication, personalization | PII — Medium |
| `Guest.Phone` | Booking form / OTA sync | WhatsApp/SMS messaging | PII — High |
| `Guest.Email` | Booking form / OTA sync | Email communication | PII — Medium |
| `Guest.IdProofUrl` | V2: ID upload | Verification | PII — Critical (NOT stored in V1) |
| `GuestTag.Tag` | System/manual | Segmentation, retention | Non-PII |
| `GuestReview.Comment` | Guest portal | Feedback, quality | Contains PII (names) — Medium |
| `GuestCoupon.CouponCode` | System | Retention | Non-PII |
| `GuestPortalToken.Token` | System | Authentication | Security — High |
| `Guest.ReferralCode` | System | Referral tracking | Non-PII |
| `CommunicationLog.*` | System | Audit, delivery tracking | Contains PII (phone/email) — High |

### 10.2 What Guest Data Is Stored

| Category | Stored | Not Stored (V1) |
|----------|--------|-----------------|
| **Identity** | Name, phone, email | ID proof images, passport number, Aadhaar |
| **Stay history** | Bookings, dates, amounts | Browsing behavior, location tracking |
| **Preferences** | Tags (system-applied) | Dietary preferences, pillow type (V2) |
| **Communication** | Message log (event type, channel, status, timestamp) | Message body content (only template ID stored) |
| **Financial** | Booking amounts, payments, coupons | Credit card numbers (handled by Razorpay) |
| **Feedback** | Review ratings and comments | Internal host notes about guest (V2) |

> **GX-PV-001**: Atlas does NOT store message body content in V1. `CommunicationLog` stores the `TemplateId` and `TemplateVersion` used. The actual rendered message can be reconstructed from template + booking data if needed for audit. This minimizes PII exposure in the database.

### 10.3 Retention Policy

| Data Type | Retention Period | After Expiry |
|-----------|-----------------|-------------|
| `Guest` record | Indefinite (needed for repeat guest tracking) | N/A |
| `GuestPortalToken` | 30 days after checkout | `IsActive = false`; token hash retained, full token deleted |
| `CommunicationLog` | 2 years | Archived to cold storage (V2); deleted |
| `GuestReview` | Indefinite | N/A |
| `GuestCoupon` | 6 months after expiry | Soft-delete (`Status = 'EXPIRED'`); hard-delete after 1 year |
| `GuestTag` | Indefinite | N/A |
| `Guest.ReferralCode` | Indefinite | N/A |
| `Booking` (existing) | Indefinite | Per existing policy |

### 10.4 Data Export Capability

> **GX-PV-002**: On request, a tenant can export all data held about a specific guest as a JSON file. This supports DPDP Act 2023 (India) compliance readiness. Export includes: Guest record, all bookings, reviews, tags, coupons, and communication log summaries (no message bodies).

**Endpoint**: `GET /api/guests/{guestId}/export` — returns JSON. Permission: `owner` only.

### 10.5 Data Deletion (V2-Ready)

| V1 | V2 |
|----|-----|
| Guest record can be anonymized by owner: Name → "Deleted Guest", Phone → hash, Email → hash | Full DPDP-compliant deletion workflow with cascading anonymization |
| No automated deletion | Retention-based auto-anonymization |

### 10.6 Access Restrictions

| Data | Who Can Access |
|------|---------------|
| Guest profile | `frontdesk`, `manager`, `owner` for own tenant |
| Guest communication log | `manager`, `owner` for own tenant |
| Guest review | `manager`, `owner` for own tenant; guest via portal |
| Guest financial data (coupons, payments) | `owner` for own tenant |
| Guest data export | `owner` only |
| Cross-tenant guest data | **NEVER** — global query filter enforced |

### 10.7 Consent Tracking

**New entity: `GuestConsent`**

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` (PK) | |
| `TenantId` | `int` | |
| `GuestId` | `int` (FK → Guest) | |
| `Channel` | `varchar(15)` | `WHATSAPP`, `SMS`, `EMAIL` |
| `ConsentStatus` | `varchar(10)` | `GRANTED`, `REVOKED` |
| `ConsentSource` | `varchar(20)` | `BOOKING_FORM`, `PORTAL`, `WHATSAPP_REPLY` |
| `GrantedAtUtc` | `datetime2` | |
| `RevokedAtUtc` | `datetime2?` | |
| `IpAddress` | `varchar(45)` | IP at time of consent action |

**Consent rules**:
- **WhatsApp**: Consent assumed on booking (guest provides phone for booking). Guest can reply "STOP" to opt out. Opt-out is processed by the WhatsApp inbound handler (`whatsapp.inbound.received` event).
- **SMS**: Same as WhatsApp.
- **Email**: Consent assumed on booking. Unsubscribe link in every email footer. Unsubscribe processed by email webhook.
- **Revoking consent**: Sets `ConsentStatus = 'REVOKED'`. All future messages to that channel for that guest are blocked. `CommunicationLog.Status = 'OPTED_OUT'`.

> **GX-PV-003**: The system MUST check `GuestConsent` before sending any message. If no consent record exists for the channel, consent is **assumed** for transactional messages (booking confirmation, check-in instructions) but **not assumed** for marketing messages (re-engagement, upsell). Marketing messages require explicit `GRANTED` consent.

---

## 11. Guest Experience Metrics

### 11.1 Tenant-Visible Metrics

| Metric | Definition | Data Source | Display |
|--------|-----------|-------------|---------|
| **Pre-Arrival Response Rate** | % of pre-arrival messages with `DELIVERED` or `READ` status | `CommunicationLog` where `EventType LIKE 'guest.%'` | % with trend |
| **Issue Resolution Time** | Avg time from guest issue report to resolution | `MaintenanceTicket` / `Incident` where `CreatedBy LIKE 'guest:%'` | Hours metric |
| **Guest Satisfaction Rating** | Avg `GuestReview.OverallRating` for period | `GuestReview` | Stars metric (1–5) |
| **Review Response Rate** | % of checkouts that resulted in a review | `GuestReview` count / `Booking` completed count | % metric |
| **Repeat Booking %** | % of guests with ≥ 2 completed bookings | `Booking` grouped by `GuestId` | % metric |
| **Direct Booking Conversion %** | % of bookings with `BookingSource = 'Direct'` | `Booking` | % metric with trend |
| **OTA-to-Direct Conversion** | Guests whose first booking was OTA and subsequent was Direct | `Booking` per `GuestId` ordered by date | Count + % |
| **Coupon Redemption Rate** | % of issued coupons with `Status = 'USED'` | `GuestCoupon` | % metric |
| **Referral Bookings** | Bookings with non-null `ReferralCode` | `Booking` | Count + revenue |
| **Self Check-In Rate** | % of check-ins done via guest portal vs. manual | `Booking` where `CheckedInAtUtc` set by portal action | % metric |

### 11.2 Management-Level Metrics (Atlas Internal)

| Metric | Definition | Access |
|--------|-----------|--------|
| **Platform Guest Satisfaction** | Avg rating across all tenants | Atlas Admin |
| **Message Delivery Health** | Overall delivery rate by channel across platform | Atlas Admin |
| **Guest Retention Cohort** | % of guests returning within 3/6/12 months (anonymized) | Atlas Admin |
| **Direct Booking Growth** | Month-over-month direct booking % increase across platform | Atlas Admin |
| **Coupon ROI** | Revenue from coupon-driven bookings vs. discount cost | Atlas Admin |
| **Portal Engagement** | Avg page views per portal session, most-used features | Atlas Admin |
| **Re-engagement Effectiveness** | % of re-engagement messages that lead to a new booking within 30 days | Atlas Admin |

---

## 12. Acceptance Criteria & Test Matrix

### 12.1 Given/When/Then Tests

#### GX-T01: Booking confirmed → pre-arrival messages scheduled

```
GIVEN a booking B1 is created for Guest G1 with CheckinDate = 2026-03-15
WHEN  B1.BookingStatus is set to 'Confirmed' (booking.confirmed event fires)
THEN  the following AutomationSchedule entries are created:
      - guest.welcome         at 2026-03-08 (T-7d)
      - guest.payment_reminder at 2026-03-12 (T-3d, only if PaymentStatus ≠ 'Paid')
      - guest.house_rules     at 2026-03-13 (T-2d)
      - guest.checkin_instructions at 2026-03-14 (T-1d)
AND   a GuestPortalToken is created for B1/G1
AND   a booking confirmation message is sent immediately via WhatsApp
```

#### GX-T02: Pre-arrival message sent at scheduled time

```
GIVEN AutomationSchedule S1 exists with EventType = 'guest.checkin_instructions', DueAtUtc = 2026-03-14 09:00 UTC
AND   the current time is 2026-03-14 09:00 UTC
WHEN  AutomationSchedulerHostedService polls
THEN  S1 is published as an OutboxMessage
AND   a CommunicationLog entry is created with Channel = 'WhatsApp', Status = 'PENDING'
AND   the message is sent to Guest.Phone via WhatsApp
AND   CommunicationLog.Status is updated to 'SENT' (then 'DELIVERED' on webhook)
```

#### GX-T03: Guest reports issue → maintenance ticket created

```
GIVEN Guest G1 has a checked-in booking B1 at Listing L1
AND   G1 accesses the guest portal via valid token
WHEN  G1 submits an issue report with Category = 'plumbing', Urgency = 'Urgent', Description = 'Shower not working'
THEN  a MaintenanceTicket is created with:
      - ListingId = L1
      - BookingId = B1
      - TicketType = 'PLUMBING'
      - Severity = 'HIGH'
      - CreatedBy = 'guest:G1'
AND   the host is notified via WhatsApp
AND   the guest sees acknowledgment on the portal
```

#### GX-T04: Guest clicks check-in → check-in time logged

```
GIVEN Guest G1 has booking B1 with CheckinDate = today
AND   G1 opens the guest portal via valid token
AND   the "I have checked in" button is visible
WHEN  G1 taps "I have checked in"
THEN  B1.CheckedInAtUtc = current UTC time
AND   a stay.checked_in OutboxMessage is published
AND   downstream automations fire (inventory deduction, room status update)
AND   the button is replaced with "Checked in at {{time}}" confirmation text
```

#### GX-T05: Check-out triggers cleaning task

```
GIVEN Booking B1 for Listing L1 is checked out (stay.checked_out event fires)
THEN  a HousekeepingTask is created for L1 with CleaningType = TURNOVER (per RA-OPS-001)
AND   a review request AutomationSchedule is created at CheckedOutAtUtc + 2h
AND   a re-engagement AutomationSchedule is created at CheckedOutAtUtc + 30 days
AND   a GuestCoupon is generated for G1
```

#### GX-T06: Review request sent after checkout

```
GIVEN Booking B1 checked out at 2026-03-18 11:00 UTC
AND   AutomationSchedule exists with EventType = 'guest.review_request', DueAtUtc = 2026-03-18 13:00 UTC
WHEN  AutomationSchedulerHostedService polls at 13:00 UTC
THEN  a review request message is sent to Guest via WhatsApp
AND   CommunicationLog entry created
AND   portal review page is active and accessible via token
```

#### GX-T07: Repeat guest tagged correctly

```
GIVEN Guest G1 has 1 completed booking with TenantId = T1
AND   G1 creates a new booking B2 with TenantId = T1
WHEN  B2.BookingStatus is set to 'Confirmed'
THEN  a GuestTag with Tag = 'RETURNING_GUEST' is created for G1 (if not already exists)
AND   the booking confirmation message includes "Welcome back, {{guest_name}}!"
```

#### GX-T08: Coupon applied successfully

```
GIVEN GuestCoupon C1 exists with CouponCode = 'ATLAS10-X7K9M2', DiscountType = 'PERCENT', DiscountValue = 10, Status = 'ACTIVE', MaxUses = 1, TimesUsed = 0
AND   Guest G1 creates a direct booking B3 with total ₹10,000
WHEN  G1 applies coupon code 'ATLAS10-X7K9M2'
THEN  B3.DiscountAmount = ₹1,000 (10% of ₹10,000)
AND   B3.FinalAmount = ₹9,000
AND   C1.TimesUsed = 1, C1.Status = 'USED'
```

#### GX-T09: Message retry works on WhatsApp failure

```
GIVEN a guest.checkin_instructions message is being sent to Guest G1 via WhatsApp
AND   the WhatsApp API returns a delivery failure
WHEN  the retry policy triggers
THEN  retry attempt 2 is made after 15 minutes
AND   retry attempt 3 is made after 30 minutes
AND   if all 3 WhatsApp attempts fail, the message falls back to SMS
AND   CommunicationLog records all attempts with AttemptCount incremented
```

#### GX-T10: No duplicate lifecycle messages sent

```
GIVEN booking.confirmed event fires for Booking B1
AND   the confirmation message and AutomationSchedule entries are created
WHEN  a duplicate booking.confirmed event fires for B1 (retry/redelivery)
THEN  the system checks IdempotencyKey = 'B1:guest.booking_confirmed:1'
AND   finds existing CommunicationLog with Status = 'SENT'
AND   skips sending a duplicate message
AND   no duplicate AutomationSchedule entries are created
```

### 12.2 Edge Case Test Matrix

| ID | Scenario | Expected Behavior |
|----|----------|-------------------|
| EC-01 | Same-day booking (confirmation and check-in same day) | All pre-arrival messages sent immediately with 5-min spacing; skip those past check-in time |
| EC-02 | Booking cancelled after pre-arrival messages scheduled | All pending `AutomationSchedule` entries for this booking cancelled (`Status = 'Cancelled'`) |
| EC-03 | Guest has no WhatsApp (phone is landline) | WhatsApp fails immediately; falls back to SMS, then Email |
| EC-04 | Guest opts out of WhatsApp mid-sequence | Remaining messages sent via SMS/Email only; `GuestConsent` updated |
| EC-05 | Two bookings for same guest overlapping scheduling | Each booking has independent schedule entries; idempotency keys include BookingId |
| EC-06 | Portal token accessed after expiry | 404 page with message "This link has expired. Contact your host for assistance." |
| EC-07 | Guest taps "check-in" twice | Second tap is idempotent — `CheckedInAtUtc` already set, no duplicate event published |
| EC-08 | Guest submits review after portal token expires | Review endpoint has a separate 60-day window (vs. 30-day general portal) |
| EC-09 | Coupon used on booking below minimum amount | Coupon rejected; error message: "Minimum booking amount ₹X required for this coupon" |
| EC-10 | Coupon expired | Coupon rejected; error message: "This coupon has expired" |
| EC-11 | Re-engagement message scheduled but guest already booked | Schedule entry cancelled (check for active booking before sending) |
| EC-12 | Host has not configured check-in instructions | Checkin instructions message uses default template without access code; "Our staff will meet you" fallback |
| EC-13 | Guest submits issue after checkout | Allowed within 24h of checkout (creates Incident, not MaintenanceTicket) |
| EC-14 | Multi-property tenant, guest stays at different properties | Each stay generates independent lifecycle; tags aggregate across stays |

---

## 13. Definition of Done — Guest Experience V1

### 13.1 Functional Checklist

| # | Requirement | Status |
|---|-------------|--------|
| 1 | Booking confirmation message sent via WhatsApp on `booking.confirmed` | ☐ |
| 2 | Pre-arrival message sequence (welcome, rules, instructions) scheduled and sent on time | ☐ |
| 3 | Balance payment reminder sent if `PaymentStatus ≠ 'Paid'` | ☐ |
| 4 | Guest portal accessible via token link — booking details visible | ☐ |
| 5 | Check-in instructions with access code shown on portal 24h before check-in | ☐ |
| 6 | Guest "I have checked in" button works and logs `CheckedInAtUtc` | ☐ |
| 7 | Not-checked-in alert fires if guest hasn't checked in by check-in time + 2h | ☐ |
| 8 | Day-1 satisfaction check message sent 20h after check-in | ☐ |
| 9 | Issue reporting form on portal creates `MaintenanceTicket` or `Incident` | ☐ |
| 10 | Host notified immediately when guest reports issue | ☐ |
| 11 | Local recommendations page on portal populated from `PropertyRecommendation` | ☐ |
| 12 | Add-on request from portal sends notification to host | ☐ |
| 13 | Check-out reminder sent 12h before checkout time | ☐ |
| 14 | Check-out triggers `TURNOVER` housekeeping task (RA-OPS-001 integration) | ☐ |
| 15 | Review request sent 2h after checkout, reminder at 48h, final at 7d | ☐ |
| 16 | Guest review (1–5 stars + comment) submittable via portal | ☐ |
| 17 | Direct booking coupon auto-generated and sent after checkout | ☐ |
| 18 | Repeat guest tagged as `RETURNING_GUEST` on 2nd booking | ☐ |
| 19 | Re-engagement message scheduled at 30 and 90 days post-checkout | ☐ |
| 20 | Guest referral code generated and included in post-stay messages | ☐ |
| 21 | Coupon redemption works on direct booking with discount applied | ☐ |
| 22 | Message logs visible on booking detail page in admin portal | ☐ |
| 23 | Failed message dashboard shows all failed/bounced messages | ☐ |
| 24 | Guest consent tracked; opt-out blocks future messages on opted-out channel | ☐ |

### 13.2 Non-Functional Checklist

| # | Requirement | Status |
|---|-------------|--------|
| 1 | All guest-facing entities implement `ITenantOwnedEntity` with global query filter | ☐ |
| 2 | No cross-tenant guest data leakage (verified by integration tests) | ☐ |
| 3 | Guest portal loads in < 2s on 3G | ☐ |
| 4 | Portal is usable on 360px-wide screen (mobile-first) | ☐ |
| 5 | Token-based access: no login required, no guest account creation | ☐ |
| 6 | Portal token expires 30 days after checkout | ☐ |
| 7 | Portal rate-limited to 60 req/token/hour | ☐ |
| 8 | Guest PII access restricted by role | ☐ |
| 9 | No ID proof images stored in V1 | ☐ |
| 10 | Message idempotency prevents duplicate sends | ☐ |
| 11 | WhatsApp → SMS → Email fallback chain works | ☐ |
| 12 | Quiet hours enforced (no messages 21:00–08:00 local) | ☐ |
| 13 | Guest data export endpoint works for DPDP readiness | ☐ |
| 14 | All acceptance tests (§12) pass | ☐ |
| 15 | Feature flags control each module component | ☐ |
| 16 | Re-engagement messages cancelled if guest re-books before schedule fires | ☐ |

---

## Appendix A — Event Types (Guest Domain)

New event types to add to `EventTypes.cs`:

| Constant | Value | Trigger |
|----------|-------|---------|
| `GuestBookingConfirmed` | `guest.booking_confirmed` | Booking confirmation message to guest |
| `GuestWelcome` | `guest.welcome` | Welcome message (T-7d) |
| `GuestPaymentReminder` | `guest.payment_reminder` | Balance payment reminder |
| `GuestHouseRules` | `guest.house_rules` | House rules delivery |
| `GuestCheckinInstructions` | `guest.checkin_instructions` | Check-in instructions (T-1d) |
| `GuestLocation` | `guest.location` | Location & directions |
| `GuestEarlyCheckinOffer` | `guest.early_checkin_offer` | Early check-in upsell |
| `GuestIdVerification` | `guest.id_verification` | ID verification request (V2) |
| `GuestSatisfactionCheck` | `guest.satisfaction_check` | Day-1 satisfaction check |
| `GuestAddonRequest` | `guest.addon_request` | Guest requested an add-on |
| `GuestCheckoutReminder` | `guest.checkout_reminder` | Check-out reminder (T-12h) |
| `GuestReviewRequest` | `guest.review_request` | Post-stay review request |
| `GuestReviewReminder` | `guest.review_reminder` | Review reminder (T+48h, T+7d) |
| `GuestCouponIssued` | `guest.coupon_issued` | Direct booking coupon sent |
| `GuestReengagement` | `guest.reengagement` | Re-engagement campaign message |
| `GuestNotCheckedIn` | `guest.not_checked_in` | Alert: guest hasn't checked in |
| `GuestIssueReported` | `guest.issue_reported` | Guest reported an issue via portal |
| `GuestIssueResolved` | `guest.issue_resolved` | Guest notified that issue resolved |
| `StayAutoCheckoutDue` | `stay.auto_checkout_due` | Auto-checkout timer |

Helper method:

```csharp
public static bool IsGuestExperienceEvent(string eventType) =>
    eventType.StartsWith("guest.", StringComparison.Ordinal);
```

---

## Appendix B — Message Template Catalogue

Default platform templates to seed per tenant. All use `ScopeType = 'PLATFORM'` and can be overridden by tenant-level templates.

| Template Key | Event Type | Channel | Category |
|-------------|------------|---------|----------|
| `BOOKING_CONFIRMED_WA` | `guest.booking_confirmed` | WhatsApp | Transactional |
| `BOOKING_CONFIRMED_SMS` | `guest.booking_confirmed` | SMS | Transactional |
| `BOOKING_CONFIRMED_EMAIL` | `guest.booking_confirmed` | Email | Transactional |
| `WELCOME_WA` | `guest.welcome` | WhatsApp | Transactional |
| `PAYMENT_REMINDER_WA` | `guest.payment_reminder` | WhatsApp | Transactional |
| `PAYMENT_REMINDER_SMS` | `guest.payment_reminder` | SMS | Transactional |
| `HOUSE_RULES_WA` | `guest.house_rules` | WhatsApp | Informational |
| `CHECKIN_INSTRUCTIONS_WA` | `guest.checkin_instructions` | WhatsApp | Transactional |
| `CHECKIN_INSTRUCTIONS_SMS` | `guest.checkin_instructions` | SMS | Transactional |
| `LOCATION_WA` | `guest.location` | WhatsApp | Informational |
| `EARLY_CHECKIN_OFFER_WA` | `guest.early_checkin_offer` | WhatsApp | Marketing |
| `SATISFACTION_CHECK_WA` | `guest.satisfaction_check` | WhatsApp | Engagement |
| `CHECKOUT_REMINDER_WA` | `guest.checkout_reminder` | WhatsApp | Transactional |
| `REVIEW_REQUEST_WA` | `guest.review_request` | WhatsApp | Engagement |
| `REVIEW_REMINDER_WA` | `guest.review_reminder` | WhatsApp | Engagement |
| `REVIEW_REMINDER_EMAIL` | `guest.review_reminder` | Email | Engagement |
| `COUPON_ISSUED_WA` | `guest.coupon_issued` | WhatsApp | Marketing |
| `REENGAGEMENT_30D_WA` | `guest.reengagement` | WhatsApp | Marketing |
| `REENGAGEMENT_90D_WA` | `guest.reengagement` | WhatsApp | Marketing |
| `NOT_CHECKED_IN_HOST_WA` | `guest.not_checked_in` | WhatsApp | Alert (to host) |
| `ISSUE_REPORTED_HOST_WA` | `guest.issue_reported` | WhatsApp | Alert (to host) |
| `ISSUE_RESOLVED_GUEST_WA` | `guest.issue_resolved` | WhatsApp | Transactional |

> **GX-TPL-001**: Marketing-category templates MUST check `GuestConsent` before sending. Transactional and Alert templates may be sent without explicit opt-in (consent assumed on booking). This distinction is enforced at the automation rule level.

---

## Appendix C — Cross-Reference to Existing Models

| Existing Entity | Relationship to Guest Experience Module |
|----------------|----------------------------------------|
| `Guest` | Core entity. Extended with `ReferralCode`. All guest experience entities link here |
| `Booking` | Lifecycle trigger. Extended with `ReferralCode` (applied coupon source). `CheckedInAtUtc`/`CheckedOutAtUtc` drive stage transitions |
| `Listing` | Provides check-in time, WiFi, listing name for templates. Links to `ListingAccessInfo` |
| `Property` | Provides address, contact phone, name for templates. Links to `PropertyRecommendation` |
| `MessageTemplate` | All guest messages use existing template infrastructure. New templates added for guest events |
| `CommunicationLog` | All message delivery tracked here. Existing fields are sufficient |
| `AutomationSchedule` | Pre-arrival and post-stay messages scheduled here. Existing model is sufficient |
| `OutboxMessage` | All guest events published here. Existing model is sufficient |
| `AuditLog` | Guest portal actions (check-in, review, issue report) logged here |
| `MaintenanceTicket` (RA-OPS-001) | Created from guest issue reports with `CreatedBy = 'guest:{guestId}'` |
| `Incident` (RA-OPS-001) | Created from guest damage/safety reports |
| `HousekeepingTask` (RA-OPS-001) | Auto-created on checkout via `stay.checked_out` event |
| `TenantProfile` | `DisplayName` used as `{{host_name}}` in templates |
| `Payment` | Payment status drives balance-due logic in pre-arrival messages |
| `User` | Staff receive alerts from guest actions (issue reports, check-in alerts) |

---

*End of RA-GUEST-001*
