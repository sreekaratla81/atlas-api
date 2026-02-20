using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260220100000_AddTenantPricingAndQuotePricingBreakdown")]
    public partial class AddTenantPricingAndQuotePricingBreakdown : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<decimal>(name: "BaseAmount", table: "Bookings", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "DiscountAmount", table: "Bookings", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "ConvenienceFeeAmount", table: "Bookings", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "FinalAmount", table: "Bookings", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "PricingSource", table: "Bookings", type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "Public");
            migrationBuilder.AddColumn<string>(name: "QuoteTokenNonce", table: "Bookings", type: "varchar(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "QuoteExpiresAtUtc", table: "Bookings", type: "datetime", nullable: true);

            migrationBuilder.AddColumn<decimal>(name: "BaseAmount", table: "Payments", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "DiscountAmount", table: "Payments", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "ConvenienceFeeAmount", table: "Payments", type: "decimal(18,2)", nullable: true);

            migrationBuilder.CreateIndex(name: "IX_QuoteRedemption_BookingId", table: "QuoteRedemption", column: "BookingId");
            migrationBuilder.CreateIndex(name: "IX_QuoteRedemption_TenantId_Nonce", table: "QuoteRedemption", columns: new[] { "TenantId", "Nonce" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_TenantPricingSettings_TenantId", table: "TenantPricingSettings", column: "TenantId", unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "QuoteRedemption");
            migrationBuilder.DropTable(name: "TenantPricingSettings");

            migrationBuilder.DropColumn(name: "BaseAmount", table: "Bookings");
            migrationBuilder.DropColumn(name: "DiscountAmount", table: "Bookings");
            migrationBuilder.DropColumn(name: "ConvenienceFeeAmount", table: "Bookings");
            migrationBuilder.DropColumn(name: "FinalAmount", table: "Bookings");
            migrationBuilder.DropColumn(name: "PricingSource", table: "Bookings");
            migrationBuilder.DropColumn(name: "QuoteTokenNonce", table: "Bookings");
            migrationBuilder.DropColumn(name: "QuoteExpiresAtUtc", table: "Bookings");

            migrationBuilder.DropColumn(name: "BaseAmount", table: "Payments");
            migrationBuilder.DropColumn(name: "DiscountAmount", table: "Payments");
            migrationBuilder.DropColumn(name: "ConvenienceFeeAmount", table: "Payments");
        }
    }
}
