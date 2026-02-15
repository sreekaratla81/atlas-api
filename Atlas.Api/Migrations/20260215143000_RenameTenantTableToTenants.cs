using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260215143000_RenameTenantTableToTenants")]
    public partial class RenameTenantTableToTenants : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Tenant]', N'V') IS NOT NULL
                    DROP VIEW [Tenant];

                IF OBJECT_ID(N'[Tenant]', N'U') IS NOT NULL AND OBJECT_ID(N'[Tenants]', N'U') IS NULL
                    EXEC sp_rename N'[Tenant]', N'Tenants';

                IF OBJECT_ID(N'[Tenants]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_Tenant' AND parent_object_id = OBJECT_ID(N'[Tenants]'))
                        EXEC sp_rename N'[Tenants].[PK_Tenant]', N'PK_Tenants', N'OBJECT';

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tenant_Slug' AND object_id = OBJECT_ID(N'[Tenants]'))
                        EXEC sp_rename N'[Tenants].[IX_Tenant_Slug]', N'IX_Tenants_Slug', N'INDEX';

                    DECLARE @oldFkName sysname;
                    DECLARE @newFkName sysname;

                    DECLARE fk_cursor CURSOR LOCAL FAST_FORWARD FOR
                    SELECT fk.name,
                           REPLACE(fk.name, '_Tenant_TenantId', '_Tenants_TenantId')
                    FROM sys.foreign_keys fk
                    WHERE fk.referenced_object_id = OBJECT_ID(N'[Tenants]')
                      AND fk.name LIKE '%_Tenant_TenantId';

                    OPEN fk_cursor;
                    FETCH NEXT FROM fk_cursor INTO @oldFkName, @newFkName;

                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = @newFkName)
                            EXEC sp_rename @oldFkName, @newFkName, N'OBJECT';

                        FETCH NEXT FROM fk_cursor INTO @oldFkName, @newFkName;
                    END

                    CLOSE fk_cursor;
                    DEALLOCATE fk_cursor;
                END;

                IF OBJECT_ID(N'[Tenant]', N'U') IS NULL AND OBJECT_ID(N'[Tenants]', N'U') IS NOT NULL
                BEGIN
                    EXEC(N'
                        CREATE VIEW [Tenant]
                        AS
                        SELECT [Id], [Name], [Slug], [Status], [CreatedAtUtc]
                        FROM [Tenants]');
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Tenant]', N'V') IS NOT NULL
                    DROP VIEW [Tenant];

                IF OBJECT_ID(N'[Tenants]', N'U') IS NOT NULL AND OBJECT_ID(N'[Tenant]', N'U') IS NULL
                BEGIN
                    DECLARE @oldFkName sysname;
                    DECLARE @newFkName sysname;

                    DECLARE fk_cursor CURSOR LOCAL FAST_FORWARD FOR
                    SELECT fk.name,
                           REPLACE(fk.name, '_Tenants_TenantId', '_Tenant_TenantId')
                    FROM sys.foreign_keys fk
                    WHERE fk.referenced_object_id = OBJECT_ID(N'[Tenants]')
                      AND fk.name LIKE '%_Tenants_TenantId';

                    OPEN fk_cursor;
                    FETCH NEXT FROM fk_cursor INTO @oldFkName, @newFkName;

                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = @newFkName)
                            EXEC sp_rename @oldFkName, @newFkName, N'OBJECT';

                        FETCH NEXT FROM fk_cursor INTO @oldFkName, @newFkName;
                    END

                    CLOSE fk_cursor;
                    DEALLOCATE fk_cursor;

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tenants_Slug' AND object_id = OBJECT_ID(N'[Tenants]'))
                        EXEC sp_rename N'[Tenants].[IX_Tenants_Slug]', N'IX_Tenant_Slug', N'INDEX';

                    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_Tenants' AND parent_object_id = OBJECT_ID(N'[Tenants]'))
                        EXEC sp_rename N'[Tenants].[PK_Tenants]', N'PK_Tenant', N'OBJECT';

                    EXEC sp_rename N'[Tenants]', N'Tenant';
                END;
                """);
        }
    }
}
