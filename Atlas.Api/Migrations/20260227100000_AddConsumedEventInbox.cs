using System;
using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260227100000_AddConsumedEventInbox")]
    public partial class AddConsumedEventInbox : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var deleteBehavior = ResolveDeleteBehavior();

            migrationBuilder.CreateTable(
                name: "ConsumedEvent",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ConsumerName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    EventId = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    EventType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false),
                    PayloadHash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumedEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumedEvent_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: deleteBehavior);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumedEvent_TenantId",
                table: "ConsumedEvent",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumedEvent_TenantId_ConsumerName_EventId",
                table: "ConsumedEvent",
                columns: new[] { "TenantId", "ConsumerName", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumedEvent_TenantId_ProcessedAtUtc",
                table: "ConsumedEvent",
                columns: new[] { "TenantId", "ProcessedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumedEvent");
        }

        private static ReferentialAction ResolveDeleteBehavior()
        {
            var value = Environment.GetEnvironmentVariable("ATLAS_DELETE_BEHAVIOR");
            return string.Equals(value, "Cascade", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                ? ReferentialAction.Cascade
                : ReferentialAction.Restrict;
        }
    }
}
