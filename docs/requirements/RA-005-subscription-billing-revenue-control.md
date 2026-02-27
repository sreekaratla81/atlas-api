# RA-005: Subscription, Billing & Revenue Control Requirements

**Addendum to:** [RA-001](RA-001-marketplace-commission-boost-ota-payments.md) | [RA-002](RA-002-governance-scale-monetization-control.md) | [RA-003](RA-003-growth-demand-network-effects.md) | [RA-004](RA-004-risk-fraud-trust-compliance.md)

**Purpose:** Define the subscription model, usage metering, billing engine, revenue leak prevention, enforcement layer, commission-subscription interaction, credit system, pricing flexibility, financial reporting, and launch readiness for the Atlas revenue system.

**Audience:** Developer, QA, Finance Ops

**Owner:** Atlas Tech Solutions

**Last updated:** 2026-02-27

**Status:** Draft

---

## Table of contents

1. [Subscription Model Architecture](#1-subscription-model-architecture)
2. [Usage Metering & Limits](#2-usage-metering--limits)
3. [Billing Engine Requirements](#3-billing-engine-requirements)
4. [Revenue Leak Prevention Controls](#4-revenue-leak-prevention-controls)
5. [Subscription Enforcement Layer](#5-subscription-enforcement-layer)
6. [Commission + Subscription Interaction Rules](#6-commission--subscription-interaction-rules)
7. [Credit & Wallet System](#7-credit--wallet-system)
8. [Pricing Evolution Flexibility](#8-pricing-evolution-flexibility)
9. [Financial Reporting Requirements](#9-financial-reporting-requirements)
10. [Definition of Done — Revenue System V1](#10-definition-of-done--revenue-system-v1)

---

## 1. Subscription model architecture

### 1.1 Plan types

Atlas offers four plan tiers. Plans are stored in the existing `BillingPlan` table.

| Plan code | Name | Monthly price (INR) | Credits included | Key limit | Listing limit | Marketplace access | OTA connections | Target |
|-----------|------|:-------------------:|:----------------:|:---------:|:-------------:|:-----------------:|:---------------:|--------|
| `FREE` | Free | 0 | 50/month (auto-grant) | 3 keys | 3 | Read-only (view analytics, no enable) | 1 property | New hosts evaluating PMS |
| `BASIC` | Basic | ₹100/key/month | Unlimited | Unlimited | 10 per property | Full (enable marketplace, boost) | 3 properties | Small hosts, 1-5 keys |
| `PRO` | Professional | ₹200/key/month | Unlimited | Unlimited | Unlimited | Full + priority support badge | Unlimited | Mid-size, 5-20 keys |
| `MARKETPLACE_ONLY` | Marketplace Only | 0 (subscription free) | Unlimited | 5 keys | 5 per property | Full | 2 properties | Hosts who only want marketplace (commission is Atlas's revenue) |

**Key** = a bookable listing unit (a `Listing` with `Status = 'Active'`). This is the metered unit for per-key plans.

### 1.2 Per-key pricing logic

For `BASIC` and `PRO` plans, the monthly subscription is computed as:

```
MonthlySubscription = PlanPricePerKey * ActiveKeyCount
```

Where `ActiveKeyCount` = count of `Listing` rows with `Status = 'Active'` for the tenant at invoice generation time.

| Config key | V1 default | Type |
|------------|:----------:|------|
| `Billing:BasicPricePerKeyInr` | 100 | decimal |
| `Billing:ProPricePerKeyInr` | 200 | decimal |

- SUB-01: Per-key price MUST be config-driven (not hardcoded).
- SUB-02: If a tenant has 0 active keys at invoice time, the invoice amount is ₹0 (no charge). A ₹0 invoice is still generated for audit completeness.
- SUB-03: Key count is snapshot at invoice generation. Mid-cycle key additions do NOT generate prorated charges in v1.
- SUB-04: V2: prorated billing for mid-cycle key changes.

### 1.3 Billing frequency

- All plans are billed monthly.
- `TenantSubscription.CurrentPeriodStartUtc` and `CurrentPeriodEndUtc` define the billing cycle.
- Invoice is generated on `CurrentPeriodEndUtc` (or 1 day before, to allow payment before period end).
- `TenantSubscription.NextInvoiceAtUtc` tracks when the next invoice should be created.

### 1.4 Grace period rules

| Rule | Value | Implementation |
|------|:-----:|---------------|
| Grace period duration | 7 days (existing `GracePeriodDays = 7`) | Config on subscription row |
| Grace period start | When `CurrentPeriodEndUtc` passes without payment | `EntitlementsService` checks date |
| During grace period | Tenant can still operate normally. Admin portal shows warning banner. `IsWithinGracePeriod = true`. | Existing logic |
| Grace period expires | Tenant locked. `LockedAtUtc` set. `LockReason = 'InvoiceOverdue'`. `Status = 'Suspended'`. | Billing worker (background job) |
| Unlock | Tenant pays invoice → `UnlockTenantAsync` → `Status = 'Active'` | Existing `CreditsService.UnlockTenantAsync` |

- SUB-05: Grace period MUST be configurable per tenant (admin override). Default: 7 days.
- SUB-06: During grace period: marketplace properties remain visible. New bookings allowed. Commission continues.
- SUB-07: After grace period expires: `BillingLockFilter` blocks all mutations (402). Marketplace properties hidden.

### 1.5 Trial credit model

| Aspect | Value |
|--------|:-----:|
| Trial duration | 30 days |
| Trial credits | 500 (existing `OnboardingGrantAmount`) |
| Credit debit | 1 credit per booking created |
| Credit exhaustion | Tenant locked with `CreditsExhausted` reason |
| Trial → Paid | Tenant selects a paid plan before trial ends. Credits are no longer consumed (unlimited on paid plans). |
| Trial → Expired | If trial ends without upgrade: `Status = 'PastDue'` → grace period → `Suspended`. |

**Existing implementation:** `CreditsService.ProvisionTrialAsync` creates `FREE_TRIAL` subscription + 500 credits. `DebitForBookingAsync` deducts credits and locks if exhausted.

- SUB-08: Trial credits MUST NOT carry over to paid plans. On upgrade, remaining trial credits expire (ledger entry: `Type = 'Expire'`).
- SUB-09: On trial: marketplace access is enabled (to allow tenant to test the marketplace flow). Commission at floor (1%).

### 1.6 Plan upgrade/downgrade rules

| Direction | Allowed? | Timing | Billing |
|-----------|:--------:|--------|---------|
| FREE → BASIC | Yes | Immediate | Prorated for remaining days in current period. |
| FREE → PRO | Yes | Immediate | Prorated. |
| FREE → MARKETPLACE_ONLY | Yes | Immediate | No charge (subscription is free). |
| BASIC → PRO | Yes | Immediate | Price difference prorated for remaining days. |
| BASIC → FREE | Yes (downgrade) | End of current billing period | No refund. Active keys above FREE limit must be deactivated before downgrade takes effect. |
| PRO → BASIC | Yes (downgrade) | End of current billing period | No refund. Listings above BASIC limit must be deactivated. |
| PRO → FREE | Yes (downgrade) | End of current billing period | No refund. Keys + listings must be reduced. |
| MARKETPLACE_ONLY → BASIC/PRO | Yes (upgrade) | Immediate | Prorated from today. |
| Any → MARKETPLACE_ONLY | Yes | End of current billing period | No refund for remaining paid period. |

**Proration formula:**

```
RemainingDays = (CurrentPeriodEndUtc - Now).Days
DailyNewRate  = NewPlanMonthlyTotal / 30
DailyOldRate  = OldPlanMonthlyTotal / 30
ProrationCharge = (DailyNewRate - DailyOldRate) * RemainingDays
```

If `ProrationCharge <= 0` (downgrade or same price): no immediate charge. Difference applied as credit on next invoice.

- SUB-10: Plan changes MUST be logged in `AuditLog`: `tenant.plan.changed` with `{oldPlan, newPlan, prorationAmount}`.
- SUB-11: Downgrades MUST validate that current usage is within the target plan's limits BEFORE processing. If over limits: return 400 with a message indicating which resources must be reduced.

### 1.7 Plan freeze rules

- SUB-12: If tenant has an outstanding unpaid invoice (Status = `Overdue`), plan changes MUST be blocked. Pay the invoice first.
- SUB-13: If tenant is in `Suspended` state, only upgrade to a paid plan is allowed (as part of the reactivation flow).
- SUB-14: During a Razorpay dispute on a subscription payment: plan change is frozen until dispute is resolved.

### 1.8 Plan cancellation rules

| Aspect | Rule |
|--------|------|
| Cancellation initiation | Tenant sets `AutoRenew = false` via admin portal. |
| Effective date | End of current billing period. Tenant has full access until then. |
| Data retention | Tenant data retained for 90 days after cancellation. After 90 days: eligible for archival (v2). |
| Marketplace properties | Hidden from marketplace at cancellation effective date. |
| Reactivation | Tenant can reactivate within 90 days by subscribing to any plan. Data intact. |
| After 90 days | Admin outreach. If no response: data archived (v2) or deleted per policy. |

- SUB-15: Cancellation MUST NOT be immediate. Always end-of-period.
- SUB-16: AuditLog: `tenant.subscription.cancelled` with effective date.

### 1.9 Enforcement behaviour summary

| Feature | FREE | BASIC | PRO | MARKETPLACE_ONLY | Trial | Suspended |
|---------|:----:|:-----:|:---:|:----------------:|:-----:|:---------:|
| Properties | 3 max | Unlimited | Unlimited | 5 max | 3 max | Read-only |
| Active keys (listings) | 3 max | Unlimited (paid per key) | Unlimited (paid per key) | 5 max | 3 max | Read-only |
| OTA connections | 1 property | 3 properties | Unlimited | 2 properties | 1 property | Existing maintained |
| Marketplace enable | No | Yes | Yes | Yes | Yes (test) | No (hidden) |
| Commission boost | No | Yes | Yes | Yes | No (floor only) | No |
| Booking creation | Yes (credit-gated) | Yes | Yes | Yes | Yes (credit-gated) | No (402) |
| Analytics | Basic | Full | Full + export | Full | Basic | Read-only |
| Support | Community | Email | Priority | Email | Community | Billing-only |

---

## 2. Usage metering & limits

### 2.1 Metered dimensions

| Dimension | Existing model | Measurement |
|-----------|---------------|-------------|
| **Properties** | `Property` rows per tenant | `COUNT(Properties WHERE TenantId = X)` |
| **Keys (active listings)** | `Listing` with `Status = 'Active'` | `COUNT(Listings WHERE TenantId = X AND Status = 'Active')` |
| **OTA connections** | `ChannelConfig` with `IsConnected = true` | `COUNT(DISTINCT PropertyId FROM ChannelConfig WHERE TenantId = X AND IsConnected = true)` |
| **API syncs** | Channex push calls per month | Structured log count: `ota.sync.push` per tenant per month |
| **Notifications** | `CommunicationLog` rows per month | `COUNT(CommunicationLog WHERE TenantId = X AND SentAtUtc >= MonthStart)` |
| **Marketplace listings** | Properties with `IsMarketplaceEnabled = true` | `COUNT(Properties WHERE TenantId = X AND IsMarketplaceEnabled = true)` |

### 2.2 Limits per plan

| Dimension | FREE | BASIC | PRO | MARKETPLACE_ONLY |
|-----------|:----:|:-----:|:---:|:----------------:|
| Properties | 3 | Unlimited | Unlimited | 5 |
| Keys (active listings) | 3 | Unlimited (metered) | Unlimited (metered) | 5 |
| OTA properties | 1 | 3 | Unlimited | 2 |
| API syncs/month | 500 | 5,000 | Unlimited | 2,000 |
| Notifications/month | 100 | 1,000 | 10,000 | 500 |
| Marketplace listings | 0 | Unlimited | Unlimited | 5 |

### 2.3 Hard cap vs soft cap

| Dimension | Cap type | Behaviour at limit |
|-----------|---------|-------------------|
| Properties | Hard | API returns 403: "Property limit reached. Upgrade your plan." |
| Keys | Soft (BASIC/PRO) | No hard cap; each additional key increases invoice. |
| Keys | Hard (FREE/MARKETPLACE_ONLY) | API blocks creating active listing beyond limit. |
| OTA connections | Hard | Channel config creation returns 403 if limit reached. |
| API syncs | Soft | Above limit: sync continues but warning logged. Next invoice includes overage charge (v2). V1: warning only. |
| Notifications | Hard | Above limit: notification not sent. Structured log: `billing.limit.notifications_exceeded`. CommunicationLog: `Status = 'SkippedLimit'`. |
| Marketplace listings | Hard (FREE/MARKETPLACE_ONLY) | Cannot enable marketplace on more properties than limit. |

### 2.4 Warning thresholds

| Threshold | Trigger | User notification |
|-----------|---------|-------------------|
| 80% of hard cap reached | Property/key/notification count >= 80% of plan limit | Admin portal: yellow banner "You're approaching your plan limit." |
| 100% of hard cap reached | At limit | Admin portal: red banner "Limit reached. Upgrade to continue." |
| Credit balance <= 50 (trial) | Credits running low | Admin portal: "50 credits remaining. Subscribe to a plan for unlimited bookings." |
| Credit balance <= 10 (trial) | Credits nearly exhausted | Admin portal: urgent banner + notification via outbox. |

### 2.5 Auto-plan upgrade triggers

- USG-01: V1: no auto-upgrade. Tenant must manually select a plan.
- USG-02: V2: when a tenant hits a hard cap, show an inline "Upgrade now" button that immediately processes upgrade with prorated charge.
- USG-03: If `AutoRenew = true` and trial is expiring: prompt to select plan 7 days before trial end. No auto-select.

---

## 3. Billing engine requirements

### 3.1 Invoice generation logic

**Trigger:** Background worker (`BillingWorker`) runs daily, checks for tenants where `NextInvoiceAtUtc <= NOW`.

**Steps:**

1. For each due tenant:
   a. Count `ActiveKeyCount` (active listings).
   b. Compute `AmountInr = PlanPricePerKey * ActiveKeyCount`. If FREE or MARKETPLACE_ONLY: `AmountInr = 0`.
   c. Apply any adjustments (credits, proration, discounts from section 6).
   d. Compute `TaxAmountInr = ROUND(AmountInr * TaxGstRate / 100, 2)`.
   e. Compute `TotalInr = AmountInr + TaxAmountInr`.
   f. Create `BillingInvoice` row with `Status = 'Draft'`.
   g. If `TotalInr > 0`: generate Razorpay payment link → set `PaymentLinkId` → set `Status = 'Issued'` → set `DueAtUtc = NOW + 7 days`.
   h. If `TotalInr = 0`: set `Status = 'Paid'` immediately (nothing to collect).
   i. Update `TenantSubscription.NextInvoiceAtUtc = CurrentPeriodEndUtc + 1 month`.
   j. Update `CurrentPeriodStartUtc` and `CurrentPeriodEndUtc` for the new cycle.

2. Send invoice notification to tenant (email/WhatsApp via outbox).

- BIL-01: Invoice generation MUST be idempotent. If a `BillingInvoice` already exists for the period: skip.
- BIL-02: Invoice generation MUST handle edge cases: tenant created mid-month, plan changed mid-cycle.
- BIL-03: Zero-amount invoices are created for audit trail but skipped for payment collection.

### 3.2 GST calculation logic

| Config key | V1 default | Type | Description |
|------------|:----------:|------|-------------|
| `Billing:GstRatePercent` | 18.00 | decimal | Standard GST rate on SaaS services |
| `Billing:GstEnabled` | true | bool | Kill switch for GST calculation |
| `Billing:AtlasGstNumber` | (configured in env) | string | Displayed on invoices |

- BIL-04: GST rate MUST be config-driven. If `GstEnabled = false`: `TaxAmountInr = 0`, `TaxGstRate = 0`.
- BIL-05: GST is computed on `AmountInr` (pre-tax subscription amount). Commission GST is handled separately (commission invoicing is a different flow, defined in RA-001).
- BIL-06: If GST rate changes, only future invoices are affected. Existing invoices retain their snapshot rate.

### 3.3 Commission vs subscription separation

| Revenue stream | Timing | Invoice type | Payment method |
|---------------|--------|-------------|---------------|
| Subscription | Monthly, pre-pay | `BillingInvoice` with `Type = 'subscription'` (new field) | Razorpay payment link (tenant pays) |
| Commission | Per-booking, real-time | Deducted from booking payment (MARKETPLACE_SPLIT) or invoiced monthly (HOST_DIRECT) | Auto-deducted or invoiced |

- BIL-07: Subscription invoices and commission invoices MUST be separate documents. A tenant receives up to 2 invoices per month: one for subscription, one for commission (if HOST_DIRECT + commission > 0).
- BIL-08: Commission for HOST_DIRECT tenants MUST be invoiced monthly: `SUM(CommissionAmount) WHERE PaymentModeSnapshot = 'HOST_DIRECT' AND BookingMonth = X`.
- BIL-09: Commission for MARKETPLACE_SPLIT tenants is auto-deducted from the booking payment (Atlas retains commission, transfers HostPayoutAmount). No separate commission invoice needed.
- BIL-10: `BillingInvoice.Type` (new field, varchar 20): `'subscription'` or `'commission'`. Default: `'subscription'`.

### 3.4 Payment retry rules

| Attempt | Timing | Action on failure |
|:-------:|--------|-------------------|
| 1 | Invoice due date | Payment link sent. If auto-debit: charge attempted. |
| 2 | Due + 2 days | Reminder notification. Payment link re-sent. |
| 3 | Due + 5 days | Final reminder. "Your account will be suspended in 2 days." |
| 4 | Due + 7 days (grace period end) | Lock tenant. `LockReason = 'InvoiceOverdue'`. |

- BIL-11: Payment retries are notification-only in v1. No auto-debit retry. Tenant must click payment link.
- BIL-12: V2: auto-debit via Razorpay Subscription API (e-mandate). Retry failed charges automatically.

### 3.5 Auto-debit handling

V1: no auto-debit. All subscription payments are tenant-initiated via payment link.

V2 design (future-ready):
- Tenant registers a Razorpay e-mandate (UPI autopay or card mandate).
- On invoice due date: system attempts auto-debit via Razorpay.
- If fails: fall through to manual payment link flow (section 3.4).

- BIL-13: V1 MUST NOT implement auto-debit. Manual payment links only.
- BIL-14: `BillingInvoice.Provider` field (existing) supports `'Manual'` (v1) and `'RazorpaySubscription'` (v2).

### 3.6 Manual payment fallback

If Razorpay payment link fails (e.g. Razorpay outage):

- Atlas Admin can mark an invoice as `Paid` manually (with `AuditLog` entry).
- Atlas Admin can generate a custom payment link or accept bank transfer.
- `BillingPayment.ProviderPaymentId` stores the external reference (payment link ID, UPI ref, bank transfer ref).

### 3.7 Required tables (existing + extensions)

| Table | Exists? | Purpose | Changes needed |
|-------|:-------:|---------|---------------|
| `BillingPlan` | Yes | Plan definitions (Code, Name, MonthlyPriceInr, CreditsIncluded, SeatLimit, ListingLimit) | Add: `PricePerKeyInr` (decimal), `MaxProperties` (int?), `MaxOtaProperties` (int?), `MaxNotificationsPerMonth` (int?), `MarketplaceEnabled` (bool), `BoostEnabled` (bool) |
| `TenantSubscription` | Yes | Active subscription per tenant | Add: `ActiveKeyCountSnapshot` (int, set at invoice time) |
| `BillingInvoice` | Yes | Invoices | Add: `Type` (varchar 20, default `'subscription'`), `LineItemsJson` (nvarchar max, nullable — structured breakdown) |
| `BillingPayment` | Yes | Payment records for invoices | No changes |
| `TenantCreditsLedger` | Yes | Append-only credit/debit ledger | No changes |
| `UsageSnapshot` (new) | No | Monthly usage snapshot for billing and analytics | `Id`, `TenantId`, `Month` (date), `ActiveKeyCount`, `PropertyCount`, `OtaConnectionCount`, `NotificationCount`, `MarketplaceListingCount`, `CreatedAtUtc` |
| `BillingAdjustment` (new) | No | Manual billing adjustments (credits, discounts, corrections) | `Id`, `TenantId`, `InvoiceId` (nullable), `Type` (credit/debit/discount/correction), `AmountInr`, `Reason`, `AdminUserId`, `CreatedAtUtc` |

- BIL-15: `UsageSnapshot` MUST be written by the billing worker at invoice generation time. This freezes the metering for that month.
- BIL-16: `BillingAdjustment` entries MUST be included in invoice calculation: `AdjustedAmount = BaseAmount - SUM(Adjustments WHERE Type = 'credit') + SUM(Adjustments WHERE Type = 'debit')`.
- BIL-17: All new columns MUST be nullable with defaults to avoid migration issues on existing data.

---

## 4. Revenue leak prevention controls

### 4.1 Tenant disables marketplace to avoid commission

**Threat:** Tenant enables marketplace to get bookings, then disables `IsMarketplaceEnabled` mid-stay to avoid commission on the current booking.

| Detection | Implementation |
|-----------|---------------|
| Commission snapshot is immutable | `CommissionPercentSnapshot` and `PaymentModeSnapshot` are set at booking creation. Disabling marketplace afterwards has zero effect on existing bookings. |
| Toggle audit | `AuditLog`: `property.marketplace.toggled` with `{propertyId, enabled, timestamp}`. |
| Frequency alert | If tenant toggles marketplace > 3 times in 30 days: structured log `revenue.signal.marketplace_toggle_abuse`. Admin review. |

- REV-01: Commission is captured at booking creation. No subsequent action (marketplace toggle, plan change, subscription cancellation) can reduce or void the commission on an existing booking.
- REV-02: MARKETPLACE_ONLY plan tenants who disable marketplace on ALL properties for > 30 days: auto-downgrade to FREE plan. They are not paying subscription AND avoiding commission.

### 4.2 Commission override manipulation

**Threat:** Tenant sets property override to minimum (= tenant default) to minimise commission, then uses the ranking benefit they accumulated while at a higher rate.

| Detection | Implementation |
|-----------|---------------|
| 24-hour cooldown | Commission change rate-limited (RA-002 AG-01). |
| 7-day damping | Ranking benefit ramps down over 7 days after lowering (RA-002 AG-07). |
| Audit trail | Every change logged with old/new values. |

- REV-03: Commission manipulation does NOT leak revenue because commission is snapshotted per booking at creation time. The snapshot uses the rate in effect at that moment.
- REV-04: No mechanism exists to retroactively lower commission on existing bookings.

### 4.3 Hidden booking attempts

**Threat:** Tenant takes a marketplace booking, then processes payment outside Atlas to avoid commission tracking.

| Detection | Implementation |
|-----------|---------------|
| Booking exists but no payment | Reconciliation check: bookings with `BookingStatus = 'Confirmed'` and no associated `Payment` row (or `Payment.Status != 'completed'`). |
| Guest complaint | Guest reports payment issue → reveals off-platform payment. |

- REV-05: Marketplace bookings MUST require online payment through Atlas (Razorpay). No "pay at property" option for marketplace bookings.
- REV-06: Daily reconciliation: flag bookings with `BookingSource LIKE 'marketplace_%'` and no completed payment.

### 4.4 Direct booking bypass

**Threat:** Tenant shares their direct booking link (non-marketplace) with guests they found through the marketplace, bypassing commission.

| Detection | Implementation |
|-----------|---------------|
| Not detectable in v1 | If guest found property on marketplace but books via tenant's direct link: Atlas sees a direct/admin booking, not marketplace. |
| V2 mitigation | Cookie-based attribution: if guest previously viewed a marketplace page, the `atlas_utm` cookie tags subsequent direct bookings. |

- REV-07: V1: this is accepted as a natural marketplace leakage. Commission model is designed to be fair at 1% floor (low enough that bypass incentive is minimal).
- REV-08: V2: attribute bookings where guest visited marketplace within 30 days to `marketplace_attributed`. Include in commission calculation.

### 4.5 Boost exploitation

(Cross-referenced from RA-002 section 2 and RA-004 section 1.B.)

- REV-09: Boost is ranking-only. It does not directly cost Atlas revenue. Higher commission = higher Atlas revenue per booking.
- REV-10: Boost suppression for low-quality properties prevents tenants from gaming ranking without providing value.

### 4.6 Required audit logs

| Event | AuditLog action | Payload |
|-------|----------------|---------|
| Plan change | `tenant.plan.changed` | `{oldPlan, newPlan, prorationAmount}` |
| Invoice created | `billing.invoice.created` | `{invoiceId, amount, period}` |
| Invoice paid | `billing.invoice.paid` | `{invoiceId, paymentId, amount}` |
| Invoice overdue | `billing.invoice.overdue` | `{invoiceId, dueDate}` |
| Tenant locked | `billing.tenant.locked` | `{reason}` |
| Tenant unlocked | `billing.tenant.unlocked` | `{}` |
| Marketplace toggled | `property.marketplace.toggled` | `{propertyId, enabled}` |
| Commission changed | `tenant.commission.changed` | `{old, new}` |
| Credit adjustment | `billing.credit.adjusted` | `{delta, reason, adminUserId}` |
| Manual invoice pay | `billing.invoice.manual_paid` | `{invoiceId, adminUserId, reference}` |

### 4.7 Periodic reconciliation logic

| Check | Frequency | Query | Alert threshold |
|-------|-----------|-------|-----------------|
| Subscription revenue vs invoices | Monthly | `SUM(BillingPayment.AmountInr WHERE Status = 'Completed')` vs `SUM(BillingInvoice.TotalInr WHERE Status = 'Paid')` | Mismatch > INR 10 |
| Commission revenue vs bookings | Daily | `SUM(CommissionAmount WHERE PaymentModeSnapshot = 'MARKETPLACE_SPLIT')` vs Atlas Razorpay account credits | Mismatch > INR 100 |
| Active key count vs invoiced count | Monthly | `COUNT(active listings)` vs `UsageSnapshot.ActiveKeyCount` | Divergence > 5% |
| Unpaid invoices > 30 days | Weekly | `BillingInvoice WHERE Status = 'Overdue' AND DueAtUtc < NOW - 30d` | Any count > 0 |
| Marketplace toggles | Weekly | Tenants with > 3 toggles in 30 days | Any count > 0 |

---

## 5. Subscription enforcement layer

### 5.1 Tenant subscription states

| State | Code | Entry condition | Display |
|-------|------|----------------|---------|
| **Active** | `Active` | Paid plan, no outstanding invoices | Normal operation. Green badge in admin portal. |
| **Trial** | `Trial` | New tenant, within 30-day trial | Normal operation with credit limit. Blue badge. |
| **Grace Period** | `PastDue` | Period ended, invoice unpaid, within grace period (7 days) | Full operation. Yellow warning banner: "Invoice overdue. Pay within {days} to avoid suspension." |
| **Payment Failed** | `PastDue` + `LockedAtUtc = null` | Auto-debit failed (v2) or payment link payment failed | Same as Grace Period. Retry payment flow active. |
| **Suspended** | `Suspended` | Grace period expired. Or manual suspension. | Read-only. Red banner. 402 on mutations. |
| **Cancelled** | `Canceled` | Tenant chose not to renew. Effective at period end. | Read-only after period. Data retained 90 days. |

### 5.2 System behaviour per state

| Feature | Active | Trial | Grace Period (PastDue) | Suspended | Cancelled (post-period) |
|---------|:------:|:-----:|:---------------------:|:---------:|:----------------------:|
| **OTA sync** | Normal push cycle | Normal | Normal | Paused. Existing channel configs retained but not synced. | Paused. |
| **Booking creation** | Allowed | Allowed (credit-gated) | Allowed | Blocked (402) | Blocked (402) |
| **Marketplace listing visibility** | Shown | Shown (if enabled) | Shown | Hidden | Hidden |
| **Commission boost eligibility** | Yes | No (floor only) | Yes | No | No |
| **Payment routing (MARKETPLACE_SPLIT)** | Normal | Normal | Normal | Existing settlements honoured. New bookings blocked. | Same as Suspended. |
| **Payment routing (HOST_DIRECT)** | Normal | Normal | Normal | Blocked (no new bookings) | Blocked |
| **Admin portal access** | Full | Full | Full + warning | Read-only (except billing page: `[AllowWhenLocked]`) | Read-only |
| **Data export** | Yes | Yes | Yes | Yes (read-only) | Yes (within 90 days) |
| **OTA disconnect on suspension** | No | No | No | No (configs preserved) | No (within 90 days) |

- ENF-01: `BillingLockFilter` (existing) handles the `Suspended` state enforcement. Returns 402 with lock reason and pay link.
- ENF-02: Marketplace visibility check (existing filter) MUST also exclude properties from tenants with `Status = 'Suspended'` or `Status = 'Canceled'`.
- ENF-03: Transition from `Trial` to `PastDue` happens automatically when `TrialEndsAtUtc` passes without plan upgrade. Background worker checks daily.
- ENF-04: Transition from `PastDue` to `Suspended` happens automatically when grace period expires. Background worker checks daily.

---

## 6. Commission + subscription interaction rules

### 6.1 Can tenant use marketplace if subscription unpaid?

| Scenario | Answer |
|----------|--------|
| PastDue (grace period) | Yes. Marketplace properties visible. Bookings allowed. Commission collected. |
| Suspended (grace expired) | No. Marketplace properties hidden. No new bookings. Existing booking settlements continue. |
| MARKETPLACE_ONLY plan (no subscription fee) | Always yes (no subscription to be unpaid). Commission is the only revenue. |

- INT-01: MARKETPLACE_ONLY tenants are NEVER locked for subscription non-payment (there is none). They CAN be locked for `Manual` or `ChargeFailed` reasons.

### 6.2 Can tenant use HOST_DIRECT while unpaid?

| Scenario | Answer |
|----------|--------|
| PastDue | Yes (grace period). |
| Suspended | No. All booking creation blocked. |

### 6.3 Can commission offset subscription fee?

| Config key | V1 default | Type |
|------------|:----------:|------|
| `Billing:CommissionOffsetEnabled` | false | bool |
| `Billing:CommissionOffsetMaxPercent` | 50 | decimal |

When enabled (v2):

```
SubscriptionDue = max(0, InvoiceAmount - (CommissionEarned * OffsetMaxPercent / 100))
```

- INT-02: V1: commission does NOT offset subscription. Separate revenue streams.
- INT-03: V2: configurable offset. Atlas commission earned in the period can reduce (but not eliminate) the subscription invoice. Maximum offset: `CommissionOffsetMaxPercent` (e.g. 50% of subscription can be offset by commission).

### 6.4 Is subscription waived for high commission tenants?

| Config key | V1 default | Type |
|------------|:----------:|------|
| `Billing:SubscriptionWaiverEnabled` | false | bool |
| `Billing:SubscriptionWaiverMinCommissionPercent` | 10.00 | decimal |
| `Billing:SubscriptionWaiverMinMonthlyGmv` | 100000 | decimal |

When enabled (v2):

If tenant's `DefaultCommissionPercent >= WaiverMinCommissionPercent` AND `MonthlyGMV >= WaiverMinMonthlyGmv`: subscription fee waived for that month. Invoice generated at ₹0.

- INT-04: V1: no waiver. All tenants on paid plans pay subscription regardless of commission.
- INT-05: V2: waiver is a promotional mechanism. Logged in `AuditLog`: `billing.subscription.waived` with `{commissionPercent, gmv}`.

### 6.5 Commission-based subscription discount

| Config key | V1 default | Type |
|------------|:----------:|------|
| `Billing:CommissionDiscountTiers` | `[]` (empty) | JSON array |

V2 tier definition example:

```json
[
  { "minCommissionPercent": 5, "subscriptionDiscountPercent": 10 },
  { "minCommissionPercent": 10, "subscriptionDiscountPercent": 25 },
  { "minCommissionPercent": 15, "subscriptionDiscountPercent": 50 }
]
```

- INT-06: V1: no commission-based discount. Config exists but tiers array is empty (no discount applied).
- INT-07: When tiers are configured: `CommissionCalculationService` reads the tenant's `DefaultCommissionPercent`, finds the highest qualifying tier, applies the `subscriptionDiscountPercent` to the subscription invoice.
- INT-08: Discount MUST appear as a line item on the invoice: "Commission loyalty discount: -₹{amount}".

### 6.6 All configurable via IOptions

All interaction parameters MUST be readable via `IOptions<BillingInteractionSettings>`:

```
BillingInteractionSettings:
  CommissionOffsetEnabled: bool
  CommissionOffsetMaxPercent: decimal
  SubscriptionWaiverEnabled: bool
  SubscriptionWaiverMinCommissionPercent: decimal
  SubscriptionWaiverMinMonthlyGmv: decimal
  CommissionDiscountTiers: Tier[]
```

---

## 7. Credit & wallet system

### 7.1 Existing credit infrastructure

The codebase already has:

- `TenantCreditsLedger`: append-only ledger with `Type` (Grant/Debit/Adjust/Expire) and `CreditsDelta`.
- `CreditsService`: `GetBalanceAsync`, `ProvisionTrialAsync`, `DebitForBookingAsync`, `LockTenantAsync`, `UnlockTenantAsync`.
- Balance = `SUM(CreditsDelta)` per tenant.
- 1 credit = 1 booking.

### 7.2 Tenant credit wallet (V1)

| Credit type | How earned | How consumed | Expiry |
|-------------|-----------|-------------|--------|
| **Trial credits** | 500 on signup | 1 per booking (Trial plan only) | On plan upgrade (remaining trial credits expired via `Expire` ledger entry) or 30 days (trial end) |
| **Plan credits** | `CreditsIncluded` from `BillingPlan`, granted monthly | 1 per booking (FREE plan only) | End of billing period (unused credits do not carry over on FREE plan) |
| **Manual adjustment** | Atlas Admin via `CreditAdjustRequestDto` | Applied to balance | No expiry |

- CRD-01: On paid plans (BASIC, PRO, MARKETPLACE_ONLY): credits are unlimited. `DebitForBookingAsync` MUST skip credit debit for paid plans. Credit system only gates FREE and Trial.
- CRD-02: FREE plan: credits are replenished monthly (grant of `CreditsIncluded` at period start). Old credits expire at period end.

### 7.3 Commission offset credits (V2)

Not V1. Architecture-ready.

| Concept | Design |
|---------|--------|
| Commission offset | Commission earned in a period can generate "offset credits" that reduce the subscription invoice. |
| Implementation | `TenantCreditsLedger` entry: `Type = 'CommissionOffset'`, `CreditsDelta = -(offsetAmount)` applied to subscription invoice calculation. |
| Config | `Billing:CommissionOffsetEnabled` (section 6.3). |

### 7.4 Referral credits (V2)

| Concept | Design |
|---------|--------|
| Host refers another host | Referring host receives X credits (configurable: `Growth:HostReferralCredits`, default 100). |
| Guest referral | Covered by coupon system (RA-003 section 5.6). Not credit-based. |
| Implementation | `TenantCreditsLedger` entry: `Type = 'Grant'`, `Reason = 'HostReferral'`, `ReferenceType = 'Tenant'`, `ReferenceId = referredTenantId`. |

### 7.5 Promotional boost credits (V2)

| Concept | Design |
|---------|--------|
| Atlas runs a promotion: "Set commission > 5% and get 3 months free" | Implement as a BillingAdjustment (Type = 'discount') on the next 3 invoices, not as credits. |
| Alternatively: grant subscription-equivalent credits | `TenantCreditsLedger` with `Type = 'Grant'`, `Reason = 'BoostPromotion'`. |

- CRD-03: V1 MUST NOT implement promotional credits. Use `BillingAdjustment` for manual discounts.

### 7.6 Manual credit adjustments

Existing: `CreditAdjustRequestDto` allows Atlas Admin to add/subtract credits.

| Requirement | Detail |
|-------------|--------|
| CRD-04 | Manual adjustments MUST include a mandatory `Reason` (max 200 chars). |
| CRD-05 | All adjustments MUST be logged in `TenantCreditsLedger` (Type = `Adjust`) AND `AuditLog`. |
| CRD-06 | Negative adjustments (taking credits away) MUST check that resulting balance >= 0. If it would go negative: reject. |

---

## 8. Pricing evolution flexibility

### 8.1 Changing per-key price

| Mechanism | Implementation |
|-----------|---------------|
| Config change | Update `Billing:BasicPricePerKeyInr` and/or `Billing:ProPricePerKeyInr` in `appsettings.json` / Azure App Config. |
| Effective when | Next invoice generation. Existing invoices are unaffected (snapshot at generation time). |
| Communication | Notify tenants 30 days before a price increase (admin-initiated, not system-automated). |
| DB change required | None. `BillingPlan.MonthlyPriceInr` can be updated, but per-key price is in config (independent of the plan row). |

- PRC-01: Per-key price MUST be read from `IOptions<BillingSettings>`, not from `BillingPlan.MonthlyPriceInr` (which is a base price for plans that are NOT per-key).
- PRC-02: `BillingPlan.PricePerKeyInr` (new field) allows per-plan per-key pricing. If null: fall back to global config.

### 8.2 % of GMV subscription

V2 concept: charge subscription as a percentage of GMV instead of per-key.

| Config key | V1 default | Purpose |
|------------|:----------:|---------|
| `Billing:GmvSubscriptionEnabled` | false | Enable GMV-based pricing |
| `Billing:GmvSubscriptionPercent` | 0.5 | Percentage of monthly GMV |
| `Billing:GmvSubscriptionMinInr` | 500 | Minimum monthly charge |
| `Billing:GmvSubscriptionMaxInr` | 50000 | Cap |

```
GmvSubscription = CLAMP(
  MonthlyGMV * GmvSubscriptionPercent / 100,
  GmvSubscriptionMinInr,
  GmvSubscriptionMaxInr
)
```

- PRC-03: V1: not implemented. Config keys exist but `GmvSubscriptionEnabled = false`.
- PRC-04: When enabled: `BillingWorker` computes GMV for the period and uses the GMV formula instead of per-key.
- PRC-05: No schema change required. `BillingInvoice.AmountInr` holds the computed amount regardless of pricing model. `LineItemsJson` records the calculation method.

### 8.3 Hybrid pricing (subscription + commission)

Already the default architecture: tenant pays subscription (section 1) AND commission (per booking). Both are independent, additive revenue streams.

- PRC-06: The system MUST support subscription-only tenants (BASIC, PRO with HOST_DIRECT — commission is reporting-only) and commission-only tenants (MARKETPLACE_ONLY — no subscription, commission on every booking).

### 8.4 City-based pricing experiments

| Mechanism | Implementation |
|-----------|---------------|
| Config | `Billing:CityPricingOverrides` JSON: `[{"city": "goa", "basicPerKey": 150, "proPerKey": 250}]` |
| Resolution | `BillingWorker` checks if tenant's primary city (most properties) has an override. If yes, use city price. |
| DB change | None. Config-driven. |

- PRC-07: V1: no city pricing. Config key exists but array is empty.
- PRC-08: V2: city pricing requires a `Property.City` field (RA-003 prerequisite).

### 8.5 Promotional pricing windows

| Mechanism | Implementation |
|-----------|---------------|
| Config | `Billing:Promotions` JSON array: `[{"code": "LAUNCH_GOA", "discountPercent": 50, "startsAt": "...", "endsAt": "...", "cityFilter": "goa"}]` |
| Application | `BillingWorker` checks for active promotions at invoice time. Applies highest qualifying discount. |
| Invoice line item | "Promotional discount (LAUNCH_GOA): -₹{amount}" |
| DB change | None. Alternatively, use `BillingAdjustment` rows for per-tenant promotions. |

- PRC-09: V1: use `BillingAdjustment` for manual promotional discounts. Automated promotion engine is v2.
- PRC-10: All pricing changes MUST avoid schema refactors. New pricing models use config + `LineItemsJson` for audit.

---

## 9. Financial reporting requirements

### 9.1 Tenant dashboard

The admin portal "Billing" tab MUST show:

| Metric | Formula | Period | Display |
|--------|---------|--------|---------|
| **Subscription cost** | `SUM(BillingInvoice.TotalInr WHERE Type = 'subscription' AND Status = 'Paid')` | Selectable: current month / last 3 / last 12 / custom | INR total |
| **Commission paid** | `SUM(CommissionAmount WHERE PaymentModeSnapshot = 'MARKETPLACE_SPLIT')` (auto-deducted) + `SUM(BillingInvoice.TotalInr WHERE Type = 'commission')` (HOST_DIRECT invoiced) | Same | INR total |
| **Boost spend** | `CommissionPaid - (BookingCount * FloorCommissionPercent * AvgBookingAmount / 100)`. Approximation: the premium above floor commission. | Same | INR total (or "N/A" if at floor) |
| **GMV** | `SUM(FinalAmount)` for all confirmed bookings | Same | INR total |
| **Net revenue** | `GMV - SubscriptionCost - CommissionPaid` | Same | INR total |
| **Credit balance** | `SUM(TenantCreditsLedger.CreditsDelta)` | Current | Integer |
| **Active plan** | `BillingPlan.Name` | Current | Text |
| **Next invoice** | `TenantSubscription.NextInvoiceAtUtc` | Upcoming | Date |
| **Invoice history** | List of `BillingInvoice` rows | All time, paginated | Table with status, amount, PDF link |

- RPT-01: Financial metrics MUST use query-time computation (no stale cache for monetary values).
- RPT-02: "Boost spend" is an approximation displayed for tenant awareness. Not a billable metric.

### 9.2 Atlas Admin dashboard

| Metric | Formula | Refresh |
|--------|---------|---------|
| **MRR (Monthly Recurring Revenue)** | `SUM(ActiveKeyCount * PricePerKey)` across all active subscriptions with paid plans | Daily |
| **GMV** | `SUM(FinalAmount)` for all marketplace bookings in period | Daily |
| **Commission revenue** | `SUM(CommissionAmount)` for all MARKETPLACE_SPLIT bookings in period | Daily |
| **Subscription revenue** | `SUM(BillingPayment.AmountInr WHERE Status = 'Completed')` in period | Daily |
| **Total revenue** | Commission revenue + Subscription revenue | Daily |
| **Active tenants** | Tenants with `TenantSubscription.Status IN ('Active', 'Trial')` | Real-time |
| **Paid tenants** | Tenants with `BillingPlan.Code IN ('BASIC', 'PRO')` | Real-time |
| **MARKETPLACE_ONLY tenants** | Tenants with `BillingPlan.Code = 'MARKETPLACE_ONLY'` | Real-time |
| **Churn rate** | Tenants who cancelled or were suspended / Total tenants at period start | Monthly |
| **Boost adoption %** | Tenants with `DefaultCommissionPercent > 1.00` / Total marketplace tenants | Daily |
| **ARPU (Avg Revenue Per User)** | Total revenue / Active tenants | Monthly |
| **Unpaid invoices** | `COUNT(BillingInvoice WHERE Status = 'Overdue')` | Real-time |
| **Credit utilisation** | `SUM(Debits) / SUM(Grants)` across all trial tenants | Monthly |

- RPT-03: Admin dashboard MUST show data freshness timestamp.
- RPT-04: Financial metrics (MRR, revenue) MUST be computed from DB at query time (not cached).
- RPT-05: Export to CSV for all admin reports.

---

## 10. Definition of Done — revenue system V1

This checklist MUST be fully satisfied before the revenue system is launched.

### Plan enforcement test

- [ ] FREE tenant cannot create more than 3 properties. API returns 403 with clear message.
- [ ] FREE tenant cannot activate more than 3 listings. API returns 403.
- [ ] BASIC/PRO tenant can create unlimited properties and listings.
- [ ] MARKETPLACE_ONLY tenant cannot exceed 5 properties / 5 active listings.
- [ ] Plan limits are enforced at API level (not just UI).

### Grace period test

- [ ] Tenant's billing period expires without payment → status transitions to `PastDue`.
- [ ] During 7-day grace period: all features work normally. Admin portal shows warning.
- [ ] After grace period: tenant locked. `Status = 'Suspended'`. Mutations return 402.
- [ ] Suspended tenant pays invoice → `Status = 'Active'`. All features restored within minutes.

### Downgrade test

- [ ] PRO → BASIC: tenant must reduce listings below BASIC limits first (if applicable). Change takes effect at period end.
- [ ] BASIC → FREE: tenant must reduce to 3 properties / 3 keys / 1 OTA. Change at period end.
- [ ] Downgrade blocked if outstanding overdue invoice exists.
- [ ] Downgrade AuditLog entry created.

### Commission + subscription interaction test

- [ ] MARKETPLACE_ONLY tenant: no subscription invoice generated. Commission deducted per booking.
- [ ] BASIC tenant with MARKETPLACE_SPLIT: subscription invoice + commission auto-deducted. Both are correct and independent.
- [ ] BASIC tenant with HOST_DIRECT: subscription invoice + commission invoice (monthly). Both generated.
- [ ] Suspended tenant: existing MARKETPLACE_SPLIT settlements continue. No new bookings.

### Invoice accuracy test

- [ ] BASIC tenant with 5 active keys: invoice = 5 × ₹100 = ₹500.
- [ ] PRO tenant with 10 active keys: invoice = 10 × ₹200 = ₹2,000.
- [ ] FREE tenant: invoice = ₹0 (generated for audit).
- [ ] MARKETPLACE_ONLY tenant: invoice = ₹0.
- [ ] Prorated upgrade (BASIC → PRO mid-cycle): proration charge is correct.
- [ ] Zero-key tenant: invoice = ₹0.

### GST accuracy test

- [ ] GST rate = 18%: ₹500 base → ₹90 GST → ₹590 total.
- [ ] GST rate changed to 12% via config: new invoices use 12%. Old invoices retain 18%.
- [ ] GST disabled (`GstEnabled = false`): invoice has 0 tax.
- [ ] Atlas GST number appears on invoice.

### Payment retry test

- [ ] Invoice issued → payment link sent to tenant.
- [ ] Day 2 reminder sent.
- [ ] Day 5 final reminder sent.
- [ ] Day 7 (grace end): tenant locked. Status = `Suspended`.
- [ ] Tenant pays via payment link: invoice status → `Paid`. Tenant unlocked.
- [ ] Admin can manually mark invoice as paid (with AuditLog).

### Reconciliation test

- [ ] Monthly reconciliation: subscription payments match invoice totals.
- [ ] Monthly reconciliation: commission amounts match booking records.
- [ ] Active key count at invoice time matches `UsageSnapshot`.
- [ ] No unpaid invoices > 30 days old without alert.

### Credit system test

- [ ] New tenant receives 500 trial credits.
- [ ] Each booking deducts 1 credit on Trial/FREE.
- [ ] Credit exhaustion → tenant locked with `CreditsExhausted`.
- [ ] Admin credit adjustment works (positive and negative).
- [ ] Plan upgrade → trial credits expired via `Expire` ledger entry.
- [ ] Paid plan bookings do NOT deduct credits.

---

## Glossary

| Term | Definition |
|------|-----------|
| **Key** | A bookable listing unit (`Listing` with `Status = 'Active'`). The metered unit for per-key pricing. |
| **Per-key pricing** | Subscription cost = number of active keys × price per key per month. |
| **MRR** | Monthly Recurring Revenue. Subscription fees from all active paid tenants. |
| **ARPU** | Average Revenue Per User. Total revenue / active tenants. |
| **GMV** | Gross Merchandise Value. Total booking amounts transacted through the platform. |
| **Grace period** | 7-day window after billing period end during which the tenant can pay without losing access. |
| **BillingLockFilter** | Global action filter that returns 402 on mutations when tenant is billing-locked. |
| **Credit** | Unit of capacity. 1 credit = 1 booking. Used on FREE and Trial plans. |
| **Commission offset** | V2 mechanism where commission earned can reduce the subscription invoice. |
| **Proration** | Charging only for the remaining days in a billing cycle when upgrading mid-cycle. |
| **UsageSnapshot** | Monthly freeze of metered dimensions for billing accuracy and audit. |
| **LineItemsJson** | Structured breakdown of invoice calculation stored on the invoice for audit and display. |
