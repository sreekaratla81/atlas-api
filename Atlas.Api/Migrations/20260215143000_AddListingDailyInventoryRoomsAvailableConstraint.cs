using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260215143000_AddListingDailyInventoryRoomsAvailableConstraint")]
    public partial class AddListingDailyInventoryRoomsAvailableConstraint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ListingDailyInventory]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM sys.check_constraints
                       WHERE [name] = N'CK_ListingDailyInventory_RoomsAvailable_NonNegative'
                         AND [parent_object_id] = OBJECT_ID(N'[ListingDailyInventory]'))
                BEGIN
                    ALTER TABLE [ListingDailyInventory]
                    ADD CONSTRAINT [CK_ListingDailyInventory_RoomsAvailable_NonNegative]
                    CHECK ([RoomsAvailable] >= 0);
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ListingDailyInventory]', N'U') IS NOT NULL
                   AND EXISTS (
                       SELECT 1
                       FROM sys.check_constraints
                       WHERE [name] = N'CK_ListingDailyInventory_RoomsAvailable_NonNegative'
                         AND [parent_object_id] = OBJECT_ID(N'[ListingDailyInventory]'))
                BEGIN
                    ALTER TABLE [ListingDailyInventory]
                    DROP CONSTRAINT [CK_ListingDailyInventory_RoomsAvailable_NonNegative];
                END;
                """);
        }
    }
}
