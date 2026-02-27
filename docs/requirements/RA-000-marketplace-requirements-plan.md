# RA-000: Marketplace Requirements — Master Plan

**Purpose:** This is the original planning document that initiated the marketplace requirements documentation effort. It captures the codebase ground-truth analysis and the structural blueprints for the HLD, LLD, and all subsequent Requirements Addenda (RA-001 through RA-006).

**Status:** Completed (all planned documents delivered)

**Downstream documents:**

- [HLD — Marketplace Commission Engine](HLD-marketplace-commission-engine.md)
- [LLD — Marketplace Commission Engine](LLD-marketplace-commission-engine.md)
- [RA-001 — Marketplace, Commission, Boost, OTA, Payments](RA-001-marketplace-commission-boost-ota-payments.md)
- [RA-002 — Governance, Scale & Control Layer](RA-002-governance-scale-monetization-control.md)
- [RA-003 — Growth, Demand & Network Effects](RA-003-growth-demand-network-effects.md)
- [RA-004 — Risk, Fraud, Trust & Compliance](RA-004-risk-fraud-trust-compliance.md)
- [RA-005 — Subscription, Billing & Revenue Control](RA-005-subscription-billing-revenue-control.md)
- [RA-006 — Operational Excellence, Admin Tooling & Support](RA-006-operational-excellence-admin-support.md)

---

## Current State (grounded in codebase)

Before writing docs, these are the key facts that shape the design and must be referenced:

- **Tenant model** (`Tenant.cs`): Has `Name`, `Slug`, `IsActive`, `Plan`, `OwnerEmail/Phone`, `CustomDomain`, `LogoUrl`. **Missing:** `DefaultCommissionPercent`, `PaymentMode`, `RazorpayLinkedAccountId`.
- **Property model** (`Property.cs`): **Already has** `CommissionPercent` (decimal?, nullable). **Missing:** `IsMarketplaceEnabled`.
- **Booking model** (`Booking.cs`): **Already has** `CommissionAmount` (decimal?, nullable). **Missing:** `CommissionPercentSnapshot`, `HostPayoutAmount`, `PaymentModeSnapshot`.
- **CommissionRates** (`CommissionRates.cs`): Currently OTA-source-based constants (Airbnb 16%, Booking.com 15%, etc.). **Not** the marketplace commission model — this is separate and must coexist.
- **Razorpay** (`RazorpayPaymentService.cs`): Standard checkout only (`KeyId`/`KeySecret` Basic Auth). **No OAuth, Route, or split settlement** exists today.
- **Channex** (`ChannexAdapter.cs`, `ChannelConfig.cs`): Basic adapter with `IChannelManagerProvider` abstraction. Connect, push rates, push availability. Property-level config with `ApiKey`, `ExternalPropertyId`.
- **No marketplace, ranking, or boost logic exists** anywhere in the codebase.
- **Billing domain** exists: Plans, Subscriptions, Invoices, Credits (`Models/Billing/`).
- **Guest portal** (`RatebotaiRepo/`): Property pages and search exist but currently use static data. Route pattern: `/homes/:propertySlug/:unitSlug`.
- **C4 diagrams** exist at `atlas-e2e/docs/04-enterprise-architecture/` — the new docs should follow the same Mermaid C4 format.

---

## Document Location

All marketplace requirements docs live under `atlas-api/docs/requirements/`:

- `RA-000-marketplace-requirements-plan.md` — this plan (entry point)
- `HLD-marketplace-commission-engine.md` — Part 1 (high-level design)
- `LLD-marketplace-commission-engine.md` — Part 2 (low-level implementation)
- `RA-001` through `RA-006` — requirements addenda

---

## Part 1: HLD Structure

The HLD contains the following sections, each grounded in existing code and tables:

1. **Product Overview** — SaaS + Marketplace hybrid, tenant vs property responsibilities, revenue modes (HOST_DIRECT vs MARKETPLACE_SPLIT)
2. **Actors** — Tenant, Guest, Atlas Admin, OTA, Razorpay (with existing integration touchpoints cited)
3. **Core Modules** — Map to existing repos/services and identify what is new vs what exists:
   - Tenant Management (exists; needs commission/payment fields)
   - Property Management (exists; needs marketplace toggle)
   - OTA Sync (exists as ChannexAdapter; needs Airbnb OAuth extension)
   - Payment Integration (exists as RazorpayPaymentService; needs OAuth + Route layer)
   - Commission Engine (new)
   - Ranking Engine (new)
   - Booking Engine (exists; needs snapshot + adapter selection)
   - Marketplace Visibility Engine (new)
4. **Payment Modes** — HOST_DIRECT (current default), MARKETPLACE_SPLIT (new Razorpay Route)
5. **Commission Model** — Default 1% tenant-level, property override >= default, snapshot at booking time, boost impact on ranking (weighted, capped, log-based scaling)
6. **Marketplace Strategy** — Path-based URLs (`atlashomestays.com/{property-slug}`), SEO, boost slider, fairness guardrails
7. **Non-Functional Requirements** — Idempotency (existing outbox pattern), retry, audit, secure token storage, minimal downtime rollout, backward compatibility
8. **Risk and Edge Cases** — Commission mid-month change, override removal, payment mode switch, OAuth expiry, split failure, OTA sync delay, overbooking
9. **C4 Component Diagram** (Mermaid) — Extend existing L2 container diagram with new components

---

## Part 2: LLD Structure

The LLD provides per-repo implementation guidance:

**A. atlas-sql / Schema:**

- New columns on Tenants: `DefaultCommissionPercent`, `PaymentMode`, `RazorpayLinkedAccountId`, `RazorpayAccessToken` (encrypted), `RazorpayRefreshToken` (encrypted)
- New column on Properties: `IsMarketplaceEnabled`
- New columns on Bookings: `CommissionPercentSnapshot`, `HostPayoutAmount`, `PaymentModeSnapshot`
- Existing `Property.CommissionPercent` repurposed as override (already nullable — fits perfectly)
- Existing `Booking.CommissionAmount` retained (already exists)
- Migration strategy, indexing, backfill logic

**B. atlas-api (.NET Core):**

- Commission Calculation Service (resolve effective rate: property override > tenant default > 1% floor)
- Ranking Score Service (weighted formula with log-based boost scaling, capped)
- Razorpay Integration Layer (OAuth flow, encrypted token storage, Route linked account, split settlement)
- Booking Flow Updates (snapshot commission, select payment adapter)
- OTA Sync Layer (ChannexAdapter stays; add Airbnb OAuth token flow via ChannelConfig)
- Validation Rules, API Contracts, Security

**C. atlas-admin-portal:**

- Tenant settings page (commission, payment mode)
- Property settings (override slider, marketplace toggle, boost explanation)
- Razorpay connect button, OTA connect buttons
- Commission change warning modal

**D. atlas-guest-portal (RatebotaiRepo):**

- Path-based routing change from `/homes/:slug/:unit` to `/{property-slug}`
- Property visibility filter (IsMarketplaceEnabled)
- Ranking sort, boost badge, commission-neutral pricing

**E. Logging and Observability**

**F. Sequence Diagrams** (Mermaid):

- Direct booking with split settlement
- Tenant connecting Razorpay via OAuth
- Tenant connecting Airbnb via Channex/OAuth

**G. Rollout Plan** — feature flags, backward compatibility, data migration

**H. Testing Plan** — unit, integration, payment simulation, commission calculation cases

**I. Documentation updates list**

---

## Key Design Decisions

- `Property.CommissionPercent` already exists and is nullable — reuse as override (no new column needed)
- `Booking.CommissionAmount` already exists — reuse (no new column needed)
- `CommissionRates.cs` (OTA source-based) is a **separate concern** from marketplace commission — both coexist
- Payment mode is tenant-level, not booking-level (snapshot at booking time in `PaymentModeSnapshot`)
- Razorpay tokens must be encrypted at rest (AES-256 or similar via DataProtection API)
