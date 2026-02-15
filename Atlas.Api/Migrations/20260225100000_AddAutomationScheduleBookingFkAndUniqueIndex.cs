using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260225100000_AddAutomationScheduleBookingFkAndUniqueIndex")]
    public partial class AddAutomationScheduleBookingFkAndUniqueIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM [AutomationSchedule] AS [a]
                    LEFT JOIN [Bookings] AS [b] ON [b].[Id] = [a].[BookingId]
                    WHERE [b].[Id] IS NULL
                )
                BEGIN
                    THROW 51001, 'Migration aborted: AutomationSchedule has rows whose BookingId does not reference an existing Bookings.Id. Clean up orphaned AutomationSchedule rows before applying this migration.', 1;
                END;

                IF EXISTS (
                    SELECT 1
                    FROM [AutomationSchedule]
                    GROUP BY [TenantId], [BookingId], [EventType], [DueAtUtc]
                    HAVING COUNT_BIG(1) > 1
                )
                BEGIN
                    THROW 51002, 'Migration aborted: duplicate AutomationSchedule rows exist for (TenantId, BookingId, EventType, DueAtUtc). Remove or merge duplicates before applying this migration.', 1;
                END;

                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_AutomationSchedule_TenantId_BookingId_DueAtUtc'
                      AND [object_id] = OBJECT_ID(N'[AutomationSchedule]')
                )
                BEGIN
                    DROP INDEX [IX_AutomationSchedule_TenantId_BookingId_DueAtUtc] ON [AutomationSchedule];
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_AutomationSchedule_TenantId_BookingId_EventType_DueAtUtc'
                      AND [object_id] = OBJECT_ID(N'[AutomationSchedule]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_AutomationSchedule_TenantId_BookingId_EventType_DueAtUtc]
                        ON [AutomationSchedule] ([TenantId], [BookingId], [EventType], [DueAtUtc]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE [name] = N'FK_AutomationSchedule_Bookings_BookingId'
                )
                BEGIN
                    ALTER TABLE [AutomationSchedule] WITH CHECK
                    ADD CONSTRAINT [FK_AutomationSchedule_Bookings_BookingId]
                        FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([Id]) ON DELETE NO ACTION;
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE [name] = N'FK_AutomationSchedule_Bookings_BookingId'
                )
                BEGIN
                    ALTER TABLE [AutomationSchedule] DROP CONSTRAINT [FK_AutomationSchedule_Bookings_BookingId];
                END;

                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_AutomationSchedule_TenantId_BookingId_EventType_DueAtUtc'
                      AND [object_id] = OBJECT_ID(N'[AutomationSchedule]')
                )
                BEGIN
                    DROP INDEX [IX_AutomationSchedule_TenantId_BookingId_EventType_DueAtUtc] ON [AutomationSchedule];
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_AutomationSchedule_TenantId_BookingId_DueAtUtc'
                      AND [object_id] = OBJECT_ID(N'[AutomationSchedule]')
                )
                BEGIN
                    CREATE INDEX [IX_AutomationSchedule_TenantId_BookingId_DueAtUtc]
                        ON [AutomationSchedule] ([TenantId], [BookingId], [DueAtUtc]);
                END;
                """);
        }
    }
}
