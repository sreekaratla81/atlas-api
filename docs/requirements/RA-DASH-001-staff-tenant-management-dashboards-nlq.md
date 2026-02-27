# RA-DASH-001 — Staff, Tenant & Management Dashboard Requirements + NLQ Reporting Layer

| Field | Value |
|-------|-------|
| **ID** | RA-DASH-001 |
| **Title** | Staff, Tenant & Management Dashboards + NLQ Reporting |
| **Status** | Draft |
| **Author** | Chief Product Architect |
| **Created** | 2026-02-27 |
| **Dependencies** | RA-001 (Marketplace/Commission), RA-003 (Growth/Ranking), RA-004 (Trust/Fraud), RA-005 (Billing), RA-006 (Operational Excellence), RA-AI-001 (Pricing Intelligence), RA-DATA-001 (Data Platform) |
| **Stack** | Azure App Service · Azure SQL · React + Vite (Cloudflare Pages) |
| **Constraints** | Single developer · No external BI tool · Scale to 100k tenants · Must use L2 snapshot tables · Must not degrade OLTP · Secure tenant isolation |

---

## Table of Contents

1. [Role-Based Dashboard Architecture](#1-role-based-dashboard-architecture)
2. [Staff Dashboard (Operational Focus)](#2-staff-dashboard-operational-focus)
3. [Tenant (Host) Dashboard](#3-tenant-host-dashboard)
4. [Atlas Management Dashboard (Internal)](#4-atlas-management-dashboard-internal)
5. [NLQ (Natural Language Query) Reporting Interface](#5-nlq-natural-language-query-reporting-interface)
6. [NLQ Technical Guardrails](#6-nlq-technical-guardrails)
7. [Dashboard Performance Requirements](#7-dashboard-performance-requirements)
8. [Drill-Down & Export Requirements](#8-drill-down--export-requirements)
9. [Audit & Compliance for Reporting](#9-audit--compliance-for-reporting)
10. [Definition of Done — Dashboard Layer V1](#10-definition-of-done--dashboard-layer-v1)
11. [Phase Planning](#11-phase-planning)

---

## 1. Role-Based Dashboard Architecture

### 1.1 Dashboard layers

Atlas serves three distinct audiences, each with a dedicated dashboard experience. A fourth layer (NLQ) overlays the management dashboard in Phase 3.

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Layer 4: NLQ Reporting Interface (Phase 3)                               │
│   Chat-like query interface for Atlas Management.                        │
│   Reads from L2 snapshot tables + L3 analytics views.                    │
│   Read-only. Role-gated. Query-capped.                                   │
├──────────────────────────────────────────────────────────────────────────┤
│ Layer 3: Atlas Management Dashboard (Phase 2)                            │
│   Platform-wide metrics, city breakdowns, risk view.                     │
│   Reads from Snap_DailyMarketplacePerformance, Snap_DailyTenantPerf.    │
│   Route: /platform/*                                                     │
├──────────────────────────────────────────────────────────────────────────┤
│ Layer 2: Tenant (Host) Dashboard (Phase 1)                               │
│   Revenue, occupancy, commission, sync health, pricing suggestions.      │
│   Reads from Snap_DailyTenantPerformance, Snap_DailyPropertyPerf.       │
│   Route: /analytics, /dashboard, /reports, /payouts                      │
├──────────────────────────────────────────────────────────────────────────┤
│ Layer 1: Staff Dashboard (Phase 1)                                       │
│   Operational: check-ins, check-outs, payments, cleaning, alerts.        │
│   Reads from L1 OLTP (Bookings, Payments, AvailabilityBlocks).          │
│   Route: /dashboard (current), /reservations                             │
└──────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Role definitions

Roles extend the existing `User.Role` string field and the Auth0 role claims defined in RA-006 §1.2.

| Role | Auth0 claim | Dashboard access | Description |
|------|-------------|-----------------|-------------|
| **Staff** | `staff` | Staff Dashboard only | Front desk, housekeeping. Day-to-day operations. Cannot see revenue or commission. |
| **Property Manager** | `property_manager` | Staff + Tenant Dashboard (read-only financial) | Manages operations and can view (not edit) financial data. |
| **Tenant Owner** | `tenant_owner` | Staff + Tenant Dashboard (full) | Property owner. Full access to all tenant-scoped data. Can configure pricing, boost, sync. |
| **Atlas Admin** | `atlas_admin` | All dashboards (tenant-scoped + platform) | V1 catch-all admin role (RA-006 §1.2 ADM-05). |
| **Finance Admin** | `atlas_finance_admin` | Management Dashboard + financial reports | Settlement, refunds, invoice data. No tenant config changes. |
| **Super Admin** | `atlas_super_admin` | All dashboards + NLQ + all overrides | Full platform access. |

- ROLE-01: The `User.Role` field stores the primary role. V1: single role per user. V2: multi-role via a `UserRole` join table.
- ROLE-02: Auth0 role claims are the authoritative source for route-level gating. The `User.Role` field is synchronized on login.
- ROLE-03: API responses MUST NOT include data outside the role's permitted scope (defense in depth beyond route gating).

### 1.3 Route-level access control

| Route group | Path prefix | Allowed roles | Gate mechanism |
|---|---|---|---|
| Staff operational | `/dashboard`, `/reservations`, `/bookings`, `/calendar`, `/guests` | All authenticated roles | `<ProtectedRoute>` (existing) |
| Tenant financial | `/analytics`, `/reports`, `/payouts`, `/billing`, `/pricing-rules` | `tenant_owner`, `property_manager` (read-only), `atlas_admin`, `atlas_super_admin` | New `<RoleGate requiredRoles={[...]}>` component |
| Tenant config | `/channel-manager`, `/external-calendars`, `/promo-codes`, `/add-ons` | `tenant_owner`, `atlas_admin`, `atlas_super_admin` | `<RoleGate>` |
| Platform admin | `/platform/*` | `atlas_admin`, `atlas_finance_admin`, `atlas_super_admin` | `<RoleGate>` + `/platform` prefix |
| NLQ interface | `/platform/nlq` | `atlas_admin`, `atlas_super_admin` | `<RoleGate>` |

### 1.4 API-level access control

| API prefix | Allowed roles | Tenant scope | Gate mechanism |
|---|---|---|---|
| `/api/bookings`, `/api/listings`, `/api/properties` | All authenticated | Current tenant (EF Core filter) | JWT + tenant context |
| `/api/analytics`, `/api/reports` | `tenant_owner`, `property_manager`, `atlas_admin` | Current tenant | JWT + role check + tenant filter |
| `/admin/reports/*` | `tenant_owner`, `atlas_admin` | Current tenant | JWT + role check |
| `/api/platform/*` | `atlas_admin`, `atlas_finance_admin`, `atlas_super_admin` | Cross-tenant (`IgnoreQueryFilters`) | JWT + `atlas_*` role claim |
| `/api/platform/nlq` | `atlas_admin`, `atlas_super_admin` | Cross-tenant (read-only) | JWT + role claim + query guardrails |

- API-01: Every API endpoint MUST validate the JWT role claim server-side. Client-side route gating is UX only — never a security boundary.
- API-02: Platform endpoints MUST use `IgnoreQueryFilters()` and include explicit `TenantId` filtering where needed (RA-006 ADM-01).
- API-03: All platform endpoint calls MUST emit `platform.admin.query` structured log (RA-006 ADM-03).

---

## 2. Staff Dashboard (Operational Focus)

The staff dashboard is the primary landing page for operational personnel. It extends the existing `Dashboard.tsx` (which currently shows Today's Check-ins/Check-outs/Upcoming Bookings/Leads with onboarding progress).

### 2.1 Today View

The "Today View" is the default tab, providing a single-screen operational snapshot.

#### 2.1.1 Check-ins today

| Field | Source | Display |
|-------|--------|---------|
| Guest name | `Booking.Guest.Name` | Text |
| Listing name | `Booking.Listing.Name` | Text |
| Check-in date | `Booking.CheckinDate` | Date (today highlighted) |
| Check-out date | `Booking.CheckoutDate` | Date |
| Nights | Computed | Number |
| Booking status | `Booking.BookingStatus` | Badge (Confirmed = blue, CheckedIn = green) |
| Payment status | Derived from `Payment` rows | Badge (Paid = green, Partial = amber, Unpaid = red) |
| Source | `Booking.BookingSource` | Badge (Walk-in, Airbnb, Booking.com, Marketplace) |
| Action | — | "Check In" button (if Confirmed) |

**Data source**: `GET /api/bookings?checkinDate={today}&status=Confirmed,CheckedIn`

**Update frequency**: Real-time (live L1 query on page load + 60-second auto-refresh).

#### 2.1.2 Check-outs today

| Field | Source | Display |
|-------|--------|---------|
| Guest name | `Booking.Guest.Name` | Text |
| Listing name | `Booking.Listing.Name` | Text |
| Check-out date | `Booking.CheckoutDate` | Date |
| Outstanding balance | `Booking.FinalAmount - SUM(Payment.Amount WHERE Status='completed')` | ₹ amount (red if > 0) |
| Action | — | "Check Out" button (if CheckedIn) |

**Data source**: `GET /api/bookings?checkoutDate={today}&status=CheckedIn`

#### 2.1.3 In-house guests

| Field | Source | Display |
|-------|--------|---------|
| Guest name | `Booking.Guest.Name` | Text |
| Listing/room | `Booking.Listing.Name` | Text |
| Check-in | `Booking.CheckinDate` | Date |
| Check-out | `Booking.CheckoutDate` | Date |
| Nights remaining | `CheckoutDate - today` | Number |
| Special notes | `Booking.Notes` | Text (truncated) |

**Data source**: `GET /api/bookings?status=CheckedIn`

**Count shown as KPI card**: "X guests in-house"

#### 2.1.4 Pending payments

| Field | Source | Display |
|-------|--------|---------|
| Guest name | `Booking.Guest.Name` | Text |
| Amount due | `FinalAmount - AmountReceived` | ₹ amount |
| Due since | `Booking.CheckinDate` | Date |
| Action | — | "Record Payment" button |

**Data source**: `GET /api/bookings?paymentStatus=pending`

- STF-01: Pending payment count is shown as a red KPI card on the Today View header.

#### 2.1.5 Cleaning schedule

| Field | Source | Display |
|-------|--------|---------|
| Listing name | `Listing.Name` | Text |
| Status | Derived: "Needs cleaning" if check-out today + no check-in after, or "Ready" | Badge |
| Next guest check-in | Next `Booking.CheckinDate` for this listing | Date or "None" |
| Turnaround window | Hours between today's check-out and next check-in | Hours (red if < 4h) |

**Data source**: Computed from bookings with `checkoutDate = today` joined with next booking for same listing.

- STF-02: V1: Cleaning schedule is derived from booking data. V2: dedicated `HousekeepingTask` entity.
- STF-03: Listings with same-day turnover (check-out + check-in on same day) MUST be highlighted with a warning icon.

#### 2.1.6 Maintenance tickets

- STF-04: V1: Maintenance tracking uses the existing `Incident` table (`AppDbContext.Incidents`).
- STF-05: Today View shows open incidents for the tenant's properties.
- STF-06: V2: Dedicated `MaintenanceTicket` entity with assignment, priority, and status workflow.

### 2.2 Booking list

Extends the existing Reservations page with operational filters.

| Filter | Type | Options |
|--------|------|---------|
| Date range | Date picker | Check-in date range (default: today ± 7 days) |
| Status | Multi-select | Lead, Confirmed, CheckedIn, CheckedOut, Cancelled |
| Payment status | Multi-select | Paid, Partial, Unpaid |
| Source | Multi-select | Walk-in, Airbnb, Booking.com, Marketplace, Other |
| Listing | Multi-select | All active listings |
| Search | Text | Guest name, phone, booking ID |

- STF-07: Quick status update: staff can change booking status inline (Confirmed → CheckedIn → CheckedOut) with a single click + confirmation dialog.
- STF-08: Payment status badge updates in real-time when a payment is recorded.

### 2.3 Alerts panel

A persistent alert strip at the top of the staff dashboard.

| Alert type | Trigger | Severity | Display |
|---|---|---|---|
| **Overbooking** | Two confirmed bookings overlap for the same listing/date | Critical (red) | "Overbooking detected: {listing} on {date}. {count} overlapping bookings." |
| **Sync issue** | `ChannelConfig.LastSyncAt` > 6 hours ago OR `LastSyncError` is not null | Warning (amber) | "Sync issue: {listing} has not synced since {time}." |
| **Payment pending** | Booking with `CheckinDate <= today` and `AmountReceived < FinalAmount` | Warning (amber) | "{count} bookings with pending payments totaling ₹{amount}." |
| **Expiring block** | `AvailabilityBlock` with `EndDate = today` and `BlockType = 'Manual'` | Info (blue) | "Manual block on {listing} expires today." |
| **Low inventory** | `ListingDailyInventory.RoomsAvailable = 0` for any date within next 7 days | Info (blue) | "{listing} is fully booked for {date}." |

**Data source**: Alerts computed server-side via `GET /api/dashboard/alerts` — returns alert objects with type, severity, message, and entity references.

**Update frequency**: 5-minute poll.

- STF-09: Alerts MUST be dismissible per user per session (dismissed state stored in `localStorage`, not server).
- STF-10: Critical alerts MUST NOT be auto-dismissed and persist until resolved.
- STF-11: Alert counts shown as badge on the dashboard nav item.

### 2.4 Permissions per role

| Component | Staff | Property Manager | Tenant Owner | Atlas Admin |
|---|:---:|:---:|:---:|:---:|
| Today View | ✓ | ✓ | ✓ | ✓ |
| Check-in/out actions | ✓ | ✓ | ✓ | ✓ |
| Record payment | ✓ | ✓ | ✓ | ✓ |
| Booking list | ✓ | ✓ | ✓ | ✓ |
| Change booking status | ✓ | ✓ | ✓ | ✓ |
| View amounts/revenue | — | Read-only | ✓ | ✓ |
| View commission | — | — | ✓ | ✓ |
| Cleaning schedule | ✓ | ✓ | ✓ | ✓ |
| Alerts | ✓ | ✓ | ✓ | ✓ |

- STF-12: Staff role sees "Amount" columns as `***` (masked) unless viewing their own property's data is permitted by the tenant owner (V2 toggle: `Tenant.StaffCanViewRevenue`, default `false`).

---

## 3. Tenant (Host) Dashboard

The tenant dashboard is the owner's command center. It consolidates revenue, operational health, and marketplace performance into a single view. Data is sourced primarily from L2 snapshot tables (RA-DATA-001).

### 3.1 Revenue overview

| Metric | Formula | Data source | Period | Display |
|--------|---------|-------------|--------|---------|
| **This month revenue** | `SUM(Booking.FinalAmount)` for check-ins in current month | L1 live query (current month = real-time) | Current month | ₹ total + trend arrow vs last month |
| **Last month revenue** | Same, previous month | `Snap_DailyTenantPerformance` | Previous month | ₹ total |
| **Occupancy %** | `NightsSold / NightsAvailable * 100` | `Snap_DailyPropertyPerformance` aggregated | Selectable: 7d / 30d / 90d | Percentage + sparkline |
| **ADR** | `Revenue / NightsSold` | Same | Same | ₹ amount |
| **RevPAR** | `Revenue / NightsAvailable` | Same | Same | ₹ amount |
| **Direct booking %** | `MarketplaceBookings / TotalBookings * 100` | `Snap_DailyRevenueMetrics` | Same | Percentage |
| **OTA dependency %** | `100 - DirectBookingPercent` | Derived | Same | Percentage |

- TEN-01: Revenue for the current month MUST use real-time L1 data (not stale snapshots). Past months use L2.
- TEN-02: All monetary values display in INR with Indian number formatting (client-side).
- TEN-03: Traffic-light indicators (RA-AI-001 §6.2) MUST be applied to occupancy, ADR, RevPAR, and direct booking %.

### 3.2 Commission overview

| Metric | Formula | Data source | Display |
|--------|---------|-------------|---------|
| **Commission paid** | `SUM(Booking.CommissionAmount)` for marketplace bookings | `Snap_DailyTenantPerformance.CommissionPaid` | ₹ total |
| **Boost spend** | Commission above floor (premium portion) | Computed: `CommissionPaid - (BookingCount * FloorPercent * AvgAmount / 100)` | ₹ total or "N/A" |
| **Net payout** | `Revenue - CommissionPaid` | Derived | ₹ total |
| **Effective commission rate** | `AVG(CommissionPercentSnapshot)` | `Booking` aggregate | Percentage |
| **Commission trend** | Month-over-month | `Snap_DailyTenantPerformance` | Line chart (6 months) |

- TEN-04: Commission data is only shown if the tenant has marketplace-enabled properties. Otherwise: "Enable marketplace to see commission data."
- TEN-05: "Boost spend" shows "N/A" if `DefaultCommissionPercent = FloorPercent`.

### 3.3 Operational health

| Metric | Source | Display |
|--------|--------|---------|
| **Sync health** | `ChannelConfig.IsConnected`, `LastSyncAt`, `LastSyncError` per property | Traffic light: Green (synced < 1h), Amber (1–6h), Red (> 6h or error) |
| **TrustScore** | `PropertyTrustScoreCache.TrustScore` per property | Score (0–100 display) + badge (Trusted Host / Moderate / At Risk) |
| **Booking trend** | `Snap_DailyPropertyPerformance.BookingCount` | Line chart (30 days, per property or aggregate) |
| **Cancellation rate** | `Cancellations / TotalBookings` trailing 90 days | Percentage + trend |
| **Response time** | Median response hours (from `CommunicationLog` timing) | Badge (< 1h = Excellent, 1–4h = Good, > 4h = Needs Improvement) |

- TEN-06: Properties with TrustScore < 0.40 MUST show an "Action Required" banner with links to improvement recommendations.
- TEN-07: Sync health per property is shown as a row in a property health table, not a single aggregate.

### 3.4 Price suggestion panel

Integrated from RA-AI-001 §6.3. The tenant dashboard includes the suggestion inbox.

| Element | Description |
|---------|-------------|
| **Pending count** | Badge on dashboard nav: "{N} pricing suggestions" |
| **Suggestion cards** | Listing, date range, current → suggested rate, % change, reason, expiry countdown |
| **Accept / Reject** | Per-suggestion actions |
| **Accept all** | Bulk action with confirmation |
| **Auto-apply toggle** | Per-listing toggle (PRO plan only) |
| **Suggestion history** | Expandable table: past suggestions with outcome |

- TEN-08: Price suggestion panel is only visible when `PricingIntelligence:Enabled = true` AND tenant plan is BASIC or higher.

### 3.5 Marketplace performance

| Metric | Source | Display |
|--------|--------|---------|
| **Boost performance** | Commission boost component from ranking | Score bar (0–1) per property |
| **Ranking position estimate** | Not an exact rank; computed as percentile of `FinalRankingScore` within the property's city | "Top X%" or "Above average" / "Below average" |
| **Marketplace views** | `PropertyViewDaily` (RA-003 §8.2) | Number + trend |
| **Conversion rate** | `Bookings / Views` | Percentage |

- TEN-09: Ranking position is an **estimate**, not an exact position. Display: "Your property ranks in the top {X}% in {city}."
- TEN-10: V1: If `PropertyViewDaily` is not yet populated, marketplace views show "Coming soon."

### 3.6 Data aggregation source mapping

| Dashboard section | Primary data source | Fallback (if snapshot missing) | Staleness tolerance |
|---|---|---|---|
| Revenue (current month) | L1 OLTP live query | N/A (always live) | Real-time |
| Revenue (past months) | `Snap_DailyTenantPerformance` | L1 query (slow) | 24 hours |
| Occupancy / ADR / RevPAR | `Snap_DailyPropertyPerformance` | L1 query | 24 hours |
| Commission | `Snap_DailyTenantPerformance` | L1 query | 24 hours |
| Revenue by source | `Snap_DailyRevenueMetrics` | L1 query | 24 hours |
| TrustScore | `PropertyTrustScoreCache` | L1 full recompute | 15 minutes |
| Sync health | L1 `ChannelConfig` | N/A | Real-time |
| Suggestions | L1 `PriceSuggestion` | N/A | Real-time |
| Booking trend | `Snap_DailyPropertyPerformance` | L1 query | 24 hours |

### 3.7 Drill-down capabilities

| Metric | Drill-down | Target |
|--------|-----------|--------|
| Revenue | Click → Revenue by listing | Table: listing, revenue, ADR, occupancy |
| Occupancy | Click → Occupancy heatmap | Calendar grid (RA-DATA-001 §7.1.2) |
| ADR | Click → ADR by date | Line chart with daily granularity |
| Commission | Click → Commission by booking | Table: booking ID, amount, commission %, source |
| Sync health | Click → Sync detail | `ChannelConfig` detail: last sync, errors, connection status |
| TrustScore | Click → Score breakdown | Radar chart: 6 components |

### 3.8 Export requirements

- TEN-11: All tabular data on the tenant dashboard MUST have a "Download CSV" button.
- TEN-12: CSV exports include: column headers, all visible rows, date range in filename.
- TEN-13: Maximum export size: 50,000 rows. If exceeded: "Please narrow the date range."
- TEN-14: Export action logged in `AuditLog`: `tenant.report.exported` with `{reportType, dateRange, rowCount}`.

---

## 4. Atlas Management Dashboard (Internal)

The management dashboard serves Atlas leadership with platform-wide metrics. Accessible only to `atlas_admin`, `atlas_finance_admin`, and `atlas_super_admin` roles.

**Route prefix**: `/platform/dashboard`

### 4.1 Platform health

#### 4.1.1 KPI cards (top row)

| Metric | Formula | Data source | Refresh |
|--------|---------|-------------|---------|
| **Total GMV** | `SUM(Snap_DailyMarketplacePerformance.TotalGmv)` for period | L2 | Daily |
| **Commission revenue** | `SUM(Snap_DailyMarketplacePerformance.CommissionRevenue)` | L2 | Daily |
| **Subscription revenue** | `SUM(BillingPayment.AmountInr WHERE Status='Completed')` | L1 (lightweight) | Real-time |
| **Total platform revenue** | Commission + Subscription | Derived | Daily |
| **Active tenants** | `COUNT(DISTINCT TenantId)` where booking in last 30d | `Snap_DailyMarketplacePerformance.ActiveTenants` | Daily |
| **Marketplace adoption %** | Tenants with ≥ 1 `IsMarketplaceEnabled` property / Total tenants | Computed | Daily |
| **Boost adoption %** | Tenants with `CommissionPercent > FloorPercent` / Marketplace tenants | Computed | Daily |
| **Sync failure %** | Failed sync events / Total sync events (24h) | `OutboxMessage` with sync topic | Near-real-time |
| **Settlement failure %** | Failed settlements / Total settlements (30d) | `Payment` status | Daily |

- MGT-01: Each KPI card shows: current value, trend arrow (vs previous period), and percentage change.
- MGT-02: Period selector: 7d, 30d, 90d, YTD, custom range.

#### 4.1.2 GMV trend chart

| Chart | Data | Type |
|-------|------|------|
| Daily GMV | `Snap_DailyMarketplacePerformance.TotalGmv` | Line chart (default 30d) |
| Monthly GMV growth | Month-over-month | Bar chart (12 months) |
| GMV by source | `Snap_DailyRevenueMetrics` aggregated platform-wide | Stacked area |

#### 4.1.3 Revenue mix chart

| Segment | Source | Display |
|---------|--------|---------|
| Subscription revenue | `BillingPayment` | Donut slice |
| Commission revenue (base) | Floor commission portion | Donut slice |
| Boost revenue (premium) | Above-floor commission | Donut slice |

### 4.2 City breakdown

| Metric | Granularity | Data source | Display |
|--------|-------------|-------------|---------|
| **GMV per city** | `Property.City` | `Snap_DailyPropertyPerformance` joined with `Property.City` | Horizontal bar chart (top 15 cities) |
| **Occupancy per city** | `Property.City` | Same | Horizontal bar |
| **Active tenants per city** | `Property.City` | Same | Horizontal bar |
| **Top performing tenants** | By RevPAR | `Snap_DailyTenantPerformance` | Table (top 20) |
| **Underperforming clusters** | Cities with avg occupancy < 30% | Computed | Table with alert icon |

- MGT-03: City breakdown MUST be filterable by date range.
- MGT-04: "Top performing tenants" shows: tenant name, city, RevPAR, occupancy %, GMV, commission rate. Clickable → tenant detail.
- MGT-05: "Underperforming clusters" triggers investigation. Admin can click → see all properties in that city.

### 4.3 Risk view

| Indicator | Threshold | Data source | Display |
|-----------|-----------|-------------|---------|
| **Chargeback %** | > 1% of bookings in 30 days | `Payment` disputes | Percentage + trend |
| **Refund %** | > 5% of bookings in 30 days | `Payment` with refund status | Percentage + trend |
| **Fraud alerts** | Active fraud signals (RA-004) | `AuditLog` with `fraud.signal.*` | Count + list |
| **Suspended tenants** | `TenantSubscription.Status = 'Suspended'` | L1 query | Count + list |
| **TrustScore drops** | Properties with > 0.15 single-day drop | `Snap_DailyTrustScore` delta | Count + list |
| **Commission oscillation** | Tenants with > 3 changes in 7 days | `AuditLog` | Count + list |
| **Settlement backlogs** | Pending settlements > 48h | `Payment` | Count + ₹ amount |

- MGT-06: Risk view is always visible regardless of period filter (uses trailing 30-day window).
- MGT-07: Each risk item is clickable → drills into the specific tenant/property/booking.

### 4.4 Required summary tables

The management dashboard relies on the following L2 tables (defined in RA-DATA-001 §3.1):

| Table | Used for |
|-------|----------|
| `Snap_DailyMarketplacePerformance` | Platform KPIs, GMV trend, revenue mix |
| `Snap_DailyTenantPerformance` | Top tenants, tenant drill-down |
| `Snap_DailyPropertyPerformance` | City breakdown, property drill-down |
| `Snap_DailyRevenueMetrics` | Revenue by source (platform-wide aggregation) |
| `Snap_DailyTrustScore` | Risk view: trust score drops |
| `PropertyTrustScoreCache` | Live trust scores |

- MGT-08: Management dashboard MUST NEVER query L1 OLTP tables for historical aggregations. Only L2 snapshots.
- MGT-09: Exception: real-time counters (pending settlements, suspended tenants) use lightweight L1 queries with covering indexes.

### 4.5 Drill-down and export

- MGT-10: Every chart and table on the management dashboard MUST support drill-down to the underlying data.
- MGT-11: Drill-down hierarchy: Platform → City → Tenant → Property → Listing → Booking.
- MGT-12: CSV export available for all tables. Export logged: `platform.data.exported`.
- MGT-13: Data freshness timestamp shown on every section: "Data as of {ComputedAtUtc}".

---

## 5. NLQ (Natural Language Query) Reporting Interface

### 5.1 Vision

The NLQ interface provides a ChatGPT-like experience for Atlas management to query platform data using natural language. It is an **overlay** on the Management Dashboard, not a replacement.

**Target users**: Atlas Admin, Super Admin.

**Phase**: Phase 3 (after Management Dashboard is stable).

### 5.2 Example queries

| Category | Example query | Expected output |
|----------|--------------|-----------------|
| Revenue | "Show revenue for Hyderabad in last 30 days" | Table: date, revenue, booking count + total row. Line chart. |
| Occupancy | "Top 10 properties by occupancy" | Table: property, tenant, city, occupancy %, ADR. |
| Trust | "Which tenants have lowest TrustScore?" | Table: tenant, property, TrustScore, components. Bottom 20. |
| Commission | "Compare commission revenue month over month" | Table: month, commission revenue, MoM %. Bar chart. |
| Forecast | "Which properties have >80% occupancy next weekend?" | Table: property, tenant, city, occupancy %, available rooms. |
| Sync | "Show sync failures in last 24 hours" | Table: tenant, property, provider, last error, last sync time. |
| Billing | "How many tenants are on PRO plan?" | Single number + breakdown by plan. |
| Anomaly | "Which properties had a TrustScore drop this week?" | Table: property, old score, new score, delta. |

### 5.3 Architecture

```
┌───────────┐    ┌──────────────┐    ┌──────────────────┐    ┌──────────────┐
│  User     │───→│  NLQ Parser  │───→│  Query Planner   │───→│  Read-only   │
│  types    │    │  (LLM-based) │    │  (template       │    │  Query       │
│  query    │    │              │    │   matching +      │    │  Executor    │
│           │    │              │    │   parameterization│    │  (L2/L3)     │
└───────────┘    └──────────────┘    └──────────────────┘    └──────┬───────┘
                                                                    │
                                                          ┌────────▼────────┐
                                                          │  Response       │
                                                          │  Formatter      │
                                                          │  (table, chart, │
                                                          │   summary text) │
                                                          └─────────────────┘
```

#### Step 1: User input

- NLQ-01: User types a natural language query in a chat-style text input.
- NLQ-02: System shows recent query history and suggested queries as quick-start buttons.

#### Step 2: NLQ → Structured query template

- NLQ-03: The NLQ parser uses an LLM (Azure OpenAI GPT-4o) to classify the query into a predefined **query domain** and extract parameters.
- NLQ-04: The parser does NOT generate raw SQL. It maps to a **query template** from a fixed library.

**Query template library (V1: 15 predefined templates)**:

| Template ID | Domain | Template | Parameters |
|---|---|---|---|
| `QT_REVENUE_BY_CITY` | Revenue | `SELECT City, SUM(Revenue) ... GROUP BY City WHERE Date BETWEEN @start AND @end` | `city`, `startDate`, `endDate` |
| `QT_REVENUE_TREND` | Revenue | Revenue by month | `startDate`, `endDate`, `granularity` |
| `QT_TOP_PROPERTIES_BY_OCCUPANCY` | Occupancy | Top N by occupancy | `limit`, `startDate`, `endDate` |
| `QT_TOP_PROPERTIES_BY_REVPAR` | Revenue | Top N by RevPAR | `limit`, `startDate`, `endDate` |
| `QT_BOTTOM_TRUSTSCORE` | Trust | Bottom N by TrustScore | `limit` |
| `QT_TRUSTSCORE_DROPS` | Trust | Properties with significant TrustScore drops | `deltaThreshold`, `period` |
| `QT_COMMISSION_MOM` | Commission | Commission revenue month over month | `months` |
| `QT_OCCUPANCY_FORECAST` | Occupancy | Forward occupancy for date range | `startDate`, `endDate`, `threshold` |
| `QT_SYNC_FAILURES` | Sync | Recent sync failures | `hours` |
| `QT_PLAN_DISTRIBUTION` | Billing | Tenant count by plan | (none) |
| `QT_TENANT_REVENUE` | Revenue | Revenue for specific tenant | `tenantId`, `startDate`, `endDate` |
| `QT_CITY_COMPARISON` | Revenue | Compare metrics across cities | `cities[]`, `startDate`, `endDate` |
| `QT_BOOKING_SOURCE_MIX` | Bookings | Booking count/revenue by source | `startDate`, `endDate` |
| `QT_SETTLEMENT_STATUS` | Billing | Settlement success/failure summary | `startDate`, `endDate` |
| `QT_DEMAND_SIGNALS` | Pricing | Active demand signals summary | `signalType`, `severity` |

- NLQ-05: If the parser cannot map the query to a known template, it returns: "I can't answer that query yet. Try one of these: {suggested queries}."
- NLQ-06: V2: Expand template library based on actual query logs. Add dynamic SQL generation with validation.

#### Step 3: Query execution

- NLQ-07: The query executor runs the parameterized template against **L2 snapshot tables and L3 views only**. NEVER against L1 OLTP.
- NLQ-08: Query execution has a hard timeout of 10 seconds.
- NLQ-09: Results are capped at 1,000 rows.

#### Step 4: Response formatting

- NLQ-10: The response includes up to three components:
  - **Summary text**: One-sentence natural language answer (LLM-generated from result data).
  - **Table**: Structured data table (if result has > 1 row).
  - **Chart**: Auto-selected chart type based on query domain (line for trends, bar for comparisons, table for lists).
- NLQ-11: Chart type selection rules:

| Domain | Default chart | Condition |
|--------|--------------|-----------|
| Revenue trend | Line chart | Time series |
| City comparison | Horizontal bar | Categorical |
| Top/Bottom N | Table | Ranked list |
| Distribution | Donut/pie | Parts of whole |
| Forecast | Bar chart | Forward-looking dates |

### 5.4 Allowed query domains

| Domain | Tables accessible | Scope |
|--------|------------------|-------|
| Bookings | `Snap_DailyPropertyPerformance`, `Snap_DailyTenantPerformance` | Platform-wide |
| Revenue | `Snap_DailyTenantPerformance`, `Snap_DailyRevenueMetrics`, `Snap_DailyMarketplacePerformance` | Platform-wide |
| Occupancy | `Snap_DailyPropertyPerformance` | Platform-wide |
| Commission | `Snap_DailyTenantPerformance`, `Snap_DailyMarketplacePerformance` | Platform-wide |
| Trust | `Snap_DailyTrustScore`, `PropertyTrustScoreCache` | Platform-wide |
| Sync | `ChannelConfig`, `OutboxMessage` (sync events only) | Platform-wide |
| Billing | `TenantSubscription`, `BillingInvoice`, `BillingPayment` | Platform-wide |
| Pricing | `Snap_DailyDemandSignals`, `PriceSuggestion` (aggregate) | Platform-wide |

- NLQ-12: Queries outside allowed domains are rejected with a friendly message.
- NLQ-13: No query may access: `User`, `PasswordHash`, `ApiKey`, `BankAccount.AccountNumber`, or any PII field.

---

## 6. NLQ Technical Guardrails

### 6.1 Read-only execution

- GRD-01: The NLQ query executor MUST use a **read-only database connection** (SQL Server user with `db_datareader` role only, no `db_datawriter`).
- GRD-02: The connection string is separate from the API connection string: `NlqConnectionString` in configuration.
- GRD-03: No `INSERT`, `UPDATE`, `DELETE`, `DROP`, `TRUNCATE`, `ALTER`, `CREATE`, or `EXEC` statements are permitted. The query executor validates the generated SQL before execution.

### 6.2 Parameterized query generation

- GRD-04: All query templates use parameterized SQL (`@param` placeholders). No string concatenation of user input into SQL.
- GRD-05: Parameter values are extracted by the LLM parser, validated against type constraints, then bound to `SqlParameter` objects.
- GRD-06: Parameter validation rules:

| Parameter type | Validation |
|----------------|------------|
| `startDate`, `endDate` | Must be valid date. Max range: 365 days. `startDate <= endDate`. |
| `limit` | Integer 1–100. Default 10. |
| `tenantId` | Positive integer. Must exist in `Tenants` table. |
| `city` | Non-empty string. Max 100 chars. Sanitized. |
| `hours` | Integer 1–720 (30 days max). |
| `threshold` | Decimal 0.00–1.00. |
| `months` | Integer 1–24. |

### 6.3 SQL injection prevention

- GRD-07: The NLQ system NEVER sends user text directly to SQL. The flow is: `User text → LLM → Template ID + Parameters → Parameterized SQL → Execution`.
- GRD-08: Even if the LLM were compromised, the template library is a fixed set of SQL strings compiled into the application. The LLM can only select from them.
- GRD-09: V2 (dynamic SQL): If dynamic SQL generation is introduced, it MUST pass through a SQL AST parser that rejects any statement type other than `SELECT`.

### 6.4 Row-level tenant isolation

- GRD-10: NLQ queries operate on **platform-wide** L2 snapshot data (not tenant-scoped). This is acceptable because NLQ is restricted to `atlas_admin` and `atlas_super_admin` roles.
- GRD-11: If NLQ is ever extended to tenant users, queries MUST include `WHERE TenantId = @callerTenantId` injected by the query executor (not the LLM).
- GRD-12: L1 table queries (sync health, billing) MUST use `IgnoreQueryFilters()` with explicit role verification.

### 6.5 Result size cap

- GRD-13: Maximum result rows: 1,000. If the query returns more: "Showing top 1,000 results. Please narrow your query."
- GRD-14: Maximum response payload: 5 MB. If exceeded: truncate and indicate truncation.

### 6.6 Query complexity cap

- GRD-15: Query templates MUST NOT contain more than 3 JOINs.
- GRD-16: Query templates MUST NOT use subqueries deeper than 1 level.
- GRD-17: All queries MUST have an execution plan cost estimate < 100 (SQL Server estimated cost). If exceeded at template registration time, the template is rejected.
- GRD-18: No `CROSS JOIN` or `CROSS APPLY` in templates.

### 6.7 Logging and monitoring

- GRD-19: Every NLQ query is logged:

| Log field | Value |
|-----------|-------|
| `nlq.query.submitted` | `{userId, userRole, rawQuery, timestamp}` |
| `nlq.query.parsed` | `{templateId, parameters, parseConfidence, llmModel}` |
| `nlq.query.executed` | `{templateId, parameters, rowsReturned, executionMs}` |
| `nlq.query.failed` | `{templateId, error, rawQuery}` |
| `nlq.query.rejected` | `{rawQuery, reason}` (unrecognized domain, parameter validation, etc.) |

- GRD-20: All logs include `userId` for accountability.

### 6.8 Rate limiting and abuse prevention

| Control | Limit | Per |
|---------|-------|-----|
| Queries per minute | 10 | User |
| Queries per hour | 100 | User |
| Queries per day | 500 | User |
| Concurrent queries | 2 | User |

- GRD-21: Rate limits are enforced server-side. Excess queries return 429 Too Many Requests.
- GRD-22: Repeated unrecognized queries (> 10 rejections in 1 hour) trigger a soft lock: user sees "Please try predefined queries or contact support."
- GRD-23: Azure OpenAI token usage is tracked per user per day. Hard cap: 50,000 tokens/user/day.

### 6.9 Explain plan storage

- GRD-24: V2: The execution plan for each query template is stored in a `NlqQueryPlan` table for performance regression detection.
- GRD-25: V1: Execution plans are logged as structured log events but not stored in a table.

---

## 7. Dashboard Performance Requirements

### 7.1 Page load SLA

| Dashboard | Target (P95) | Measurement |
|-----------|:------------:|-------------|
| Staff dashboard (Today View) | < 1.5 seconds | First Contentful Paint |
| Tenant dashboard (Revenue Overview) | < 2.0 seconds | Largest Contentful Paint |
| Management dashboard (Platform Health) | < 2.5 seconds | Largest Contentful Paint |
| NLQ query response | < 5.0 seconds | Time from submit to first result |

### 7.2 Chart rendering SLA

| Chart type | Target | Notes |
|------------|:------:|-------|
| Line chart (12 months, single series) | < 200ms | Client-side rendering from API data |
| Bar chart (top 15 cities) | < 200ms | Same |
| Heatmap (31 days × 50 listings) | < 500ms | Client-side rendering |
| Donut chart | < 100ms | Small data set |

- PERF-01: Chart libraries MUST be lazy-loaded (dynamic import) to avoid blocking initial page render.

### 7.3 Snapshot table usage rules

- PERF-02: Dashboard API endpoints MUST use L2 snapshot tables for any aggregation spanning > 7 days.
- PERF-03: L1 OLTP queries are permitted ONLY for:
  - Current-day data (today's check-ins, check-outs, in-house guests)
  - Real-time state queries (sync status, pending payments, active bookings)
  - Single-entity lookups (booking detail, property detail)
- PERF-04: Dashboard API endpoints MUST NOT use `COUNT(*)` on L1 tables without a `WHERE` clause that leverages an index.

### 7.4 Caching strategy

| Data | Cache location | TTL | Invalidation |
|------|:-------------:|:---:|-------------|
| Staff Today View data | No cache (real-time L1) | — | Auto-refresh every 60s |
| Tenant Revenue (current month) | React Query cache | 5 min | On booking event |
| Tenant Revenue (past months) | React Query cache | 1 hour | On snapshot refresh |
| Management KPI cards | Server-side in-memory cache | 15 min | On snapshot refresh |
| Management charts | React Query cache | 30 min | Manual refresh button |
| NLQ results | No cache (each query is unique) | — | — |

- PERF-05: Server-side caching uses `IMemoryCache` with sliding expiration. No distributed cache in V1.
- PERF-06: Client-side caching uses React Query with `staleTime` and `cacheTime` configured per query key.
- PERF-07: A "Refresh" button on each dashboard section invalidates the client cache and re-fetches.

### 7.5 Background refresh strategy

- PERF-08: The management dashboard pre-warms its cache on the first admin login of the day by pre-fetching all KPI queries.
- PERF-09: Auto-refresh intervals:

| Dashboard | Auto-refresh interval | Implementation |
|-----------|:--------------------:|----------------|
| Staff Today View | 60 seconds | `setInterval` + React Query `refetchInterval` |
| Staff Alerts | 5 minutes | React Query `refetchInterval` |
| Tenant Revenue | No auto-refresh (manual) | User clicks "Refresh" or navigates |
| Management KPIs | 15 minutes | Server cache expiry triggers re-fetch |

### 7.6 Maximum acceptable staleness

| Dashboard type | Metric category | Max staleness |
|---------------|----------------|:-------------:|
| Staff | Today's bookings, payments | 60 seconds |
| Staff | Alerts | 5 minutes |
| Tenant | Current month revenue | 5 minutes |
| Tenant | Past revenue, ADR, RevPAR | 24 hours |
| Tenant | TrustScore | 15 minutes |
| Tenant | Sync health | Real-time |
| Management | GMV, commission, revenue | 24 hours |
| Management | Risk indicators | 1 hour |
| Management | Active tenants, listings | 24 hours |

---

## 8. Drill-Down & Export Requirements

### 8.1 CSV export

- EXP-01: Every data table on every dashboard MUST have a "Download CSV" button.
- EXP-02: CSV format: UTF-8 with BOM, comma-separated, quoted strings, ISO date format.
- EXP-03: Filename convention: `Atlas_{ReportType}_{StartDate}_{EndDate}.csv`.
- EXP-04: Maximum rows per CSV: 50,000 (tenant dashboards), 100,000 (management dashboard).
- EXP-05: CSV generation happens server-side via streaming response (`Content-Type: text/csv`). No client-side generation for large datasets.

### 8.2 PDF export (future)

- EXP-06: V2: PDF export for financial reports (revenue summary, commission statement, invoice history).
- EXP-07: V2: PDF uses a server-side rendering library (e.g., QuestPDF or wkhtmltopdf).
- EXP-08: V1: "Print-friendly" CSS stylesheet applied on `Ctrl+P` / browser print for financial pages.

### 8.3 Filter state persistence

- EXP-09: Dashboard filter state (date range, property selection, status filters) MUST persist across page navigation within the same session.
- EXP-10: Implementation: URL query parameters for shareable state (`?from=2026-02-01&to=2026-02-28&propertyId=5`).
- EXP-11: Last-used filter state saved to `localStorage` per user per dashboard.
- EXP-12: "Reset filters" button clears to default (last 30 days, all properties, all statuses).

### 8.4 Date range filters

| Filter option | Value | Available on |
|---|---|---|
| Today | Current date | Staff |
| Last 7 days | Today − 7 → today | All |
| Last 30 days | Today − 30 → today | All |
| Last 90 days | Today − 90 → today | Tenant, Management |
| This month | 1st of current month → today | All |
| Last month | 1st of previous month → last day | All |
| Year to date | Jan 1 → today | Tenant, Management |
| Custom range | Date picker | All |

- EXP-13: Maximum custom range: 365 days. If exceeded: "Please select a range of 365 days or fewer."
- EXP-14: Date range applies to all sections on the dashboard simultaneously (global filter).

### 8.5 Multi-property filters

- EXP-15: Tenant dashboard MUST support multi-property selection (checkbox list or dropdown with multi-select).
- EXP-16: Default: all properties selected. Deselecting a property excludes its data from all metrics.
- EXP-17: Management dashboard supports filtering by: city (multi-select), plan type (multi-select), marketplace status, sync mode.
- EXP-18: Filter combinations MUST be reflected in exported CSV (filter criteria included as header rows in the CSV).

### 8.6 Tenant-level vs global-level filtering

| Dashboard | Filter scope | Mechanism |
|-----------|-------------|-----------|
| Staff | Current tenant only | EF Core tenant filter (automatic) |
| Tenant | Current tenant only | EF Core tenant filter |
| Management | All tenants (global) | `IgnoreQueryFilters()` + explicit role check |
| Management (drill-down) | Specific tenant | `IgnoreQueryFilters()` + `WHERE TenantId = @selected` |

- EXP-19: When a management user drills into a specific tenant, the dashboard re-renders with that tenant's data using the tenant dashboard layout (but retaining the admin navigation).

---

## 9. Audit & Compliance for Reporting

### 9.1 Report access tracking

Every report access and export MUST be logged in `AuditLog`.

| Action | AuditLog fields | Trigger |
|--------|----------------|---------|
| Dashboard viewed | `Action = 'dashboard.viewed'`, `PayloadJson = {dashboardType, userId, role}` | Page load |
| Report generated | `Action = 'report.generated'`, `PayloadJson = {reportType, filters, rowCount}` | API response |
| CSV exported | `Action = 'report.exported.csv'`, `PayloadJson = {reportType, filters, rowCount, fileName}` | Download |
| PDF exported (V2) | `Action = 'report.exported.pdf'`, same fields | Download |
| NLQ query executed | `Action = 'nlq.query.executed'`, `PayloadJson = {rawQuery, templateId, parameters, rowCount}` | Query |
| Drill-down accessed | `Action = 'report.drilldown'`, `PayloadJson = {parentReport, drilldownEntity, entityId}` | Click |

### 9.2 Financial report audit requirements

For financial reports (revenue, commission, settlement, billing), additional audit controls apply:

- AUD-01: Financial reports MUST include a "Report generated at {timestamp}" watermark.
- AUD-02: Financial reports MUST include the identity of the user who generated/exported the report.
- AUD-03: Financial report CSVs MUST include a header block:

```
# Atlas Homestays - Revenue Report
# Generated: 2026-02-27 14:30:00 UTC
# Generated by: admin@atlashomestays.com (Super Admin)
# Period: 2026-02-01 to 2026-02-28
# Filters: All properties, All sources
# Rows: 1,234
```

- AUD-04: Financial report exports are retained in the `AuditLog` for 2 years (matching AuditLog retention in RA-DATA-001 §2.5).

### 9.3 Cross-tenant access tracking

- AUD-05: When a management user views a specific tenant's data, the `AuditLog` MUST record: `Action = 'platform.tenant.data_accessed'` with `{adminUserId, targetTenantId, dataType, filters}`.
- AUD-06: A daily summary report of cross-tenant data access is auto-generated for the Super Admin: "Yesterday, {N} tenant records were accessed by {M} admin users."

### 9.4 Compliance with data retention

- AUD-07: Dashboard data display adheres to the retention policies defined in RA-DATA-001 §2.5. Data beyond retention is not shown (the UI shows "Historical data is not available beyond {cutoff date}").
- AUD-08: Financial data (invoices, payments) is retained for 7 years per Indian tax requirements (separate from snapshot retention).

---

## 10. Definition of Done — Dashboard Layer V1

### 10.1 Checklist

| # | Criterion | Verification method | Phase |
|---|-----------|-------------------|:-----:|
| 1 | Staff dashboard Today View shows check-ins, check-outs, in-house guests accurately | E2E test (Playwright) | P1 |
| 2 | Staff dashboard alerts detect overbooking, sync issues, pending payments | Integration test with mock data | P1 |
| 3 | Staff can change booking status inline (Confirmed → CheckedIn → CheckedOut) | E2E test | P1 |
| 4 | Tenant Revenue Overview matches `AdminReportsController.GetAnalytics()` output | Cross-validation integration test | P1 |
| 5 | Tenant Revenue (current month) uses real-time L1 data, past months use L2 snapshots | Test: verify query source by checking response headers | P1 |
| 6 | Tenant commission metrics match booking ledger (`SUM(CommissionAmount)`) | Reconciliation test | P1 |
| 7 | Tenant TrustScore matches `PropertyTrustScoreCache` | Integration test | P1 |
| 8 | Tenant sync health reflects `ChannelConfig` accurately | Integration test | P1 |
| 9 | Price suggestion panel works (accept, reject, accept-all, auto-apply toggle) | E2E test | P1 |
| 10 | Management dashboard GMV matches `Snap_DailyMarketplacePerformance` | Reconciliation test | P2 |
| 11 | Management revenue mix (subscription + commission + boost) sums correctly | Integration test | P2 |
| 12 | City breakdown shows correct top 15 cities by GMV | Integration test | P2 |
| 13 | Risk view detects and displays all 7 risk indicators | Integration test | P2 |
| 14 | Role-based access: Staff cannot see revenue, Property Manager sees read-only | E2E test with role variations | P1 |
| 15 | Role-based access: `atlas_admin` can access `/platform/*`, non-admin gets 403 | Integration test | P2 |
| 16 | No cross-tenant data leakage: Tenant A dashboard shows 0 data from Tenant B | Security integration test | P1 |
| 17 | CSV export produces valid CSV with headers and correct row count | Integration test | P1 |
| 18 | Filter state persists across navigation (URL params + localStorage) | E2E test | P1 |
| 19 | Dashboard page load times within SLA (P95 < 2s for staff/tenant, < 2.5s for management) | Performance test (Lighthouse) | P1/P2 |
| 20 | NLQ supports 15 predefined query templates | Integration test per template | P3 |
| 21 | NLQ guardrails: read-only enforcement, parameter validation, rate limiting | Security integration test | P3 |
| 22 | NLQ does not access PII fields | Template audit | P3 |
| 23 | All report access and exports logged in `AuditLog` | Integration test: verify AuditLog rows | P1 |
| 24 | Financial report CSVs include audit header block | Unit test on CSV generator | P1 |

### 10.2 Non-functional requirements

| Requirement | Target | Phase |
|------------|--------|:-----:|
| Staff dashboard page load (P95) | < 1.5 seconds | P1 |
| Tenant dashboard page load (P95) | < 2.0 seconds | P1 |
| Management dashboard page load (P95) | < 2.5 seconds | P2 |
| NLQ query response time (P95) | < 5.0 seconds | P3 |
| Chart rendering time | < 500ms | All |
| CSV export (50k rows) generation time | < 10 seconds | P1 |
| Zero cross-tenant data leakage | 0 incidents | All |
| Dashboard auto-refresh reliability | 99.9% of refresh cycles succeed | All |
| NLQ availability | 99% uptime during business hours | P3 |

---

## 11. Phase Planning

### 11.1 Phase 1 — Staff Dashboard + Tenant Dashboard Basic (Weeks 1–6)

| Deliverable | Details | Priority |
|---|---|---|
| Staff Today View | Check-ins, check-outs, in-house guests, pending payments, cleaning schedule | P0 |
| Staff Booking List | Filters, inline status update, payment status | P0 |
| Staff Alerts | Overbooking, sync issue, payment pending | P0 |
| Tenant Revenue Overview | This/last month revenue, occupancy, ADR, RevPAR, direct booking %, OTA % | P0 |
| Tenant Commission Overview | Commission paid, boost spend, net payout, trend | P1 |
| Tenant Operational Health | Sync health, TrustScore, booking trend, cancellation rate | P1 |
| Tenant Price Suggestion Panel | Suggestion inbox (if RA-AI-001 is enabled) | P1 |
| Role-based access (Staff / Owner) | `<RoleGate>` component, API role checks | P0 |
| CSV export (tenant) | All tabular data | P1 |
| Filter persistence | URL params + localStorage | P1 |
| Audit logging | Report access + export logging | P0 |

**Success criteria**: Staff can run daily operations entirely from the dashboard. Tenant owner can track revenue and health without external spreadsheets.

### 11.2 Phase 2 — Management Dashboard + City Reports (Weeks 7–12)

| Deliverable | Details | Priority |
|---|---|---|
| Management Platform Health | KPI cards (GMV, commission, subscription, active tenants, adoption %) | P0 |
| GMV Trend Chart | Daily/monthly line chart | P0 |
| Revenue Mix Chart | Subscription + commission + boost donut | P1 |
| City Breakdown | GMV, occupancy, tenants per city. Top 15 + underperforming clusters | P0 |
| Top/Bottom Tenants | RevPAR, occupancy, GMV ranking | P1 |
| Risk View | Chargeback, refund, fraud alerts, suspended tenants, TrustScore drops, settlement backlogs | P0 |
| Drill-down (Platform → City → Tenant → Property) | Hierarchical navigation | P1 |
| CSV export (management) | All tables, up to 100k rows | P1 |
| Role-based access (Admin roles) | `atlas_admin`, `atlas_finance_admin`, `atlas_super_admin` | P0 |
| Cross-tenant access audit logging | Track which admin viewed which tenant's data | P0 |

**Success criteria**: Atlas leadership can assess platform health, identify growth cities, and detect risks without manual SQL queries.

### 11.3 Phase 3 — NLQ Reporting Interface (Weeks 13–20)

| Deliverable | Details | Priority |
|---|---|---|
| NLQ Chat UI | Text input, query history, suggested queries, response display (table + chart + summary) | P0 |
| NLQ Parser | Azure OpenAI GPT-4o integration. NLQ → template ID + parameters. | P0 |
| Query Template Library | 15 predefined templates (section 5.3) | P0 |
| Query Executor | Read-only connection, parameterized execution, timeout, result cap | P0 |
| Response Formatter | Auto-select chart type, generate summary text | P1 |
| Guardrails | Rate limiting, parameter validation, PII exclusion, complexity cap | P0 |
| NLQ Logging | Full query lifecycle logging | P0 |
| Template expansion | Based on actual query logs, add 10 more templates | P2 |

**Success criteria**: Admin can answer 80% of ad-hoc data questions via NLQ without writing SQL. Guardrails prevent misuse.

### 11.4 Feature flags

| Flag | Default | Scope | Phase |
|------|:-------:|-------|:-----:|
| `Dashboard:StaffEnabled` | `true` | Global | P1 |
| `Dashboard:TenantRevenueEnabled` | `true` | Global | P1 |
| `Dashboard:TenantCommissionEnabled` | `true` | Global | P1 |
| `Dashboard:TenantPriceSuggestionsEnabled` | (follows `PricingIntelligence:Enabled`) | Global | P1 |
| `Dashboard:ManagementEnabled` | `false` | Global | P2 |
| `Dashboard:ManagementCityBreakdownEnabled` | `true` | Global | P2 |
| `Dashboard:ManagementRiskViewEnabled` | `true` | Global | P2 |
| `Dashboard:NlqEnabled` | `false` | Global | P3 |
| `Dashboard:NlqMaxQueriesPerDay` | `500` | Per user | P3 |
| `Dashboard:NlqMaxTokensPerDay` | `50000` | Per user | P3 |

### 11.5 Dependencies between phases

```
Phase 1 (Staff + Tenant)
  ├── Requires: L1 OLTP tables (existing)
  ├── Requires: Snap_DailyPropertyPerformance, Snap_DailyTenantPerformance (RA-DATA-001)
  ├── Requires: PropertyTrustScoreCache (RA-DATA-001)
  └── Optional: PriceSuggestion table (RA-AI-001) — gated by feature flag

Phase 2 (Management)
  ├── Requires: Phase 1 complete
  ├── Requires: Snap_DailyMarketplacePerformance (RA-DATA-001)
  ├── Requires: Snap_DailyRevenueMetrics (RA-DATA-001)
  ├── Requires: Snap_DailyTrustScore (RA-DATA-001)
  └── Requires: Admin role enforcement (RA-006)

Phase 3 (NLQ)
  ├── Requires: Phase 2 complete
  ├── Requires: Azure OpenAI GPT-4o provisioned
  ├── Requires: Read-only SQL user configured
  └── Requires: All L2 snapshot tables populated with ≥ 30 days of data
```

---

## Appendix A: API Endpoint Reference

### Staff Dashboard APIs

| Endpoint | Method | Description | Auth |
|----------|--------|-------------|------|
| `/api/dashboard/today` | GET | Today's check-ins, check-outs, in-house count | All authenticated |
| `/api/dashboard/alerts` | GET | Active alerts for current tenant | All authenticated |
| `/api/bookings?checkinDate=X&status=Y` | GET | Filtered booking list | All authenticated |
| `/api/bookings/{id}/status` | PATCH | Update booking status | All authenticated |

### Tenant Dashboard APIs

| Endpoint | Method | Description | Auth |
|----------|--------|-------------|------|
| `/api/analytics?startDate=X&endDate=Y&listingId=Z` | GET | Revenue, ADR, RevPAR, occupancy | `tenant_owner`, `property_manager`, `atlas_admin` |
| `/api/analytics/trends` | GET | Monthly trends (12 months) | Same |
| `/api/analytics/channel-performance` | GET | Revenue by booking source | Same |
| `/api/analytics/commission` | GET | Commission summary | `tenant_owner`, `atlas_admin` |
| `/api/analytics/trust-score?propertyId=X` | GET | TrustScore + components | Same |
| `/api/pricing/suggestions` | GET | Pending price suggestions | Same |
| `/api/pricing/suggestions/{id}/accept` | POST | Accept suggestion | `tenant_owner` |
| `/api/pricing/suggestions/{id}/reject` | POST | Reject suggestion | `tenant_owner` |
| `/api/data/export/my-performance` | GET | CSV export (tenant-scoped) | `tenant_owner` |

### Management Dashboard APIs

| Endpoint | Method | Description | Auth |
|----------|--------|-------------|------|
| `/api/platform/dashboard/health` | GET | Platform KPIs | `atlas_admin`, `atlas_finance_admin` |
| `/api/platform/dashboard/gmv-trend` | GET | GMV time series | Same |
| `/api/platform/dashboard/revenue-mix` | GET | Subscription + commission + boost | Same |
| `/api/platform/dashboard/city-breakdown` | GET | Metrics by city | Same |
| `/api/platform/dashboard/top-tenants` | GET | Top N by RevPAR/GMV | Same |
| `/api/platform/dashboard/risk` | GET | Risk indicators | Same |
| `/api/platform/dashboard/drilldown/tenant/{id}` | GET | Tenant-specific metrics | Same |
| `/api/platform/data/export/{type}` | GET | CSV export (platform-wide) | `atlas_admin`, `atlas_super_admin` |

### NLQ APIs

| Endpoint | Method | Description | Auth |
|----------|--------|-------------|------|
| `/api/platform/nlq/query` | POST | Submit NLQ query | `atlas_admin`, `atlas_super_admin` |
| `/api/platform/nlq/templates` | GET | List available query templates | Same |
| `/api/platform/nlq/history` | GET | User's recent NLQ queries | Same |
| `/api/platform/nlq/suggestions` | GET | Suggested queries | Same |

---

## Appendix B: UI Component Hierarchy

```
App
├── /login → LoginPage
├── / (ProtectedRoute + AppLayout)
│   ├── /dashboard → StaffDashboardPage
│   │   ├── TodayView
│   │   │   ├── CheckInList
│   │   │   ├── CheckOutList
│   │   │   ├── InHouseGuestList
│   │   │   ├── PendingPaymentsList
│   │   │   └── CleaningSchedule
│   │   ├── AlertStrip
│   │   └── BookingListWithFilters
│   ├── /analytics → TenantDashboardPage
│   │   ├── RevenueOverview (KPI cards)
│   │   ├── CommissionOverview
│   │   ├── OperationalHealth
│   │   │   ├── SyncHealthTable
│   │   │   ├── TrustScoreCards
│   │   │   └── BookingTrendChart
│   │   ├── PriceSuggestionPanel
│   │   ├── MarketplacePerformance
│   │   └── RevenueTrendCharts
│   ├── /platform/dashboard → ManagementDashboardPage (RoleGate)
│   │   ├── PlatformHealthKPIs
│   │   ├── GmvTrendChart
│   │   ├── RevenueMixChart
│   │   ├── CityBreakdownSection
│   │   ├── TopTenantsTable
│   │   └── RiskViewPanel
│   └── /platform/nlq → NlqInterfacePage (RoleGate)
│       ├── NlqChatInput
│       ├── NlqQueryHistory
│       ├── NlqResultDisplay
│       │   ├── SummaryText
│       │   ├── ResultTable
│       │   └── ResultChart
│       └── NlqSuggestedQueries
```

---

## Appendix C: Structured Log Events

| Event | Level | Fields | Trigger |
|-------|-------|--------|---------|
| `dashboard.staff.loaded` | Info | `{userId, tenantId, loadTimeMs}` | Page load |
| `dashboard.tenant.loaded` | Info | `{userId, tenantId, period, loadTimeMs}` | Page load |
| `dashboard.management.loaded` | Info | `{userId, role, period, loadTimeMs}` | Page load |
| `dashboard.alert.generated` | Info | `{alertType, severity, tenantId, entityId}` | Alert detection |
| `dashboard.alert.dismissed` | Debug | `{alertType, userId}` | User dismisses |
| `report.generated` | Info | `{reportType, tenantId, filters, rowCount}` | API response |
| `report.exported.csv` | Info | `{reportType, tenantId, filters, rowCount, fileName}` | Download |
| `nlq.query.submitted` | Info | `{userId, role, rawQuery}` | Query |
| `nlq.query.parsed` | Info | `{templateId, parameters, confidence}` | Parse |
| `nlq.query.executed` | Info | `{templateId, rowsReturned, executionMs}` | Execute |
| `nlq.query.failed` | Warn | `{templateId, error}` | Error |
| `nlq.query.rejected` | Warn | `{rawQuery, reason}` | Guardrail |
| `nlq.ratelimit.exceeded` | Warn | `{userId, limit, window}` | Rate limit |
| `platform.tenant.data_accessed` | Info | `{adminUserId, targetTenantId, dataType}` | Drill-down |

---

## Appendix D: Configuration Reference

| Config key | V1 default | Type | Section |
|------------|:----------:|------|:-------:|
| `Dashboard:StaffEnabled` | `true` | bool | §11.4 |
| `Dashboard:TenantRevenueEnabled` | `true` | bool | §11.4 |
| `Dashboard:TenantCommissionEnabled` | `true` | bool | §11.4 |
| `Dashboard:ManagementEnabled` | `false` | bool | §11.4 |
| `Dashboard:NlqEnabled` | `false` | bool | §11.4 |
| `Dashboard:NlqMaxQueriesPerDay` | 500 | int | §6.8 |
| `Dashboard:NlqMaxTokensPerDay` | 50000 | int | §6.8 |
| `Dashboard:StaffAutoRefreshSeconds` | 60 | int | §7.5 |
| `Dashboard:AlertRefreshSeconds` | 300 | int | §7.5 |
| `Dashboard:ManagementCacheTtlMinutes` | 15 | int | §7.4 |
| `Dashboard:CsvMaxRowsTenant` | 50000 | int | §8.1 |
| `Dashboard:CsvMaxRowsManagement` | 100000 | int | §8.1 |
| `Dashboard:MaxDateRangeDays` | 365 | int | §8.4 |
| `Dashboard:NlqQueryTimeoutSeconds` | 10 | int | §5.3 |
| `Dashboard:NlqMaxResultRows` | 1000 | int | §6.5 |
| `Dashboard:NlqRateLimitPerMinute` | 10 | int | §6.8 |
| `Dashboard:NlqRateLimitPerHour` | 100 | int | §6.8 |
| `NlqConnectionString` | (read-only user) | string | §6.1 |

---

*End of RA-DASH-001*
