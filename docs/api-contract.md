# Atlas API Contract

## Base URL
- **Production**: `https://atlas-homes-api-gxdqfjc2btc0atbv.centralus-01.azurewebsites.net`
- **Local (placeholder)**: `https://localhost:<port>`

**Non-production path base:** In non-production environments, the application uses `UsePathBase("/api")` (see `Atlas.Api/Program.cs`). This means local base URLs should include `/api` (for example: `https://localhost:<port>/api`). Endpoints that already include an `api/` route prefix will therefore be reachable at `/api/api/...` locally.

## Authentication / Authorization
- JWT authentication middleware is present in `Atlas.Api/Program.cs` but **commented out**, and `UseAuthentication()`/`UseAuthorization()` are not enabled. As coded, endpoints do **not** require authentication.
- The only explicit authorization attribute is `[AllowAnonymous]` on `AdminReportsController` (`Atlas.Api/Controllers/AdminReportsController.cs`).

## Conventions
- **Validation errors**: Several endpoints call `ValidationProblem(ModelState)` which produces `ProblemDetails` responses (usually `application/problem+json`) when model or business rules fail. Other errors sometimes return plain strings (see multiple controllers).
- **Filtering**: No global pagination/sorting. Endpoint-specific filters are documented under each endpoint.
- **Swagger**: Swagger UI is enabled at `/swagger` (per `Atlas.Api/Program.cs`), but this document is derived directly from code.

## Endpoints

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
  - `include` (string?, optional; supports `guest`)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<BookingListDto>>` (booking summaries)
- **Status codes**: 200, 500 (not explicitly annotated)

#### `GET /bookings/{id}`
- **Purpose**: Get a booking by id.
- **Request body**: none
- **Response**: `ActionResult<BookingDto>` (booking)
- **Status codes**: 200, 404, 500 (not explicitly annotated)

#### `POST /bookings`
- **Purpose**: Create a booking.
- **Request body**: `CreateBookingRequest`
  - **Required fields**: `ListingId`, `GuestId`, `CheckinDate`, `CheckoutDate`, `BookingSource`, `AmountReceived`, `GuestsPlanned`, `GuestsActual`, `ExtraGuestCharge`, `PaymentStatus` (non-nullable or `[Required]` in `Atlas.Api/DTOs/CreateBookingRequest.cs`)
- **Response**: `ActionResult<BookingDto>` (created booking)
- **Status codes**: 201, 400, 404, 500 (not explicitly annotated)

#### `PUT /bookings/{id}`
- **Purpose**: Update a booking.
- **Request body**: `UpdateBookingRequest`
  - **Required fields**: `ListingId`, `GuestId`, `CheckinDate`, `CheckoutDate`, `BookingSource`, `PaymentStatus`, `AmountReceived` (non-nullable or `[Required]` in `Atlas.Api/DTOs/UpdateBookingRequest.cs`)
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
- **Purpose**: List payments.
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<Payment>>` (payments)
- **Status codes**: 200 (not explicitly annotated)

#### `GET /api/payments/{id}`
- **Purpose**: Get a payment by id.
- **Request body**: none
- **Response**: `ActionResult<Payment>` (payment)
- **Status codes**: 200, 404 (not explicitly annotated)

#### `POST /api/payments`
- **Purpose**: Create a payment.
- **Request body**: `Payment`
  - **Required fields**: `BookingId`, `Amount`, `Method`, `Type`, `ReceivedOn`, `Note` (non-nullable/required in `Atlas.Api/Models/Payment.cs`)
- **Response**: `ActionResult<Payment>` (created payment)
- **Status codes**: 201, 400 (not explicitly annotated)

#### `PUT /api/payments/{id}`
- **Purpose**: Update a payment.
- **Request body**: `Payment`
  - **Required fields**: `Id` (route/body must match), `BookingId`, `Amount`, `Method`, `Type`, `ReceivedOn`, `Note`
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

### Reports (`Atlas.Api/Controllers/ReportsController.cs`)

#### `GET /reports/calendar-earnings`
- **Purpose**: Get calendarized booking earnings for a listing/month.
- **Query params**:
  - `listingId` (int, required)
  - `month` (string, required; format `yyyy-MM`)
- **Request body**: none
- **Response**: `ActionResult<IEnumerable<CalendarEarningEntry>>` (calendar entries with earnings)
- **Status codes**: 200, 400 (invalid month), 500 (not explicitly annotated)

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
