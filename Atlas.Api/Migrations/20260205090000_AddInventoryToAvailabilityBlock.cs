using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Atlas.Api.Data;

#nullable disable

namespace Atlas.Api.Migrations
{
    [Migration("20260205090000_AddInventoryToAvailabilityBlock")]
    [DbContext(typeof(AppDbContext))]
    public partial class AddInventoryToAvailabilityBlock : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Inventory",
                table: "AvailabilityBlock",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Inventory",
                table: "AvailabilityBlock");
        }
    }
}
