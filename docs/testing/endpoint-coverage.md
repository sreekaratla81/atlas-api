# Endpoint Test Coverage

This matrix tracks happy-path and failure-path coverage for each contract endpoint documented in [../api-contract.md](../api-contract.md). "Smoke" refers to fast unit-style checks (Atlas.Api.Tests); "Nightly" refers to integration/API-level coverage (Atlas.Api.IntegrationTests).

| Endpoint | Happy test | Failure test | Suite (Smoke/Nightly) | Notes |
| --- | --- | --- | --- | --- |
| GET /availability | `AvailabilityApiTests.Get_ReturnsAvailableListingsWithPricing` | `AvailabilityControllerTests.GetAvailability_ReturnsBadRequest_WhenDatesInvalid` | Nightly / Smoke | Happy path covered via integration; failure validated in controller unit test. |
| GET /properties | `PropertiesApiTests.GetAll_ReturnsOk` | Gap (no list failure path) | Nightly | Consider a failure-mode check (e.g., database issue) if meaningful. |
| GET /properties/{id} | Gap (no success fetch) | `PropertiesApiTests.Get_ReturnsNotFound_WhenMissing` | Nightly | Add a success read-by-id scenario. |
| POST /properties | `PropertiesApiTests.Post_CreatesProperty` | Gap (validation/duplicate failure) | Nightly | Add bad payload/validation coverage. |
| PUT /properties/{id} | `PropertiesApiTests.Put_UpdatesProperty` | `PropertiesApiTests.Put_ReturnsBadRequest_OnIdMismatch` | Nightly |  |
| DELETE /properties/{id} | `PropertiesApiTests.Delete_RemovesProperty` | `PropertiesApiTests.Delete_ReturnsNotFound_WhenMissing` | Nightly |  |
| GET /listings | `ListingsApiTests.GetAll_ReturnsOk` | Gap (no list failure path) | Nightly |  |
| GET /listings/{id} | Gap (no success fetch) | `ListingsApiTests.Get_ReturnsNotFound_WhenMissing` | Nightly | Add a happy-path read. |
| POST /listings | `ListingsApiTests.Post_CreatesListing` | Gap (validation failure) | Nightly |  |
| PUT /listings/{id} | `ListingsApiTests.Put_UpdatesListing` | `ListingsApiTests.Put_ReturnsBadRequest_OnIdMismatch` | Nightly |  |
| DELETE /listings/{id} | `ListingsApiTests.Delete_RemovesListing` | `ListingsApiTests.Delete_ReturnsNotFound_WhenMissing` | Nightly |  |
| GET /guests | `GuestsApiTests.GetAll_ReturnsOk` | Gap (no list failure path) | Nightly |  |
| GET /guests/{id} | Gap (no success fetch) | `GuestsApiTests.Get_ReturnsNotFound_WhenMissing` | Nightly | Add a happy-path read. |
| POST /guests | `GuestsApiTests.Post_CreatesGuest` | Gap (validation failure) | Nightly |  |
| PUT /guests/{id} | `GuestsApiTests.Put_UpdatesGuest` | `GuestsApiTests.Put_ReturnsBadRequest_OnIdMismatch` | Nightly |  |
| DELETE /guests/{id} | `GuestsApiTests.Delete_RemovesGuest` | `GuestsApiTests.Delete_ReturnsNotFound_WhenMissing` | Nightly |  |
| GET /bookings | `BookingsApiTests.GetAll_ReturnsOk` | Gap (no list failure path) | Nightly |  |
| GET /bookings/{id} | Gap (no success fetch) | `BookingsApiTests.Get_ReturnsNotFound_WhenMissing` | Nightly | Add a happy-path read. |
| POST /bookings | `BookingsApiTests.Post_CreatesBooking` (plus variants without notes/payment status) | `BookingsApiTests.Post_ReturnsBadRequest_WhenOverlappingConfirmedBookingExists` | Nightly | Consider adding validation failures (missing required ids). |
| PUT /bookings/{id} | `BookingsApiTests.Put_UpdatesBooking` | `BookingsApiTests.Put_ReturnsBadRequest_OnIdMismatch` | Nightly |  |
| DELETE /bookings/{id} | `BookingsApiTests.Delete_RemovesBooking` | `BookingsApiTests.Delete_ReturnsNotFound_WhenMissing` | Nightly |  |
| POST /bookings/{id}/cancel | `BookingsApiTests.Post_Cancel_UpdatesStatusAndAvailability` | Gap (e.g., missing booking) | Nightly | Add not-found/invalid-state coverage. |
| POST /bookings/{id}/checkin | `BookingsApiTests.Post_CheckIn_UpdatesStatusAndTimestamp` | Gap (e.g., missing booking) | Nightly |  |
| POST /bookings/{id}/checkout | `BookingsApiTests.Post_CheckOut_UpdatesStatusAndTimestamp` | Gap (e.g., missing booking) | Nightly |  |
| GET /bankaccounts | `BankAccountsApiTests.GetAll_ReturnsOk` | Gap (no list failure path) | Nightly |  |
| GET /bankaccounts/{id} | Gap (no success fetch) | `BankAccountsApiTests.Get_ReturnsNotFound_WhenMissing` | Nightly | Add a happy-path read. |
| POST /bankaccounts | `BankAccountsApiTests.Post_CreatesAccount` | Gap (validation failure) | Nightly |  |
| PUT /bankaccounts/{id} | `BankAccountsApiTests.Put_UpdatesAccount` | `BankAccountsApiTests.Put_ReturnsNotFound_WhenMissing` | Nightly |  |
| DELETE /bankaccounts/{id} | `BankAccountsApiTests.Delete_RemovesAccount` | `BankAccountsApiTests.Delete_ReturnsNotFound_WhenMissing` | Nightly |  |
| GET /api/payments | `PaymentsApiTests.GetAll_ReturnsOk` | Gap (no list failure path) | Nightly |  |
| GET /api/payments/{id} | `PaymentsApiTests.Get_ReturnsOk` | `PaymentsApiTests.Get_ReturnsNotFound_WhenMissing` | Nightly |  |
| POST /api/payments | `PaymentsApiTests.Post_CreatesPayment` | Gap (validation failure) | Nightly |  |
| PUT /api/payments/{id} | `PaymentsApiTests.Put_UpdatesPayment` | `PaymentsApiTests.Put_ReturnsBadRequest_OnIdMismatch` | Nightly |  |
| DELETE /api/payments/{id} | `PaymentsApiTests.Delete_RemovesPayment` | `PaymentsApiTests.Delete_ReturnsNotFound_WhenMissing` | Nightly |  |
| GET /api/incidents | `IncidentsApiTests.GetAll_ReturnsOk` | Gap (no list failure path) | Nightly |  |
| GET /api/incidents/{id} | Gap (no success fetch) | `IncidentsApiTests.Get_ReturnsNotFound_WhenMissing` | Nightly | Add a happy-path read. |
| POST /api/incidents | `IncidentsApiTests.Post_CreatesIncident` | Gap (validation failure) | Nightly |  |
| PUT /api/incidents/{id} | `IncidentsApiTests.Put_UpdatesIncident` | `IncidentsApiTests.Put_ReturnsBadRequest_OnIdMismatch` | Nightly |  |
| DELETE /api/incidents/{id} | `IncidentsApiTests.Delete_RemovesIncident` | `IncidentsApiTests.Delete_ReturnsNotFound_WhenMissing` | Nightly |  |
| GET /api/users | `UsersApiTests.GetAll_ReturnsOk` | Gap (no list failure path) | Nightly |  |
| GET /api/users/{id} | Gap (no success fetch) | `UsersApiTests.Get_ReturnsNotFound_WhenMissing` | Nightly | Add a happy-path read. |
| POST /api/users | `UsersApiTests.Post_CreatesUser` | Gap (validation failure) | Nightly |  |
| PUT /api/users/{id} | `UsersApiTests.Put_UpdatesUser` | `UsersApiTests.Put_ReturnsBadRequest_OnIdMismatch` | Nightly |  |
| DELETE /api/users/{id} | `UsersApiTests.Delete_RemovesUser` | `UsersApiTests.Delete_ReturnsNotFound_WhenMissing` | Nightly |  |
| GET /reports/calendar-earnings | `ReportsApiTests.GetCalendarEarnings_ReturnsOk` | Gap (invalid month/filters) | Nightly | Additional error-path coverage needed. |
| GET /reports/bank-account-earnings | `ReportsApiTests.GetBankAccountEarnings_ReturnsOk` | Gap (no failure coverage) | Nightly |  |
| GET /admin/reports/bookings | `AdminReportsApiTests.GetBookings_ReturnsOk` | Gap (no failure coverage) | Nightly |  |
| GET /admin/reports/listings | `AdminReportsApiTests.GetListings_ReturnsOk` | Gap (no failure coverage) | Nightly |  |
| GET /admin/reports/payouts | `AdminReportsApiTests.GetPayouts_ReturnsOk` | Gap (no failure coverage) | Nightly |  |
| GET /admin/reports/earnings/monthly | `AdminReportsApiTests.GetMonthlyEarnings_ReturnsOk` | Gap (no failure coverage) | Nightly |  |
| POST /admin/reports/earnings/monthly | `AdminReportsApiTests.PostMonthlyEarnings_ReturnsOk` | Gap (invalid filter) | Nightly |  |
| GET /admin/reports/payouts/daily | `AdminReportsApiTests.GetDailyPayout_ReturnsOk` | Gap (no failure coverage) | Nightly |  |
| GET /admin/reports/bookings/source | `AdminReportsApiTests.GetBookingSource_ReturnsOk` | Gap (no failure coverage) | Nightly |  |
| GET /admin/reports/bookings/calendar | `AdminReportsApiTests.GetCalendarBookings_ReturnsOk` | Gap (no failure coverage) | Nightly |  |
| OPTIONS /test-cors | Gap (no coverage) | Gap (no coverage) | — | Add smoke test verifying 200 response. |
