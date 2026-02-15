using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
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
                        name: "FK_QuoteRedemption_Booking_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Booking",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuoteRedemption_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<decimal>(name: "BaseAmount", table: "Booking", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "DiscountAmount", table: "Booking", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "ConvenienceFeeAmount", table: "Booking", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "FinalAmount", table: "Booking", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "PricingSource", table: "Booking", type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "Public");
            migrationBuilder.AddColumn<string>(name: "QuoteTokenNonce", table: "Booking", type: "varchar(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "QuoteExpiresAtUtc", table: "Booking", type: "datetime", nullable: true);

            migrationBuilder.AddColumn<decimal>(name: "BaseAmount", table: "Payment", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "DiscountAmount", table: "Payment", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "ConvenienceFeeAmount", table: "Payment", type: "decimal(18,2)", nullable: true);

            migrationBuilder.CreateIndex(name: "IX_QuoteRedemption_BookingId", table: "QuoteRedemption", column: "BookingId");
            migrationBuilder.CreateIndex(name: "IX_QuoteRedemption_TenantId_Nonce", table: "QuoteRedemption", columns: new[] { "TenantId", "Nonce" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_TenantPricingSettings_TenantId", table: "TenantPricingSettings", column: "TenantId", unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "QuoteRedemption");
            migrationBuilder.DropTable(name: "TenantPricingSettings");

            migrationBuilder.DropColumn(name: "BaseAmount", table: "Booking");
            migrationBuilder.DropColumn(name: "DiscountAmount", table: "Booking");
            migrationBuilder.DropColumn(name: "ConvenienceFeeAmount", table: "Booking");
            migrationBuilder.DropColumn(name: "FinalAmount", table: "Booking");
            migrationBuilder.DropColumn(name: "PricingSource", table: "Booking");
            migrationBuilder.DropColumn(name: "QuoteTokenNonce", table: "Booking");
            migrationBuilder.DropColumn(name: "QuoteExpiresAtUtc", table: "Booking");

            migrationBuilder.DropColumn(name: "BaseAmount", table: "Payment");
            migrationBuilder.DropColumn(name: "DiscountAmount", table: "Payment");
            migrationBuilder.DropColumn(name: "ConvenienceFeeAmount", table: "Payment");
        }
    }
}
