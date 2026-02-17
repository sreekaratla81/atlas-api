using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ListingDailyRate_ListingPricing_ListingPricingListingId",
                table: "ListingDailyRate");

            migrationBuilder.DropIndex(
                name: "IX_ListingDailyRate_ListingPricingListingId",
                table: "ListingDailyRate");

            migrationBuilder.DropColumn(
                name: "ListingPricingListingId",
                table: "ListingDailyRate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ListingPricingListingId",
                table: "ListingDailyRate",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingDailyRate_ListingPricingListingId",
                table: "ListingDailyRate",
                column: "ListingPricingListingId");

            migrationBuilder.AddForeignKey(
                name: "FK_ListingDailyRate_ListingPricing_ListingPricingListingId",
                table: "ListingDailyRate",
                column: "ListingPricingListingId",
                principalTable: "ListingPricing",
                principalColumn: "ListingId");
        }
    }
}
