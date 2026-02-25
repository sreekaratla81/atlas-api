using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAddOnServicesAndListingAddOns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AddOnServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PriceType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "per_booking"),
                    Category = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddOnServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AddOnServices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    GuestName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GuestEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GuestPhone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ListingName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CheckinDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckoutDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Nights = table.Column<int>(type: "int", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GstRate = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    GstAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SupplierGstin = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: true),
                    SupplierLegalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupplierAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PlaceOfSupply = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "generated")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingInvoices_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingInvoices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChannelConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "channex"),
                    ApiKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExternalPropertyId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    IsConnected = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastSyncAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    LastSyncError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelConfigs_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChannelConfigs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingPricingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    RuleType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "LOS"),
                    MinNights = table.Column<int>(type: "int", nullable: true),
                    MaxNights = table.Column<int>(type: "int", nullable: true),
                    SeasonStart = table.Column<DateTime>(type: "date", nullable: true),
                    SeasonEnd = table.Column<DateTime>(type: "date", nullable: true),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingPricingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingPricingRules_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingPricingRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromoCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    DiscountType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Percent"),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime", nullable: true),
                    ValidTo = table.Column<DateTime>(type: "datetime", nullable: true),
                    UsageLimit = table.Column<int>(type: "int", nullable: true),
                    TimesUsed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ListingId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromoCodes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    GuestId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    HostResponse = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HostResponseAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reviews_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reviews_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingAddOns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    AddOnServiceId = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    OverridePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingAddOns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingAddOns_AddOnServices_AddOnServiceId",
                        column: x => x.AddOnServiceId,
                        principalTable: "AddOnServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingAddOns_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddOnServices_TenantId",
                table: "AddOnServices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingInvoices_BookingId",
                table: "BookingInvoices",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingInvoices_InvoiceNumber",
                table: "BookingInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingInvoices_TenantId",
                table: "BookingInvoices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelConfigs_PropertyId",
                table: "ChannelConfigs",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelConfigs_TenantId",
                table: "ChannelConfigs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelConfigs_TenantId_PropertyId_Provider",
                table: "ChannelConfigs",
                columns: new[] { "TenantId", "PropertyId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingAddOns_AddOnServiceId",
                table: "ListingAddOns",
                column: "AddOnServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingAddOns_ListingId_AddOnServiceId",
                table: "ListingAddOns",
                columns: new[] { "ListingId", "AddOnServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingPricingRules_ListingId",
                table: "ListingPricingRules",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingPricingRules_TenantId",
                table: "ListingPricingRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingPricingRules_TenantId_ListingId_RuleType",
                table: "ListingPricingRules",
                columns: new[] { "TenantId", "ListingId", "RuleType" });

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_TenantId",
                table: "PromoCodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_TenantId_Code",
                table: "PromoCodes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_BookingId",
                table: "Reviews",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_GuestId",
                table: "Reviews",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ListingId",
                table: "Reviews",
                column: "ListingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingInvoices");

            migrationBuilder.DropTable(
                name: "ChannelConfigs");

            migrationBuilder.DropTable(
                name: "ListingAddOns");

            migrationBuilder.DropTable(
                name: "ListingPricingRules");

            migrationBuilder.DropTable(
                name: "PromoCodes");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "AddOnServices");
        }
    }
}
