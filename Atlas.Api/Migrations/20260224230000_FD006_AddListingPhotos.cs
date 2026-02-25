using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    public partial class FD006_AddListingPhotos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListingPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContentType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsCover = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingPhotos_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ListingPhotos_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingPhotos_ListingId",
                table: "ListingPhotos",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingPhotos_TenantId",
                table: "ListingPhotos",
                column: "TenantId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ListingPhotos");
        }
    }
}
