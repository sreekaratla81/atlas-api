using System;
using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260215120000_AddListingDailyInventory")]
    public partial class AddListingDailyInventory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListingDailyInventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    RoomsAvailable = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingDailyInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingDailyInventory_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingDailyInventory_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingDailyInventory_ListingId",
                table: "ListingDailyInventory",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingDailyInventory_TenantId",
                table: "ListingDailyInventory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingDailyInventory_TenantId_ListingId_Date",
                table: "ListingDailyInventory",
                columns: new[] { "TenantId", "ListingId", "Date" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListingDailyInventory");
        }
    }
}
