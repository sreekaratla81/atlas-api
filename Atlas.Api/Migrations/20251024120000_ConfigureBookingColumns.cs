using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureBookingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BookingStatus",
                table: "Bookings",
                type: "varchar(20)",
                nullable: false,
                defaultValue: "Lead",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "Lead");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Bookings",
                type: "varchar(10)",
                nullable: false,
                defaultValue: "INR",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "INR");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalReservationId",
                table: "Bookings",
                type: "varchar(100)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BookingSource",
                table: "Bookings",
                type: "varchar(50)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BookingStatus",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Lead",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldDefaultValue: "Lead");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "INR",
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldDefaultValue: "INR");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalReservationId",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BookingSource",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);
        }
    }
}
