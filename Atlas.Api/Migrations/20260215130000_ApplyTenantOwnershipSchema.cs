using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260215130000_ApplyTenantOwnershipSchema")]
    public partial class ApplyTenantOwnershipSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Tenant]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Tenant] (
                        [Id] int NOT NULL IDENTITY,
                        [Name] varchar(100) NOT NULL,
                        [Slug] varchar(50) NOT NULL,
                        [Status] varchar(20) NOT NULL,
                        [CreatedAtUtc] datetime NOT NULL,
                        CONSTRAINT [PK_Tenant] PRIMARY KEY ([Id])
                    );
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tenant_Slug' AND object_id = OBJECT_ID(N'[Tenant]'))
                BEGIN
                    CREATE UNIQUE INDEX [IX_Tenant_Slug] ON [Tenant] ([Slug]);
                END;

                IF NOT EXISTS (SELECT 1 FROM [Tenant] WHERE [Slug] = 'atlas')
                BEGIN
                    INSERT INTO [Tenant] ([Name], [Slug], [Status], [CreatedAtUtc])
                    VALUES ('Atlas', 'atlas', 'Active', GETUTCDATE());
                END;
                """);

            AddTenantOwnership(migrationBuilder, "Properties");
            AddTenantOwnership(migrationBuilder, "Listings");
            AddTenantOwnership(migrationBuilder, "Bookings");
            AddTenantOwnership(migrationBuilder, "Guests");
            AddTenantOwnership(migrationBuilder, "Payments");
            AddTenantOwnership(migrationBuilder, "Users");
            AddTenantOwnership(migrationBuilder, "ListingPricing");
            AddTenantOwnership(migrationBuilder, "ListingDailyRate");
            AddTenantOwnership(migrationBuilder, "AvailabilityBlock");
            AddTenantOwnership(migrationBuilder, "MessageTemplate");
            AddTenantOwnership(migrationBuilder, "CommunicationLog");
            AddTenantOwnership(migrationBuilder, "OutboxMessage");
            AddTenantOwnership(migrationBuilder, "AutomationSchedule");
            AddTenantOwnership(migrationBuilder, "BankAccounts");

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ListingDailyInventory]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [ListingDailyInventory] (
                        [Id] bigint NOT NULL IDENTITY,
                        [ListingId] int NOT NULL,
                        [Date] date NOT NULL,
                        [RoomsAvailable] int NOT NULL,
                        [Source] varchar(20) NOT NULL,
                        [Reason] varchar(200) NULL,
                        [UpdatedByUserId] int NULL,
                        [UpdatedAtUtc] datetime NOT NULL DEFAULT (GETUTCDATE()),
                        [TenantId] int NOT NULL,
                        CONSTRAINT [PK_ListingDailyInventory] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ListingDailyInventory_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_ListingDailyInventory_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenant] ([Id]) ON DELETE NO ACTION
                    );
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ListingDailyInventory_ListingId' AND object_id = OBJECT_ID(N'[ListingDailyInventory]'))
                    CREATE INDEX [IX_ListingDailyInventory_ListingId] ON [ListingDailyInventory] ([ListingId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ListingDailyInventory_TenantId' AND object_id = OBJECT_ID(N'[ListingDailyInventory]'))
                    CREATE INDEX [IX_ListingDailyInventory_TenantId] ON [ListingDailyInventory] ([TenantId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ListingDailyInventory_TenantId_ListingId_Date' AND object_id = OBJECT_ID(N'[ListingDailyInventory]'))
                    CREATE UNIQUE INDEX [IX_ListingDailyInventory_TenantId_ListingId_Date] ON [ListingDailyInventory] ([TenantId], [ListingId], [Date]);
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ListingDailyRate_ListingId_Date' AND object_id = OBJECT_ID(N'[ListingDailyRate]'))
                    DROP INDEX [IX_ListingDailyRate_ListingId_Date] ON [ListingDailyRate];

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ListingDailyRate_TenantId_ListingId_Date' AND object_id = OBJECT_ID(N'[ListingDailyRate]'))
                    CREATE UNIQUE INDEX [IX_ListingDailyRate_TenantId_ListingId_Date] ON [ListingDailyRate] ([TenantId], [ListingId], [Date]);
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Listings_TenantId_PropertyId' AND object_id = OBJECT_ID(N'[Listings]'))
                    CREATE INDEX [IX_Listings_TenantId_PropertyId] ON [Listings] ([TenantId], [PropertyId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bookings_TenantId_ListingId' AND object_id = OBJECT_ID(N'[Bookings]'))
                    CREATE INDEX [IX_Bookings_TenantId_ListingId] ON [Bookings] ([TenantId], [ListingId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_TenantId_BookingId' AND object_id = OBJECT_ID(N'[Payments]'))
                    CREATE INDEX [IX_Payments_TenantId_BookingId] ON [Payments] ([TenantId], [BookingId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ListingPricing_TenantId_ListingId' AND object_id = OBJECT_ID(N'[ListingPricing]'))
                    CREATE UNIQUE INDEX [IX_ListingPricing_TenantId_ListingId] ON [ListingPricing] ([TenantId], [ListingId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AvailabilityBlock_TenantId_ListingId_StartDate_EndDate' AND object_id = OBJECT_ID(N'[AvailabilityBlock]'))
                    CREATE INDEX [IX_AvailabilityBlock_TenantId_ListingId_StartDate_EndDate] ON [AvailabilityBlock] ([TenantId], [ListingId], [StartDate], [EndDate]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MessageTemplate_TenantId_EventType_Channel' AND object_id = OBJECT_ID(N'[MessageTemplate]'))
                    CREATE INDEX [IX_MessageTemplate_TenantId_EventType_Channel] ON [MessageTemplate] ([TenantId], [EventType], [Channel]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CommunicationLog_TenantId_BookingId' AND object_id = OBJECT_ID(N'[CommunicationLog]'))
                    CREATE INDEX [IX_CommunicationLog_TenantId_BookingId] ON [CommunicationLog] ([TenantId], [BookingId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AutomationSchedule_TenantId_BookingId_DueAtUtc' AND object_id = OBJECT_ID(N'[AutomationSchedule]'))
                    CREATE INDEX [IX_AutomationSchedule_TenantId_BookingId_DueAtUtc] ON [AutomationSchedule] ([TenantId], [BookingId], [DueAtUtc]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BankAccounts_TenantId_AccountNumber' AND object_id = OBJECT_ID(N'[BankAccounts]'))
                    CREATE INDEX [IX_BankAccounts_TenantId_AccountNumber] ON [BankAccounts] ([TenantId], [AccountNumber]);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ListingDailyRate_TenantId_ListingId_Date' AND object_id = OBJECT_ID(N'[ListingDailyRate]'))
                    DROP INDEX [IX_ListingDailyRate_TenantId_ListingId_Date] ON [ListingDailyRate];

                IF OBJECT_ID(N'[ListingDailyInventory]', N'U') IS NOT NULL
                    DROP TABLE [ListingDailyInventory];
                """);
        }

        private static void AddTenantOwnership(MigrationBuilder migrationBuilder, string tableName)
        {
            // Batch 1: Add column so it exists before UPDATE (SQL Server parses entire batch before execution).
            migrationBuilder.Sql($$"""
                IF COL_LENGTH(N'[{{tableName}}]', N'TenantId') IS NULL
                    ALTER TABLE [{{tableName}}] ADD [TenantId] int NULL;
                """);

            // Batch 2: Backfill, make NOT NULL, add index and FK.
            migrationBuilder.Sql($$"""
                DECLARE @DefaultTenantId int = (SELECT TOP 1 [Id] FROM [Tenant] WHERE [Slug] = 'atlas' ORDER BY [Id]);
                UPDATE [{{tableName}}]
                SET [TenantId] = @DefaultTenantId
                WHERE [TenantId] IS NULL;

                ALTER TABLE [{{tableName}}] ALTER COLUMN [TenantId] int NOT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_{{tableName}}_TenantId' AND object_id = OBJECT_ID(N'[{{tableName}}]'))
                    CREATE INDEX [IX_{{tableName}}_TenantId] ON [{{tableName}}] ([TenantId]);

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_{{tableName}}_Tenant_TenantId')
                    ALTER TABLE [{{tableName}}] WITH CHECK ADD CONSTRAINT [FK_{{tableName}}_Tenant_TenantId]
                        FOREIGN KEY([TenantId]) REFERENCES [Tenant] ([Id]) ON DELETE NO ACTION;
                """);
        }
    }
}
