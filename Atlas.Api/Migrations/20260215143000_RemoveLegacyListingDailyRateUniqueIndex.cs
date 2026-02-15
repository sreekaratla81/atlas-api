using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260215143000_RemoveLegacyListingDailyRateUniqueIndex")]
    public partial class RemoveLegacyListingDailyRateUniqueIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ListingDailyRate_ListingId_Date' AND object_id = OBJECT_ID(N'[ListingDailyRate]'))
                    DROP INDEX [IX_ListingDailyRate_ListingId_Date] ON [ListingDailyRate];

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ListingDailyRate_TenantId_ListingId_Date' AND object_id = OBJECT_ID(N'[ListingDailyRate]'))
                    CREATE UNIQUE INDEX [IX_ListingDailyRate_TenantId_ListingId_Date] ON [ListingDailyRate] ([TenantId], [ListingId], [Date]);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ListingDailyRate_TenantId_ListingId_Date' AND object_id = OBJECT_ID(N'[ListingDailyRate]'))
                    DROP INDEX [IX_ListingDailyRate_TenantId_ListingId_Date] ON [ListingDailyRate];

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ListingDailyRate_ListingId_Date' AND object_id = OBJECT_ID(N'[ListingDailyRate]'))
                    CREATE UNIQUE INDEX [IX_ListingDailyRate_ListingId_Date] ON [ListingDailyRate] ([ListingId], [Date]);
                """);
        }
    }
}
