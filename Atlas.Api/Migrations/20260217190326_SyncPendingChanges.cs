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
            // Idempotent: dev/prod may not have this FK if created from different migration history.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ListingDailyRate_ListingPricing_ListingPricingListingId')
    ALTER TABLE [ListingDailyRate] DROP CONSTRAINT [FK_ListingDailyRate_ListingPricing_ListingPricingListingId];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ListingDailyRate_ListingPricingListingId' AND object_id = OBJECT_ID(N'[ListingDailyRate]'))
    DROP INDEX [IX_ListingDailyRate_ListingPricingListingId] ON [ListingDailyRate];

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ListingDailyRate]') AND name = N'ListingPricingListingId')
    ALTER TABLE [ListingDailyRate] DROP COLUMN [ListingPricingListingId];
");
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
