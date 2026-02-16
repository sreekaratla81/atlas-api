# Database Schema

This document reflects the schema defined by `AppDbContext` and the entity classes in `Atlas.Api/Models`. Indexes are defined in `AppDbContext.OnModelCreating`; see the model configuration for composite indexes (e.g. tenant + date, tenant + status).

## AutomationSchedule
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | bigint | No |
| TenantId | int | No |
| BookingId | int | No |
| EventType | varchar(50) | No |
| DueAtUtc | datetime | No |
| Status | varchar(20) | No |
| PublishedAtUtc | datetime | Yes |
| CompletedAtUtc | datetime | Yes |
| AttemptCount | int | No |
| LastError | text | Yes |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id
- BookingId → Bookings.Id (logical; no FK in schema)

**Relationships**
- Tenant-owned; many rows per tenant/booking.

## AvailabilityBlock
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | bigint | No |
| TenantId | int | No |
| ListingId | int | No |
| BookingId | int | Yes |
| StartDate | date | No |
| EndDate | date | No |
| BlockType | varchar(30) | No |
| Source | varchar(30) | No |
| Status | varchar(20) | No |
| Inventory | bit | No |
| CreatedAtUtc | datetime | No |
| UpdatedAtUtc | datetime | No |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id
- ListingId → Listings.Id
- BookingId → Bookings.Id

**Indexes**
- (TenantId, ListingId, StartDate, EndDate)
- (BookingId)

**Relationships**
- Tenant-owned; many blocks per listing; optionally one booking.

## BankAccounts
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| TenantId | int | No |
| BankName | nvarchar(100) | No |
| AccountNumber | nvarchar(50) | No |
| IFSC | nvarchar(20) | No |
| AccountType | nvarchar(50) | No |
| CreatedAt | datetime2 | No |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id

**Indexes**
- (TenantId, AccountNumber)

**Relationships**
- Tenant-owned; many per tenant.

## Bookings
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| TenantId | int | No |
| ListingId | int | No |
| GuestId | int | No |
| CheckinDate | datetime2 | No |
| CheckoutDate | datetime2 | No |
| BookingSource | varchar(50) | Yes |
| BookingStatus | varchar(20) | No |
| TotalAmount | decimal(18,2) | Yes |
| BaseAmount | decimal(18,2) | Yes |
| DiscountAmount | decimal(18,2) | Yes |
| ConvenienceFeeAmount | decimal(18,2) | Yes |
| FinalAmount | decimal(18,2) | Yes |
| PricingSource | varchar(30) | No |
| QuoteTokenNonce | varchar(50) | Yes |
| QuoteExpiresAtUtc | datetime | Yes |
| Currency | varchar(10) | No |
| ExternalReservationId | varchar(100) | Yes |
| ConfirmationSentAtUtc | datetime | Yes |
| RefundFreeUntilUtc | datetime | Yes |
| CheckedInAtUtc | datetime | Yes |
| CheckedOutAtUtc | datetime | Yes |
| CancelledAtUtc | datetime | Yes |
| PaymentStatus | nvarchar(max) | No |
| AmountReceived | decimal(18,2) | No |
| BankAccountId | int | Yes |
| GuestsPlanned | int | Yes |
| GuestsActual | int | Yes |
| ExtraGuestCharge | decimal(18,2) | Yes |
| CommissionAmount | decimal(18,2) | Yes |
| Notes | nvarchar(max) | No |
| CreatedAt | datetime2 | No |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id
- ListingId → Listings.Id
- GuestId → Guests.Id
- BankAccountId → BankAccounts.Id

**Indexes**
- (TenantId, ListingId)

**Relationships**
- Tenant-owned; many bookings per listing and per guest; optionally one bank account.

## CommunicationLog
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | bigint | No |
| TenantId | int | No |
| BookingId | int | Yes |
| GuestId | int | Yes |
| Channel | varchar(20) | No |
| EventType | varchar(50) | No |
| ToAddress | varchar(100) | No |
| TemplateId | int | Yes |
| TemplateVersion | int | No |
| CorrelationId | varchar(100) | No |
| IdempotencyKey | varchar(150) | No |
| Provider | varchar(50) | No |
| ProviderMessageId | varchar(100) | Yes |
| Status | varchar(20) | No |
| AttemptCount | int | No |
| LastError | text | Yes |
| CreatedAtUtc | datetime | No |
| SentAtUtc | datetime | Yes |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id
- BookingId → Bookings.Id
- GuestId → Guests.Id
- TemplateId → MessageTemplate.Id

**Indexes**
- Unique on IdempotencyKey
- (TenantId, BookingId)

**Relationships**
- Tenant-owned; optionally one booking, one guest, one message template.

## Guests
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| TenantId | int | No |
| Name | nvarchar(max) | No |
| Phone | nvarchar(max) | No |
| Email | nvarchar(max) | No |
| IdProofUrl | nvarchar(max) | Yes |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id

**Relationships**
- Tenant-owned; many guests per tenant.

## Incidents
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| ListingId | int | No |
| BookingId | int | Yes |
| Description | nvarchar(max) | No |
| ActionTaken | nvarchar(max) | No |
| Status | nvarchar(max) | No |
| CreatedBy | nvarchar(max) | No |
| CreatedOn | datetime2 | No |

**Primary Key**
- Id

**Foreign Keys**
- None (ListingId/BookingId not enforced as FKs in schema)

**Relationships**
- Not tenant-owned; references listing and optional booking by id.

## ListingDailyRate
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | bigint | No |
| TenantId | int | No |
| ListingId | int | No |
| Date | date | No |
| NightlyRate | decimal(18,2) | No |
| Currency | varchar(10) | No |
| Source | varchar(20) | No |
| Reason | varchar(200) | Yes |
| UpdatedByUserId | int | Yes |
| UpdatedAtUtc | datetime | No |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id
- ListingId → Listings.Id

**Indexes**
- Unique index on (TenantId, ListingId, Date)

**Relationships**
- Many daily rates belong to one tenant.
- Many daily rates belong to one listing.

## ListingPricing
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| TenantId | int | No |
| ListingId | int | No |
| BaseNightlyRate | decimal(18,2) | No |
| WeekendNightlyRate | decimal(18,2) | Yes |
| ExtraGuestRate | decimal(18,2) | Yes |
| Currency | varchar(10) | No |
| UpdatedAtUtc | datetime | No |

**Primary Key**
- ListingId

**Foreign Keys**
- TenantId → Tenants.Id
- ListingId → Listings.Id

**Indexes**
- Unique index on (TenantId, ListingId)

**Relationships**
- Each listing pricing row corresponds to one tenant-owned listing (one-to-one).

## Listings
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| TenantId | int | No |
| PropertyId | int | No |
| Name | nvarchar(max) | No |
| Floor | int | No |
| Type | nvarchar(max) | No |
| CheckInTime | nvarchar(max) | Yes |
| CheckOutTime | nvarchar(max) | Yes |
| Status | nvarchar(max) | No |
| WifiName | nvarchar(max) | No |
| WifiPassword | nvarchar(max) | No |
| MaxGuests | int | No |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id
- PropertyId → Properties.Id

**Indexes**
- Non-unique index on (TenantId, PropertyId)

**Relationships**
- Many listings belong to one tenant.
- Many listings belong to one property.

## MessageTemplate
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| TenantId | int | No |
| TemplateKey | varchar(100) | Yes |
| EventType | varchar(50) | No |
| Channel | varchar(20) | No |
| ScopeType | varchar(20) | No |
| ScopeId | int | Yes |
| Language | varchar(10) | No |
| TemplateVersion | int | No |
| IsActive | bit | No |
| Subject | varchar(200) | Yes |
| Body | text | No |
| CreatedAtUtc | datetime | No |
| UpdatedAtUtc | datetime | No |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id

**Indexes**
- (TenantId, EventType, Channel)

**Relationships**
- Tenant-owned; many templates per tenant.

## OutboxMessage
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | uniqueidentifier | No |
| TenantId | int | No |
| Topic | varchar(80) | No |
| EventType | varchar(50) | No |
| PayloadJson | text | No |
| CorrelationId | varchar(255) | Yes |
| EntityId | varchar(255) | Yes |
| OccurredUtc | datetime | No |
| SchemaVersion | int | No |
| Status | varchar(20) | No |
| NextAttemptUtc | datetime | Yes |
| CreatedAtUtc | datetime | No |
| PublishedAtUtc | datetime | Yes |
| AttemptCount | int | No |
| LastError | text | Yes |
| AggregateType | varchar(50) | Yes (legacy) |
| AggregateId | varchar(50) | Yes (legacy) |
| HeadersJson | text | Yes |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id

**Relationships**
- Tenant-owned; outbox for domain events. Topic/EntityId are primary; AggregateType/AggregateId are legacy.

## EnvironmentMarker
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| Marker | varchar(10) | No |

**Primary Key**
- Id

**Indexes**
- Unique index on Marker

**Foreign Keys**
- None

**Relationships**
- None

## Payments
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| TenantId | int | No |
| BookingId | int | No |
| Amount | decimal(18,2) | No |
| BaseAmount | decimal(18,2) | Yes |
| DiscountAmount | decimal(18,2) | Yes |
| ConvenienceFeeAmount | decimal(18,2) | Yes |
| Method | nvarchar(50) | No |
| Type | nvarchar(20) | No |
| ReceivedOn | datetime2 | No |
| Note | nvarchar(500) | Yes |
| RazorpayOrderId | nvarchar(100) | Yes |
| RazorpayPaymentId | nvarchar(100) | Yes |
| RazorpaySignature | nvarchar(200) | Yes |
| Status | nvarchar(20) | No |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id
- BookingId → Bookings.Id

**Indexes**
- (TenantId, BookingId)

**Relationships**
- Tenant-owned; many payments per booking.

## Properties
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| TenantId | int | No |
| Name | nvarchar(max) | No |
| Address | nvarchar(max) | No |
| Type | nvarchar(max) | No |
| OwnerName | nvarchar(max) | No |
| ContactPhone | nvarchar(max) | No |
| CommissionPercent | decimal(5,2) | Yes |
| Status | nvarchar(max) | No |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id

**Relationships**
- Tenant-owned; many properties per tenant.

## Users
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| TenantId | int | No |
| Name | nvarchar(max) | No |
| Phone | nvarchar(max) | No |
| Email | nvarchar(max) | No |
| PasswordHash | nvarchar(max) | No |
| Role | nvarchar(max) | No |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id

**Relationships**
- Tenant-owned; many users per tenant.

## Multi-tenant additions
- Core domain tables include a non-null `TenantId` column and enforce tenant isolation through EF Core global query filters.
- Tenant-owned entities: `Properties`, `Listings`, `Bookings`, `Guests`, `Payments`, `ListingPricing`, `ListingDailyRate`, `ListingDailyInventory`, `AvailabilityBlock`, `BankAccounts`, `Users`, `MessageTemplate`, `CommunicationLog`, `OutboxMessage`, `AutomationSchedule`, `TenantPricingSettings`, `QuoteRedemption`. `Incidents` is not tenant-owned.
- `TenantId` is automatically populated on insert and validated on update in `SaveChanges`/`SaveChangesAsync`.
- `TenantId` is used in uniqueness constraints where tenant-scoped uniqueness is required to prevent cross-tenant collisions.

## Tenants
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| Name | varchar(100) | No |
| Slug | varchar(50) | No |
| Status | varchar(20) | No |
| CreatedAtUtc | datetime | No |

**Primary Key**
- Id

**Unique Indexes**
- `IX_Tenants_Slug` (Slug)

**Relationships**
- One tenant has many rows across tenant-owned domain tables through `TenantId` (canonical table name: `Tenants`).

## ListingDailyInventory
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | bigint | No |
| TenantId | int | No |
| ListingId | int | No |
| Date | date | No |
| RoomsAvailable | int | No |
| Source | varchar(20) | No |
| Reason | varchar(200) | Yes |
| UpdatedByUserId | int | Yes |
| UpdatedAtUtc | datetime | No |

**Primary Key**
- Id

**Foreign Keys**
- TenantId → Tenants.Id
- ListingId → Listings.Id

**Unique Indexes**
- `UX_ListingDailyInventory_TenantId_ListingId_Date` (`TenantId`, `ListingId`, `Date`)

**Relationships**
- Many daily inventory rows belong to one tenant.
- Many daily inventory rows belong to one listing.

## Calendar query indexes
The admin calendar endpoints rely on tenant-scoped predicates and date windows. Key indexes are:
- `Listings`: (`TenantId`, `PropertyId`) for property-level listing lookup per tenant.
- `ListingPricing`: unique (`TenantId`, `ListingId`) for per-listing base/weekend rate resolution.
- `ListingDailyRate`: unique (`TenantId`, `ListingId`, `Date`) for per-day price overrides.
- `ListingDailyInventory`: unique (`TenantId`, `ListingId`, `Date`) for per-day inventory overrides.
- `AvailabilityBlock`: (`TenantId`, `ListingId`, `StartDate`, `EndDate`) for overlap checks against requested date ranges.

## TenantPricingSettings

Tenant-scoped pricing knobs used by public pricing and quoted bookings.

| Column | Type | Null | Notes |
|---|---|---|---|
| TenantId | int | No | PK; FK → Tenants.Id (one row per tenant) |
| ConvenienceFeePercent | decimal(5,2) | No | Default `3.00` |
| GlobalDiscountPercent | decimal(5,2) | No | Default `0.00` |
| UpdatedAtUtc | datetime | No | Default `GETUTCDATE()` |
| UpdatedBy | varchar(100) | Yes | Audit field |

**Indexes**
- Unique on TenantId

## QuoteRedemption

One-time quote nonce redemption table with tenant-safe replay protection.

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | bigint | No | PK identity |
| TenantId | int | No | FK → Tenants.Id |
| Nonce | varchar(50) | No | Unique with TenantId (`UNIQUE (TenantId, Nonce)`) |
| RedeemedAtUtc | datetime | No | Redemption timestamp |
| BookingId | int | Yes | FK → Bookings.Id (optional) |

**Foreign Keys**
- TenantId → Tenants.Id
- BookingId → Bookings.Id

## Booking pricing breakdown fields

`Bookings` stores server-calculated audit fields (see Bookings table): `BaseAmount`, `DiscountAmount`, `ConvenienceFeeAmount`, `FinalAmount`, `PricingSource` (`Public`, `Quoted`, `Promo`, `Manual`), `QuoteTokenNonce`, `QuoteExpiresAtUtc`. `TotalAmount` remains for backward compatibility; prefer `FinalAmount` for new reporting.

## Payment pricing breakdown fields

`Payments` stores `BaseAmount`, `DiscountAmount`, `ConvenienceFeeAmount` (see Payments table) for reconciliation and settlement auditing.

## Multi-tenant safety notes

- Quote issue/validate/redeem is tenant-scoped through tenant resolution + EF tenant filters.
- Quote replay protection is scoped by `(TenantId, Nonce)`.
- A quote token issued in one tenant cannot validate/redeem in another tenant.
