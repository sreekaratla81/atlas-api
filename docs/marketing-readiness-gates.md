# Marketing Readiness Gates — Atlas Homestays

**Purpose:** Before marketing launch, verify end-to-end coverage for "market-ready" flows. This document defines must-pass scenarios, test inventory, and a manual smoke checklist.

**Last updated:** 2026-02-19

---

## 1. Plan Tiers (Positioning)

| Plan | Features | Test Focus |
|------|----------|------------|
| **Free** | Property Management, Basic Reports, Manual Bookings, Atlas Subdomain | Core PMS, availability, manual booking |
| **Growth** | OTA Sync, Custom Website, Razorpay, WhatsApp/Email Alerts, GST Invoicing | Payments, notifications, GST, guest microsite |
| **Pro** | AI Dynamic Pricing, Revenue Analytics, Forecasting, AI Chat, Multi-Property | P2 / future |

---

## 2. Must-Pass Scenarios (Before Marketing)

### A) Free Plan — Core PMS

| # | Scenario | Preconditions | Steps | Expected | Test Type | Priority |
|---|----------|---------------|-------|----------|-----------|----------|
| F1 | Listing detail fetch uses `Listings.Id` | Property + listing seeded | Guest portal navigates to listing detail | `GET /listings/{listingId}` called with Listing.Id (not property id or unit slug); page loads | E2E | P0 |
| F2 | Pricing never 0 | Listing with base rate; optional daily override, global discount | Availability/breakdown API | daily override → global discount → base rate; never 0 | API | P0 |
| F3 | Availability: admin block → guest unavailable | Listing with availability | Admin blocks date | Guest availability excludes listing for that date | API + E2E | P0 |
| F4 | Availability: booking locks inventory | Listing available | Create booking for dates | Guest availability excludes listing for stay dates | API | P0 |
| F5 | Availability: cancel releases inventory | Booking exists | Cancel booking | Guest availability includes listing again | API | P0 |
| F6 | Manual booking flow | Tenant, property, listing | Admin: create guest → create booking linked to guest | Booking created, tenant-scoped | API | P0 |
| F7 | `/pricing/breakdown` matches UI display | Listing, check-in/out | Guest calls breakdown; UI displays | Amounts consistent | API | P0 |

### B) Growth Plan — Payments, Alerts, GST, Website

| # | Scenario | Preconditions | Steps | Expected | Test Type | Priority |
|---|----------|---------------|-------|----------|-----------|----------|
| G1 | Razorpay order amount computed server-side | Listing, pricing | POST Razorpay order with `BookingDraft` | Server uses `GetPublicBreakdownAsync`, ignores client `Amount` | API | P1 |
| G2 | Razorpay verify updates payment/booking state | Order created | Verify webhook | Payment completed, booking paid | API | P1 |
| G3 | Notifications: booking.created produces outbox | Booking created | Check outbox | Outbox message for booking.created | API | P1 |
| G4 | GST invoice: number series per tenant | Tenant, booking | Generate invoice | Invoice number unique per tenant | API | P1 |
| G5 | GST invoice: PDF downloadable | Invoice exists | Download PDF | PDF returned | API | P1 |
| G6 | Guest microsite: tenant page loads listings | Tenant configured | Guest visits tenant page | Listings + availability search work | E2E | P1 |

### C) Cross-Cutting Reliability

| # | Scenario | Preconditions | Steps | Expected | Test Type | Priority |
|---|----------|---------------|-------|----------|-----------|----------|
| R1 | Tenant/env safety banner | Admin portal loads | UI fetches `/ops/db-info` | If marker ≠ expected (e.g. PROD vs DEV), show warning banner | E2E / Manual | P0 |
| R2 | Tenant isolation via X-Tenant-Slug | Two tenants | Request with wrong tenant header | 404 or empty; no cross-tenant data | API | P0 |
| R3 | Pending EF migrations fail CI | Code change | Run CI | Integration tests fail if pending migrations | API | P0 |
| R4 | Key endpoints return expected schema | API running | GET health, db-info, listings | Status 200, required fields present | API | P0 |

---

## 3. Test Inventory (Current State)

### atlas-api

| File | What it validates |
|------|-------------------|
| `OpsApiTests.cs` | Health, db-info, outbox |
| `MigrationSafetyTests.cs` | No pending migrations |
| `GuestPortalContractTests.cs` | GET listings/{id} with X-Tenant-Slug, Razorpay order contract |
| `AvailabilityApiTests.cs` | Availability with blocks, pricing, daily override |
| `BookingsApiTests.cs` | CRUD, manual booking flow |
| `PricingSettingsAndQuotesApiTests.cs` | Pricing breakdown, global discount, quote cross-tenant |
| `PricingServiceTests.cs` | Daily override precedence |
| `AdminCalendarApiTests.cs` | Calendar PUT, inventory |
| `MarketingReadinessApiTests.cs` | Tenant isolation, pricing never 0, Razorpay server-side amount |

### atlas-e2e (Playwright)

| File | What it validates |
|------|-------------------|
| `smoke.e2e.spec.ts` | Admin/guest portals load |
| `availability.e2e.spec.ts` | Admin block → guest unavailable; price override → guest sees it |
| `admin-calendar-ui.e2e.spec.ts` | Admin calendar save flow |
| `listing-detail.e2e.spec.ts` | F1: Listing detail uses listingId, page loads |

### RatebotaiRepo

| File | What it validates |
|------|-------------------|
| `propertyDetailsRouteSmoke.test.tsx` | Property details route |
| `smokeBookingFlow.test.tsx` | Booking flow smoke |
| `listingResolver.test.ts` | resolveListing calls `/listings/{id}` with correct id |
| `scripts/smoke-preview-routes.mjs` | Preview routes smoke |

### Gaps (addressed in implementation)

- Tenant isolation: explicit X-Tenant-Slug test for listings
- Pricing never 0: explicit fallback chain test
- Razorpay server-side amount: test when BookingDraft provided
- E2E: Golden path 1 (admin → guest pricing/availability)
- E2E: Golden path 2 (admin booking → guest unavailable)
- E2E: Regression guard listing detail URL
- UI safety banner: admin portal reads db-info (P0 doc; implementation TBD)

---

## 4. 10-Minute Manual Smoke Checklist (P0 Only)

**Prerequisites:** API + Admin + Guest running; tenant with property + listing.

1. **Admin loads** — Open admin portal; redirects to auth or dashboard.
2. **Guest loads** — Open guest portal; homepage loads.
3. **Listing detail** — Navigate to a listing (use Listing.Id in URL, e.g. `/homes/.../2`); page loads, price > 0.
4. **Availability** — Search dates; listing appears with correct price (not 0).
5. **Admin block** — In admin calendar, set rooms = 0 for a date; guest search excludes that listing.
6. **Manual booking** — Admin: create guest, create booking; booking appears in list.
7. **Health** — `GET /health` returns `{"status":"healthy"}`.
8. **Db-info** — `GET /ops/db-info` returns `environment`, `database`, `marker`.

---

## 5. Commands

```powershell
# API tests (from atlas-api/)
cd atlas-api
dotnet build -c Release
dotnet test -c Release --no-build

# Playwright E2E (from atlas-e2e/)
# Start API + admin + guest first: ./atlas-e2e/scripts/start-servers.ps1
cd atlas-e2e
npm install
npm test

# Full marketing readiness gate (manual)
# 1. atlas-api: dotnet test
# 2. atlas-admin-portal: npm ci && npx vitest run
# 3. RatebotaiRepo: npm ci && npm test
# 4. atlas-e2e: start-servers.ps1, then npm test
# 5. Run 10-min manual smoke checklist (§4)
```

---

## 6. CI Gates

| Repo | Job | Gates |
|------|-----|-------|
| atlas-api | CI | Build, unit, integration (including migration safety) |
| atlas-admin-portal | CI | Lint, build, vitest |
| RatebotaiRepo | CI | Lint, build, test, smoke:preview |
| atlas-e2e | E2E Validate | npm ci, playwright install, test --list (full run requires manual start-servers.ps1) |
