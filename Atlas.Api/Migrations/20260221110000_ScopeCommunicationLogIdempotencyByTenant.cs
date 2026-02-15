using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260221110000_ScopeCommunicationLogIdempotencyByTenant")]
    public partial class ScopeCommunicationLogIdempotencyByTenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CommunicationLog_IdempotencyKey' AND object_id = OBJECT_ID(N'[CommunicationLog]'))
                    DROP INDEX [IX_CommunicationLog_IdempotencyKey] ON [CommunicationLog];

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CommunicationLog_TenantId_IdempotencyKey' AND object_id = OBJECT_ID(N'[CommunicationLog]'))
                    CREATE UNIQUE INDEX [IX_CommunicationLog_TenantId_IdempotencyKey] ON [CommunicationLog] ([TenantId], [IdempotencyKey]);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only migration.
        }
    }
}
