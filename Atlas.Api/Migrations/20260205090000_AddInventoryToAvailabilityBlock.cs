using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryToAvailabilityBlock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF COL_LENGTH('dbo.AvailabilityBlock','Inventory') IS NULL\n"
                + "BEGIN\n"
                + "    ALTER TABLE dbo.AvailabilityBlock ADD Inventory bit NOT NULL CONSTRAINT DF_AvailabilityBlock_Inventory DEFAULT (1)\n"
                + "END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF COL_LENGTH('dbo.AvailabilityBlock','Inventory') IS NOT NULL\n"
                + "BEGIN\n"
                + "    ALTER TABLE dbo.AvailabilityBlock DROP COLUMN Inventory\n"
                + "END");
        }
    }
}
