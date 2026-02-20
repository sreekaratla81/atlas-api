# Atlas API – API Coverage Audit (DB → FE needs)

**Context:** Single-developer project; minimal changes; industry-standard best practices. All code changes must update relevant documentation (api-contract.md and in-repo docs). Respect test vs prod (InMemory/LocalDb vs SQL Server; cascade in tests vs restrict in prod; inline seeding; rollback for isolation).

**Canonical inputs:** `docs/db-schema.md`, `docs/api-contract.md`, and live code (Controllers, DTOs, Models, AppDbContext, tenant middleware).

---

## Step 1 – FE-driven API inventory

For each FE surface, endpoints the FE will need and why:

### Table: Admin Portal

| # | Needed endpoint / capability | Why |
|---|------------------------------|-----|
| 1 | **Properties** – List, Get, Create, Update, Delete | Admin CRUD for properties. |
| 2 | **Listings** – List, Get, Create, Update, Delete | Admin CRUD for listings. |
| 3 | **Guests** – List, Get, Create, Update, Delete | Admin CRUD for guests. |
| 4 | **Bookings** – List (with filters), Get, Create, Update, Delete, Cancel, CheckIn, CheckOut | Admin CRUD + lifecycle; list needs date range + listingId + bookingId filters. |
| 5 | **Payments** – List (with filters), Get, Create, Update, Delete | Admin CRUD; list needs bookingId/date filters for grids. |
| 6 | **BankAccounts** – List, Get, Create, Update, Delete | Admin CRUD. |
| 7 | **Users** – List, Get, Create, Update, Delete | Admin user management. |
| 8 | **Admin Calendar** – GET availability (daily inventory + daily rate + blocks), PUT bulk upsert (inventory + price overrides) | Daily availability, price overrides, inventory in bulk (ListingDailyInventory, ListingDailyRate, AvailabilityBlock). |
| 9 | **Tenant pricing settings** – GET, PUT (convenience fee %, global discount %) | TenantPricingSettings management. |
| 10 | **MessageTemplate** – List, Get, Create, Update, Delete (or at least List + Get + Update) | Message template management. |
| 11 | **CommunicationLog** – List with filters (bookingId, guestId, date range, channel, status) + optional pagination | Communication logs list/search for support. |
| 12 | **AutomationSchedule** – List with filters (bookingId, status, eventType), optional Retry/Cancel actions | Automation schedules list and operational actions. |
| 13 | **OutboxMessage** – Read-only list with filters (tenant, date, status) + optional pagination | Ops diagnostics for outbox. |
| 14 | **Admin Reports** – Bookings, Listings, Payouts, Monthly earnings, Daily payouts, Bookings by source, Calendar | Already covered by AdminReportsController. |
| 15 | **Incidents** – List, Get, Create, Update, Delete | Admin incidents (existing). |

### Table: Guest Portal

| # | Needed endpoint / capability | Why |
|---|------------------------------|-----|
| 1 | **Public listings** – List (tenant-scoped, no internal fields) | Discovery; must not expose WifiPassword, internal IDs, etc. |
| 2 | **Availability search** – By property + date range + guests | Check availability and get nightly rates. |
| 3 | **Pricing breakdown** – By listing + checkIn + checkOut | Show base, discount, fee, final amount. |
| 4 | **Quote** – Issue (POST), Validate (GET) | Quote issue/validate flow. |
| 5 | **Razorpay** – Create order (POST), Verify payment (POST) → booking creation | Create order + verify → booking. |
| 6 | **Booking status** – Get by id; optionally Get by externalReservationId | Confirmation page (by id or by external ref). |

---

## Step 2 – Existing vs missing (API Gap Map)

| Needed endpoint / capability | Exists? | Existing route/controller | Missing pieces | Priority | Notes / assumptions |
|-----------------------------|--------|---------------------------|----------------|----------|----------------------|
| **Admin: Properties CRUD** | Yes | `GET/POST/PUT/DELETE /properties`, PropertiesController | None | — | Document that TenantId is server-managed. |
| **Admin: Listings CRUD** | Yes | `GET/POST/PUT/DELETE /listings`, ListingsController | None | — | Same. |
| **Admin: Guests CRUD** | Yes | `GET/POST/PUT/DELETE /guests`, GuestsController | None | — | Same. |
| **Admin: Bookings CRUD + lifecycle** | Yes | `GET/POST/PUT/DELETE /bookings`, `POST .../cancel`, `.../checkin`, `.../checkout`, BookingsController | List has checkinStart/checkinEnd and include=guest; **missing** listingId, bookingId query filters; no pagination | P1 | Add optional listingId, bookingId to GET /bookings. |
| **Admin: Payments list + CRUD** | Partial | `GET/POST/PUT/DELETE /api/payments`, PaymentsController | **Missing** filters: bookingId, date range; returns raw Payment entity (consider DTO); no pagination | P1 | Add bookingId, receivedFrom, receivedTo (or similar) to GET. |
| **Admin: BankAccounts CRUD** | Yes | `GET/POST/PUT/DELETE /bankaccounts`, BankAccountsController | None | — | DTOs already used. |
| **Admin: Users CRUD** | Yes | `GET/POST/PUT/DELETE /api/users`, UsersController | None | — | TenantId server-managed. |
| **Admin: Calendar GET + PUT** | Yes | `GET/PUT /admin/calendar/availability`, AdminCalendarController | Idempotency-Key on PUT (doc says 409 on reuse); otherwise complete | — | Document Idempotency-Key. |
| **Admin: Tenant pricing settings** | Yes | `GET/PUT /tenant/settings/pricing`, TenantPricingSettingsController | None | — | — |
| **Admin: MessageTemplate** | **No** | — | **Missing** full CRUD (or at least List, Get, Update). Tenant-scoped; DTOs; no TenantId in body | P0 | New controller + DTOs. |
| **Admin: CommunicationLog list** | **No** | — | **Missing** GET list with filters (bookingId, guestId, from/to, channel, status), pagination, DTO (no sensitive payload) | P1 | New endpoint(s). |
| **Admin: AutomationSchedule list** | **No** | — | **Missing** GET list with filters (bookingId, status, eventType); optional Retry/Cancel (POST) if needed | P2 | New controller or admin sub-route. |
| **Admin: OutboxMessage read-only** | **No** | — | **Missing** GET list (ops-only) with filters (date, status), pagination; DTO without full payload if needed | P2 | Ops-only; consider /ops/outbox. |
| **Admin: Reports** | Yes | AdminReportsController | None | — | — |
| **Admin: Incidents CRUD** | Yes | `GET/POST/PUT/DELETE /api/incidents` | None | — | — |
| **Guest: Public listings** | Partial | `GET /listings/public`, ListingsController | **Returns full Listing entity** (includes WifiPassword, etc.). Should return a **safe DTO** (no wifi password, minimal internal fields) | P0 | New DTO + endpoint or change response shape. |
| **Guest: Availability search** | Yes | `GET /availability` (propertyId, checkIn, checkOut, guests) | None | — | — |
| **Guest: Pricing breakdown** | Yes | `GET /pricing/breakdown` | None | — | — |
| **Guest: Quote issue/validate** | Yes | `POST /quotes`, `GET /quotes/validate?token=...` | None | — | — |
| **Guest: Razorpay order + verify** | Yes | `POST /api/Razorpay/order`, `POST /api/Razorpay/verify` | None | — | — |
| **Guest: Booking by id** | Yes | `GET /bookings/{id}` | None | — | — |
| **Guest: Booking by externalReservationId** | **No** | — | **Missing** e.g. `GET /bookings/by-reference?externalReservationId=...` (or route param) for confirmation page | P0 | Tenant-scoped; return same BookingDto shape; 404 if not found. |

---

## Step 3 – Design rules for new/missing endpoints

- **Tenant scoping:** Enforce via existing tenant resolution (X-Tenant-Slug header; dev API host fallback for dev only) + EF global filters. Never accept TenantId in request body.
- **Responses:** Return DTOs for new endpoints; do not expose internal fields (e.g. WifiPassword on public listings).
- **Filtering/pagination:** Add date-range, listingId, bookingId where FE grids need them; pagination optional but preferred for logs/lists.
- **Validation:** Use ProblemDetails/ValidationProblem; use 400/404/409/422 consistently.
- **Idempotency:** Support Idempotency-Key header where required (e.g. bulk upserts, payment-related flows); return 409 on reuse with different payload.
- **Routes:** Keep current conventions (some under /api/*, most at root). Do not add a new global path base.

---

## Step 4 – Proposed missing endpoints (specs)

### 1. Public listings (guest) – safe response

- **Option A (recommended):** New route `GET /listings/public/v2` (or keep `GET /listings/public`) returning a **PublicListingDto** (id, name, propertyId, property name/address, floor, type, status, maxGuests, checkInTime, checkOutTime; **exclude** WifiName, WifiPassword, TenantId, and internal-only fields).
- **Option B:** Change existing `GET /listings/public` to return PublicListingDto and update api-contract + clients.

**Request:** None (tenant from X-Tenant-Slug header).  
**Response:** `200` array of PublicListingDto.  
**Status codes:** 200, 500.

---

### 2. Booking by external reservation id (guest)

- **Route:** `GET /bookings/by-reference?externalReservationId={value}` (or `GET /bookings/external/{externalReservationId}`).
- **Query/route:** Single parameter externalReservationId (required).
- **Response:** Same BookingDto as `GET /bookings/{id}`; 404 if not found or tenant mismatch.
- **Status codes:** 200, 400 (missing param), 404, 500.

---

### 3. MessageTemplate CRUD (admin)

- **Routes:**
  - `GET /api/message-templates` – List (optional filters: eventType, channel, isActive). Optional pagination (page, pageSize).
  - `GET /api/message-templates/{id}` – Get one.
  - `POST /api/message-templates` – Create (body: DTO without TenantId).
  - `PUT /api/message-templates/{id}` – Update.
  - `DELETE /api/message-templates/{id}` – Delete.
- **Request DTO (create/update):** TemplateKey, EventType, Channel, ScopeType, ScopeId, Language, TemplateVersion, IsActive, Subject, Body. No TenantId.
- **Response DTO:** Same shape + Id, TenantId (for admin display only), CreatedAtUtc, UpdatedAtUtc.
- **Status codes:** 200/201/204, 400, 404, 422.

---

### 4. CommunicationLog list (admin)

- **Route:** `GET /api/communication-logs` with query params: bookingId (optional), guestId (optional), fromUtc (optional), toUtc (optional), channel (optional), status (optional), page (optional), pageSize (optional).
- **Response:** Array of CommunicationLogDto (Id, TenantId, BookingId, GuestId, Channel, EventType, ToAddress, TemplateId, Status, AttemptCount, CreatedAtUtc, SentAtUtc; exclude or redact LastError/ProviderMessageId if sensitive).
- **Status codes:** 200, 400, 500.

---

### 5. AutomationSchedule list (admin)

- **Route:** `GET /api/automation-schedules` with query params: bookingId (optional), status (optional), eventType (optional), page (optional), pageSize (optional).
- **Response:** Array of AutomationScheduleDto (Id, TenantId, BookingId, EventType, DueAtUtc, Status, PublishedAtUtc, CompletedAtUtc, AttemptCount; optional LastError for admin).
- **Optional later:** `POST /api/automation-schedules/{id}/retry`, `POST /api/automation-schedules/{id}/cancel` (P2).
- **Status codes:** 200, 500.

---

### 6. OutboxMessage list – ops only (admin)

- **Route:** `GET /ops/outbox` with query params: fromUtc (optional), toUtc (optional), published (optional bool), page (optional), pageSize (optional).
- **Response:** Array of OutboxMessageDto (Id, TenantId, Topic, EntityId, EventType, CreatedAtUtc, PublishedAtUtc, AttemptCount; optionally LastError; PayloadJson/HeadersJson optional or truncated for size).
- **Status codes:** 200, 500.

---

### 7. Bookings list – extra filters (admin)

- **Existing:** `GET /bookings` with checkinStart, checkinEnd, include.
- **Add:** Optional query params listingId (int?), bookingId (int?) to filter by listing or single booking id.
- **No new route;** extend existing GET /bookings.

---

### 8. Payments list – filters (admin)

- **Existing:** `GET /api/payments` (no filters).
- **Add:** Optional query params bookingId (int?), receivedFrom (DateTime?), receivedTo (DateTime?). Optional page, pageSize.
- **Consider:** Return PaymentListDto (or existing entity) with consistent shape; avoid leaking internal-only fields to unintended callers.

---

## Summary: Gap list and priorities

| Priority | Item | Action |
|----------|------|--------|
| P0 | Public listings safe DTO | Add PublicListingDto; change or add GET /listings/public response. |
| P0 | Booking by externalReservationId | Add GET /bookings/by-reference?externalReservationId=... (or equivalent). |
| P0 | MessageTemplate CRUD | New controller + DTOs; List, Get, Create, Update, Delete. |
| P1 | Bookings list filters | Add listingId, bookingId to GET /bookings. |
| P1 | Payments list filters | Add bookingId, receivedFrom, receivedTo (and optional pagination) to GET /api/payments. |
| P1 | CommunicationLog list | New GET /api/communication-logs with filters + pagination + DTO. |
| P2 | AutomationSchedule list | New GET /api/automation-schedules with filters + DTO. |
| P2 | OutboxMessage list (ops) | New GET /ops/outbox with filters + DTO. |

---

## Step 4 – Approval gate

**Approve implementation? (Yes/No).** — **Approved and implemented.**

---

## Implementation summary (completed)

### Files changed

**New files**
- `Atlas.Api/DTOs/PublicListingDto.cs`
- `Atlas.Api/DTOs/MessageTemplateDtos.cs`
- `Atlas.Api/DTOs/CommunicationLogDtos.cs`
- `Atlas.Api/DTOs/AutomationScheduleDtos.cs`
- `Atlas.Api/DTOs/OutboxMessageDtos.cs`
- `Atlas.Api/Controllers/MessageTemplatesController.cs`
- `Atlas.Api/Controllers/CommunicationLogsController.cs`
- `Atlas.Api/Controllers/AutomationSchedulesController.cs`
- `Atlas.Api.IntegrationTests/MessageTemplatesApiTests.cs`
- `Atlas.Api.IntegrationTests/CommunicationLogsApiTests.cs`
- `Atlas.Api.IntegrationTests/AutomationSchedulesApiTests.cs`

**Modified files**
- `Atlas.Api/Controllers/ListingsController.cs` — GET /listings/public returns `PublicListingDto`
- `Atlas.Api/Controllers/BookingsController.cs` — GET /bookings/by-reference; GET /bookings gains listingId, bookingId
- `Atlas.Api/Controllers/PaymentsController.cs` — GET /api/payments gains bookingId, receivedFrom, receivedTo, page, pageSize
- `Atlas.Api/Controllers/OpsController.cs` — GET /ops/outbox
- `Atlas.Api.IntegrationTests/ListingsApiTests.cs` — GetPublic_ReturnsPublicListingDto_WithoutWifiPassword
- `Atlas.Api.IntegrationTests/BookingsApiTests.cs` — GetByReference_*, GetAll_WithListingId_*, GetAll_WithBookingId_*
- `Atlas.Api.IntegrationTests/PaymentsApiTests.cs` — GetAll_WithBookingId_*, GetAll_WithPagination
- `Atlas.Api.IntegrationTests/OpsApiTests.cs` — GetOutbox_*
- `docs/api-contract.md` — New and updated endpoint descriptions
- `docs/api-examples.http` — New sample requests

### New endpoints summary

| Method | Route | Purpose |
|--------|--------|--------|
| GET | /listings/public | Public listings (safe DTO, no wifi) |
| GET | /bookings/by-reference?externalReservationId= | Booking by external id |
| GET | /bookings | + query: listingId, bookingId |
| GET | /api/payments | + query: bookingId, receivedFrom, receivedTo, page, pageSize |
| GET/POST/PUT/DELETE | /api/message-templates | Message template CRUD |
| GET | /api/communication-logs | List with filters + pagination |
| GET | /api/automation-schedules | List with filters + pagination |
| GET | /ops/outbox | Read-only outbox list (ops) |

### How to run tests

```bash
cd atlas-api
dotnet test Atlas.Api.IntegrationTests/Atlas.Api.IntegrationTests.csproj
```

To run only the new API-gap tests:

```bash
dotnet test Atlas.Api.IntegrationTests/Atlas.Api.IntegrationTests.csproj --filter "FullyQualifiedName~MessageTemplatesApiTests|FullyQualifiedName~CommunicationLogsApiTests|FullyQualifiedName~AutomationSchedulesApiTests|FullyQualifiedName~OpsApiTests.GetOutbox|FullyQualifiedName~ListingsApiTests.GetPublic_|FullyQualifiedName~BookingsApiTests.GetByReference|FullyQualifiedName~BookingsApiTests.GetAll_With|FullyQualifiedName~PaymentsApiTests.GetAll_With"
```
