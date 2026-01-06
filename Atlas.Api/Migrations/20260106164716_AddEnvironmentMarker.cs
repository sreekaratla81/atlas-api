using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvironmentMarker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnvironmentMarker",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Marker = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentMarker", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentMarker_Marker",
                table: "EnvironmentMarker",
                column: "Marker",
                unique: true);

            var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var markerValue = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase)
                ? "PROD"
                : "DEV";

            migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM EnvironmentMarker)
BEGIN
    UPDATE EnvironmentMarker SET Marker = '{markerValue}' WHERE Id = (SELECT TOP(1) Id FROM EnvironmentMarker);
END
ELSE
BEGIN
    INSERT INTO EnvironmentMarker (Marker) VALUES ('{markerValue}');
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnvironmentMarker");
        }
    }
}
