using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class RestoreSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarPricingDto",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RoomsAvailable = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentMarker",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Marker = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentMarker", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionTaken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IFSC = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankAccounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsumedEvent",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ConsumerName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    EventId = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    EventType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false),
                    PayloadHash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumedEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumedEvent_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Guests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdProofUrl = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Guests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessageTemplate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    TemplateKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    EventType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ScopeType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ScopeId = table.Column<int>(type: "int", nullable: true),
                    Language = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Subject = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageTemplate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageTemplate_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Topic = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    EventType = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    OccurredUtc = table.Column<DateTime>(type: "datetime", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    NextAttemptUtc = table.Column<DateTime>(type: "datetime", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    AggregateType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    AggregateId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    HeadersJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommissionPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Properties_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantPricingSettings",
                columns: table => new
                {
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ConvenienceFeePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 3.00m),
                    GlobalDiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0.00m),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPricingSettings", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_TenantPricingSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Listings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Floor = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheckInTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheckOutTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WifiName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WifiPassword = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxGuests = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Listings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Listings_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Listings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    GuestId = table.Column<int>(type: "int", nullable: false),
                    CheckinDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckoutDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BookingSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    BookingStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Lead"),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ConvenienceFeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PricingSource = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "Public"),
                    QuoteTokenNonce = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    QuoteExpiresAtUtc = table.Column<DateTime>(type: "datetime", nullable: true),
                    Currency = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false, defaultValue: "INR"),
                    ExternalReservationId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ConfirmationSentAtUtc = table.Column<DateTime>(type: "datetime", nullable: true),
                    RefundFreeUntilUtc = table.Column<DateTime>(type: "datetime", nullable: true),
                    CheckedInAtUtc = table.Column<DateTime>(type: "datetime", nullable: true),
                    CheckedOutAtUtc = table.Column<DateTime>(type: "datetime", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime", nullable: true),
                    PaymentStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AmountReceived = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: true),
                    GuestsPlanned = table.Column<int>(type: "int", nullable: true),
                    GuestsActual = table.Column<int>(type: "int", nullable: true),
                    ExtraGuestCharge = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingDailyInventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    RoomsAvailable = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingDailyInventory", x => x.Id);
                    table.CheckConstraint("CK_ListingDailyInventory_RoomsAvailable_NonNegative", "[RoomsAvailable] >= 0");
                    table.ForeignKey(
                        name: "FK_ListingDailyInventory_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingDailyInventory_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingDailyRate",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    NightlyRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false, defaultValue: "INR"),
                    Source = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingDailyRate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingDailyRate_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingDailyRate_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingPricing",
                columns: table => new
                {
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BaseNightlyRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WeekendNightlyRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ExtraGuestRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false, defaultValue: "INR"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingPricing", x => x.ListingId);
                    table.ForeignKey(
                        name: "FK_ListingPricing_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingPricing_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutomationSchedule",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationSchedule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationSchedule_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationSchedule_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AvailabilityBlock",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    BlockType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    Inventory = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityBlock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvailabilityBlock_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvailabilityBlock_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvailabilityBlock_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    GuestId = table.Column<int>(type: "int", nullable: true),
                    Channel = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    EventType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    ToAddress = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: true),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Provider = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationLog_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationLog_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationLog_MessageTemplate_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "MessageTemplate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationLog_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ConvenienceFeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Method = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReceivedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RazorpayOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RazorpayPaymentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RazorpaySignature = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuoteRedemption",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Nonce = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    RedeemedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteRedemption", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteRedemption_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuoteRedemption_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppInboundMessage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    FromNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ToNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    GuestId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppInboundMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppInboundMessage_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppInboundMessage_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppInboundMessage_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSchedule_BookingId",
                table: "AutomationSchedule",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSchedule_TenantId",
                table: "AutomationSchedule",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSchedule_TenantId_BookingId_EventType_DueAtUtc",
                table: "AutomationSchedule",
                columns: new[] { "TenantId", "BookingId", "EventType", "DueAtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityBlock_BookingId",
                table: "AvailabilityBlock",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityBlock_ListingId_StartDate_EndDate",
                table: "AvailabilityBlock",
                columns: new[] { "ListingId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityBlock_TenantId",
                table: "AvailabilityBlock",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityBlock_TenantId_ListingId_StartDate_EndDate",
                table: "AvailabilityBlock",
                columns: new[] { "TenantId", "ListingId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_TenantId",
                table: "BankAccounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_TenantId_AccountNumber",
                table: "BankAccounts",
                columns: new[] { "TenantId", "AccountNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BankAccountId",
                table: "Bookings",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_GuestId",
                table: "Bookings",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ListingId",
                table: "Bookings",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TenantId",
                table: "Bookings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TenantId_ListingId",
                table: "Bookings",
                columns: new[] { "TenantId", "ListingId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLog_BookingId",
                table: "CommunicationLog",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLog_GuestId",
                table: "CommunicationLog",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLog_TemplateId",
                table: "CommunicationLog",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLog_TenantId",
                table: "CommunicationLog",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLog_TenantId_BookingId",
                table: "CommunicationLog",
                columns: new[] { "TenantId", "BookingId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLog_TenantId_IdempotencyKey",
                table: "CommunicationLog",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumedEvent_TenantId",
                table: "ConsumedEvent",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumedEvent_TenantId_ConsumerName_EventId",
                table: "ConsumedEvent",
                columns: new[] { "TenantId", "ConsumerName", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumedEvent_TenantId_ProcessedAtUtc",
                table: "ConsumedEvent",
                columns: new[] { "TenantId", "ProcessedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentMarker_Marker",
                table: "EnvironmentMarker",
                column: "Marker",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guests_TenantId",
                table: "Guests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingDailyInventory_ListingId",
                table: "ListingDailyInventory",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingDailyInventory_TenantId",
                table: "ListingDailyInventory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingDailyInventory_TenantId_ListingId_Date",
                table: "ListingDailyInventory",
                columns: new[] { "TenantId", "ListingId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingDailyRate_ListingId",
                table: "ListingDailyRate",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingDailyRate_TenantId",
                table: "ListingDailyRate",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingDailyRate_TenantId_ListingId_Date",
                table: "ListingDailyRate",
                columns: new[] { "TenantId", "ListingId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingPricing_TenantId",
                table: "ListingPricing",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingPricing_TenantId_ListingId",
                table: "ListingPricing",
                columns: new[] { "TenantId", "ListingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Listings_PropertyId",
                table: "Listings",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_TenantId",
                table: "Listings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_TenantId_PropertyId",
                table: "Listings",
                columns: new[] { "TenantId", "PropertyId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplate_TenantId",
                table: "MessageTemplate",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplate_TenantId_EventType_Channel",
                table: "MessageTemplate",
                columns: new[] { "TenantId", "EventType", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Status_NextAttemptUtc",
                table: "OutboxMessage",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_TenantId",
                table: "OutboxMessage",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_TenantId_CreatedAtUtc",
                table: "OutboxMessage",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BookingId",
                table: "Payments",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId",
                table: "Payments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId_BookingId",
                table: "Payments",
                columns: new[] { "TenantId", "BookingId" });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_TenantId",
                table: "Properties",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteRedemption_BookingId",
                table: "QuoteRedemption",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteRedemption_TenantId",
                table: "QuoteRedemption",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteRedemption_TenantId_Nonce",
                table: "QuoteRedemption",
                columns: new[] { "TenantId", "Nonce" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantPricingSettings_TenantId",
                table: "TenantPricingSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInboundMessage_BookingId",
                table: "WhatsAppInboundMessage",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInboundMessage_GuestId",
                table: "WhatsAppInboundMessage",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInboundMessage_TenantId",
                table: "WhatsAppInboundMessage",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInboundMessage_TenantId_Provider_ProviderMessageId",
                table: "WhatsAppInboundMessage",
                columns: new[] { "TenantId", "Provider", "ProviderMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInboundMessage_TenantId_ReceivedAtUtc",
                table: "WhatsAppInboundMessage",
                columns: new[] { "TenantId", "ReceivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationSchedule");

            migrationBuilder.DropTable(
                name: "AvailabilityBlock");

            migrationBuilder.DropTable(
                name: "CalendarPricingDto");

            migrationBuilder.DropTable(
                name: "CommunicationLog");

            migrationBuilder.DropTable(
                name: "ConsumedEvent");

            migrationBuilder.DropTable(
                name: "EnvironmentMarker");

            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "ListingDailyInventory");

            migrationBuilder.DropTable(
                name: "ListingDailyRate");

            migrationBuilder.DropTable(
                name: "ListingPricing");

            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "QuoteRedemption");

            migrationBuilder.DropTable(
                name: "TenantPricingSettings");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WhatsAppInboundMessage");

            migrationBuilder.DropTable(
                name: "MessageTemplate");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropTable(
                name: "Guests");

            migrationBuilder.DropTable(
                name: "Listings");

            migrationBuilder.DropTable(
                name: "Properties");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
