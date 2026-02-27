# RA-003: Growth, Demand & Network Effects Requirements

**Addendum to:** [RA-001](RA-001-marketplace-commission-boost-ota-payments.md) | [RA-002](RA-002-governance-scale-monetization-control.md) | [HLD](HLD-marketplace-commission-engine.md) | [LLD](LLD-marketplace-commission-engine.md)

**Purpose:** Define the marketplace growth architecture, SEO engine, search/discovery, host activation flywheel, guest retention layer, demand monetization, data network effects, analytics, growth experimentation, and launch readiness checklist for AtlasHomestays.com.

**Audience:** Developer, QA, Product, Growth Ops

**Owner:** Atlas Tech Solutions

**Last updated:** 2026-02-27

**Status:** Draft

---

## Table of contents

1. [Marketplace Growth Architecture](#1-marketplace-growth-architecture)
2. [SEO & Content Engine Requirements](#2-seo--content-engine-requirements)
3. [Search & Discovery Engine Requirements](#3-search--discovery-engine-requirements)
4. [Host Activation & Retention Flywheel](#4-host-activation--retention-flywheel)
5. [Guest Data & Repeat Booking Layer](#5-guest-data--repeat-booking-layer)
6. [Demand Monetization Controls](#6-demand-monetization-controls)
7. [Data Network Effects Strategy](#7-data-network-effects-strategy)
8. [Analytics & Reporting Requirements](#8-analytics--reporting-requirements)
9. [Growth Experiments Framework](#9-growth-experiments-framework)
10. [Definition of Done for Marketplace Growth V1](#10-definition-of-done-for-marketplace-growth-v1)

---

## 1. Marketplace growth architecture

### 1.1 Supply-first strategy

Atlas targets 0–10 key hosts in the initial phase. The marketplace cannot generate demand until there is sufficient supply. The growth sequence is:

| Phase | Hosts | Listings | Primary goal | Demand source |
|:-----:|:-----:|:--------:|-------------|---------------|
| 0 — Seed | 0–10 | 10–30 | Prove PMS value, onboard manually, refine admin portal | None (PMS-only) |
| 1 — Curate | 10–50 | 30–150 | Enable marketplace, curate quality, build SEO base pages | Organic SEO + host referral traffic |
| 2 — Grow | 50–500 | 150–1,500 | Demand starts compounding, activate paid ads, repeat guests | SEO + paid + referral + repeat |
| 3 — Scale | 500–100k | 1,500–300k | Network effects self-sustain, commission revenue funds growth | All channels |

**Requirements:**

| ID | Requirement |
|----|-------------|
| GRO-01 | The marketplace MUST NOT be publicly listed until at least 10 marketplace-enabled properties with active listings exist (configurable: `Growth:MinListingsForPublicLaunch`, default 10). |
| GRO-02 | Phase 0 hosts MUST receive white-glove onboarding: manual checklist, Atlas Admin co-fills property data if needed. |
| GRO-03 | Each phase transition MUST be an Atlas Admin decision, gated by supply threshold in config. No auto-transition. |

### 1.2 Demand activation stages

| Stage | Trigger | Actions |
|-------|---------|---------|
| Pre-launch | < 10 marketplace properties | No public URLs. SEO pages generated but `noindex`. Internal testing only. |
| Soft launch | 10–50 marketplace properties | Remove `noindex`. Submit sitemap to Google. Enable `atlashomestays.com/search`. No paid ads yet. |
| Growth launch | > 50 marketplace properties in >= 3 cities | Enable paid ads. Enable guest referral programme. Activate repeat-booking email campaigns (via notification outbox). |
| Scale | > 500 marketplace properties | Full A/B testing. City-level commission experiments. Sponsored placement (future). |

### 1.3 Traffic acquisition channels

| Channel | V1 implementation | Measurement | Cost model |
|---------|-------------------|-------------|-----------|
| **SEO** | City pages, property pages, structured data, sitemap | `utm_source=organic` or referrer = search engine | Free |
| **Paid ads** | Google Ads / Meta linking to city or search pages with UTM tags | `utm_source=google_ads`, `utm_campaign` | CPC (external) |
| **Host referral traffic** | Host shares their Atlas property link (`atlashomestays.com/{property-slug}`) to their own audience | `utm_source=host_share` or referrer tracking | Free (commission-funded) |
| **Direct repeat guests** | Guest returns to `atlashomestays.com` directly or via bookmarked link | `utm_source=direct` or no referrer | Free |
| **Guest referral** | Guest shares a referral link (v1: coupon code, v2: tracked link) | `promoCode` on booking with `source = referral` | Coupon discount cost |

### 1.4 Attribution tracking model

Attribution is tracked at the booking level via `Booking.BookingSource` (existing field, varchar 50) and supplemented by query-string parameters stored on the guest session.

| Field / mechanism | Storage | Values |
|-------------------|---------|--------|
| `Booking.BookingSource` | Booking row | `marketplace_organic`, `marketplace_paid`, `marketplace_host_share`, `marketplace_direct`, `marketplace_referral`, `admin_manual`, `ota_channex` |
| UTM parameters | `GuestSession.UtmSource`, `UtmMedium`, `UtmCampaign` (new lightweight table or fields on Booking) | Standard UTM values from query string |
| Landing page | `GuestSession.LandingPath` | First page path the guest visited |
| Referrer | `GuestSession.Referrer` | HTTP Referer header (first touch) |

**V1 simplification:** Store `BookingSource` on the booking. UTM detail stored as JSON in `Booking.AttributionJson` (nvarchar max, nullable) to avoid a separate table. Populate from query string at checkout.

| ID | Requirement |
|----|-------------|
| ATT-01 | `BookingSource` MUST be set on every marketplace booking. Default: `marketplace_organic`. |
| ATT-02 | UTM parameters from the guest's first visit MUST be captured in a session cookie (`atlas_utm`, 30-day expiry, HttpOnly, SameSite=Lax) and written to `AttributionJson` at booking creation. |
| ATT-03 | Attribution data MUST NOT be modified after booking creation (same immutability principle as commission snapshots). |

### 1.5 Guest identity model (lightweight V1)

Atlas V1 does NOT require guest accounts or login.

| Model | Implementation |
|-------|---------------|
| Guest identification | By phone number + email at booking time. Existing `Guest` model (`Name`, `Phone`, `Email`). |
| Cross-tenant identity | V1: none. Guest with same phone booking at two tenants creates two `Guest` rows (different TenantIds). |
| Marketplace identity | V2: introduce a `MarketplaceGuest` table (phone as primary key, not tenant-scoped). V1: rely on phone matching for repeat detection. |
| Authentication | V1: none. Guest books as a walk-in. V2: OTP-based login for booking history. |

- GID-01: Guest MUST NOT be required to create an account to book. Phone + email + name at checkout is sufficient.
- GID-02: Repeat guest detection (section 5) MUST use phone number as the primary matching key, with email as secondary.
- GID-03: No passwords are stored for guests in V1.

### 1.6 Funnel stages

```
Visit → Search → View → Book → Repeat
```

| Stage | Definition | Metric | Tracking |
|-------|-----------|--------|----------|
| **Visit** | Guest lands on any atlashomestays.com page | Unique visitors (by session cookie) | Cloudflare Analytics or lightweight page-view endpoint |
| **Search** | Guest performs a search or views a city/locality page | Search events | Structured log: `marketplace.search` with location, dates, guest count |
| **View** | Guest views a property detail page | Property view events | Existing audit: `marketplace.property.viewed` |
| **Book** | Guest completes payment | Booking confirmed | Existing: `booking.confirmed` |
| **Repeat** | Same guest (by phone) books again on the marketplace | Repeat booking flag | `Booking.IsRepeatGuest` (computed at creation) |

**Funnel metrics (computed daily, stored in analytics cache):**

| Metric | Formula |
|--------|---------|
| Search-to-view rate | Views / Searches |
| View-to-book rate | Bookings / Views |
| Overall conversion | Bookings / Visits |
| Repeat rate | Repeat bookings / Total bookings (trailing 90 days) |

### 1.7 Acceptance criteria

| ID | Given | When | Then |
|----|-------|------|------|
| AC-GRO-01 | < 10 marketplace properties exist | Guest visits atlashomestays.com/search | Results page shows "Coming soon" or is `noindex` (based on Growth:MinListingsForPublicLaunch) |
| AC-GRO-02 | Guest arrives via `?utm_source=google_ads&utm_campaign=goa_summer` | Guest completes booking | `Booking.BookingSource = 'marketplace_paid'`, `AttributionJson` contains UTM fields |
| AC-GRO-03 | Guest books without any UTM | Guest completes booking | `Booking.BookingSource = 'marketplace_organic'` |
| AC-GRO-04 | Same guest (phone) has a prior marketplace booking | Guest books again | `Booking.IsRepeatGuest = true`, BookingSource includes attribution |

---

## 2. SEO & content engine requirements

### 2.1 URL structure

AtlasHomestays.com uses path-based URLs for all marketplace pages. The guest portal (RatebotaiRepo) renders all routes.

| Page type | URL pattern | Example | Source of truth |
|-----------|------------|---------|-----------------|
| Homepage | `/` | `atlashomestays.com/` | Static + dynamic featured properties |
| City page | `/{city-slug}` | `atlashomestays.com/goa` | Auto-generated from properties in that city |
| Locality page | `/{city-slug}/{locality-slug}` | `atlashomestays.com/goa/calangute` | Auto-generated from property addresses |
| Property page | `/{property-slug}` | `atlashomestays.com/sunset-villa-calangute` | Property record (marketplace-enabled) |
| Unit/listing page | `/homes/{property-slug}/{unit-slug}` | `atlashomestays.com/homes/sunset-villa/studio-room` | Existing route (already in App.tsx) |
| Search | `/search?location=...&checkIn=...&checkOut=...&guests=...` | `atlashomestays.com/search?location=goa` | Dynamic query |
| Blog | `/blog/{category}/{slug}` | Existing | Static content |

**Requirements:**

| ID | Requirement |
|----|-------------|
| SEO-01 | City slugs MUST be lowercase, hyphenated, ASCII-only (e.g. `goa`, `north-goa`, `mumbai`). Auto-generated from city name. |
| SEO-02 | Locality slugs MUST be unique within their city (e.g. `calangute` under `goa`). |
| SEO-03 | Property slugs MUST be globally unique (enforced by DB constraint). Auto-generated from property name + locality (e.g. `sunset-villa-calangute`). Append numeric suffix on collision. |
| SEO-04 | URLs MUST NOT contain TenantId, PropertyId, or any internal IDs. Slugs only. |
| SEO-05 | If a property is deactivated or removed from marketplace, its URL MUST return 410 Gone (not 404) so search engines de-index it. |

### 2.2 Auto-generated city pages

City and locality pages are created automatically when a marketplace-enabled property exists in that location.

**Data source:** `Property.Address` is parsed to extract city and locality. V1: manual `Property.City` and `Property.Locality` fields (new, varchar 100 each). V2: address parsing service.

| Element | Content | Dynamic? |
|---------|---------|:--------:|
| Page title | "{City} Homestays & Vacation Rentals — AtlasHomestays" | Yes (city name) |
| H1 | "Homestays in {City}" | Yes |
| Description paragraph | "Discover {count} handpicked homestays in {city}. Book directly for the best prices." | Yes (count from DB) |
| Property cards | Top-ranked marketplace properties in that city (max 20 per page, paginated) | Yes |
| Neighbourhood links | Links to locality pages within the city | Yes |
| FAQ section | "When is the best time to visit {city}?", "How many homestays are in {city}?" (template-driven) | Semi-dynamic |

| ID | Requirement |
|----|-------------|
| SEO-06 | A city page MUST be auto-generated when the first marketplace-enabled property in that city reaches `Active` status. |
| SEO-07 | A city page MUST be de-indexed (410 or `noindex`) if zero active marketplace properties exist in that city. |
| SEO-08 | City and locality pages MUST be server-side rendered (SSR) or pre-rendered (SSG) for crawlability. V1: pre-rendered at build time (Cloudflare Pages) with ISR (Incremental Static Regeneration) every 1 hour, or on-demand via webhook on property change. |

### 2.3 Dynamic internal linking rules

| From page | Link to | Anchor text | Condition |
|-----------|---------|-------------|-----------|
| Homepage | City pages | "Homestays in {City}" | City has >= 3 marketplace properties |
| City page | Locality pages | "Homestays in {Locality}" | Locality has >= 1 marketplace property |
| City page | Property pages | Property card (name + photo + price) | Top-ranked properties |
| Property page | City page | Breadcrumb: "Home > {City} > {Property}" | Always |
| Property page | Related properties | "Similar stays in {Locality}" | Same city, max 4 cards |
| Blog post | City/property pages | Inline contextual links | Manual in blog content |

- SEO-09: Internal links MUST use absolute paths (`/goa/calangute`) not full URLs, for domain portability.
- SEO-10: Related property links MUST be rendered server-side.

### 2.4 Canonical rules

| Page | Canonical URL |
|------|--------------|
| Property page | `https://atlashomestays.com/{property-slug}` |
| City page | `https://atlashomestays.com/{city-slug}` |
| Locality page | `https://atlashomestays.com/{city-slug}/{locality-slug}` |
| Search page (any params) | `https://atlashomestays.com/search` (canonical strips query params to avoid duplicate indexing) |
| Paginated city page (`?page=2`) | `https://atlashomestays.com/{city-slug}` (canonical to page 1; use `rel="next"` / `rel="prev"`) |

- SEO-11: Every page MUST have a `<link rel="canonical">` tag.
- SEO-12: Search pages MUST include `<meta name="robots" content="noindex, follow">` to avoid indexing thin/duplicate search permutations.

### 2.5 Sitemap generation

| Sitemap | Content | Update frequency | Max URLs |
|---------|---------|:----------------:|:--------:|
| `sitemap-cities.xml` | All city pages | Daily | ~500 |
| `sitemap-properties.xml` | All marketplace-enabled property pages | Daily | 50,000 per file (split if exceeded) |
| `sitemap-blog.xml` | All blog posts | Weekly | ~200 |
| `sitemap-index.xml` | Links to all sitemap files | Daily | — |

| ID | Requirement |
|----|-------------|
| SEO-13 | Sitemaps MUST be regenerated daily via a background job (outbox-triggered or cron). |
| SEO-14 | Sitemaps MUST be served from `atlashomestays.com/sitemap-index.xml`. |
| SEO-15 | When a property is added/removed from marketplace, sitemap regeneration SHOULD be triggered within 1 hour (webhook or scheduled). |
| SEO-16 | `robots.txt` MUST reference the sitemap index: `Sitemap: https://atlashomestays.com/sitemap-index.xml`. |

### 2.6 Meta tag generation rules

| Page type | `<title>` | `<meta description>` |
|-----------|----------|---------------------|
| Homepage | "AtlasHomestays — Handpicked Homestays & Vacation Rentals in India" | "Book unique homestays and vacation rentals across India. Best prices guaranteed. Direct bookings with verified hosts." |
| City | "{City} Homestays & Vacation Rentals — AtlasHomestays" | "Discover {count} handpicked homestays in {City}. Book directly for the best prices. No middlemen." |
| Locality | "Homestays in {Locality}, {City} — AtlasHomestays" | "{count} curated homestays in {Locality}, {City}. Compare prices and book directly." |
| Property | "{PropertyName} — Homestay in {City} — AtlasHomestays" | Truncated property description (max 155 chars) or "{PropertyName} in {Locality}, {City}. {MaxGuests} guests. From INR {Price}/night." |
| Search | "Search Homestays — AtlasHomestays" | "Find and book the best homestays across India." |

| ID | Requirement |
|----|-------------|
| SEO-17 | `<title>` MUST be <= 60 characters. Truncate property name if needed, preserving city. |
| SEO-18 | `<meta description>` MUST be 120-160 characters. |
| SEO-19 | Each property page MUST have a unique title and description. |
| SEO-20 | Open Graph (`og:title`, `og:description`, `og:image`, `og:url`) MUST be set on property pages for social sharing. `og:image` = first listing photo URL. |

### 2.7 Structured data (schema.org)

| Page | Schema type | Key properties |
|------|------------|----------------|
| Property page | `LodgingBusiness` + `Product` | `name`, `description`, `address`, `image`, `aggregateRating`, `offers` (price, availability, currency) |
| City page | `ItemList` of `LodgingBusiness` | `numberOfItems`, `itemListElement[]` (name, url, image) |
| Review display | `Review` | `author`, `reviewRating`, `datePublished`, `reviewBody` |

| ID | Requirement |
|----|-------------|
| SEO-21 | Structured data MUST be embedded as JSON-LD in `<script type="application/ld+json">`. |
| SEO-22 | `aggregateRating` MUST only be included if the property has >= 1 review with `Rating`. |
| SEO-23 | `offers.price` MUST reflect the lowest active listing price for the property. |
| SEO-24 | Structured data MUST be validated against Google's Rich Results Test before launch. |

### 2.8 Page performance targets

| Metric | Target | Measurement |
|--------|:------:|-------------|
| Largest Contentful Paint (LCP) | < 2.5s | Lighthouse / PageSpeed Insights |
| First Input Delay (FID) | < 100ms | Web Vitals |
| Cumulative Layout Shift (CLS) | < 0.1 | Web Vitals |
| Time to First Byte (TTFB) | < 600ms | Cloudflare analytics |
| Total page weight (property page) | < 500 KB (excluding images) | Build tool |
| Image lazy-loading | All below-fold images | `loading="lazy"` attribute |

- SEO-25: City and property pages MUST pass Core Web Vitals thresholds for mobile.
- SEO-26: Cloudflare caching MUST be enabled for all static and semi-static pages (`Cache-Control: public, max-age=3600, s-maxage=86400`).

### 2.9 Property onboarding → SEO page auto-update

When a property becomes marketplace-enabled and active:

1. `Property.IsMarketplaceEnabled` set to true + `Property.Status = 'Active'` + at least one listing with pricing.
2. Background job (or on next sitemap rebuild): generate/update city page for `Property.City`, locality page for `Property.Locality`.
3. Property page becomes accessible at `/{property-slug}`.
4. Sitemap regenerated to include new property URL.
5. If this is the first property in a new city: city page is auto-created with `noindex` removed (assuming overall marketplace is past soft launch).

- SEO-27: From property activation to Google-crawlable URL MUST take < 24 hours (sitemap rebuild + search engine crawl cycle).
- SEO-28: Property deactivation MUST remove the URL from the sitemap and return 410 within 1 hour.

---

## 3. Search & discovery engine requirements

### 3.1 Search input model

| Parameter | Type | Required | Validation | Default |
|-----------|------|:--------:|-----------|---------|
| `location` | string | No | Max 100 chars. Matched against city, locality, property name. | None (show all) |
| `checkIn` | date | No | Must be today or future. ISO 8601. | None |
| `checkOut` | date | No | Must be > checkIn. ISO 8601. | None |
| `guests` | int | No | 1–20 | 2 |
| `minPrice` | decimal | No | >= 0 | None |
| `maxPrice` | decimal | No | > minPrice if both set | None |
| `amenities` | string[] | No | Known amenity slugs (comma-separated) | None |
| `propertyType` | string | No | Known property type slugs | None |
| `sort` | string | No | `recommended`, `price_low`, `price_high`, `rating`, `newest` | `recommended` |
| `page` | int | No | >= 1 | 1 |

- SRC-01: Location search MUST support fuzzy matching (Levenshtein distance <= 2 or contains). V1: SQL `LIKE '%term%'` on City, Locality, and Property.Name. V2: full-text index.
- SRC-02: If `checkIn` and `checkOut` are provided, only properties with at least one listing available for those dates MUST be returned (availability check against `ListingDailyInventory` or absence of conflicting bookings).
- SRC-03: Guest count filter MUST match against `Listing.MaxGuests >= guests`.

### 3.2 Filters

| Filter | DB column | UI control | Implementation |
|--------|-----------|-----------|----------------|
| Location | `Property.City`, `Property.Locality` | Text input with autocomplete | SQL LIKE + cached city list |
| Price range | `ListingPricing.WeekdayPrice` (or computed nightly rate) | Dual-handle slider | WHERE clause |
| Guests | `Listing.MaxGuests` | Stepper (1-20) | WHERE clause |
| Amenities | Future: `ListingAmenity` join table | Checkbox group | V1: not implemented (no amenity model). V2: join table. |
| Property type | `Property.Type` | Dropdown / pills | WHERE clause |
| Dates | `Booking.CheckinDate/CheckoutDate` or `AvailabilityBlock` | Date picker | Availability subquery |

- SRC-04: Filters MUST be combinable (AND logic). All filters are optional.
- SRC-05: Filter counts (e.g. "12 homes in Goa") MUST reflect the current filter state.

### 3.3 Ranking blend

Results sorted by `recommended` use the ranking score from RA-001 section 3 / RA-002 section 4, extended with availability and conversion signals.

| Component | Weight | Source | V1 implementation |
|-----------|:------:|--------|-------------------|
| BaseQuality | 0.25 | Profile completeness, photos, description | Computed per property (see section 4) |
| CommissionBoost | 0.20 | `log10(effectiveRate) / log10(20)`, capped | Config: `Ranking:W_Commission` |
| ReviewScore | 0.20 | Average rating, dampened by review count | From `Review` table |
| RecencyScore | 0.15 | Days since last confirmed booking (decay) | From `Booking` table |
| AvailabilityScore | 0.10 | Percentage of next-30-days available | Computed from inventory/bookings |
| ConversionRate | 0.10 | Views-to-bookings ratio (trailing 90 days) | From analytics events |

**Weights are config-driven** (`IOptions<RankingSettings>`). They MUST sum to 1.0, validated on startup.

- SRC-06: When `sort = recommended`, results MUST be ordered by the blended ranking score (descending).
- SRC-07: When `sort = price_low`, results MUST be ordered by lowest listing nightly rate, with ranking score as tiebreaker.
- SRC-08: Personalization weight (future): V1 = 0.0. Placeholder config `Ranking:W_Personalization` for future use.

### 3.4 Pagination strategy

| Parameter | Value |
|-----------|-------|
| Page size | 20 properties per page |
| Maximum depth | 50 pages (1,000 results) |
| Implementation | Offset-based (`OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY`) |
| Response includes | `totalCount`, `page`, `pageSize`, `results[]` |

- SRC-09: V1 uses offset pagination. V2 (at > 50k results): keyset pagination for performance.
- SRC-10: Total count MUST be computed with the same filters applied (no unfiltered count).

### 3.5 Caching strategy

| Layer | Scope | TTL | Key structure | Invalidation |
|-------|-------|:---:|---------------|-------------|
| Ranking scores | Per-property | 15 min | `ranking:{propertyId}` | Time-based (batch recompute every 15 min) |
| Search results | Per-query | 5 min | `search:{hash(location,dates,guests,sort,page)}` | Time-based |
| Property counts per city | Per-city | 15 min | `city_count:{citySlug}` | Time-based |
| Autocomplete city list | Global | 1 hour | `cities:autocomplete` | On property marketplace toggle change |

- SRC-11: V1 uses `IMemoryCache`. No Redis.
- SRC-12: Cache MUST be invalidated eagerly when a property is added/removed from marketplace (write-through for city counts and autocomplete).

### 3.6 Cold-start handling for new properties

New properties have no reviews, no bookings, no conversion data. Without special handling they would never surface.

| Signal | Cold-start rule |
|--------|----------------|
| ReviewScore | If no reviews: `ReviewScore = 0.50` (neutral, neither penalised nor rewarded). |
| RecencyScore | New property with 0 bookings: `RecencyScore = 0.70` (slight boost for freshness). Decays normally after 30 days if no booking. |
| ConversionRate | If < 10 property views: `ConversionRate = 0.50` (neutral). |
| AvailabilityScore | Computed normally (new property should have high availability). |
| Newness boost | For 14 days after marketplace activation: add `0.05` to the raw ranking score (capped at 1.0). Config: `Ranking:NewnessBoostDuration = 14`, `Ranking:NewnessBoostValue = 0.05`. |

- SRC-13: Cold-start defaults MUST be applied transparently (same ranking formula, different defaults for missing data).
- SRC-14: Newness boost MUST expire after the configured duration. No manual intervention required.

---

## 4. Host activation & retention flywheel

### 4.1 Host onboarding checklist

Existing `OnboardingChecklistItem` model (Key, Title, Stage, Status). The marketplace extends the checklist:

| Key | Title | Stage | Blocking for marketplace? |
|-----|-------|-------|:------------------------:|
| `profile_complete` | Complete your host profile | FastStart | No |
| `kyc_documents` | Upload KYC documents | FastStart | No |
| `property_created` | Create your first property | FastStart | Yes |
| `listing_created` | Create your first listing | FastStart | Yes |
| `pricing_set` | Set listing pricing | FastStart | Yes |
| `photos_uploaded` | Upload listing photos | FastStart | No (recommended) |
| `bank_account_added` | Add bank account for payouts | Growth | No |
| `channex_connected` | Connect OTA via Channex | Growth | No |
| `razorpay_connected` | Connect Razorpay for payments | Growth | Yes (for MARKETPLACE_SPLIT) |
| `marketplace_enabled` | Enable marketplace visibility | Growth | N/A (this IS the toggle) |
| `first_booking` | Receive your first booking | Milestone | No |
| `first_review` | Receive your first guest review | Milestone | No |

- ACT-01: Checklist items with Stage = `FastStart` MUST be seeded on tenant creation.
- ACT-02: Checklist items with Stage = `Growth` MUST be seeded when tenant completes all `FastStart` items.
- ACT-03: `Milestone` items MUST be auto-completed by the system when the event occurs.

### 4.2 Property completeness score

`BaseQuality` in the ranking formula is derived from property/listing completeness.

| Component | Max points | Criteria |
|-----------|:----------:|---------|
| Property name + description | 10 | Name: 2 pts. Description > 50 chars: 4 pts. Description > 200 chars: 6 pts. Description > 500 chars: 8 pts. Both present: 10 pts. |
| Photos | 25 | 0 photos: 0 pts. 1-2: 10 pts. 3-5: 18 pts. 6+: 25 pts. |
| Pricing configured | 15 | At least one listing with `ListingPricing.WeekdayPrice > 0`: 15 pts. |
| Check-in/out times | 5 | Both `CheckInTime` and `CheckOutTime` set: 5 pts. |
| MaxGuests set | 5 | `MaxGuests > 0`: 5 pts. |
| Address completeness | 10 | Address present: 5 pts. City + Locality filled: 10 pts. |
| WiFi details | 5 | `WifiName` + `WifiPassword` non-empty: 5 pts. |
| OTA connected | 10 | At least one `ChannelConfig.IsConnected = true`: 10 pts. |
| Bank account | 5 | `BankAccount` exists for property's tenant: 5 pts. |
| Active listing | 10 | At least one listing with `Status = 'Active'`: 10 pts. |
| **Total** | **100** | |

```
BaseQuality = TotalPoints / 100   (range 0.0 – 1.0)
```

- ACT-04: `BaseQuality` MUST be recomputed when any contributing field changes (on save) and cached on the property or in the ranking cache.
- ACT-05: Admin portal MUST show the completeness score as a percentage with a breakdown of missing items.

### 4.3 Photo quality score

V1 does not evaluate photo quality programmatically. Photo contribution to `BaseQuality` is count-based (section 4.2).

V2 hooks (architecture-ready, not v1):
- Image resolution check: minimum 1200x800. Below threshold: photo counts but gets 50% weight.
- Duplicate detection: perceptual hash comparison across photos in same listing. Duplicates excluded.

- ACT-06: V1: photo quality = photo count contribution. No ML, no resolution enforcement.

### 4.4 Response time metric

```
MedianResponseTimeHours = MEDIAN(
  DATEDIFF(hour, Booking.CreatedAt, FirstCommunication.SentAtUtc)
) over last 90 days
```

Where `FirstCommunication` = earliest `CommunicationLog` entry for that booking with `Status = 'Sent'`.

| Bracket | Label | UI display |
|---------|-------|-----------|
| < 1 hour | Excellent | Green badge: "Responds within 1 hour" |
| 1–4 hours | Good | Green badge: "Responds within a few hours" |
| 4–24 hours | Average | Yellow badge: "Responds within a day" |
| > 24 hours or no data | Poor / No data | No badge displayed |

- ACT-07: Response time MUST be displayed on the property detail page (marketplace guest portal).
- ACT-08: Response time badge MUST only be shown after >= 5 bookings (insufficient data otherwise).

### 4.5 Booking conversion metric

```
ConversionRate = ConfirmedBookings / PropertyDetailViews (trailing 90 days)
```

- Views tracked by `marketplace.property.viewed` log events.
- Bookings tracked by `Booking.BookingStatus = 'Confirmed'` for that property.

### 4.6 Host dashboard growth metrics

The admin portal MUST display a "Growth" or "Performance" tab for each tenant.

| Metric | Formula | Display |
|--------|---------|---------|
| Direct booking % | `MarketplaceBookings / TotalBookings * 100` | Percentage + trend (vs. previous period) |
| OTA dependency % | `OTABookings / TotalBookings * 100` | Percentage + trend |
| Commission paid (period) | `SUM(CommissionAmount) WHERE PaymentModeSnapshot = 'MARKETPLACE_SPLIT'` | INR amount |
| Boost ROI | `(MarketplaceRevenue - CommissionPaid) / CommissionPaid` | Ratio or "N/A" if no commission |
| Avg. rating | `AVG(Review.Rating)` | Stars + count |
| Completeness score | `BaseQuality * 100` | Percentage bar |
| Response time | Median (section 4.4) | Badge |

### 4.7 Nudging mechanisms

Nudges are displayed in the admin portal as contextual cards. They are NOT push notifications in v1.

| Nudge | Condition | Message | CTA |
|-------|-----------|---------|-----|
| **Increase commission** | `DefaultCommissionPercent = 1.00` (floor) AND property has been on marketplace > 30 days AND `ConversionRate < 0.02` | "Properties with higher commission get up to 3x more visibility. Consider increasing your commission to boost bookings." | "Adjust Commission" → links to commission settings |
| **Improve listing** | `BaseQuality < 0.60` | "Your listing is {score}% complete. Properties above 80% get 2x more views. Add photos and descriptions." | "Complete Listing" → links to property editor |
| **Enable marketplace** | Property exists, pricing set, `IsMarketplaceEnabled = false` | "Your property is ready for the marketplace! Enable it to start receiving direct bookings." | "Enable Marketplace" → toggles `IsMarketplaceEnabled` |
| **Connect OTA** | No `ChannelConfig` for any property | "Connect your OTA channels to sync availability and avoid double bookings." | "Connect Channels" → links to Channel Manager |
| **Upload photos** | Photo count < 3 across all listings for a property | "Properties with 3+ photos get 40% more bookings. Add more photos!" | "Upload Photos" → links to listing editor |

- ACT-09: Nudges MUST be dismissible (per tenant, per nudge key). Dismissed nudges reappear after 30 days if condition still met.
- ACT-10: Nudge display logic runs on admin portal page load (client-side checks against API response). No background workers for nudges.
- ACT-11: Nudge metrics (display count, click count, dismiss count) SHOULD be logged for future analysis.

---

## 5. Guest data & repeat booking layer

### 5.1 Guest account model (minimal friction V1)

| Aspect | V1 design | V2 evolution |
|--------|-----------|-------------|
| Registration | None. Guest provides Name + Phone + Email at checkout. | Optional OTP-based sign-in for booking history. |
| Identity | `Guest` record per-tenant. Phone is primary identifier. | `MarketplaceGuest` (cross-tenant, phone-keyed) links to per-tenant Guest records. |
| Authentication | None. | OTP via SMS/WhatsApp. |
| Profile | No guest profile page. | "My Bookings" page showing cross-tenant booking history. |
| Data storage | Existing `Guest` model (tenant-scoped). | Same + `MarketplaceGuest` bridge table. |

- GST-01: Guests MUST NOT be required to register or log in to complete a booking.
- GST-02: The checkout form MUST pre-fill Name/Phone/Email if the guest previously booked at the SAME tenant (match by cookie or phone, within tenant scope).

### 5.2 Guest history storage

V1 relies on the existing tenant-scoped `Booking` + `Guest` tables. No cross-tenant guest history in v1.

| Data point | Storage | Scope |
|-----------|---------|-------|
| Booking history | `Booking` rows linked to `Guest` | Per-tenant |
| Contact info | `Guest.Name`, `Guest.Phone`, `Guest.Email` | Per-tenant |
| Attribution | `Booking.BookingSource`, `Booking.AttributionJson` | Per-booking |
| Repeat flag | `Booking.IsRepeatGuest` (computed, bool) | Per-booking |

- GST-03: `Booking.IsRepeatGuest` MUST be set to `true` at booking creation if another confirmed marketplace booking exists for a guest with the same phone number (across ANY tenant, requires `IgnoreQueryFilters()` for the check).
- GST-04: The cross-tenant phone lookup for repeat detection MUST be read-only and MUST NOT expose any data from other tenants. It only sets a boolean flag.

### 5.3 Repeat booking detection

```sql
-- At booking creation time (pseudocode)
DECLARE @isRepeat BIT = CASE
  WHEN EXISTS (
    SELECT 1 FROM Bookings b
    JOIN Guests g ON b.GuestId = g.Id AND b.TenantId = g.TenantId
    WHERE g.Phone = @newGuestPhone
      AND b.BookingSource LIKE 'marketplace_%'
      AND b.BookingStatus IN ('Confirmed', 'CheckedIn', 'CheckedOut')
      AND b.Id != @currentBookingId
  ) THEN 1 ELSE 0 END
```

- GST-05: Repeat detection MUST match on phone number (E.164 normalised). Email is NOT used for repeat detection (guests may use different emails).
- GST-06: Only marketplace bookings count for repeat detection (admin-created or OTA bookings do not).

### 5.4 Guest segmentation model

V1 segmentation is rule-based, computed on-demand for analytics. No real-time segmentation engine.

| Segment | Definition | Use |
|---------|-----------|-----|
| First-time | `IsRepeatGuest = false` | Baseline metrics |
| Repeat | `IsRepeatGuest = true` | Repeat rate tracking |
| High-value | Total marketplace spend > INR 50,000 (across all bookings by phone) | V2: targeted campaigns |
| Dormant | Last booking > 180 days ago | V2: win-back campaigns |
| Referred | `BookingSource = 'marketplace_referral'` | Referral programme tracking |

- GST-07: Segmentation is analytics-only in V1. No real-time segment assignment on the guest record.
- GST-08: Segment queries MUST use `IgnoreQueryFilters()` and aggregate across tenants by guest phone.

### 5.5 Coupon system architecture

Existing `PromoCode` model supports tenant-scoped coupons with `Code`, `DiscountType` (Percent/Flat), `DiscountValue`, `ValidFrom`, `ValidTo`, `UsageLimit`, `TimesUsed`, `ListingId`, `IsActive`.

**Marketplace extensions (V1):**

| Feature | Implementation |
|---------|---------------|
| Platform-wide coupons | New: `PromoCode.Scope` = `'tenant'` (existing) or `'marketplace'`. Marketplace coupons created by Atlas Admin, TenantId = null (not tenant-scoped). |
| First-booking discount | Marketplace coupon: `Code = 'WELCOME10'`, `DiscountType = 'Percent'`, `DiscountValue = 10`, `Scope = 'marketplace'`. Guest eligibility: `IsRepeatGuest = false`. |
| Referral coupon | Generated per referral link: `Code = 'REF-{hash}'`, auto-created. See section 5.6. |

| ID | Requirement |
|----|-------------|
| CPN-01 | Marketplace coupons MUST be validated at checkout: scope check, date range, usage limit, eligibility (first-time vs. repeat). |
| CPN-02 | Coupon discount MUST be applied BEFORE commission calculation. Commission is computed on the discounted `FinalAmount`. |
| CPN-03 | If `FinalAmount` after discount is <= 0, the booking MUST be rejected (no free bookings via coupon). Minimum: INR 1. |
| CPN-04 | Coupon usage MUST be incremented only on successful booking confirmation (not on order creation). |
| CPN-05 | Marketplace coupons MUST bypass the TenantId query filter (they are platform-level). Implement as `PromoCode` rows with `TenantId = 0` or a sentinel, excluded from the tenant filter. |

### 5.6 Referral system V1

**Host-side referral:**

| Aspect | Implementation |
|--------|---------------|
| Mechanism | Host shares their property URL: `atlashomestays.com/{property-slug}?ref={tenantSlug}`. |
| Attribution | `ref` param stored in session cookie. If booking occurs, `BookingSource = 'marketplace_host_share'`. |
| Reward | V1: no monetary reward for host. The reward is the direct booking itself (saving OTA commission). V2: credit-based reward. |

**Guest-side referral:**

| Aspect | Implementation |
|--------|---------------|
| Mechanism | After booking confirmation, guest sees "Share with friends" with a referral link: `atlashomestays.com?promo=REF-{shortHash}`. |
| Referral coupon | `PromoCode` created: `Code = 'REF-{shortHash}'`, `Scope = 'marketplace'`, `DiscountType = 'Percent'`, `DiscountValue = 10` (configurable: `Growth:ReferralDiscountPercent`), `UsageLimit = 5`, `ValidTo = +90 days`. |
| Referred guest booking | Guest enters coupon at checkout. `BookingSource = 'marketplace_referral'`. |
| Referrer reward | V1: no reward for the referring guest. V2: credit on next booking. |

- REF-01: Referral coupon generation MUST be idempotent per guest phone. Same guest gets the same referral code on repeat requests.
- REF-02: Referral codes MUST be short (8 chars), URL-safe, collision-resistant.
- REF-03: Referral programme MUST be feature-flag gated: `growth.referral.enabled` (default: false until Growth Launch phase).

### 5.7 Privacy-safe storage practices

| Data | Classification | Retention | Access control |
|------|---------------|-----------|----------------|
| Guest Name | PII | As long as booking exists | Tenant-scoped (tenant sees their own guests only) |
| Guest Phone | PII, primary identifier | As long as booking exists | Tenant-scoped. Cross-tenant repeat detection returns boolean only, not the other tenant's data. |
| Guest Email | PII | As long as booking exists | Tenant-scoped |
| Guest ID proof URL | Sensitive PII | 90 days post-checkout, then SHOULD be deleted | Tenant-scoped |
| AttributionJson | Non-PII (UTM params) | As long as booking exists | Tenant-scoped |
| Session cookie (`atlas_utm`) | Non-PII | 30-day expiry | Client-side, HttpOnly |

| ID | Requirement |
|----|-------------|
| PRV-01 | Guest PII MUST NOT be exposed in marketplace public APIs. Property pages show review author first name only (e.g. "Rahul M."). |
| PRV-02 | Guest PII MUST NOT be stored in logs. Structured logs may include GuestId (integer) but MUST NOT include phone or email. |
| PRV-03 | Cross-tenant phone lookup for repeat detection MUST NOT return or log the matching tenant or booking details. Only a boolean result. |
| PRV-04 | V2: implement `Guest.DataDeletionRequestedAt` for GDPR/DPDP Act compliance. On request: anonymise name, hash phone/email, retain booking financials. |

---

## 6. Demand monetization controls

### 6.1 Commission model flexibility

All commission parameters MUST be config-driven (not hardcoded). Reads via `IOptions<CommissionSettings>`.

| Config key | V1 default | Type | Description |
|------------|:----------:|------|-------------|
| `Commission:SystemDefaultPercent` | 1.00 | decimal | Rate for tenants who haven't explicitly set theirs |
| `Commission:FloorPercent` | 1.00 | decimal | Absolute minimum |
| `Commission:CeilingPercent` | 20.00 | decimal | Absolute maximum |
| `Commission:PropertyOverrideEnabled` | true | bool | Kill-switch for property-level overrides |
| `Commission:CooldownHours` | 24 | int | Minimum hours between commission changes |
| `Commission:DampingDays` | 7 | int | Days for ranking to fully reflect a commission change |

### 6.2 Promotional commission windows

Atlas Admin can create time-limited commission promotions for demand stimulation.

| Entity | Fields | Purpose |
|--------|--------|---------|
| `CommissionPromotion` (new, platform-level) | `Id`, `Name`, `DiscountPercent`, `StartsAtUtc`, `EndsAtUtc`, `Scope` (`global`, `city`, `tenant`), `ScopeValue` (city slug or tenantId), `IsActive` | Temporarily reduce effective commission for participants |

```
EffectiveCommission = MAX(
  Floor,
  ResolvedRate - ActivePromotion.DiscountPercent
)
```

| ID | Requirement |
|----|-------------|
| MON-01 | Promotional discounts MUST NOT push effective commission below the floor. |
| MON-02 | Multiple promotions MUST NOT stack. If overlapping, the one with the highest discount applies. |
| MON-03 | Promotions MUST be forward-looking only. Existing booking snapshots are immutable. |
| MON-04 | V1 simplification: `Tenant.CommissionDiscountPercent` (decimal, default 0) as a manual override. Promotion entity is V2. |

### 6.3 Sponsored placement model (future)

Not V1. Architecture hooks only.

| Concept | Design |
|---------|--------|
| Sponsored position | A property can pay extra for a guaranteed top-N slot in search results for a city. |
| Implementation | `SponsoredPlacement` entity: PropertyId, CitySlug, Position (1-3), StartsAtUtc, EndsAtUtc, PaidAmount, Status. |
| Ranking interaction | Sponsored properties are inserted at their purchased position, pushed above organic results. Labelled "Sponsored" in UI. |
| Revenue | One-time or weekly flat fee (not commission-based). |

- MON-05: V1 MUST NOT implement sponsored placements. The ranking result array MUST be structured to support inserting sponsored items at specific positions in V2 (array of `{property, isSponsored, position}`).

### 6.4 City-level commission experiments

From RA-002 section 6.5 — elaborated here for growth context.

| Experiment | Config | Example |
|-----------|--------|---------|
| City-specific floor | `CityCommissionConfig.FloorPercent` | Goa: floor 2% (higher demand, justified higher take-rate) |
| City-specific ceiling | `CityCommissionConfig.CeilingPercent` | Mumbai: ceiling 15% (smaller average booking, cap lower) |
| Launch discount per city | `CommissionPromotion` with `Scope = 'city'` | New city launch: 0% commission for 90 days |

- MON-06: City-level config MUST override global config but MUST NOT override tenant/property-level settings that are above the city floor.
- MON-07: V1: no `CityCommissionConfig` table. V2: add table. V1 alternative: use `CommissionPromotion` with city scope for experiments.

### 6.5 A/B testing framework (config-based)

No dedicated A/B testing infrastructure in V1. Use feature flags + cohort assignment.

| Mechanism | Implementation |
|-----------|---------------|
| Cohort assignment | Deterministic hash: `Hash(tenantId or guestPhone) % 100`. Percentile ranges define cohorts. |
| Experiment definition | `appsettings.json` (or env var override): `Experiments:{name}:Enabled`, `Experiments:{name}:CohortStart`, `Experiments:{name}:CohortEnd`, `Experiments:{name}:Variant`. |
| Ranking weight test | Override `Ranking:W_Commission` for cohort A (e.g. 0.25 → 0.30). Compare conversion rates. |
| Commission elasticity test | Offer different `CommissionPromotion.DiscountPercent` to different city cohorts. |

| ID | Requirement |
|----|-------------|
| EXP-01 | Experiment cohort assignment MUST be deterministic (same tenant always in same cohort for the experiment's duration). |
| EXP-02 | Experiment results MUST be measurable via analytics queries (section 8). No real-time dashboards in V1. |
| EXP-03 | All experiment config MUST be changeable without code deploy (env var or `appsettings.json` override). |

### 6.6 Conversion rate tracking

| Event | Tracked by | Storage |
|-------|-----------|---------|
| Property view | Structured log: `marketplace.property.viewed` with `{propertyId, source, sessionId}` | Log / analytics table |
| Booking initiated | `booking.payment.initiated` | Existing outbox |
| Booking confirmed | `booking.confirmed` | Existing outbox |
| Booking abandoned | View event with no corresponding booking within 24 hours | Computed by daily batch |

```
PropertyConversionRate = ConfirmedBookings / PropertyViews (90-day trailing)
CityConversionRate    = CityBookings / CityPageViews (90-day trailing)
OverallConversion     = TotalBookings / TotalVisits (30-day trailing)
```

- CVR-01: Conversion rates MUST be recomputed daily by a background job (no real-time).
- CVR-02: Conversion rate is used as a ranking signal (section 3.3). A property with 0 views gets neutral score (0.50).

---

## 7. Data network effects strategy

### 7.1 Reviews improve ranking

```
Virtuous cycle:
  More bookings → more reviews → higher ReviewScore → higher ranking → more bookings
```

| Mechanism | Implementation |
|-----------|---------------|
| Review prompt | After checkout: notification (WhatsApp/email) via outbox asking guest to review. Sent 24 hours after `CheckedOutAtUtc`. |
| ReviewScore formula | `AVG(Rating) / 5.0`, dampened: `ReviewScore * MIN(1.0, ReviewCount / 3)`. |
| Network effect | Properties with more reviews rank higher, attracting more bookings, generating more reviews. |

- NET-01: Review prompt notification MUST be sent automatically (via existing notification pipeline, event: `BookingCheckedOut`, template: `review_request`).
- NET-02: Review submission URL in the notification MUST be a deep-link: `atlashomestays.com/{property-slug}/review?booking={bookingRef}`.

### 7.2 Booking velocity improves ranking

```
RecencyScore = 1.0 - MIN(1.0, DaysSinceLastBooking / 90)
```

| Mechanism | Implementation |
|-----------|---------------|
| Recency decay | Properties with recent bookings rank higher. Score decays linearly over 90 days. |
| Velocity signal | A property with 5 bookings in 30 days has a consistently high RecencyScore. |
| Network effect | Higher ranking → more bookings → maintained recency → sustained ranking. |

- NET-03: RecencyScore MUST use only confirmed marketplace bookings (not admin-created or OTA). Self-bookings excluded per RA-002 AG-09.

### 7.3 Repeat guests improve trust score

| Signal | Impact |
|--------|--------|
| `Booking.IsRepeatGuest = true` | Repeat bookings count double toward RecencyScore: `effectiveRecency = DaysSinceLastBooking / 2` for repeat bookings. |
| Repeat rate > 20% | Future V2: "Guests love this place" badge on property page. V1: metric tracked but no badge. |

- NET-04: Repeat guest weighting in RecencyScore MUST be configurable: `Ranking:RepeatGuestMultiplier` (default 2.0).

### 7.4 Response time affects exposure

From section 4.4 and RA-002 section 4.3:

| Response bracket | Ranking penalty multiplier |
|-----------------|:-------------------------:|
| < 1 hour | 1.00 (no penalty) |
| 1–4 hours | 0.98 |
| 4–24 hours | 0.95 |
| > 24 hours | 0.90 |
| No data (< 5 bookings) | 1.00 (no penalty) |

- NET-05: Response time penalty MUST be applied as a multiplier on the final ranking score (not a component weight).

### 7.5 Feedback loop between commission & performance

```
Higher commission → better ranking position → more views → more bookings
  → better conversion rate → even higher ranking → host sees ROI → maintains commission
```

**Safeguards against artificial inflation (from RA-002 section 2, summarised):**

| Guardrail | Mechanism |
|-----------|-----------|
| 24-hour cooldown | Commission changes limited to once per 24 hours |
| 7-day damping | Ranking boost ramps linearly over 7 days for new commission rate |
| Commission weight cap | CommissionBoost is max 20-25% of total score (config-driven) |
| Quality floor | BaseQuality < 0.50 → CommissionBoost forced to 0 |
| Self-booking exclusion | Bookings by tenant's own contact excluded from RecencyScore |
| Review validation | Reviews require completed booking; one per guest per booking |
| Frequency alert | > 3 commission changes in 7 days → flagged for admin review |

- NET-06: The system MUST log all ranking score components per property per computation cycle (structured log: `ranking.computed` with `{propertyId, baseQuality, commissionBoost, reviewScore, recencyScore, availabilityScore, conversionRate, finalScore}`). This enables auditing the feedback loop.

---

## 8. Analytics & reporting requirements

### 8.1 Tenant analytics (admin portal)

The admin portal "Performance" tab MUST show these metrics for the logged-in tenant.

| Metric | Formula | Period | Display |
|--------|---------|--------|---------|
| **Direct booking revenue** | `SUM(FinalAmount) WHERE BookingSource LIKE 'marketplace_%'` | Selectable: 7d / 30d / 90d / custom | INR total + trend sparkline |
| **OTA booking revenue** | `SUM(FinalAmount) WHERE BookingSource = 'ota_channex'` | Same | INR total + trend |
| **Commission paid** | `SUM(CommissionAmount) WHERE PaymentModeSnapshot = 'MARKETPLACE_SPLIT'` | Same | INR total |
| **Effective commission rate** | `AVG(CommissionPercentSnapshot)` for period | Same | Percentage |
| **Boost impact** | `CommissionBoost component value` for each property | Current | Score bar (0-1) per property |
| **Marketplace views** | Count of `marketplace.property.viewed` events | Same | Number + trend |
| **Conversion rate** | Bookings / Views | Same | Percentage |
| **Traffic source breakdown** | Bookings grouped by `BookingSource` | Same | Pie chart or horizontal bar |
| **Average rating** | `AVG(Review.Rating)` | All time | Stars + count |
| **Completeness score** | `BaseQuality * 100` per property | Current | Percentage bar |
| **Response time** | Median response hours | Trailing 90d | Badge label |

- ANA-01: All tenant analytics MUST be scoped to the tenant's own data (EF Core filters apply automatically).
- ANA-02: View counts come from structured logs. V1: store daily aggregates in a `PropertyViewDaily` table (PropertyId, Date, ViewCount) updated by a daily batch job. V2: real-time event store.
- ANA-03: Sparkline trends show current vs. previous period (e.g. last 30d vs. 30d before that). Up/down arrow + percentage change.

### 8.2 Atlas Admin analytics (platform dashboard)

| Metric | Formula | Frequency |
|--------|---------|-----------|
| **Gross Merchandise Value (GMV)** | `SUM(FinalAmount)` for all marketplace bookings | Daily refresh |
| **Commission revenue** | `SUM(CommissionAmount)` for all MARKETPLACE_SPLIT bookings | Daily refresh |
| **Active tenants** | Tenants with at least 1 confirmed booking in last 30 days | Daily refresh |
| **Total tenants** | Count of all tenants | Real-time |
| **Marketplace adoption %** | Tenants with >= 1 `IsMarketplaceEnabled` property / Total tenants | Daily refresh |
| **Boost adoption %** | Tenants where `DefaultCommissionPercent > 1.00` or any property has override / Marketplace-adopted tenants | Daily refresh |
| **Average commission rate** | `AVG(CommissionPercentSnapshot)` across all marketplace bookings in period | Daily refresh |
| **Marketplace GMV growth** | GMV current month / GMV previous month | Monthly |
| **Top cities** | GMV grouped by `Property.City`, top 10 | Daily refresh |
| **Failed settlements** | Count where `SettlementStatus = 'Failed'` | Real-time (query) |
| **Properties indexed** | Count where `IsMarketplaceEnabled = true AND Status = 'Active'` | Real-time (query) |

### 8.3 Data refresh expectations

| Data type | Refresh strategy | Latency |
|-----------|-----------------|:-------:|
| Booking financials | Real-time (query on demand) | < 5s |
| View counts / funnel | Daily batch aggregation | 24 hours |
| Ranking scores | Batch recompute every 15 min | 15 min |
| Conversion rates | Daily batch | 24 hours |
| Commission reports | Daily batch | 24 hours |
| Tenant growth charts | Daily batch | 24 hours |

- ANA-04: Admin dashboard MUST display the data freshness timestamp: "Data as of {timestamp}".
- ANA-05: Financial figures (GMV, commission) MUST be query-time accurate (no stale cache for monetary values).
- ANA-06: View/funnel metrics MAY have 24-hour latency and MUST be labelled "Updated daily".

---

## 9. Growth experiments framework

### 9.1 Feature flag system

V1 uses a config-driven feature flag system. No third-party feature flag service.

| Flag | Config key | Default | Scope | Purpose |
|------|-----------|:-------:|-------|---------|
| Marketplace public | `Features:MarketplacePublic` | false | Global | Master switch for public marketplace |
| Marketplace search | `Features:MarketplaceSearch` | false | Global | Search endpoint availability |
| Ranking engine | `Features:RankingEngine` | false | Global | Use ranked results vs. alphabetical |
| Commission boost | `Features:CommissionBoost` | false | Global | Whether commission affects ranking |
| Guest referral | `Features:GuestReferral` | false | Global | Referral link + coupon generation |
| City pages | `Features:CityPages` | false | Global | Auto-generated city SEO pages |
| Review prompts | `Features:ReviewPrompts` | false | Global | Automated review request notifications |
| Repeat detection | `Features:RepeatDetection` | false | Global | Cross-tenant repeat guest detection |
| Newness boost | `Features:NewnessBoost` | true | Global | 14-day new property ranking boost |
| Sponsored placement | `Features:SponsoredPlacement` | false | Global | V2 placeholder |

| ID | Requirement |
|----|-------------|
| FLG-01 | Feature flags MUST be readable via `IOptions<FeatureSettings>` or `IConfiguration`. |
| FLG-02 | Feature flags MUST be overridable via environment variables (for Azure App Service Configuration). |
| FLG-03 | Toggling a feature flag MUST NOT require a code deployment. App Service config update + restart suffices. |
| FLG-04 | When a feature is off, its associated API endpoints MUST return 404 (not 500 or partial data). |

### 9.2 Experiment cohorts

| Concept | Implementation |
|---------|---------------|
| Cohort assignment | `Hash(subjectId) % 100` produces a percentile (0-99). Experiment config defines percentile ranges per variant. |
| Subject | Tenant (for supply-side experiments) or Guest phone hash (for demand-side experiments). |
| Stability | Same subject always in same percentile. Changing experiment ranges moves cohort boundaries, not subject assignments. |

**Example experiment definition:**

```json
{
  "Experiments": {
    "RankingWeightTest": {
      "Enabled": true,
      "Subject": "tenant",
      "Variants": {
        "control": { "Start": 0, "End": 49 },
        "higher_commission_weight": { "Start": 50, "End": 99 }
      },
      "Overrides": {
        "higher_commission_weight": {
          "Ranking:W_Commission": 0.30,
          "Ranking:W_Base": 0.20
        }
      }
    }
  }
}
```

- EXP-04: Experiment variant MUST be resolved at request time and included in structured logs for all ranking and booking events (`experiment:{name}:variant:{variantName}`).
- EXP-05: No more than 2 experiments SHOULD run simultaneously to avoid interaction effects.

### 9.3 Ranking weight testing

| Test | Hypothesis | Metric | Duration |
|------|-----------|--------|----------|
| Higher commission weight (0.25 → 0.30) | Higher-commission properties convert better, increasing Atlas revenue without hurting guest satisfaction | Commission revenue per booking, guest satisfaction (rating) | 30 days |
| Lower commission weight (0.25 → 0.15) | Quality-first ranking improves guest conversion | Overall conversion rate, repeat rate | 30 days |
| Remove newness boost | Properties earn ranking purely on performance | New property booking velocity | 14 days |

### 9.4 Commission elasticity testing

| Test | Design | Metric |
|------|--------|--------|
| Price sensitivity | Offer 0% commission promo in one city, 1% in control city | New tenant signups, marketplace adoption rate |
| Boost ROI perception | Show "Boost ROI" metric prominently in one cohort, hidden in control | Commission increase rate, average commission level |
| Floor increase | Raise floor from 1% to 2% for new tenants in one city | Tenant signup rate, churn rate, revenue per booking |

### 9.5 City pilot strategy

New cities are launched in stages to validate demand before full marketing spend.

| Stage | Criteria | Actions |
|-------|---------|---------|
| Scout | Atlas identifies target city with >= 5 interested hosts | Manual outreach. PMS onboarding. No marketplace. |
| Seed | >= 5 marketplace-enabled properties in the city | Enable city page (`/{city-slug}`). Soft SEO launch. |
| Grow | >= 10 properties + first organic booking | Enable paid ads for the city. Enable city-specific commission promo if needed. |
| Established | >= 30 properties + > 100 bookings/month | Full marketing. Include in homepage featured cities. |

- PIL-01: City pilot transitions MUST be Atlas Admin decisions (manual flag per city, no auto-transition).
- PIL-02: Pilot city metrics MUST be trackable in the admin dashboard (filter analytics by city).

---

## 10. Definition of Done for marketplace growth V1

This checklist MUST be fully satisfied before declaring marketplace growth features ready.

### SEO live

- [ ] City pages are generated for all cities with >= 1 marketplace property.
- [ ] Property pages are accessible at `/{property-slug}` for all marketplace-enabled properties.
- [ ] `<title>`, `<meta description>`, and Open Graph tags are populated on all marketplace pages.
- [ ] JSON-LD structured data (`LodgingBusiness`, `aggregateRating`, `offers`) renders on property pages.
- [ ] `sitemap-index.xml` is generated and accessible. Contains city and property sitemaps.
- [ ] `robots.txt` references the sitemap.
- [ ] Google Search Console has sitemap submitted (manual step).
- [ ] Canonical tags are correct on all pages (verified via crawler).
- [ ] Core Web Vitals pass on mobile for property and city pages (LCP < 2.5s, CLS < 0.1).
- [ ] Pages that should be `noindex` (search, pre-launch) have the correct meta robots tag.

### Search stable

- [ ] `GET /marketplace/properties` returns correct results for location, date, guest, price filters.
- [ ] Sort modes work: `recommended`, `price_low`, `price_high`, `rating`, `newest`.
- [ ] Pagination returns consistent results (no duplicates, no missing properties across pages).
- [ ] Empty result states display gracefully ("No stays found. Try a different location or dates.").
- [ ] Autocomplete for city names works.
- [ ] Search response time < 1000ms (p95, cache miss).
- [ ] Availability filter correctly excludes properties with conflicting bookings.

### Ranking consistent

- [ ] Ranking formula produces deterministic, repeatable results for the same data.
- [ ] Weights sum to 1.0 (validated on startup).
- [ ] Cold-start defaults apply correctly for new properties (neutral scores, newness boost).
- [ ] CommissionBoost is suppressed when BaseQuality < 0.50 or average rating < 2.5.
- [ ] 24-hour cooldown prevents rapid commission changes from affecting ranking.
- [ ] 7-day damping gradually applies new commission boost.
- [ ] Response time and cancellation rate penalties apply correctly.
- [ ] Ranking batch job completes within 60 seconds for current property count.

### Commission boost measurable

- [ ] Tenant admin portal shows Boost Impact per property (score visualisation).
- [ ] Tenant admin portal shows Commission Paid and Effective Rate for period.
- [ ] Nudge card appears for tenants at floor commission with low conversion.
- [ ] Commission change audit log is populated correctly.

### Analytics accurate

- [ ] Tenant dashboard shows: direct revenue, OTA revenue, commission paid, views, conversion, traffic sources.
- [ ] Atlas Admin dashboard shows: GMV, commission revenue, active tenants, marketplace adoption %, boost adoption %, top cities.
- [ ] Funnel metrics (visit → search → view → book → repeat) are tracked and computable.
- [ ] `BookingSource` is correctly set on all marketplace bookings.
- [ ] Attribution JSON captures UTM parameters from query strings.
- [ ] Data freshness labels are displayed.

### First 100 properties indexed

- [ ] >= 100 marketplace-enabled, active properties exist in the database.
- [ ] These properties span >= 3 cities.
- [ ] All 100 properties have at least 1 listing with pricing.
- [ ] All 100 property pages are accessible and return HTTP 200.
- [ ] All 100 properties appear in the sitemap.
- [ ] City pages exist for all cities represented.
- [ ] Google Search Console shows indexing progress (may take days; check coverage report).

### Infrastructure and operations

- [ ] All feature flags are documented and toggled to correct states for launch.
- [ ] `Features:MarketplacePublic = true`, `Features:MarketplaceSearch = true`, `Features:RankingEngine = true`.
- [ ] `Features:CommissionBoost = true`.
- [ ] `Features:CityPages = true`.
- [ ] Daily aggregation job for view counts and conversion rates runs without errors.
- [ ] Ranking batch job runs every 15 minutes without errors.
- [ ] Sitemap regeneration runs daily without errors.
- [ ] Memory cache eviction is functioning (no stale data > configured TTL).

---

## Glossary

| Term | Definition |
|------|-----------|
| **GMV (Gross Merchandise Value)** | Total value of bookings transacted through the marketplace (FinalAmount sum). |
| **Cold start** | The period when a new property has insufficient data (reviews, bookings) for accurate ranking. |
| **Newness boost** | Temporary ranking bonus for newly marketplace-enabled properties (14-day default). |
| **Cohort** | A group of tenants or guests assigned to the same experiment variant via deterministic hash. |
| **Attribution** | Tracking which marketing channel (SEO, paid, referral, direct) led to a booking. |
| **Funnel** | The stages a guest passes through: Visit → Search → View → Book → Repeat. |
| **BaseQuality** | A 0–1 score derived from property/listing completeness (photos, description, pricing, etc.). |
| **Damping** | Gradual ramp-up of ranking boost over 7 days after a commission change, preventing gaming. |
| **ISR (Incremental Static Regeneration)** | Technique where static pages are regenerated on-demand or on a schedule, combining SSG performance with dynamic content. |
| **Flywheel** | A self-reinforcing growth loop where each component's output feeds the next (supply → demand → reviews → ranking → more supply). |
| **Network effect** | The phenomenon where the marketplace becomes more valuable as more hosts and guests participate. |
