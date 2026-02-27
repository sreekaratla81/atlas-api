# RA-004: Risk, Fraud, Trust & Compliance Requirements

**Addendum to:** [RA-001](RA-001-marketplace-commission-boost-ota-payments.md) | [RA-002](RA-002-governance-scale-monetization-control.md) | [RA-003](RA-003-growth-demand-network-effects.md)

**Purpose:** Define the fraud threat model, trust scoring, payment risk flows, compliance guardrails, data privacy, incident response playbooks, suspension/enforcement logic, monitoring/alerting, and minimum viable risk controls for the Atlas marketplace.

**Audience:** Developer, QA, Support, Platform Ops

**Owner:** Atlas Tech Solutions

**Last updated:** 2026-02-27

**Status:** Draft

---

## Table of contents

1. [Fraud Threat Modeling V1](#1-fraud-threat-modeling-v1)
2. [Trust Score Architecture](#2-trust-score-architecture)
3. [Payment Risk & Dispute Flow](#3-payment-risk--dispute-flow)
4. [Compliance Guardrails](#4-compliance-guardrails)
5. [Data Privacy & Security Requirements](#5-data-privacy--security-requirements)
6. [Incident Response Playbooks](#6-incident-response-playbooks)
7. [Suspension & Enforcement Logic](#7-suspension--enforcement-logic)
8. [Monitoring & Alerting Requirements](#8-monitoring--alerting-requirements)
9. [Minimum Viable Risk Controls — Lean V1](#9-minimum-viable-risk-controls--lean-v1)
10. [Definition of Done — Trust Layer V1](#10-definition-of-done--trust-layer-v1)

---

## 1. Fraud threat modeling V1

### 1.A Booking fraud

#### 1.A.1 Fake bookings to manipulate ranking

| Attribute | Detail |
|-----------|--------|
| **Risk** | Tenant creates bookings with fabricated guest data to inflate RecencyScore and ConversionRate, gaining an unfair ranking advantage. |
| **Detection signals** | (a) Multiple bookings from the same IP/device in a short window. (b) Guest phone numbers follow a sequential pattern. (c) Bookings are immediately cancelled after confirmation. (d) Payment amounts are suspiciously low (minimum allowed). (e) Guest contact matches other recent bookings at different tenant properties. |
| **Required logging** | `AuditLog`: `booking.created` with `{bookingId, guestPhone, guestEmail, ipAddress, userAgent, paymentAmount}`. Structured log: `fraud.signal.fake_booking_suspect` with all detection signals. |
| **Mitigation** | FRD-01: Bookings where `Guest.Phone == Tenant.OwnerPhone` or `Guest.Email == Tenant.OwnerEmail` are flagged as self-bookings (RA-002 AG-09) and excluded from ranking signals. FRD-02: V1: no auto-block. Admin report of flagged bookings. V2: rate-limit bookings per guest phone to 3/day across all properties. |
| **Escalation** | Structured log alert → Atlas Admin reviews flagged booking report → manual tenant warning or suspension. |

#### 1.A.2 Self-booking by host

| Attribute | Detail |
|-----------|--------|
| **Risk** | Host books their own property (using a different phone or a friend's details) to inflate metrics and claim commission deductions. |
| **Detection signals** | (a) Guest contact matches tenant contact (primary check). (b) Guest booking IP matches tenant admin portal IP. (c) Multiple bookings from same device fingerprint across guest portal and admin portal. (d) Booking cancelled shortly after creation (within 24 hours). (e) Payment refunded within 48 hours. |
| **Required logging** | `AuditLog`: `fraud.signal.self_booking_suspect` with `{bookingId, matchType, guestPhone, tenantPhone}`. |
| **Mitigation** | FRD-03: Contact-match bookings are excluded from all ranking signals (RecencyScore, ConversionRate). FRD-04: V1: no payment blocking for self-bookings (they may be legitimate test bookings). FRD-05: Admin dashboard shows "Self-booking suspects" report filterable by date. |
| **Escalation** | Weekly admin review of self-booking report → if pattern confirmed (> 3 in 30 days): tenant warning. Repeat offence: move to LIMITED state (section 7). |

#### 1.A.3 Card testing / payment abuse

| Attribute | Detail |
|-----------|--------|
| **Risk** | Bad actor uses the marketplace checkout to test stolen card numbers against Razorpay. Rapid succession of small-amount payment attempts. |
| **Detection signals** | (a) > 5 failed payment attempts from same IP in 10 minutes. (b) Multiple different card numbers used from same session/IP. (c) Payment amounts are the absolute minimum. (d) Guest data is clearly fake (random names, invalid phones). |
| **Required logging** | Structured log: `fraud.signal.card_testing` with `{ip, sessionId, attemptCount, timeWindowMinutes}`. |
| **Mitigation** | FRD-06: Rate-limit Razorpay order creation to 5 per IP per 10 minutes. Return HTTP 429 with `Retry-After: 600`. FRD-07: Rate-limit per guest phone: 3 orders per phone per hour. FRD-08: Razorpay's own fraud detection layer is the primary defence (card-network level); Atlas adds the IP/phone rate limits as a secondary layer. |
| **Escalation** | Auto-rate-limit at threshold → structured log alert → Atlas Admin can IP-ban via Cloudflare WAF rule if persistent. |

#### 1.A.4 Repeated cancellations to game system

| Attribute | Detail |
|-----------|--------|
| **Risk** | Tenant creates bookings then cancels repeatedly to keep RecencyScore artificially high (if cancelled bookings accidentally count). |
| **Detection signals** | (a) CancellationRate > 30% (trailing 90 days). (b) Same guest-phone books and cancels at the same property > 2 times. |
| **Required logging** | Structured log: `fraud.signal.cancellation_gaming` with `{tenantId, propertyId, cancellationRate, cancelCount30d}`. |
| **Mitigation** | FRD-09: Only `Confirmed`, `CheckedIn`, and `CheckedOut` bookings count toward RecencyScore and ConversionRate. Cancelled bookings are excluded. FRD-10: CancellationRate > 30% triggers boost suppression (RA-002 section 4.4). FRD-11: CancellationRate > 50% triggers property HIDDEN state (section 7). |
| **Escalation** | Automated boost suppression at 30% → admin alert at 50% → property hidden at 50% → tenant warning. |

---

### 1.B Commission manipulation

#### 1.B.1 Temporary high commission for ranking spike

| Attribute | Detail |
|-----------|--------|
| **Risk** | Tenant sets 20% commission for a few hours during peak booking window, then reverts to 1% after gaining top ranking. |
| **Detection signals** | (a) Commission change reverted within 48 hours. (b) Commission value jumped > 5pp in a single change. (c) Commission changed > 3 times in 7 days. |
| **Required logging** | `AuditLog`: `tenant.commission.changed` / `property.commission.changed` with old/new rates and timestamps (already defined in RA-002 section 2.7). |
| **Mitigation** | FRD-12: 24-hour cooldown between commission changes (RA-002 AG-01/AG-02). FRD-13: 7-day damping before full ranking effect applies (RA-002 AG-07). FRD-14: Commission value at time T is the "damped" value, not the spot rate, for ranking purposes. |
| **Escalation** | Frequency alert (> 3 changes/7 days) → admin review → manual commission lock (admin sets rate and applies 30-day cooldown override). |

#### 1.B.2 Boost spamming

| Attribute | Detail |
|-----------|--------|
| **Risk** | Tenant sets property-level overrides on all properties to maximum (20%) solely for ranking; listings may be low quality. |
| **Detection signals** | (a) All of tenant's properties at ceiling commission. (b) BaseQuality < 0.50 on those properties. (c) No actual bookings despite high ranking position. |
| **Required logging** | Structured log: `fraud.signal.boost_spam` with `{tenantId, propertiesAtMax, avgBaseQuality}`. |
| **Mitigation** | FRD-15: BaseQuality < 0.50 suppresses commission boost entirely (RA-002 AG-16). FRD-16: Average rating < 2.5 suppresses commission boost (RA-002 section 4.4). FRD-17: The log-scale boost formula `log10(rate)/log10(20)` naturally provides diminishing returns above ~8%. |
| **Escalation** | Automatic suppression via quality floor → admin report of tenants with all-max-commission + low quality. |

#### 1.B.3 Commission flip-flopping

| Attribute | Detail |
|-----------|--------|
| **Risk** | Tenant alternates between two commission rates every 24 hours (just after cooldown expires) to test ranking impact and exploit any lag. |
| **Detection signals** | (a) Commission oscillates between two values > 3 times in 14 days. (b) Pattern: raise → lower → raise → lower. |
| **Required logging** | Structured log: `fraud.signal.commission_oscillation` with `{tenantId, changeHistory7d}`. |
| **Mitigation** | FRD-18: 7-day damping means oscillating never reaches full boost. FRD-19: V2: "stability bonus" — commission held stable for > 30 days gets a 5% ranking score bonus. V1: damping only. |
| **Escalation** | Admin alert at oscillation threshold → manual review → warning → potential commission lock. |

---

### 1.C Review manipulation

#### 1.C.1 Fake guest accounts

| Attribute | Detail |
|-----------|--------|
| **Risk** | Host creates fake guest identities to book and review their own property. |
| **Detection signals** | (a) Guest phone is a VoIP or temporary number. (b) Guest contact info matches across multiple "distinct" reviews at the same property. (c) Review submitted immediately after checkout (< 1 hour; unusual for genuine guests). (d) All reviews are 5 stars with minimal body text. |
| **Required logging** | `AuditLog`: `fraud.signal.fake_review_suspect` with `{reviewId, bookingId, guestId, matchSignals[]}`. |
| **Mitigation** | FRD-20: Reviews require a completed booking (RA-002 REP-01). FRD-21: Self-review detection: guest contact matches tenant contact → review flagged and excluded from ReviewScore (RA-002 AG-13). FRD-22: V1: no VoIP detection. V2: phone validation service integration. |
| **Escalation** | Auto-flag on contact match → admin review → review hidden if confirmed fake. |

#### 1.C.2 Coordinated reviews

| Attribute | Detail |
|-----------|--------|
| **Risk** | Host solicits friends/family to book, stay (or not), and leave positive reviews. |
| **Detection signals** | (a) Cluster of 5-star reviews within a short window (> 3 reviews in 7 days for a low-traffic property). (b) Reviewing guests have never booked any other property. (c) Reviews are generic (low text diversity). |
| **Required logging** | Structured log: `fraud.signal.review_cluster` with `{propertyId, reviewCountLast7d, avgRating}`. |
| **Mitigation** | FRD-23: ReviewScore dampening for properties with < 3 reviews (RA-002 section 4.3). This naturally limits the impact of the first few reviews. FRD-24: V1: no text analysis. V2: similarity scoring across reviews for the same property. |
| **Escalation** | Review cluster alert → admin review → bulk hide if confirmed. |

#### 1.C.3 Review bombing

| Attribute | Detail |
|-----------|--------|
| **Risk** | Competitor or disgruntled guest submits multiple negative reviews (using different bookings or exploiting any review system gaps). |
| **Detection signals** | (a) Multiple 1-star reviews in short succession for a single property. (b) Same guest phone/email across multiple reviews. (c) Review text is abusive or off-topic. |
| **Required logging** | Structured log: `fraud.signal.review_bomb` with `{propertyId, oneStarCountLast7d}`. |
| **Mitigation** | FRD-25: One review per guest per booking (RA-002 REP-02). FRD-26: Review must be within 30 days of checkout (RA-002 REP-03). FRD-27: Host can report a review to Atlas Admin. Admin can hide the review. FRD-28: V2: profanity filter + abuse keyword detection. |
| **Escalation** | Host reports → admin review → hide offending reviews → investigate if coordinated. |

---

### 1.D Refund abuse

#### 1.D.1 Host disputes after payout

| Attribute | Detail |
|-----------|--------|
| **Risk** | Host claims they never received the guest or the booking was fraudulent, seeking to keep the payout AND get a reversal credit. |
| **Detection signals** | (a) Dispute raised > 7 days after checkout. (b) Host has multiple disputes across different bookings. (c) Guest did check in (check-in record exists). |
| **Required logging** | `AuditLog`: `dispute.host.raised` with `{bookingId, reason, daysAfterCheckout}`. |
| **Mitigation** | FRD-29: V1 disputes are manual only (RA-002 BR-D01). Atlas Admin reviews booking evidence (check-in time, communication logs, payment records). FRD-30: Host dispute count is tracked; > 3 disputes in 90 days triggers admin review. |
| **Escalation** | Admin investigates → resolve in favour of guest or host → log resolution. Repeat offenders → tenant WARNING state. |

#### 1.D.2 Guest refund after service used

| Attribute | Detail |
|-----------|--------|
| **Risk** | Guest completes the stay, then requests a refund claiming the property was not as advertised, attempting to get a free stay. |
| **Detection signals** | (a) Refund requested after `CheckedOutAtUtc`. (b) No complaints raised during the stay (no communication log entries). (c) Guest has pattern of post-checkout refund requests. |
| **Required logging** | `AuditLog`: `refund.request` with `{bookingId, requestedBy, timing (pre/during/post-stay), amount}`. |
| **Mitigation** | FRD-31: Refund policy is config-driven per tenant. Atlas marketplace default: full refund if cancelled > 48 hours before check-in; 50% if 24-48 hours; no refund if < 24 hours or after check-in. FRD-32: Post-checkout refunds require Atlas Admin approval (no self-service). FRD-33: Guest refund history tracked across tenants (by phone) for pattern detection. |
| **Escalation** | Guest contacts support → admin reviews timing + evidence → approve/reject → log decision. |

#### 1.D.3 Chargeback scenarios

| Attribute | Detail |
|-----------|--------|
| **Risk** | Guest initiates a chargeback with their bank after a legitimate stay, or using a stolen card. |
| **Detection signals** | (a) Razorpay webhook `payment.dispute.created`. (b) Dispute reason codes from Razorpay. |
| **Required logging** | `AuditLog`: `payment.chargeback.received` with `{bookingId, paymentId, razorpayDisputeId, amount, reason}`. |
| **Mitigation** | FRD-34: On `payment.dispute.created` webhook: freeze pending settlement (if MARKETPLACE_SPLIT and not yet settled). Mark booking as `Disputed`. Alert Atlas Admin. FRD-35: For HOST_DIRECT: notify tenant. Atlas is not party to the dispute. FRD-36: Atlas Admin gathers evidence (booking confirmation, guest communication, check-in records) and responds via Razorpay dashboard. FRD-37: V1: fully manual. No automated dispute response. |
| **Escalation** | Webhook received → auto-freeze settlement → admin alert → manual investigation → accept or contest via Razorpay. |

---

## 2. Trust score architecture

### 2.1 Composite TrustScore definition

`TrustScore` is a per-property score in the range [0.0, 1.0] computed from operational quality signals. It serves as a gatekeeper for marketplace visibility and a modifier for ranking.

| Component | Weight | Formula | Range |
|-----------|:------:|---------|:-----:|
| ReviewRating | 0.25 | `AVG(Review.Rating) / 5.0`, dampened by `MIN(1.0, ReviewCount / 5)` | 0–1 |
| BookingCompletionRate | 0.20 | `CompletedBookings / TotalConfirmedBookings` (90-day trailing) | 0–1 |
| CancellationRate (inverted) | 0.20 | `1.0 - (HostCancellations / TotalBookings)` (90-day trailing) | 0–1 |
| ResponseTime | 0.15 | Mapped from median response hours: < 1h → 1.0, 1-4h → 0.80, 4-24h → 0.50, > 24h → 0.20 | 0–1 |
| ComplaintRatio (inverted) | 0.10 | `1.0 - MIN(1.0, Complaints / TotalBookings)` (90-day trailing). V1: Complaints = refund requests + disputes. | 0–1 |
| ChargebackRatio (inverted) | 0.10 | `1.0 - MIN(1.0, Chargebacks * 10 / TotalBookings)` (90-day trailing). Multiplied by 10 to amplify even small chargeback counts. | 0–1 |

```
TrustScore = (0.25 * ReviewRating)
           + (0.20 * BookingCompletionRate)
           + (0.20 * CancellationRateInverted)
           + (0.15 * ResponseTime)
           + (0.10 * ComplaintRatioInverted)
           + (0.10 * ChargebackRatioInverted)
```

### 2.2 TrustScore interaction with ranking

TrustScore acts as a **multiplier** on the final ranking score, not an additive component:

```
FinalRankingScore = RawRankingScore * TrustMultiplier
```

| TrustScore range | TrustMultiplier | Effect |
|:----------------:|:---------------:|--------|
| >= 0.80 | 1.00 | No penalty. Full ranking. |
| 0.60 – 0.79 | 0.90 | 10% ranking reduction. |
| 0.40 – 0.59 | 0.70 | 30% ranking reduction. |
| 0.20 – 0.39 | 0.40 | 60% ranking reduction. Effectively buried. |
| < 0.20 | 0.00 | Hidden from marketplace (minimum threshold). |

### 2.3 TrustScore interaction with commission boost

| Condition | Effect |
|-----------|--------|
| TrustScore >= 0.60 | Commission boost applies normally. |
| TrustScore 0.40 – 0.59 | Commission boost reduced by 50%: `EffectiveBoost = CommissionBoost * 0.50`. |
| TrustScore < 0.40 | Commission boost forced to 0.0 regardless of commission rate. Paying more commission does not help a distrusted property. |

### 2.4 Minimum thresholds for visibility

| Threshold | Value | Consequence |
|-----------|:-----:|------------|
| Marketplace listing visibility | TrustScore >= 0.20 | Below 0.20: property hidden from marketplace search. Tenant notified. |
| Boost eligibility | TrustScore >= 0.40 | Below 0.40: commission boost disabled. |
| "Trusted Host" badge | TrustScore >= 0.85 AND ReviewCount >= 5 | Shown on property card and detail page. |

### 2.5 Edge-case handling

#### New property (cold start)

| Signal | Default | Rationale |
|--------|:-------:|-----------|
| ReviewRating | 0.60 (neutral-positive) | Benefit of the doubt; not penalised for having no reviews. |
| BookingCompletionRate | 1.00 | No bookings to complete = perfect rate. |
| CancellationRate | 0.00 (→ inverted = 1.00) | No cancellations = perfect rate. |
| ResponseTime | 0.80 (good) | Benefit of the doubt. |
| ComplaintRatio | 0.00 (→ inverted = 1.00) | No complaints = perfect. |
| ChargebackRatio | 0.00 (→ inverted = 1.00) | No chargebacks = perfect. |
| **Cold-start TrustScore** | **~0.88** | New properties start with a healthy trust score. |

Cold-start TrustScore decays toward actual values as data accumulates. Transition rule: each component uses its default value until the property has >= 3 data points for that signal, then switches to the computed value.

#### No reviews yet

- ReviewRating component uses default (0.60).
- ReviewRating dampener is `MIN(1.0, ReviewCount / 5)` = `0 / 5 = 0.0`.
- Effective ReviewRating contribution = `0.60 * 0.0 = 0.0`.
- To avoid penalising, when ReviewCount = 0: use default 0.60 WITHOUT dampener. Dampener applies only when ReviewCount >= 1.

#### Low traffic property

- If total bookings in 90 days < 3: BookingCompletionRate, CancellationRate, and ComplaintRatio use their default (perfect) values. Not enough data for meaningful signals.
- ResponseTime: if < 3 communications, use default (0.80).

### 2.6 Computation and caching

- TST-01: TrustScore MUST be recomputed in the same batch job as ranking scores (every 15 minutes).
- TST-02: TrustScore MUST be stored per-property in the ranking cache (key: `trust:{propertyId}`).
- TST-03: TrustScore components MUST be individually stored for tenant dashboard display and admin debugging.
- TST-04: TrustScore changes > 0.10 in a single computation cycle MUST generate a structured log alert (`trust.score.significant_change`).

---

## 3. Payment risk & dispute flow

### 3.1 Booking payment state machine

Every booking with a payment follows this state machine. States apply to the `Payment.Status` field (extended from current `pending` / `completed` / `failed`).

```
        ┌──────────────────────────────────────────────────────────────────────┐
        │                                                                      │
        ▼                                                                      │
    CREATED ──► AUTHORIZED ──► CAPTURED ──► SETTLED                            │
        │           │              │            │                              │
        │         (fail)         (fail)       (fail)                           │
        │           │              │            │                              │
        │           ▼              │            ▼                              │
        │        FAILED            │     SETTLEMENT_FAILED ──► MANUAL_REVIEW  │
        │                          │                                │          │
        │                          │                                ▼          │
        │                          │                            RESOLVED ──────┘
        │                          │
        │                          ▼
        │                   REFUNDED_PARTIAL
        │                          │
        │                          ▼
        │                   REFUNDED_FULL
        │
        │                   CHARGEBACK ──► DISPUTED ──► RESOLVED
        │
        └──► CANCELLED (before capture)
```

### 3.2 State definitions and transitions

| State | Description | Allowed transitions | Trigger |
|-------|------------|-------------------|---------|
| `CREATED` | Razorpay order created. Guest on checkout page. | `AUTHORIZED`, `CANCELLED`, `FAILED` | `POST /api/razorpay/create-order` |
| `AUTHORIZED` | Card authorised but not captured (Razorpay auto-capture handles this; may not be a distinct state in practice). | `CAPTURED`, `FAILED` | Razorpay callback |
| `CAPTURED` | Payment captured. Booking confirmed. | `SETTLED`, `REFUNDED_PARTIAL`, `REFUNDED_FULL`, `CHARGEBACK` | Razorpay `payment.captured` webhook or verify endpoint |
| `SETTLED` | Host payout transferred (MARKETPLACE_SPLIT only). For HOST_DIRECT, CAPTURED is the terminal success state. | `REFUNDED_PARTIAL`, `REFUNDED_FULL`, `CHARGEBACK` | Settlement worker confirms transfer |
| `SETTLEMENT_FAILED` | Settlement transfer failed after retries. | `SETTLED` (retry), `MANUAL_REVIEW` | Settlement worker max retries exceeded |
| `MANUAL_REVIEW` | Admin intervention required for settlement. | `SETTLED` (admin retry), `RESOLVED` | Auto-escalation from SETTLEMENT_FAILED |
| `REFUNDED_PARTIAL` | Part of the amount refunded to guest. | `REFUNDED_FULL`, `CHARGEBACK` | Admin-initiated partial refund |
| `REFUNDED_FULL` | Full amount refunded to guest. Terminal state. | `CHARGEBACK` (rare, bank-initiated) | Admin-initiated full refund |
| `CHARGEBACK` | Bank/card network dispute initiated. | `DISPUTED` | Razorpay `payment.dispute.created` webhook |
| `DISPUTED` | Under investigation. Settlement frozen. | `RESOLVED` | Admin begins investigation |
| `RESOLVED` | Dispute or manual review resolved. Terminal state. | (none) | Admin resolution |
| `CANCELLED` | Guest abandoned checkout. No payment captured. Terminal state. | (none) | Timeout (30 min) or guest navigates away |
| `FAILED` | Payment attempt failed (card declined, etc.). | `CREATED` (guest retries with new order) | Razorpay failure callback |

### 3.3 Ledger adjustments per state

Each state transition that involves money MUST generate an immutable ledger entry. A ledger entry is a `Payment` row (existing model) with the appropriate `Type` and signed `Amount`.

| Transition | Ledger entry (Payment row) | Fields |
|-----------|---------------------------|--------|
| → CAPTURED | Credit: `Type = 'capture'`, `Amount = +FinalAmount` | `RazorpayPaymentId`, `RazorpayOrderId`, `Status = 'completed'` |
| → SETTLED | Settlement: `Type = 'settlement'`, `Amount = -HostPayoutAmount` | `Note = transfer:{transferId}` |
| → REFUNDED_PARTIAL | Refund: `Type = 'refund'`, `Amount = -RefundAmount` | `Note = refund:{razorpayRefundId}, reason:{reason}` |
| → REFUNDED_FULL | Refund: `Type = 'refund'`, `Amount = -FinalAmount` | Same |
| → CHARGEBACK | Chargeback: `Type = 'chargeback'`, `Amount = -ChargebackAmount` | `Note = dispute:{disputeId}` |
| DISPUTED → RESOLVED (in favour of merchant) | Reversal: `Type = 'chargeback_reversal'`, `Amount = +ChargebackAmount` | `Note = dispute:{disputeId}:won` |
| DISPUTED → RESOLVED (in favour of guest) | No reversal. Chargeback stands. | `Note = dispute:{disputeId}:lost` |

- PAY-01: Every ledger entry MUST be append-only. No updates to existing Payment rows.
- PAY-02: `SUM(Payment.Amount) WHERE BookingId = X` MUST equal the net financial position for that booking at all times.
- PAY-03: Ledger entries for settlement, refund, and chargeback MUST reference the original capture Payment.Id in a `RelatedPaymentId` field (new, nullable FK).

### 3.4 Split settlement risk scenarios

| Scenario | Risk | Handling |
|----------|------|---------|
| Linked account KYC not activated | Transfer rejected by Razorpay | Settlement worker: catch 4xx, mark `SETTLEMENT_FAILED`. Notify tenant to complete KYC. No retry (permanent failure). |
| Linked account suspended | Transfer rejected | Same as above. Admin alerted. |
| Insufficient funds in Atlas account | Transfer fails (Atlas hasn't collected enough) | Should not occur (Atlas collects first, transfers after). If it does: critical alert, manual investigation. |
| Host requests more than HostPayoutAmount | Not possible via system (amount computed, not user-provided) | Validation: `transferAmount == HostPayoutAmount`. Reject discrepancy. |
| Double settlement | Two transfers for same booking | Idempotency key: `settlement:{BookingId}:{PaymentId}`. Razorpay returns same TransferId. |

### 3.5 Chargeback handling flow

```
Razorpay webhook: payment.dispute.created
  → Verify webhook signature (HMAC-SHA256)
  → Look up Payment by RazorpayPaymentId
  → If not found: log warning, return 200 (stale/test event)
  → If found:
    → Create chargeback Payment ledger entry (Type = 'chargeback')
    → If MARKETPLACE_SPLIT and settlement not yet initiated: cancel settlement outbox row
    → If MARKETPLACE_SPLIT and already settled: flag for admin (host funds may need clawback)
    → Set Booking.BookingStatus = 'Disputed'
    → Write AuditLog: payment.chargeback.received
    → Send alert to Atlas Admin
    → Return 200
```

### 3.6 Refund approval logic

| Condition | Auto-approve? | Approver |
|-----------|:------------:|---------|
| Guest cancels > 48h before check-in | Yes | System |
| Guest cancels 24-48h before check-in | Yes (50% refund) | System |
| Guest cancels < 24h before check-in | No | Atlas Admin |
| Post-check-in refund request | No | Atlas Admin |
| Post-checkout refund request | No | Atlas Admin (requires strong justification) |
| Tenant-initiated refund (any time) | Yes (full amount) | System (tenant consents to refund their guest) |

- PAY-04: Auto-approved refunds MUST be processed within 5 minutes (outbox-driven).
- PAY-05: Admin-approved refunds MUST be logged in AuditLog with approver, reason, and amount.
- PAY-06: Refund amount MUST NOT exceed the original captured amount minus any prior partial refunds.

### 3.7 Commission reversal rules

(Consolidated from RA-002 section 3.6.)

| Refund type | Commission action | Calculation |
|-------------|------------------|-------------|
| Full refund, MARKETPLACE_SPLIT | Full commission reversal | `CommissionRefunded = CommissionAmount` |
| Partial refund, MARKETPLACE_SPLIT | Pro-rata commission reversal | `CommissionRefunded = RefundAmount * CommissionPercentSnapshot / 100` |
| Full refund, HOST_DIRECT | No commission reversal | Commission was reporting-only |
| Chargeback won by guest | Full commission reversal | Same as full refund |
| Chargeback won by merchant | No reversal | Chargeback entry reversed |

### 3.8 Refund timing constraints

| Constraint | Value | Enforcement |
|-----------|:-----:|-------------|
| Maximum refund window | 90 days from capture | System rejects refund requests > 90 days (Razorpay limit). |
| Minimum processing time | Instant (Razorpay processes; bank takes 5-7 days) | Inform guest of bank timeline. |
| Settlement freeze on dispute | Until RESOLVED | Settlement worker skips DISPUTED bookings. |

---

## 4. Compliance guardrails

*This section defines system enforcement requirements. It does not constitute legal advice.*

### 4.1 No pooled funds holding

| Requirement | Implementation |
|-------------|---------------|
| CMP-01: Atlas MUST NOT hold guest funds in a pooled account beyond the settlement window. | MARKETPLACE_SPLIT uses Razorpay Route. Razorpay holds the funds and disburses. Atlas triggers the transfer. No Atlas-managed escrow. |
| CMP-02: HOST_DIRECT payments go directly to the host's Razorpay account. Atlas never touches the funds. | Current architecture. |
| CMP-03: Settlement transfers MUST be initiated within 72 hours of booking confirmation (excluding weekends/bank holidays). | Settlement worker processes the outbox within minutes. The 72h is a compliance upper bound. |

### 4.2 Split settlement requirements

| Requirement | Detail |
|-------------|--------|
| CMP-04 | Atlas MUST use Razorpay Route (or equivalent) for split settlement. Atlas MUST NOT receive the full amount and manually transfer to hosts. |
| CMP-05 | The split (Atlas commission vs. host payout) MUST be determined at booking creation time (snapshot) and MUST NOT be modified retroactively. |
| CMP-06 | Settlement amounts MUST match the snapshot: `TransferAmount == HostPayoutAmount`. No manual amount overrides in the settlement call. |

### 4.3 Payment token data storage

| Data | Storage rules |
|------|---------------|
| Razorpay OAuth access token | Encrypted at rest (`IDataProtector`, purpose `"RazorpayOAuthTokens"`). Column type: `varbinary`. MUST NOT appear in logs, API responses, or error messages. |
| Razorpay OAuth refresh token | Same encryption. Same rules. |
| Razorpay API key (host's, for HOST_DIRECT) | Stored in tenant config. V1: plaintext in DB. V2: encrypted. MUST NOT appear in public API responses. |
| Razorpay webhook secret | Stored in `appsettings.json` (or Azure Key Vault in production). MUST NOT be committed to source control. |
| Channex API keys | Per-property, stored in `ChannelConfig.ApiKey`. V1: plaintext. MUST NOT appear in public API responses. |
| Guest card details | NEVER stored. Razorpay handles PCI compliance. Atlas receives only `RazorpayPaymentId`. |

### 4.4 KYC state tracking for marketplace tenants

Razorpay requires linked account KYC for Route settlement.

| KYC state | Stored as | What system does |
|-----------|-----------|-----------------|
| `created` | `Tenant.RazorpayKycStatus = 'created'` | Tenant can't use MARKETPLACE_SPLIT. Admin portal shows "KYC pending". |
| `needs_clarification` | `RazorpayKycStatus = 'needs_clarification'` | Same. Nudge tenant to complete. |
| `under_review` | `RazorpayKycStatus = 'under_review'` | Same. Show "KYC under review (2-3 days)". |
| `activated` | `RazorpayKycStatus = 'activated'` | MARKETPLACE_SPLIT enabled. Green badge. |
| `suspended` | `RazorpayKycStatus = 'suspended'` | MARKETPLACE_SPLIT disabled. Existing settlements frozen. Alert admin. Red badge. |

- CMP-07: KYC status MUST be refreshed on each Razorpay OAuth token refresh or via Razorpay account webhook.
- CMP-08: If KYC becomes `suspended` while there are pending settlements: freeze outbox rows, alert admin.

### 4.5 Suspension rules for non-compliant tenants

| Trigger | Suspension type | Reversible? |
|---------|----------------|:-----------:|
| KYC incomplete for > 30 days after OAuth | `LIMITED` (MARKETPLACE_SPLIT disabled, HOST_DIRECT still works) | Yes, on KYC completion |
| Chargeback ratio > 5% (trailing 90 days) | `WARNING` + admin alert | Yes, on improvement |
| Chargeback ratio > 10% | `SUSPENDED` (all marketplace features disabled) | Yes, on admin review |
| Subscription unpaid (grace period expired) | `SUSPENDED` (existing BillingLockFilter) | Yes, on payment |
| Fraud confirmed by admin | `SUSPENDED` (manual) | Admin decision |

### 4.6 Audit log retention policy

| Data | Minimum retention | Rationale |
|------|:-----------------:|-----------|
| AuditLog | 7 years | Financial regulation / tax audit support |
| Payment records | 7 years | Same |
| Booking records | 7 years | Same |
| CommunicationLog | 3 years | Customer service / dispute resolution |
| OutboxMessage | 90 days after completion (archivable) | Operational |
| ConsumedEvent | 90 days (archivable) | Idempotency (after 90d, reprocessing is moot) |
| Structured logs | 90 days (Application Insights or log files) | Operational debugging |

- CMP-09: Audit log entries MUST NEVER be deleted or modified. Immutable by design (no UPDATE/DELETE API).
- CMP-10: Archive strategy for old OutboxMessage/ConsumedEvent rows: move to `_Archive` table or delete after retention window. V1: no archival (table size manageable at early scale). V2: scheduled archival job.

---

## 5. Data privacy & security requirements

### 5.1 Token encryption policy

| Token | Encryption | Algorithm | Key management |
|-------|-----------|-----------|----------------|
| Razorpay OAuth tokens | `IDataProtector` (.NET Data Protection API) | AES-256-CBC with HMAC-SHA256 integrity | Keys stored in Azure Blob Storage + Azure Key Vault (production). Local file system (development). |
| Channex API keys | V1: plaintext. V2: `IDataProtector` (purpose: `"ChannelManagerTokens"`). | — | Same as above in V2. |

- SEC-01: Data Protection keys MUST be configured for Azure deployment (`PersistKeysToAzureBlobStorage` + `ProtectKeysWithAzureKeyVault`).
- SEC-02: Development environments MUST use ephemeral keys (no production key material).

### 5.2 Secret rotation policy

| Secret | Rotation frequency | Rotation method | Impact of rotation |
|--------|:-----------------:|-----------------|-------------------|
| Razorpay API key/secret (Atlas's) | Annually or on compromise | Regenerate in Razorpay dashboard → update `appsettings.json` / Azure App Config | Brief downtime for new order creation during deploy. |
| Razorpay webhook secret | Annually or on compromise | Regenerate in Razorpay dashboard → update config → restart App Service | Webhooks rejected until new secret deployed. |
| Razorpay OAuth client secret | Annually or on compromise | Regenerate in Razorpay Partner dashboard → update config | OAuth callbacks fail until updated. Existing tokens remain valid until expiry. |
| Data Protection API keys | Auto-rotated by .NET (default: 90-day lifetime, auto-managed) | Automatic | Old keys still usable for decryption (key ring). |
| Auth0 client secret | Annually or on compromise | Regenerate in Auth0 dashboard → update config | Admin portal login fails until updated. |
| JWT signing key | Managed by Auth0 | Auth0 auto-rotation | Transparent (JWKS endpoint). |

- SEC-03: All secrets MUST be stored in Azure App Service Configuration (environment variables) or Azure Key Vault. NEVER in `appsettings.json` committed to source control.
- SEC-04: `appsettings.Development.json` MAY contain development-only test secrets. MUST be in `.gitignore`.

### 5.3 Webhook signature validation

| Webhook source | Validation method | Current implementation |
|---------------|-------------------|----------------------|
| Razorpay | HMAC-SHA256: `HMAC(webhookSecret, requestBody)` compared to `X-Razorpay-Signature` header | Implemented in `RazorpayController.Webhook()` |
| Channex | V1: no inbound webhooks. V2: validate per Channex docs. | N/A |

- SEC-05: Webhook endpoints MUST reject requests with missing or invalid signatures. Return 400 (not 200).
- SEC-06: Webhook endpoints MUST NOT expose internal error details in the response body. Log internally, return generic status.
- SEC-07: Webhook handlers MUST respond within 5 seconds. Heavy processing goes to outbox.

### 5.4 Role-based access control

| Role | Scope | Capabilities | Authentication |
|------|-------|-------------|----------------|
| **Tenant Admin (Owner)** | Own tenant's data only | Full CRUD on properties, listings, bookings, channel configs, pricing. Read/write commission settings. View own analytics. Export own data. | Auth0 JWT (audience: atlas-api, tenant resolved from header) |
| **Tenant User (Staff)** | Own tenant's data only | Subset of admin: manage bookings, respond to guests. Cannot change commission, payment mode, or billing. | Auth0 JWT (same, with restricted permissions) |
| **Atlas Admin** | All tenants (IgnoreQueryFilters) | Read all tenant data. Override commission. Retry/resolve settlements. Manage disputes. Hide reviews. Suspend tenants. View platform analytics. | Auth0 JWT with `atlas_admin` role claim |
| **Guest (anonymous)** | Public marketplace data only | Search, view property pages, create bookings, submit reviews (with booking ref). | No authentication. Rate-limited by IP/phone. |

- SEC-08: All tenant-scoped endpoints MUST require `[Authorize]` and resolve TenantId from the authenticated context.
- SEC-09: Atlas Admin endpoints MUST require a separate role check (`[Authorize(Roles = "atlas_admin")]` or equivalent policy).
- SEC-10: Guest-facing (marketplace) endpoints MUST be `[AllowAnonymous]` but rate-limited.
- SEC-11: No endpoint MUST allow a tenant to read another tenant's data. EF Core global filters enforce this.

### 5.5 PII storage rules

| Data | Classification | Stored where | Exposure rules |
|------|---------------|-------------|---------------|
| Guest Name | PII | `Guest.Name` | Shown to booking tenant. Marketplace: first name + last initial only on reviews. |
| Guest Phone | PII, identifier | `Guest.Phone` | Shown to booking tenant. NEVER in public API responses. Used for repeat detection (boolean result only across tenants). |
| Guest Email | PII | `Guest.Email` | Shown to booking tenant. NEVER in public API responses. |
| Guest ID proof | Sensitive PII | `Guest.IdProofUrl` (Blob storage) | Shown to booking tenant only. Delete 90 days post-checkout (V2). |
| Tenant owner name/phone/email | PII | `Tenant.*` | Shown only to that tenant and Atlas Admin. |
| Card details | PCI-sensitive | NEVER stored in Atlas. Razorpay-only. | N/A |

- SEC-12: Structured logs MUST NOT contain PII fields (phone, email, name). Use integer IDs (GuestId, TenantId) only.
- SEC-13: Error messages returned to clients MUST NOT contain PII from other entities.

### 5.6 Data retention rules

(Cross-referenced with section 4.6.)

| Data class | Retention | Deletion method |
|-----------|:---------:|----------------|
| Financial records (Payments, Bookings, AuditLog) | 7 years minimum | No deletion. Archive to cold storage after 7 years (V2). |
| Communication records | 3 years | Archive after 3 years (V2). |
| Operational data (OutboxMessage, ConsumedEvent) | 90 days after terminal state | Scheduled cleanup job (V2). |
| Guest PII | Duration of booking relationship + 90 days | V2: anonymise on deletion request (hash phone/email, redact name). |
| Logs (Application Insights) | 90 days | Auto-purge by Application Insights retention policy. |

### 5.7 Guest data export/delete capability (future-ready)

- SEC-14: V2: `GET /api/guest/export?phone={phone}` returns all bookings, reviews, and communication logs for that phone number across all tenants. Requires OTP verification.
- SEC-15: V2: `POST /api/guest/delete-request` anonymises the guest record: `Name → "Deleted Guest"`, `Phone → SHA256(phone)`, `Email → SHA256(email)`, `IdProofUrl → null`. Booking and payment records are retained (financial obligation) but guest identity is anonymised.
- SEC-16: V1: guest data requests handled manually by Atlas Admin (export via tenant data export).

---

## 6. Incident response playbooks

### 6.1 Payment outage (Razorpay down)

| Phase | Actions |
|-------|---------|
| **Detection** | API returns 5xx/timeout on `CreateOrder`. Circuit breaker trips after 5 consecutive failures in 60 seconds. Structured log: `vendor.razorpay.circuit_open`. |
| **Immediate mitigation** | Guest portal shows "Payments temporarily unavailable. Please try again in a few minutes." No booking drafts created during outage. Existing confirmed bookings unaffected. Settlement worker pauses (backoff). |
| **Communication** | If > 15 minutes: Atlas Admin notified via alert. If > 1 hour: consider adding banner to guest portal. Check Razorpay status page. |
| **Recovery** | Circuit breaker auto-closes after 30-second cool-off and 1 successful probe. Settlement worker drains queued items. Verify no double-charges (idempotency keys protect). |
| **Audit trail** | Structured logs capture: circuit open/close times, failed request count, recovery timestamp. AuditLog: `incident.payment_outage` with duration. |

### 6.2 OTA outage (Channex down)

| Phase | Actions |
|-------|---------|
| **Detection** | Rate/availability push fails for all properties. Structured log: `vendor.channex.push_failed` count > 10 in 5 minutes. |
| **Immediate mitigation** | Push queue builds up (outbox rows). iCal direct-URL sync continues independently (no Channex dependency for iCal). Bookings unaffected. |
| **Communication** | If > 1 hour: alert Atlas Admin. If > 6 hours: notify affected tenants "OTA sync delayed; rates on Airbnb/Booking.com may be stale." |
| **Recovery** | Flush all queued pushes on recovery. Verify rates match between Atlas and OTA via spot checks. |
| **Audit trail** | Structured logs. `incident.ota_outage` AuditLog with duration and affected property count. |

### 6.3 Overbooking conflict

| Phase | Actions |
|-------|---------|
| **Detection** | Guest or host reports double-booking for same dates. Or: automated availability check detects conflict (V2). |
| **Immediate mitigation** | Identify which booking was first by `CreatedAt`. Atlas booking = marketplace or direct. OTA booking = synced via Channex/iCal. |
| **Communication** | Contact both guests (if two Atlas bookings) or guest + host (if one OTA). Offer alternative dates or full refund to the later booking. |
| **Recovery** | Refund the later booking. If OTA booking was first: refund Atlas booking + apologise. If Atlas booking was first: host contacts OTA to cancel. Root cause: iCal sync delay → document and consider reducing sync interval. |
| **Audit trail** | AuditLog: `incident.overbooking` with both booking IDs, resolution, root cause. |

### 6.4 Fraudulent booking spike

| Phase | Actions |
|-------|---------|
| **Detection** | > 20 bookings from same IP in 1 hour. > 10 failed payment attempts from same IP in 10 minutes. Anomalous booking velocity for a single property. Alert: `fraud.signal.booking_spike`. |
| **Immediate mitigation** | Rate limiter auto-blocks IP (FRD-06). If systemic: enable Cloudflare "Under Attack Mode" for the checkout path. Review bookings for card-testing pattern. |
| **Communication** | Atlas Admin alerted immediately. If real bookings affected (legitimate guests blocked): disable rate limit for specific IPs after verification. |
| **Recovery** | Review and cancel confirmed fraudulent bookings. Refund captured amounts. Strengthen rate limits if pattern persists. Add offending IPs/ranges to Cloudflare block list. |
| **Audit trail** | Structured logs: all rate-limit events. AuditLog: `incident.fraud_spike` with IPs, booking IDs, resolution. |

### 6.5 Chargeback spike

| Phase | Actions |
|-------|---------|
| **Detection** | > 3 chargebacks in 24 hours (absolute count, regardless of total volume). Alert: `fraud.signal.chargeback_spike`. |
| **Immediate mitigation** | Freeze all pending settlements for affected tenants. Do NOT auto-refund or auto-settle any disputed amounts. |
| **Communication** | Atlas Admin alerted. If concentrated on one tenant: contact tenant. If spread across tenants: investigate for systemic issue (stolen card ring, payment page compromise). |
| **Recovery** | Per-chargeback: follow section 3.5 flow. If systemic: rotate webhook secret, verify Razorpay integration security, review recent code changes. |
| **Audit trail** | AuditLog per chargeback. Incident summary: `incident.chargeback_spike`. |

### 6.6 Commission calculation bug discovered

| Phase | Actions |
|-------|---------|
| **Detection** | Daily reconciliation report shows commission sum mismatch. Or: tenant reports incorrect commission on a booking. |
| **Immediate mitigation** | Do NOT retroactively change any `CommissionPercentSnapshot` or `CommissionAmount` values (FIN-06, FIN-07). Identify affected bookings by date range and incorrect values. |
| **Communication** | Notify affected tenants: "We identified a commission calculation issue affecting bookings between {date1} and {date2}. We are issuing corrections." |
| **Recovery** | Fix the bug in code. For affected bookings: calculate the correct commission. Issue manual credits or debits as needed (credit notes for overcharged tenants; debit notes for undercharged). All adjustments via AuditLog. |
| **Audit trail** | AuditLog: `incident.commission_bug` with date range, booking count, fix description. Per-booking: `admin.commission.adjustment`. Post-mortem document. |

### 6.7 Unauthorized admin action

| Phase | Actions |
|-------|---------|
| **Detection** | Unexpected AuditLog entry (e.g. tenant suspension, commission override, settlement resolution) from an unrecognised ActorUserId or during off-hours. |
| **Immediate mitigation** | Revoke the suspected admin's Auth0 session. Rotate any secrets the admin had access to. Freeze any settlements they modified. |
| **Communication** | Alert project owner. Review full AuditLog for the suspect user ID in the last 30 days. |
| **Recovery** | Reverse any improper changes (unlock wrongly suspended tenants, revert commission overrides). Strengthen access control (consider 2FA enforcement on Auth0 for admin role). |
| **Audit trail** | AuditLog is the primary evidence. Preserve all entries. `incident.unauthorized_admin` with findings. |

### 6.8 Data leak suspicion

| Phase | Actions |
|-------|---------|
| **Detection** | Unusual API response patterns (large data exports). Tenant reports seeing another tenant's data. External report of Atlas data appearing publicly. |
| **Immediate mitigation** | If cross-tenant leak confirmed: disable the affected API endpoint immediately. If external leak: assess scope (which data, which tenants). |
| **Communication** | Notify affected tenants within 72 hours (regulatory best practice). Notify Atlas Admin immediately. |
| **Recovery** | Fix the leaking endpoint. Audit all API responses for cross-tenant data leakage (integration test). Rotate any exposed secrets/tokens. Review EF Core query filter registration for the affected entity. |
| **Audit trail** | AuditLog: `incident.data_leak_suspicion` with scope, affected tenants, remediation. Preserve all relevant logs for investigation. |

---

## 7. Suspension & enforcement logic

### 7.1 Tenant-level states

| State | Display | Marketplace visible | Bookings allowed | Admin portal | Settlement | Entry triggers |
|-------|---------|:-------------------:|:-----------------:|:------------:|:----------:|----------------|
| `ACTIVE` | Normal | Yes (if properties enabled) | Yes | Full access | Normal | Default. Subscription active + no flags. |
| `WARNING` | Yellow banner | Yes | Yes | Full access + warning | Normal | Chargeback ratio > 5%. Dispute count > 3 in 90d. Fraud signals flagged. |
| `LIMITED` | Orange banner | Yes (existing only, no new properties) | Yes (existing only) | Read-mostly (no new properties, no commission changes) | Normal for existing | KYC incomplete > 30d. TrustScore < 0.40 on all properties. |
| `SUSPENDED` | Red banner. 402 on mutations. | No | No | Read-only | Frozen (existing bookings honoured, new settlements paused until resolved) | Subscription expired (BillingLockFilter). Fraud confirmed. Chargeback ratio > 10%. Admin manual. |

**State transition rules:**

```
ACTIVE → WARNING        (automatic on trigger)
ACTIVE → LIMITED        (automatic on trigger)
ACTIVE → SUSPENDED      (automatic: billing; manual: admin)
WARNING → ACTIVE        (automatic when trigger resolves)
WARNING → LIMITED       (additional trigger)
WARNING → SUSPENDED     (escalation)
LIMITED → ACTIVE        (trigger resolves: KYC completed, TrustScore recovers)
LIMITED → SUSPENDED     (escalation)
SUSPENDED → ACTIVE      (admin manual restore after issue resolved)
SUSPENDED → LIMITED     (admin partial restore)
```

- ENF-01: State transitions MUST be logged in AuditLog with reason and trigger.
- ENF-02: `WARNING` and `LIMITED` are automatic (system-enforced). `SUSPENDED` is either automatic (billing) or manual (admin).
- ENF-03: Only Atlas Admin can transition from `SUSPENDED` to `ACTIVE` or `LIMITED`.

### 7.2 Property-level states

| State | Display | In search results | Bookable | Entry triggers |
|-------|---------|:-----------------:|:--------:|----------------|
| `ACTIVE` | Normal | Yes (if marketplace-enabled) | Yes | Default. Passes all quality and trust checks. |
| `HIDDEN` | Not shown to guests. Tenant sees "Hidden" badge. | No | No (via marketplace). Direct/OTA bookings still work. | TrustScore < 0.20. CancellationRate > 50%. Admin manual hide. |
| `DELISTED` | Removed from marketplace entirely. Tenant sees "Delisted" badge with reason. | No | No (via marketplace). Direct/OTA still work. | Repeated violations after HIDDEN warning. Fraud confirmed on this property. Admin manual. |

**Transition rules:**

```
ACTIVE → HIDDEN         (automatic on trust/cancellation trigger; admin manual)
ACTIVE → DELISTED       (admin manual only, for severe violations)
HIDDEN → ACTIVE         (automatic when trigger resolves; admin manual)
HIDDEN → DELISTED       (admin manual escalation)
DELISTED → HIDDEN       (admin manual, with conditions)
DELISTED → ACTIVE       (admin manual, requires re-review)
```

- ENF-04: Property state is independent of tenant state. A `SUSPENDED` tenant has all properties hidden regardless of property state.
- ENF-05: Property state changes MUST be logged in AuditLog.
- ENF-06: Tenant MUST be notified (admin portal banner + notification via outbox) when a property is HIDDEN or DELISTED, with the reason.

### 7.3 System behaviour per state

| Feature | ACTIVE tenant + ACTIVE property | WARNING tenant | LIMITED tenant | SUSPENDED tenant | HIDDEN property | DELISTED property |
|---------|:------:|:------:|:------:|:------:|:------:|:------:|
| Marketplace search visibility | Yes | Yes | Yes (existing) | No | No | No |
| New booking creation | Yes | Yes | Yes (existing) | No (402) | No (marketplace) | No (marketplace) |
| Commission settings | Full | Full | Read-only | Read-only | N/A | N/A |
| Property creation | Yes | Yes | No | No (402) | N/A | N/A |
| Settlement processing | Normal | Normal | Normal | Frozen (new); existing honoured | Normal | Normal |
| Ranking participation | Normal | Normal | Normal (no new) | Excluded | Excluded | Excluded |
| Data export | Yes | Yes | Yes | Yes (read-only access) | N/A | N/A |

---

## 8. Monitoring & alerting requirements

### 8.1 Alert definitions

All alerts use structured logging with severity levels. V1: alerts are log-based, monitored via Azure Application Insights alerts or a lightweight log scanner.

| Alert | Severity | Trigger | Detection query | Action |
|-------|:--------:|---------|-----------------|--------|
| **Commission mismatch** | Critical | `SUM(CommissionAmount)` vs settlement records diverge by > INR 100 | Daily reconciliation job | Admin investigation. See playbook 6.6. |
| **Split settlement failure** | High | `SettlementStatus = 'Failed'` AND `AttemptCount >= 5` | Settlement worker log: `settlement.max_retries_exceeded` | Admin dashboard. Retry or resolve manually. |
| **Webhook failure rate** | High | > 10% of Razorpay webhooks return non-200 in 1 hour | Log query: `razorpay.webhook.*` with status != 200 | Check webhook secret validity. Review recent deploys. |
| **Booking velocity anomaly** | Medium | Property receives > 10 bookings in 1 hour (unusual for homestay) | Log query: `booking.confirmed` grouped by propertyId per hour | Investigate for fraud (section 6.4). |
| **Cancellation spike** | Medium | Tenant cancellation rate jumps > 20pp in 7 days | Daily batch: compare 7-day rolling rate vs prior 7 days | Admin review. If host-initiated: contact tenant. |
| **Boost abuse** | Medium | Tenant has > 3 commission changes in 7 days | Log query: `AuditLog WHERE action LIKE '%commission.changed%'` grouped by TenantId in 7-day window | Admin review. See section 1.B. |
| **TrustScore drop** | Medium | Property TrustScore drops > 0.10 in one computation cycle | Trust batch job: `trust.score.significant_change` | Review components. Auto-enforcement may trigger HIDDEN. |
| **Circuit breaker open** | High | Razorpay or Channex circuit breaker trips | Log: `vendor.*.circuit_open` | Check vendor status. See playbooks 6.1, 6.2. |
| **Cross-tenant data access attempt** | Critical | EF Core filter bypass detected or TenantId mismatch in SaveChanges | Log: `security.cross_tenant_violation` | Immediate code review. Potential data leak (playbook 6.8). |

### 8.2 Minimal observability stack (no heavy infra)

| Component | Tool | Cost |
|-----------|------|:----:|
| Structured logging | Serilog → Azure Application Insights | Free tier (5 GB/month) |
| Alert rules | Application Insights Alerts (log-based) | Free (up to 10 rules) |
| Dashboard | Application Insights workbook or Azure portal | Free |
| Health check | `/health` endpoint + Azure App Service health check | Free |
| Uptime monitoring | Cloudflare health check (free tier) or UptimeRobot | Free |

- MON-01: All alerts MUST be actionable (not noisy). Start with high-severity only. Add medium-severity after tuning.
- MON-02: Alert delivery: email to Atlas Admin (V1). V2: Slack/Teams webhook.
- MON-03: Dashboard MUST show: active alerts, settlement queue depth, webhook success rate, booking volume (last 24h), chargeback count (last 30d).
- MON-04: No Prometheus, Grafana, ELK, or other heavy infra in V1. Application Insights covers all needs.

---

## 9. Minimum viable risk controls — lean V1

### 9.1 Mandatory for launch

These controls MUST be implemented before the marketplace is publicly accessible.

| Control | Section ref | Complexity | Justification |
|---------|:-----------:|:----------:|---------------|
| Razorpay webhook signature validation | 5.3 | Already done | Payment security baseline. |
| Rate limiting on booking/payment endpoints | 1.A.3 | Low | Card testing prevention. |
| Self-booking detection (contact match) | 1.A.2 | Low | Basic ranking integrity. |
| Commission cooldown (24h) | 1.B.1 | Low | Prevent gaming. |
| Commission damping (7-day ramp) | 1.B.1 | Medium | Ranking integrity. |
| BookingStatus immutability (snapshot fields) | 3.2 | Already done | Financial integrity. |
| Idempotency keys on payment callbacks | 3.2 | Already done | Double-charge prevention. |
| TrustScore computation (basic, 6 components) | 2.1 | Medium | Marketplace quality. |
| TrustScore-based visibility threshold | 2.4 | Low | Bad listings auto-hidden. |
| Refund timing rules | 3.6 | Low | Guest/host fairness. |
| Role-based access control | 5.4 | Already done | Data isolation. |
| AuditLog on all admin/financial actions | 5.5 | Partially done | Compliance trail. |
| Settlement state machine | 3.1 | Medium | Financial correctness. |
| Tenant WARNING/SUSPENDED states | 7.1 | Medium | Platform safety. |
| Property HIDDEN state | 7.2 | Low | Quality enforcement. |
| Daily reconciliation check | 8.1 | Low | Financial integrity. |
| Basic alert rules (5 high-severity) | 8.1 | Low | Operational awareness. |

### 9.2 Phase 2 (post-launch, within 3 months)

| Control | Section ref | Justification for deferral |
|---------|:-----------:|--------------------------|
| IP-based rate limiting with Cloudflare WAF | 1.A.3 | Requires Cloudflare Pro plan or WAF rules config. V1 uses app-level rate limiting. |
| VoIP phone number detection | 1.C.1 | Requires third-party phone validation service. |
| Review text analysis (coordinated reviews) | 1.C.2 | Requires NLP or at least keyword matching. |
| Automated refund approval (pre-checkin) | 3.6 | V1: all refunds manual for safety. Phase 2: auto-approve per policy. |
| Guest data export/delete API | 5.7 | GDPR/DPDP Act compliance. V1: manual process. |
| Tenant LIMITED state (distinct from SUSPENDED) | 7.1 | V1 uses ACTIVE/SUSPENDED binary. |
| Property DELISTED state (distinct from HIDDEN) | 7.2 | V1 uses ACTIVE/HIDDEN binary. |
| Commission stability bonus | 1.B.3 | Nice-to-have for ranking integrity; damping sufficient in V1. |
| Chargeback auto-freeze logic | 3.5 | V1: manual freeze. Phase 2: webhook-triggered. |
| Enchanced alert rules (medium-severity) | 8.1 | Tune thresholds first during V1. |

### 9.3 Future roadmap (6+ months)

| Control | Section ref | Justification for long deferral |
|---------|:-----------:|-------------------------------|
| ML-based fraud detection | 1.A | Requires training data from V1 operation. Architecture hook: outbox events for fraud signals. |
| Review sentiment analysis | 1.C | Requires NLP model. V1 collects data; future model trained on it. |
| Device fingerprinting | 1.A.1 | Requires client-side library. Privacy considerations. |
| Automated chargeback response | 3.5 | Requires evidence assembly automation. V1 is manual. |
| Real-time TrustScore (event-driven) | 2.6 | V1 batch is sufficient. Event-driven when scale demands it. |
| PII anonymisation pipeline | 5.7 | Requires DPDP Act clarity on timelines. Architecture ready. |
| External fraud vendor integration (Sift, Riskified) | 1.A | Cost-prohibitive at early scale. Architecture hook: `IFraudDetectionProvider` interface. |

### 9.4 Extensibility hooks for future ML

Even in V1, the architecture MUST support future ML integration through:

| Hook | Implementation |
|------|---------------|
| Fraud signal events | Every `fraud.signal.*` structured log also writes an `OutboxMessage` with `Topic = 'fraud.signals'`. Future ML consumer can subscribe. |
| Feature store | Daily batch writes TrustScore components to a `PropertyTrustDaily` table (PropertyId, Date, each component score). This becomes training data. |
| Scoring interface | `ITrustScoreProvider` interface with `ComputeScoreAsync(propertyId)`. V1 implementation: rule-based. V2: swap in ML model. |
| Labelled data collection | Admin actions on fraud (confirm/dismiss) are logged as labels: `fraud.label.confirmed` / `fraud.label.dismissed` in AuditLog. |

---

## 10. Definition of Done — trust layer V1

This checklist MUST be fully satisfied before the marketplace is publicly launched.

### Payment dispute test

- [ ] Simulated chargeback webhook (`payment.dispute.created`) is received and processed correctly.
- [ ] Booking status transitions to `Disputed`.
- [ ] Pending settlement (if any) is frozen (not transferred).
- [ ] AuditLog entry created with dispute details.
- [ ] Admin alert generated.
- [ ] Admin can resolve dispute (mark as won or lost).
- [ ] Chargeback reversal ledger entry created when dispute won.

### Commission rollback test

- [ ] Commission bug simulation: manually set an incorrect `CommissionPercentSnapshot` on a test booking.
- [ ] Verify that NO automated process overwrites or recalculates the value (FIN-06).
- [ ] Manual adjustment (credit/debit) can be issued via admin action.
- [ ] AuditLog captures the adjustment with reason and admin ID.
- [ ] Reconciliation report correctly identifies the discrepancy before adjustment.

### Boost abuse simulation

- [ ] Tenant attempts to change commission twice within 24 hours → second change rejected (429).
- [ ] Tenant sets 20% commission on day 1 → ranking boost on day 2 is < 30% of full boost (damping verified).
- [ ] Tenant sets 20% commission with BaseQuality < 0.50 → CommissionBoost = 0.0 (suppressed).
- [ ] Tenant changes commission > 3 times in 7 days → structured log alert generated.
- [ ] Self-booking (guest phone = tenant phone) → booking excluded from RecencyScore.

### Suspension scenario test

- [ ] Tenant subscription expires → `BillingLockFilter` returns 402 on mutation requests.
- [ ] Suspended tenant's properties do not appear in marketplace search.
- [ ] Existing confirmed bookings for suspended tenant are still accessible to guests.
- [ ] Settlement continues for existing MARKETPLACE_SPLIT bookings of suspended tenant.
- [ ] Admin can manually suspend a tenant → state transitions logged in AuditLog.
- [ ] Admin can restore a suspended tenant → marketplace properties reappear within 15 minutes.

### Fraud scenario dry-run

- [ ] Rate limiter: > 5 Razorpay order creation requests from same IP in 10 minutes → 429 returned on 6th.
- [ ] Rate limiter: > 3 orders per phone per hour → 429 returned on 4th.
- [ ] Self-booking flag: booking with guest phone matching tenant phone → `fraud.signal.self_booking_suspect` logged.
- [ ] Self-review flag: review from guest whose phone matches tenant phone → review flagged, excluded from ReviewScore.
- [ ] Admin report: "Self-booking suspects" shows flagged bookings.

### TrustScore validation

- [ ] New property with no data → TrustScore ~0.88 (cold-start defaults).
- [ ] Property with 5 reviews (avg 4.0), 10 bookings (1 cancelled), 2h response time → TrustScore computed correctly per formula.
- [ ] Property with TrustScore < 0.20 → automatically hidden from marketplace.
- [ ] Property with TrustScore 0.40-0.59 → CommissionBoost reduced by 50%.
- [ ] TrustScore drop > 0.10 in one cycle → alert generated.
- [ ] TrustScore components visible in admin portal per property.

### Webhook and security validation

- [ ] Razorpay webhook with valid signature → processed.
- [ ] Razorpay webhook with invalid signature → 400 returned, event not processed.
- [ ] Razorpay webhook with missing signature → 400 returned.
- [ ] Duplicate webhook (same RazorpayPaymentId) → idempotently skipped.
- [ ] Razorpay OAuth tokens are encrypted in DB (not plaintext).
- [ ] No PII (phone, email, name) appears in structured logs (grep verification).
- [ ] Tenant A cannot access Tenant B's data via API (cross-tenant integration test).

### Monitoring and alerting

- [ ] Application Insights receives structured logs from all fraud signal, settlement, and trust events.
- [ ] At least 5 high-severity alert rules configured and tested (commission mismatch, settlement failure, webhook failure, booking anomaly, cross-tenant violation).
- [ ] `/health` endpoint returns 200 when all dependencies are reachable.
- [ ] Daily reconciliation job runs without errors on test data.

---

## Glossary

| Term | Definition |
|------|-----------|
| **TrustScore** | Composite 0–1 score per property derived from reviews, completion rate, cancellation rate, response time, complaints, and chargebacks. |
| **TrustMultiplier** | Mapping from TrustScore to a ranking multiplier (1.0 for high trust, 0.0 for very low trust). |
| **Cold start** | Period when a new property has insufficient data; default values are used for trust/ranking components. |
| **Damping** | Gradual ramp-up of ranking benefit over 7 days after a commission change, to prevent spike-and-revert gaming. |
| **Cooldown** | Minimum 24-hour interval between commission changes, enforced by API. |
| **Circuit breaker** | Pattern that stops calling a failing external service temporarily to prevent cascade failures. |
| **Ledger entry** | An immutable `Payment` row recording a financial event (capture, settlement, refund, chargeback). |
| **Chargeback** | Bank-initiated payment reversal, typically due to fraud or dispute. |
| **Boost suppression** | Forcing CommissionBoost to 0.0 when quality or trust thresholds are not met. |
| **Self-booking** | A booking where the guest's contact information matches the tenant owner's, flagged for ranking exclusion. |
| **Feature store** | Daily snapshot of trust/ranking components per property, serving as training data for future ML models. |
