using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CalendarPricingDto is created by RestoreSnapshot (runs after this migration).
            // Only alter if table exists (e.g. from prior migration history).
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CalendarPricingDto')
                BEGIN
                    ALTER TABLE [CalendarPricingDto] ALTER COLUMN [Date] datetime2 NOT NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CalendarPricingDto')
                BEGIN
                    ALTER TABLE [CalendarPricingDto] ALTER COLUMN [Date] date NOT NULL;
                END
            ");
        }
    }
}
