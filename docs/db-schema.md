# Database Schema

This document reflects the schema defined by `AppDbContext` and the entity classes in `Atlas.Api/Models`.

## AutomationSchedule
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | bigint | No |
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
- None

**Relationships**
- None

## AvailabilityBlock
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | bigint | No |
| ListingId | int | No |
| BookingId | int | Yes |
| StartDate | date | No |
| EndDate | date | No |
| BlockType | varchar(30) | No |
| Source | varchar(30) | No |
| Status | varchar(20) | No |
| CreatedAtUtc | datetime | No |
| UpdatedAtUtc | datetime | No |

**Primary Key**
- Id

**Foreign Keys**
- ListingId → Listings.Id
- BookingId → Bookings.Id

**Relationships**
- Many availability blocks belong to one listing.
- Many availability blocks optionally reference one booking.

## BankAccounts
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| BankName | nvarchar(100) | No |
| AccountNumber | nvarchar(50) | No |
| IFSC | nvarchar(20) | No |
| AccountType | nvarchar(50) | No |
| CreatedAt | datetime2 | No |

**Primary Key**
- Id

**Foreign Keys**
- None

**Relationships**
- None

## Bookings
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| ListingId | int | No |
| GuestId | int | No |
| CheckinDate | datetime2 | No |
| CheckoutDate | datetime2 | No |
| BookingSource | varchar(50) | Yes |
| BookingStatus | varchar(20) | No |
| TotalAmount | decimal(18,2) | Yes |
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
- ListingId → Listings.Id
- GuestId → Guests.Id
- BankAccountId → BankAccounts.Id

**Relationships**
- Many bookings belong to one listing.
- Many bookings belong to one guest.
- Many bookings optionally reference one bank account.

## CommunicationLog
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | bigint | No |
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
- BookingId → Bookings.Id
- GuestId → Guests.Id
- TemplateId → MessageTemplate.Id

**Relationships**
- Many communication log entries optionally reference one booking.
- Many communication log entries optionally reference one guest.
- Many communication log entries optionally reference one message template.

## Guests
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| Name | nvarchar(max) | No |
| Phone | nvarchar(max) | No |
| Email | nvarchar(max) | No |
| IdProofUrl | nvarchar(max) | Yes |

**Primary Key**
- Id

**Foreign Keys**
- None

**Relationships**
- None

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
- None

**Relationships**
- None

## ListingDailyRate
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | bigint | No |
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
- ListingId → Listings.Id

**Relationships**
- Many daily rates belong to one listing.

## ListingPricing
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| ListingId | int | No |
| BaseNightlyRate | decimal(18,2) | No |
| WeekendNightlyRate | decimal(18,2) | Yes |
| ExtraGuestRate | decimal(18,2) | Yes |
| Currency | varchar(10) | No |
| UpdatedAtUtc | datetime | No |

**Primary Key**
- ListingId

**Foreign Keys**
- ListingId → Listings.Id

**Relationships**
- Each listing pricing row corresponds to one listing (one-to-one).

## Listings
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
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
- PropertyId → Properties.Id

**Relationships**
- Many listings belong to one property.

## MessageTemplate
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
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
- None

**Relationships**
- None

## OutboxMessage
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | uniqueidentifier | No |
| AggregateType | varchar(50) | No |
| AggregateId | varchar(50) | No |
| EventType | varchar(50) | No |
| PayloadJson | text | No |
| HeadersJson | text | Yes |
| CreatedAtUtc | datetime | No |
| PublishedAtUtc | datetime | Yes |
| AttemptCount | int | No |
| LastError | text | Yes |

**Primary Key**
- Id

**Foreign Keys**
- None

**Relationships**
- None

## Payments
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| BookingId | int | No |
| Amount | decimal(18,2) | No |
| Method | nvarchar(max) | No |
| Type | nvarchar(max) | No |
| ReceivedOn | datetime2 | No |
| Note | nvarchar(max) | No |

**Primary Key**
- Id

**Foreign Keys**
- None

**Relationships**
- None

## Properties
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
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
- None

**Relationships**
- None

## Users
**Columns**
| Column | Type | Nullable |
| --- | --- | --- |
| Id | int | No |
| Name | nvarchar(max) | No |
| Phone | nvarchar(max) | No |
| Email | nvarchar(max) | No |
| PasswordHash | nvarchar(max) | No |
| Role | nvarchar(max) | No |

**Primary Key**
- Id

**Foreign Keys**
- None

**Relationships**
- None
