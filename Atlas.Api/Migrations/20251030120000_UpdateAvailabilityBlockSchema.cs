using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAvailabilityBlockSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "AvailabilityBlocks",
                newName: "AvailabilityBlock");

            migrationBuilder.RenameIndex(
                name: "IX_AvailabilityBlocks_ListingId_StartDate_EndDate",
                table: "AvailabilityBlock",
                newName: "IX_AvailabilityBlock_ListingId_StartDate_EndDate");

            migrationBuilder.RenameIndex(
                name: "IX_AvailabilityBlocks_BookingId",
                table: "AvailabilityBlock",
                newName: "IX_AvailabilityBlock_BookingId");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AvailabilityBlocks",
                table: "AvailabilityBlock");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "AvailabilityBlock",
                newName: "CreatedAtUtc");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "AvailabilityBlock");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "AvailabilityBlock",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "AvailabilityBlock",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "AvailabilityBlock",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AvailabilityBlock",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "Active");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "AvailabilityBlock",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "BlockType",
                table: "AvailabilityBlock",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Booking");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "AvailabilityBlock",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "AvailabilityBlock",
                type: "datetime",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AvailabilityBlock",
                table: "AvailabilityBlock",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_AvailabilityBlock",
                table: "AvailabilityBlock");

            migrationBuilder.DropColumn(
                name: "BlockType",
                table: "AvailabilityBlock");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "AvailabilityBlock");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "AvailabilityBlock");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "AvailabilityBlock",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "AvailabilityBlock",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "AvailabilityBlock",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AvailabilityBlock",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Active",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Active");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "AvailabilityBlock",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "AvailabilityBlock",
                type: "datetime2",
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "AvailabilityBlock",
                newName: "CreatedAt");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AvailabilityBlocks",
                table: "AvailabilityBlock",
                column: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_AvailabilityBlock_ListingId_StartDate_EndDate",
                table: "AvailabilityBlock",
                newName: "IX_AvailabilityBlocks_ListingId_StartDate_EndDate");

            migrationBuilder.RenameIndex(
                name: "IX_AvailabilityBlock_BookingId",
                table: "AvailabilityBlock",
                newName: "IX_AvailabilityBlocks_BookingId");

            migrationBuilder.RenameTable(
                name: "AvailabilityBlock",
                newName: "AvailabilityBlocks");
        }
    }
}
