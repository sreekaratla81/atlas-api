using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260215160000_RenameTenantTableToTenants")]
    public partial class RenameTenantTableToTenants : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Tenant]', N'U') IS NOT NULL AND OBJECT_ID(N'[Tenants]', N'U') IS NULL
                    EXEC sp_rename N'[Tenant]', N'Tenants';

                IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE [name] = N'PK_Tenant' AND [parent_object_id] = OBJECT_ID(N'[Tenants]'))
                    EXEC sp_rename N'[Tenants].[PK_Tenant]', N'PK_Tenants', N'OBJECT';

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Tenant_Slug' AND [object_id] = OBJECT_ID(N'[Tenants]'))
                    EXEC sp_rename N'[Tenants].[IX_Tenant_Slug]', N'IX_Tenants_Slug', N'INDEX';

                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Properties_Tenant_TenantId')
                    EXEC sp_rename N'[FK_Properties_Tenant_TenantId]', N'FK_Properties_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Listings_Tenant_TenantId')
                    EXEC sp_rename N'[FK_Listings_Tenant_TenantId]', N'FK_Listings_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Bookings_Tenant_TenantId')
                    EXEC sp_rename N'[FK_Bookings_Tenant_TenantId]', N'FK_Bookings_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Guests_Tenant_TenantId')
                    EXEC sp_rename N'[FK_Guests_Tenant_TenantId]', N'FK_Guests_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Payments_Tenant_TenantId')
                    EXEC sp_rename N'[FK_Payments_Tenant_TenantId]', N'FK_Payments_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Users_Tenant_TenantId')
                    EXEC sp_rename N'[FK_Users_Tenant_TenantId]', N'FK_Users_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_ListingPricing_Tenant_TenantId')
                    EXEC sp_rename N'[FK_ListingPricing_Tenant_TenantId]', N'FK_ListingPricing_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_ListingDailyRate_Tenant_TenantId')
                    EXEC sp_rename N'[FK_ListingDailyRate_Tenant_TenantId]', N'FK_ListingDailyRate_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AvailabilityBlock_Tenant_TenantId')
                    EXEC sp_rename N'[FK_AvailabilityBlock_Tenant_TenantId]', N'FK_AvailabilityBlock_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_MessageTemplate_Tenant_TenantId')
                    EXEC sp_rename N'[FK_MessageTemplate_Tenant_TenantId]', N'FK_MessageTemplate_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_CommunicationLog_Tenant_TenantId')
                    EXEC sp_rename N'[FK_CommunicationLog_Tenant_TenantId]', N'FK_CommunicationLog_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OutboxMessage_Tenant_TenantId')
                    EXEC sp_rename N'[FK_OutboxMessage_Tenant_TenantId]', N'FK_OutboxMessage_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AutomationSchedule_Tenant_TenantId')
                    EXEC sp_rename N'[FK_AutomationSchedule_Tenant_TenantId]', N'FK_AutomationSchedule_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_BankAccounts_Tenant_TenantId')
                    EXEC sp_rename N'[FK_BankAccounts_Tenant_TenantId]', N'FK_BankAccounts_Tenants_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_ListingDailyInventory_Tenant_TenantId')
                    EXEC sp_rename N'[FK_ListingDailyInventory_Tenant_TenantId]', N'FK_ListingDailyInventory_Tenants_TenantId', N'OBJECT';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Properties_Tenants_TenantId')
                    EXEC sp_rename N'[FK_Properties_Tenants_TenantId]', N'FK_Properties_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Listings_Tenants_TenantId')
                    EXEC sp_rename N'[FK_Listings_Tenants_TenantId]', N'FK_Listings_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Bookings_Tenants_TenantId')
                    EXEC sp_rename N'[FK_Bookings_Tenants_TenantId]', N'FK_Bookings_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Guests_Tenants_TenantId')
                    EXEC sp_rename N'[FK_Guests_Tenants_TenantId]', N'FK_Guests_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Payments_Tenants_TenantId')
                    EXEC sp_rename N'[FK_Payments_Tenants_TenantId]', N'FK_Payments_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Users_Tenants_TenantId')
                    EXEC sp_rename N'[FK_Users_Tenants_TenantId]', N'FK_Users_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_ListingPricing_Tenants_TenantId')
                    EXEC sp_rename N'[FK_ListingPricing_Tenants_TenantId]', N'FK_ListingPricing_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_ListingDailyRate_Tenants_TenantId')
                    EXEC sp_rename N'[FK_ListingDailyRate_Tenants_TenantId]', N'FK_ListingDailyRate_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AvailabilityBlock_Tenants_TenantId')
                    EXEC sp_rename N'[FK_AvailabilityBlock_Tenants_TenantId]', N'FK_AvailabilityBlock_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_MessageTemplate_Tenants_TenantId')
                    EXEC sp_rename N'[FK_MessageTemplate_Tenants_TenantId]', N'FK_MessageTemplate_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_CommunicationLog_Tenants_TenantId')
                    EXEC sp_rename N'[FK_CommunicationLog_Tenants_TenantId]', N'FK_CommunicationLog_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OutboxMessage_Tenants_TenantId')
                    EXEC sp_rename N'[FK_OutboxMessage_Tenants_TenantId]', N'FK_OutboxMessage_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AutomationSchedule_Tenants_TenantId')
                    EXEC sp_rename N'[FK_AutomationSchedule_Tenants_TenantId]', N'FK_AutomationSchedule_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_BankAccounts_Tenants_TenantId')
                    EXEC sp_rename N'[FK_BankAccounts_Tenants_TenantId]', N'FK_BankAccounts_Tenant_TenantId', N'OBJECT';
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_ListingDailyInventory_Tenants_TenantId')
                    EXEC sp_rename N'[FK_ListingDailyInventory_Tenants_TenantId]', N'FK_ListingDailyInventory_Tenant_TenantId', N'OBJECT';

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Tenants_Slug' AND [object_id] = OBJECT_ID(N'[Tenants]'))
                    EXEC sp_rename N'[Tenants].[IX_Tenants_Slug]', N'IX_Tenant_Slug', N'INDEX';

                IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE [name] = N'PK_Tenants' AND [parent_object_id] = OBJECT_ID(N'[Tenants]'))
                    EXEC sp_rename N'[Tenants].[PK_Tenants]', N'PK_Tenant', N'OBJECT';

                IF OBJECT_ID(N'[Tenants]', N'U') IS NOT NULL AND OBJECT_ID(N'[Tenant]', N'U') IS NULL
                    EXEC sp_rename N'[Tenants]', N'Tenant';
                """);
        }
    }
}
