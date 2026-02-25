# Billing Domain

## Overview

AtlasHomestays uses a credit-based billing model with subscription tiers:

- New tenants receive free trial credits on onboarding.
- Credits are consumed per booking creation.
- When credits are exhausted or an invoice is overdue, the tenant enters **LOCKED** mode.
- Locked tenants can still **read** all their data (bookings, guests, reports) and access billing to pay.
- Paying an invoice or receiving credits unlocks the tenant.

## Subscription State Machine

```text
                    ┌──── CreditsExhausted ────┐
                    │                           ▼
  ┌──────┐     ┌───────┐     ┌──────────┐     ┌───────────┐
  │ Trial │────►│ Active │────►│ PastDue  │────►│ Suspended │
  └──────┘     └───────┘     └──────────┘     └───────────┘
                    ▲              │                 │
                    │              │                 │
                    └──── PayInvoice / AddCredits ───┘
                    
  Any state ──► Canceled (manual, no recovery except new subscription)
```

### States

| Status | Meaning | Writes Allowed? |
| --- | --- | --- |
| Trial | Free trial period, has credits | Yes |
| Active | Paid subscription with credits | Yes |
| PastDue | Period ended, within grace period (7 days default) | Yes (grace) |
| Suspended | Credits exhausted OR invoice overdue past grace | **No** (LOCKED) |
| Canceled | Manually canceled; no active plan | **No** (LOCKED) |

### Lock Triggers

| Trigger | LockReason | Resolution |
| --- | --- | --- |
| Credits reach 0 after booking | `CreditsExhausted` | Pay invoice / admin credits adjust |
| Invoice overdue past grace period | `InvoiceOverdue` | Pay invoice |
| Manual admin action | `Manual` | Admin unlock |
| Payment gateway failure | `ChargeFailed` | Retry payment |

## Credit Ledger

Append-only table. **Never update or delete rows.** Balance = `SUM(CreditsDelta)`.

### Ledger Entry Types

| Type | CreditsDelta | When |
| --- | --- | --- |
| Grant | +N | Onboarding (500), plan subscription, admin adjust |
| Debit | -1 | Each booking creation |
| Adjust | ±N | Manual admin adjustment |
| Expire | -N | Trial expiry job (future) |

### What Consumes Credits

| Action | Cost | Reference |
| --- | --- | --- |
| Booking creation | 1 credit | BookingId |
| (Future) WhatsApp notification | 1 credit | MessageId |
| (Future) SMS notification | 0.5 credit | MessageId |

## Invoice Lifecycle

```text
Draft → Issued → Paid
                 └──→ Overdue (past DueAtUtc + grace)
                 └──→ Void (canceled/reversed)
```

- GST rate: 18% (default for India SaaS services).
- Invoice generation: On subscription creation/renewal.
- Payment: Via Razorpay payment link or manual.

## LOCKED Mode Enforcement

### API Layer (BillingLockFilter)

Global `IAsyncActionFilter` in the MVC pipeline:

- **Blocks**: POST, PUT, PATCH, DELETE requests → returns HTTP 402 with body:

  ```json
  { "code": "TENANT_LOCKED", "reason": "CreditsExhausted", "balance": 0, "invoiceId": "...", "payUrl": "/billing/invoices/.../pay-link" }
  ```

- **Allows**: GET, HEAD, OPTIONS (read-only) always.
- **Exempt** (via attributes):
  - `[AllowWhenLocked]`: Billing endpoints (pay invoice, subscribe, view plans).
  - `[BillingExempt]`: Onboarding, platform-admin, health checks.

### Admin Portal Layer

- `BillingContext` fetches entitlements on app load.
- `LockBanner` component appears at top of every page when locked.
- 402 responses from API intercepted by Axios and surfaced as toast.
- Navigation restricted: only Billing, Reservations (read), Reports (read), Settings (view).

## Plans

| Code | Name | Monthly Price (INR) | Credits | Notes |
| --- | --- | --- | --- | --- |
| FREE_TRIAL | Free Trial | 0 | 500 | Auto-created on onboarding |
| STARTER | Starter | 999 | 200 | (Seed when needed) |
| GROWTH | Growth | 2499 | 1000 | (Seed when needed) |
| PRO | Pro | 4999 | 5000 | (Seed when needed) |

## Data Safety

- **No data is ever deleted** when a tenant is locked.
- All historical bookings, guests, invoices, and reports remain accessible (GET).
- Tenant `IsActive` flag is separate from billing lock (IsActive=false means tenant is fully deactivated by platform admin).
- Billing lock only prevents write operations; it does not affect data visibility.
