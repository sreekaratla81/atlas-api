using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCheckTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlannedCheckinTime",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ActualCheckinTime",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PlannedCheckoutTime",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ActualCheckoutTime",
                table: "Bookings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "PlannedCheckinTime",
                table: "Bookings",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ActualCheckinTime",
                table: "Bookings",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "PlannedCheckoutTime",
                table: "Bookings",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ActualCheckoutTime",
                table: "Bookings",
                type: "time",
                nullable: true);
        }
    }
}
