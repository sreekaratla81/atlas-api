# Atlas API Contract

This document is derived from the codebase and is maintained for use as **AI context** (e.g. ChatGPT, Cursor). Keep it in sync when adding or changing controllers or DTOs. See `api-examples.http` for runnable examples.

## Base URL
- **Production**: `https://atlas-homes-api-gxdqfjc2btc0atbv.centralus-01.azurewebsites.net`
- **Local**: `https://localhost:<port>` (e.g. 5001)

**Path conventions:** The app does **not** use a global path base. Most routes are under the root (e.g. `/properties`, `/listings`, `/bookings`, `/availability`, `/admin/reports`). A few controllers use the `api/` prefix in their route: **Payments** (`/api/payments`), **Incidents** (`/api/incidents`), **Users** (`/api/users`), **Razorpay** (`/api/Razorpay`). When calling locally, use base `https://localhost:<port>` and append the path (e.g. `GET /properties`, `GET /api/payments`).

## Authentication / Authorization
- JWT authentication middleware is present in `Atlas.Api/Program.cs` but **commented out**, and `UseAuthentication()`/`UseAuthorization()` are not enabled. As coded, endpoints do **not** require authentication.
- The only explicit authorization attribute is `[AllowAnonymous]` on `AdminReportsController` (`Atlas.Api/Controllers/AdminReportsController.cs`).

## Conventions
- **Swagger / OpenAPI**: Interactive docs at `/swagger` when not in Production. Use for exploratory testing. Swagger UI is disabled in Production.
- **Validation errors**: Several endpoints call `ValidationProblem(ModelState)` which produces `ProblemDetails` responses (usually `application/problem+json`) when model or business rules fail. Other errors sometimes return plain strings (see multiple controllers).
- **Filtering**: No global pagination/sorting. Endpoint-specific filters are documented under each endpoint.

## Endpoints

### Health
- **`GET /health`** — Liveness probe; returns 200 with `{ "status": "healthy" }`. No authentication. Use for load balancer or platform health checks.

### Operations (`Atlas.Api/Controllers/OpsController.cs`)

#### `GET /ops/db-info`
- **Purpose**: Return operational environment metadata without exposing secrets.
- **Request body**: none
- **Response**: JSON object containing `environment` (ASPNETCORE_ENVIRONMENT), `server` (SQL Server name), `database` (database name), and `marker` (value from the `EnvironmentMarker` table indicating DEV/PROD alignment).
- **Notes**: Intended for safety checks; avoids returning connection strings or credentials.
- **Status codes**: 200 on success; 500-style `ProblemDetails` if the marker record is missing.

#### `GET /ops/outbox`
- **Purpose**: Read-only list of outbox messages for ops diagnostics. Tenant-scoped via EF filters.
- **Query params**: `fromUtc` (DateTime?, optional), `toUtc` (DateTime?, optional), `published` (bool?, optional), `page` (int, default 1), `pageSize` (int, default 50, max 200)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<OutboxMessageDto>>` — `Id`, `TenantId`, `Topic`, `EntityId`, `EventType`, `CreatedAtUtc`, `PublishedAtUtc`, `AttemptCount`, `LastError` (no full payload). Legacy `AggregateType`/`AggregateId` deprecated in favor of `Topic`/`EntityId`.
- **Status codes**: 200, 500

### Availability (`Atlas.Api/Controllers/AvailabilityController.cs`)

#### `GET /availability`
- **Purpose**: Get availability for a property within a date range.
- **Query params**:
  - `propertyId` (int, required)
  - `checkIn` (DateTime, required)
  - `checkOut` (DateTime, required)
  - `guests` (int, required)
- **Request body**: none
- **Response**: `ActionResult<AvailabilityResponseDto>` (availability summary with listings and nightly rates)
- **Status codes**: 200, 400, 404, 500 (not explicitly annotated; inferred from code paths)

#### `GET /availability/listing-availability`
- **Purpose**: Get listing availability for a date range (e.g. for calendar UI).
- **Query params**: `listingId` (int, required), `startDate` (DateTime, required), `months` (int, optional, default 2, 1–12)
- **Request body**: none
- **Response**: `ActionResult<ListingAvailabilityResponseDto>`
- **Status codes**: 200, 400, 500

#### `POST /availability/blocks`
- **Purpose**: Block availability for a listing in a date range (creates/removes blocks; overlapping blocked periods return 422).
- **Request body**: `AvailabilityBlockRequestDto` with `ListingId`, `StartDate`, `EndDate`
- **Response**: 200 with `{ message, blockedDates }` or 422 on conflict, 500 on error

#### `PATCH /availability/update-inventory`
- **Purpose**: Set inventory (available/blocked) for a single listing date.
- **Query params**: `listingId` (int), `date` (DateTime), `inventory` (bool)
- **Response**: 200 OK or 400/404/500

### Properties (`Atlas.Api/Controllers/PropertiesController.cs`)

#### `GET /properties`
- **Purpose**: List all properties.
- **Query params**: none
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<Property>>` (property list)
- **Status codes**: 200, 500 (not explicitly annotated)

#### `GET /properties/{id}`
- **Purpose**: Get a property by id.
- **Query params**: none
- **Request body**: none
- **Response**: `ActionResult<Property>` (property)
- **Status codes**: 200, 404 (not explicitly annotated)

#### `POST /properties`
- **Purpose**: Create a property.
- **Request body**: `Property`
  - **Required fields**: `Name`, `Address`, `Type`, `OwnerName`, `ContactPhone`, `Status` (required properties in `Atlas.Api/Models/Property.cs`)
- **Response**: `ActionResult<Property>` (created property)
- **Status codes**: 201, 400, 500 (not explicitly annotated)

#### `PUT /properties/{id}`
- **Purpose**: Update a property.
- **Request body**: `Property`
  - **Required fields**: `Id` (route/body must match), `Name`, `Address`, `Type`, `OwnerName`, `ContactPhone`, `Status`
- **Response**: `IActionResult`
- **Status codes**: 204, 400, 404, 500 (not explicitly annotated)

#### `DELETE /properties/{id}`
- **Purpose**: Delete a property.
- **Request body**: none
- **Response**: `IActionResult`
- **Status codes**: 204, 404, 500 (not explicitly annotated)

### Listings (`Atlas.Api/Controllers/ListingsController.cs`)

#### `GET /listings`
- **Purpose**: List all listings (includes `Property`).
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<Listing>>` (listing list)
- **Status codes**: 200, 500 (not explicitly annotated)

#### `GET /listings/{id}`
- **Purpose**: Get a listing by id (includes `Property`).
- **Request body**: none
- **Response**: `ActionResult<Listing>` (listing)
- **Status codes**: 200, 404, 500 (not explicitly annotated)

#### `GET /listings/public`
- **Purpose**: List listings for public/guest-facing use (tenant-scoped). Returns a safe DTO only; excludes WifiName, WifiPassword, TenantId, and internal-only fields.
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<PublicListingDto>>` — each item has `Id`, `PropertyId`, `PropertyName`, `PropertyAddress`, `Name`, `Floor`, `Type`, `CheckInTime`, `CheckOutTime`, `Status`, `MaxGuests`.
- **Status codes**: 200, 500 (not explicitly annotated)

#### `POST /listings`
- **Purpose**: Create a listing.
- **Request body**: `Listing`
  - **Required fields**: `PropertyId`, `Property`, `Name`, `Type`, `Status`, `WifiName`, `WifiPassword`, `Floor`, `MaxGuests` (required/non-nullable properties in `Atlas.Api/Models/Listing.cs`)
- **Response**: `ActionResult<Listing>` (created listing)
- **Status codes**: 201, 400, 500 (not explicitly annotated)

#### `PUT /listings/{id}`
- **Purpose**: Update a listing.
- **Request body**: `Listing`
  - **Required fields**: `Id` (route/body must match), `PropertyId`, `Property`, `Name`, `Type`, `Status`, `WifiName`, `WifiPassword`, `Floor`, `MaxGuests`
- **Response**: `IActionResult`
- **Status codes**: 204, 400, 404, 500 (not explicitly annotated)

#### `DELETE /listings/{id}`
- **Purpose**: Delete a listing.
- **Request body**: none
- **Response**: `IActionResult`
- **Status codes**: 204, 404, 500 (not explicitly annotated)

### Guests (`Atlas.Api/Controllers/GuestsController.cs`)

#### `GET /guests`
- **Purpose**: List all guests.
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<Guest>>` (guest list)
- **Status codes**: 200 (not explicitly annotated)

#### `GET /guests/{id}`
- **Purpose**: Get a guest by id.
- **Request body**: none
- **Response**: `ActionResult<Guest>` (guest)
- **Status codes**: 200, 404 (not explicitly annotated)

#### `POST /guests`
- **Purpose**: Create a guest.
- **Request body**: `Guest`
  - **Required fields**: `Name`, `Phone`, `Email` (required properties in `Atlas.Api/Models/Guest.cs`)
- **Response**: `ActionResult<Guest>` (created guest)
- **Status codes**: 201, 400 (not explicitly annotated)

#### `PUT /guests/{id}`
- **Purpose**: Update a guest.
- **Request body**: `Guest`
  - **Required fields**: `Id` (route/body must match), `Name`, `Phone`, `Email`
- **Response**: `IActionResult`
- **Status codes**: 204, 400, 404 (not explicitly annotated)

#### `DELETE /guests/{id}`
- **Purpose**: Delete a guest.
- **Request body**: none
- **Response**: `IActionResult`
- **Status codes**: 204, 404 (not explicitly annotated)

### Bookings (`Atlas.Api/Controllers/BookingsController.cs`)

#### `GET /bookings`
- **Purpose**: List bookings with optional filters.
- **Query params**:
  - `checkinStart` (DateTime?, optional)
  - `checkinEnd` (DateTime?, optional)
  - `listingId` (int?, optional)
  - `bookingId` (int?, optional; when set, returns at most one booking)
  - `include` (string?, optional; supports `guest`)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<BookingListDto>>` (booking summaries)
- **Status codes**: 200, 500 (not explicitly annotated)

#### `GET /bookings/by-reference`
- **Purpose**: Get a booking by external reservation id (e.g. for guest confirmation page). Tenant-scoped.
- **Query params**: `externalReservationId` (string, required)
- **Request body**: none
- **Response**: `ActionResult<BookingDto>` (same shape as `GET /bookings/{id}`); 404 if not found or tenant mismatch.
- **Status codes**: 200, 400 (missing param), 404, 500

#### `GET /bookings/{id}`
- **Purpose**: Get a booking by id.
- **Request body**: none
- **Response**: `ActionResult<BookingDto>` (booking)
- **Status codes**: 200, 404, 500 (not explicitly annotated)

#### `POST /bookings`
- **Purpose**: Create a booking.
- **Request body**: `CreateBookingRequest`
  - **Required**: `ListingId`, `GuestId`, `CheckinDate`, `CheckoutDate`, `BookingSource`, `AmountReceived`, `GuestsPlanned`, `GuestsActual`, `ExtraGuestCharge`, `PaymentStatus`
  - **Optional**: `BookingStatus`, `TotalAmount`, `Currency`, `ExternalReservationId`, `ConfirmationSentAtUtc`, `RefundFreeUntilUtc`, `BankAccountId`, `Notes`, and other datetime fields
- **Response**: `ActionResult<BookingDto>` (created booking)
- **Status codes**: 201, 400, 404, 500 (not explicitly annotated)

#### `PUT /bookings/{id}`
- **Purpose**: Update a booking.
- **Request body**: `UpdateBookingRequest`
  - **Required**: `Id` (match route), `ListingId`, `GuestId`, `CheckinDate`, `CheckoutDate`, `BookingSource`, `PaymentStatus`, `AmountReceived`
  - **Optional**: `BookingStatus`, `TotalAmount`, `Currency`, `BankAccountId`, `GuestsPlanned`, `GuestsActual`, `ExtraGuestCharge`, `CommissionAmount`, `Notes`, and other datetime fields
- **Response**: `IActionResult`
- **Status codes**: 204, 400, 404, 500 (not explicitly annotated)

#### `DELETE /bookings/{id}`
- **Purpose**: Delete a booking.
- **Request body**: none
- **Response**: `IActionResult`
- **Status codes**: 204, 404, 500 (not explicitly annotated)

#### `POST /bookings/{id}/cancel`
- **Purpose**: Cancel a booking.
- **Request body**: none
- **Response**: `ActionResult<BookingDto>` (updated booking)
- **Status codes**: 200, 400, 404, 500 (not explicitly annotated)

#### `POST /bookings/{id}/checkin`
- **Purpose**: Mark a booking as checked in.
- **Request body**: none
- **Response**: `ActionResult<BookingDto>` (updated booking)
- **Status codes**: 200, 400, 404, 500 (not explicitly annotated)

#### `POST /bookings/{id}/checkout`
- **Purpose**: Mark a booking as checked out.
- **Request body**: none
- **Response**: `ActionResult<BookingDto>` (updated booking)
- **Status codes**: 200, 400, 404, 500 (not explicitly annotated)

### Bank Accounts (`Atlas.Api/Controllers/BankAccountsController.cs`)

#### `GET /bankaccounts`
- **Purpose**: List bank accounts.
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<BankAccountResponseDto>>` (bank accounts)
- **Status codes**: 200 (not explicitly annotated)

#### `GET /bankaccounts/{id}`
- **Purpose**: Get a bank account by id.
- **Request body**: none
- **Response**: `ActionResult<BankAccountResponseDto>` (bank account)
- **Status codes**: 200, 404 (not explicitly annotated)

#### `POST /bankaccounts`
- **Purpose**: Create a bank account.
- **Request body**: `BankAccountRequestDto`
  - **Required fields**: `BankName`, `AccountNumber`, `IFSC`, `AccountType` (non-nullable properties in `Atlas.Api/DTOs/BankAccountRequestDto.cs`)
- **Response**: `ActionResult<BankAccountResponseDto>` (created bank account)
- **Status codes**: 201, 400 (not explicitly annotated)

#### `PUT /bankaccounts/{id}`
- **Purpose**: Update a bank account.
- **Request body**: `BankAccountRequestDto`
  - **Required fields**: `BankName`, `AccountNumber`, `IFSC`, `AccountType`
- **Response**: `IActionResult`
- **Status codes**: 204, 400, 404 (not explicitly annotated)

#### `DELETE /bankaccounts/{id}`
- **Purpose**: Delete a bank account.
- **Request body**: none
- **Response**: `IActionResult`
- **Status codes**: 204, 404 (not explicitly annotated)

### Payments (`Atlas.Api/Controllers/PaymentsController.cs`)

#### `GET /api/payments`
- **Purpose**: List payments with optional filters and pagination.
- **Query params**: `bookingId` (int?, optional), `receivedFrom` (DateTime?, optional), `receivedTo` (DateTime?, optional), `page` (int, default 1), `pageSize` (int, default 100, max 500)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<Payment>>` (payments, ordered by ReceivedOn descending)
- **Status codes**: 200 (not explicitly annotated)

#### `GET /api/payments/{id}`
- **Purpose**: Get a payment by id.
- **Request body**: none
- **Response**: `ActionResult<Payment>` (payment)
- **Status codes**: 200, 404 (not explicitly annotated)

#### `POST /api/payments`
- **Purpose**: Create a payment.
- **Request body**: `Payment`
  - **Required**: `BookingId`, `Amount`, `Method`, `Type`, `ReceivedOn`
  - **Optional**: `Note`, `RazorpayOrderId`, `RazorpayPaymentId`, `RazorpaySignature`, `Status` (default `pending`). `TenantId` is set server-side from tenant context.
- **Response**: `ActionResult<Payment>` (created payment)
- **Status codes**: 201, 400 (not explicitly annotated)

#### `PUT /api/payments/{id}`
- **Purpose**: Update a payment.
- **Request body**: `Payment`
  - **Required**: `Id` (route/body must match), `BookingId`, `Amount`, `Method`, `Type`, `ReceivedOn`
  - **Optional**: `Note`, Razorpay fields, `Status`
- **Response**: `IActionResult`
- **Status codes**: 204, 400, 404 (not explicitly annotated)

#### `DELETE /api/payments/{id}`
- **Purpose**: Delete a payment.
- **Request body**: none
- **Response**: `IActionResult`
- **Status codes**: 204, 404 (not explicitly annotated)

### Incidents (`Atlas.Api/Controllers/IncidentsController.cs`)

#### `GET /api/incidents`
- **Purpose**: List incidents.
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<Incident>>` (incidents)
- **Status codes**: 200 (not explicitly annotated)

#### `GET /api/incidents/{id}`
- **Purpose**: Get an incident by id.
- **Request body**: none
- **Response**: `ActionResult<Incident>` (incident)
- **Status codes**: 200, 404 (not explicitly annotated)

#### `POST /api/incidents`
- **Purpose**: Create an incident.
- **Request body**: `Incident`
  - **Required fields**: `ListingId`, `Description`, `ActionTaken`, `Status`, `CreatedBy`, `CreatedOn` (non-nullable/required in `Atlas.Api/Models/Incident.cs`)
- **Response**: `ActionResult<Incident>` (created incident)
- **Status codes**: 201, 400 (not explicitly annotated)

#### `PUT /api/incidents/{id}`
- **Purpose**: Update an incident.
- **Request body**: `Incident`
  - **Required fields**: `Id` (route/body must match), `ListingId`, `Description`, `ActionTaken`, `Status`, `CreatedBy`, `CreatedOn`
- **Response**: `IActionResult`
- **Status codes**: 204, 400, 404 (not explicitly annotated)

#### `DELETE /api/incidents/{id}`
- **Purpose**: Delete an incident.
- **Request body**: none
- **Response**: `IActionResult`
- **Status codes**: 204, 404 (not explicitly annotated)

### Users (`Atlas.Api/Controllers/UsersController.cs`)

#### `GET /api/users`
- **Purpose**: List users.
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<User>>` (users)
- **Status codes**: 200 (not explicitly annotated)

#### `GET /api/users/{id}`
- **Purpose**: Get a user by id.
- **Request body**: none
- **Response**: `ActionResult<User>` (user)
- **Status codes**: 200, 404 (not explicitly annotated)

#### `POST /api/users`
- **Purpose**: Create a user.
- **Request body**: `User`
  - **Required fields**: `Name`, `Phone`, `Email`, `PasswordHash`, `Role` (required properties in `Atlas.Api/Models/User.cs`)
- **Response**: `ActionResult<User>` (created user)
- **Status codes**: 201, 400 (not explicitly annotated)

#### `PUT /api/users/{id}`
- **Purpose**: Update a user.
- **Request body**: `User`
  - **Required fields**: `Id` (route/body must match), `Name`, `Phone`, `Email`, `PasswordHash`, `Role`
- **Response**: `IActionResult`
- **Status codes**: 204, 400, 404 (not explicitly annotated)

#### `DELETE /api/users/{id}`
- **Purpose**: Delete a user.
- **Request body**: none
- **Response**: `IActionResult`
- **Status codes**: 204, 404 (not explicitly annotated)

### Message Templates (`Atlas.Api/Controllers/MessageTemplatesController.cs`)

#### `GET /api/message-templates`
- **Purpose**: List message templates with optional filters and pagination. Tenant-scoped.
- **Query params**: `eventType` (string?, optional), `channel` (string?, optional), `isActive` (bool?, optional), `page` (int, default 1), `pageSize` (int, default 50, max 200)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<MessageTemplateResponseDto>>`
- **Status codes**: 200, 500

#### `GET /api/message-templates/{id}`
- **Purpose**: Get a message template by id.
- **Request body**: none
- **Response**: `ActionResult<MessageTemplateResponseDto>`; 404 if not found.
- **Status codes**: 200, 404, 500

#### `POST /api/message-templates`
- **Purpose**: Create a message template. TenantId is set server-side.
- **Request body**: `MessageTemplateCreateUpdateDto` — `EventType`, `Channel`, `ScopeType`, `Language`, `Body` (required); `TemplateKey`, `ScopeId`, `TemplateVersion`, `IsActive`, `Subject` (optional).
- **Response**: 201 with `MessageTemplateResponseDto`
- **Status codes**: 201, 400, 422, 500

#### `PUT /api/message-templates/{id}`
- **Purpose**: Update a message template.
- **Request body**: `MessageTemplateCreateUpdateDto` (same as POST)
- **Response**: 200 with `MessageTemplateResponseDto`
- **Status codes**: 200, 400, 404, 422, 500

#### `DELETE /api/message-templates/{id}`
- **Purpose**: Delete a message template.
- **Request body**: none
- **Response**: 204
- **Status codes**: 204, 404, 500

### Communication Logs (`Atlas.Api/Controllers/CommunicationLogsController.cs`)

#### `GET /api/communication-logs`
- **Purpose**: List communication logs with filters and pagination. Tenant-scoped.
- **Query params**: `bookingId` (int?, optional), `guestId` (int?, optional), `fromUtc` (DateTime?, optional), `toUtc` (DateTime?, optional), `channel` (string?, optional), `status` (string?, optional), `page` (int, default 1), `pageSize` (int, default 50, max 200)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<CommunicationLogDto>>` — Id, TenantId, BookingId, GuestId, Channel, EventType, ToAddress, TemplateId, TemplateVersion, Status, AttemptCount, CreatedAtUtc, SentAtUtc, LastError.
- **Status codes**: 200, 500

### Automation Schedules (`Atlas.Api/Controllers/AutomationSchedulesController.cs`)

#### `GET /api/automation-schedules`
- **Purpose**: List automation schedules with filters and pagination. Tenant-scoped.
- **Query params**: `bookingId` (int?, optional), `status` (string?, optional), `eventType` (string?, optional), `page` (int, default 1), `pageSize` (int, default 50, max 200)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<AutomationScheduleDto>>` — Id, TenantId, BookingId, EventType, DueAtUtc, Status, PublishedAtUtc, CompletedAtUtc, AttemptCount, LastError.
- **Status codes**: 200, 500

### Reports (`Atlas.Api/Controllers/ReportsController.cs`)

#### `GET /reports/calendar-earnings`
- **Purpose**: Get calendarized booking earnings for a listing/month.
- **Query params**:
  - `listingId` (int, required)
  - `month` (string, required; format `yyyy-MM`)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<CalendarEarningEntry>>` (calendar entries with earnings)
- **Status codes**: 200, 400 (invalid month), 500 (not explicitly annotated)
- **Notes**: Earnings are counted per night with the check-in date included and
  the check-out date excluded. This matches the booking calendar display.

#### `GET /reports/bank-account-earnings`
- **Purpose**: Get earnings by bank account for a fixed fiscal year.
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<BankAccountEarnings>>` (bank account earnings)
- **Status codes**: 200 (not explicitly annotated)

### Admin Reports (`Atlas.Api/Controllers/AdminReportsController.cs`)

> Marked `[AllowAnonymous]`.

#### `GET /admin/reports/bookings`
- **Purpose**: Get booking report rows filtered by date/listings.
- **Query params**:
  - `startDate` (DateTime?, optional)
  - `endDate` (DateTime?, optional)
  - `listingIds` (List<int>?, optional)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<BookingInfo>>` (booking report rows)
- **Status codes**: 200 (not explicitly annotated)

#### `GET /admin/reports/listings`
- **Purpose**: Get listing report rows.
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<ListingInfo>>` (listing report rows)
- **Status codes**: 200 (not explicitly annotated)

#### `GET /admin/reports/payouts`
- **Purpose**: Get payout report rows filtered by date/listings.
- **Query params**:
  - `startDate` (DateTime?, optional)
  - `endDate` (DateTime?, optional)
  - `listingIds` (List<int>?, optional)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<DailyPayout>>` (payout rows)
- **Status codes**: 200 (not explicitly annotated)

#### `GET /admin/reports/earnings/monthly`
- **Purpose**: Get rolling 12-month earnings summary.
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<MonthlyEarningsSummary>>` (month summaries)
- **Status codes**: 200, 500 (not explicitly annotated)

#### `POST /admin/reports/earnings/monthly`
- **Purpose**: Get 12-month earnings summary with filter.
- **Request body**: `ReportFilter`
  - **Required fields**: none (all properties nullable in `Atlas.Api/Models/Reports/ReportFilter.cs`)
- **Response**: `ActionResult<IEnumerable<MonthlyEarningsSummary>>`
- **Status codes**: 200, 500 (not explicitly annotated)

#### `GET /admin/reports/payouts/daily`
- **Purpose**: Get daily payout report rows filtered by date/listings.
- **Query params**:
  - `startDate` (DateTime?, optional)
  - `endDate` (DateTime?, optional)
  - `listingIds` (List<int>?, optional)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<DailyPayout>>`
- **Status codes**: 200 (not explicitly annotated)

#### `GET /admin/reports/bookings/source`
- **Purpose**: Get booking counts grouped by source.
- **Query params**:
  - `startDate` (DateTime?, optional)
  - `endDate` (DateTime?, optional)
  - `listingIds` (List<int>?, optional)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<SourceBookingSummary>>`
- **Status codes**: 200 (not explicitly annotated)

#### `GET /admin/reports/bookings/calendar`
- **Purpose**: Get calendar booking rows.
- **Query params**:
  - `startDate` (DateTime?, optional)
  - `endDate` (DateTime?, optional)
  - `listingIds` (List<int>?, optional)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<CalendarBooking>>`
- **Status codes**: 200 (not explicitly annotated)

### Infrastructure (`Atlas.Api/Program.cs`)

#### `OPTIONS /test-cors`
- **Purpose**: CORS preflight test endpoint.
- **Request body**: none
- **Response**: `IResult` (OK)
- **Status codes**: 200 (not explicitly annotated)

## Tenant Resolution
- Tenant is resolved in order: **1)** `X-Tenant-Slug` header, **2)** known Atlas API host on Azure (`atlas-homes-api*.azurewebsites.net`), **3)** default tenant only in Development/IntegrationTest/Testing/Local. There is no subdomain-based resolution.
- Known-host fallback: When the request is to the Atlas API Azure host (dev or prod), the default tenant is used so `/listings`, Swagger, and direct browser calls work without the header.
- In Production, requests from **unknown hosts** without the header get `400` with `{"error":"Tenant could not be resolved."}`.
- Paths that skip tenant resolution (no header needed): `/health`, `/swagger`, `/swagger/*`.

### Admin Calendar (`Atlas.Api/Controllers/AdminCalendarController.cs`)

#### `GET /admin/calendar/availability`
- **Purpose**: Return availability calendar cells for one property (optionally one listing), tenant-scoped.
- **Tenant scoping behavior**: Listing/pricing/rate/inventory/block queries are constrained by EF global filters (`TenantId`), so only rows owned by the resolved tenant are included.
- **Query params**:
  - `propertyId` (int, required, must be > 0)
  - `from` (DateTime, required; normalized to date)
  - `days` (int, optional, default `30`, must be > 0)
  - `listingId` (int, optional)
- **Response body**: `200 OK` with `AdminCalendarAvailabilityCellDto[]`
  - `date` (`date`)
  - `listingId` (`int`)
  - `roomsAvailable` (`int`; sourced from `ListingDailyInventory.RoomsAvailable`, defaults to `1`, forced to `0` when blocked)
  - `effectivePrice` (`decimal(18,2)` semantic; `priceOverride` when present, otherwise base/weekend price from `ListingPricing`)
  - `priceOverride` (`decimal(18,2)?`; nullable daily override from `ListingDailyRate.NightlyRate`)
  - `isBlocked` (`bit`/bool)
- **Validation / errors**:
  - `400` when `propertyId <= 0` or `days <= 0`
  - `404` when `listingId` is provided but not visible to the current tenant

#### `PUT /admin/calendar/availability`
- **Purpose**: Bulk upsert listing daily availability and optional price overrides, tenant-scoped.
- **Tenant scoping behavior**: `TenantId` is assigned by the DbContext tenant ownership rule; clients must not pass `tenantId` in payloads.
- **Request body**: `AdminCalendarAvailabilityBulkUpsertRequestDto`
  - `cells` (required, min length 1)
  - each cell:
    - `listingId` (`int`, required)
    - `date` (`date`, required)
    - `roomsAvailable` (`int`, required, must be `>= 0`; persisted to `ListingDailyInventory.RoomsAvailable`)
    - `priceOverride` (`decimal(18,2)?`, optional, must be `>= 0` when supplied; persisted to `ListingDailyRate.NightlyRate`, removed when omitted/null)
- **Response body**: `200 OK` with `AdminCalendarAvailabilityBulkUpsertResponseDto`
  - `updatedCells` (`int`)
  - `deduplicated` (`bool`)
  - `cells` (`AdminCalendarAvailabilityCellDto[]`)
    - each returned cell includes `roomsAvailable` (`int`) and `priceOverride` (`decimal(18,2)?`) explicitly.
- **Validation / errors**:
  - `400` for model validation failures, including negative `roomsAvailable` or negative `priceOverride`
  - `404` when one or more referenced listings are not visible to the current tenant
  - `409` when an `Idempotency-Key` is reused with a different payload hash


## Tenant Pricing Settings API

### GET `/tenant/settings/pricing`
Returns current tenant pricing configuration.

Response:
```json
{
  "convenienceFeePercent": 3.0,
  "globalDiscountPercent": 0.0,
  "updatedAtUtc": "2026-02-20T10:00:00Z",
  "updatedBy": "ops-user"
}
```

### PUT `/tenant/settings/pricing`
Updates tenant pricing configuration.

Request:
```json
{
  "convenienceFeePercent": 5,
  "globalDiscountPercent": 10,
  "updatedBy": "ops-user"
}
```

Validation: both percentages must be in `0..100`.

## Pricing API

### GET `/pricing/breakdown`
Query params: `listingId`, `checkIn`, `checkOut`.

Returns server-computed breakdown:
- `BaseAmount`
- `DiscountAmount = BaseAmount * GlobalDiscountPercent / 100`
- `ConvenienceFeeAmount = (BaseAmount - DiscountAmount) * ConvenienceFeePercent / 100` when `FeeMode=CustomerPays`
- `FinalAmount = BaseAmount - DiscountAmount + ConvenienceFeeAmount`

Rounding strategy: amount components are rounded to 2 decimal places using midpoint-away-from-zero. Razorpay order amount uses paise conversion from `FinalAmount * 100`.

## Quotes API

### POST `/quotes`
Issues signed HMAC-SHA256 quote token.

Request payload includes:
- tenant identity (resolved server-side)
- `listingId`, `checkIn`, `checkOut`, `guests`
- `quotedBaseAmount`
- `feeMode` (`CustomerPays`/`Absorb`)
- `expiresAtUtc`
- nonce (generated server-side)

### GET `/quotes/validate?token=...`
Validates signature, expiry, and tenant match.
Returns breakdown if valid.

Policy: global discount is **not** applied to quoted bookings by default unless quote is explicitly issued with `applyGlobalDiscount=true`.

## Razorpay contract changes

### POST `/api/Razorpay/order`
- Client must send booking draft or quote token.
- Client amount is ignored for pricing decisions (backward-compatible field retained).
- Server computes final amount from public pricing or validated quote.

### POST `/api/Razorpay/verify`
On successful verification, booking/payment pricing breakdown fields are persisted for audit + reconciliation.
