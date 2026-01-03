using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCommunicationLogsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationLogs_Bookings_BookingId",
                table: "CommunicationLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationLogs_MessageTemplates_MessageTemplateId",
                table: "CommunicationLogs");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "CommunicationLogs");

            migrationBuilder.DropColumn(
                name: "Recipient",
                table: "CommunicationLogs");

            migrationBuilder.RenameColumn(
                name: "MessageTemplateId",
                table: "CommunicationLogs",
                newName: "TemplateId");

            migrationBuilder.RenameIndex(
                name: "IX_CommunicationLogs_MessageTemplateId",
                table: "CommunicationLogs",
                newName: "IX_CommunicationLogs_TemplateId");

            migrationBuilder.AlterColumn<int>(
                name: "BookingId",
                table: "CommunicationLogs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "CommunicationLogs",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderMessageId",
                table: "CommunicationLogs",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "CommunicationLogs",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                table: "CommunicationLogs",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "GuestId",
                table: "CommunicationLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "CommunicationLogs",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ToAddress",
                table: "CommunicationLogs",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TemplateVersion",
                table: "CommunicationLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "CommunicationLogs",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "CommunicationLogs",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "CommunicationLogs",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "CommunicationLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "CommunicationLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAtUtc",
                table: "CommunicationLogs",
                type: "datetime",
                nullable: true);

            migrationBuilder.Sql("UPDATE CommunicationLogs SET IdempotencyKey = CONVERT(varchar(150), NEWID()) WHERE IdempotencyKey IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                table: "CommunicationLogs",
                type: "varchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLogs_GuestId",
                table: "CommunicationLogs",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLogs_IdempotencyKey",
                table: "CommunicationLogs",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationLogs_Bookings_BookingId",
                table: "CommunicationLogs",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationLogs_Guests_GuestId",
                table: "CommunicationLogs",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationLogs_MessageTemplates_TemplateId",
                table: "CommunicationLogs",
                column: "TemplateId",
                principalTable: "MessageTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationLogs_Bookings_BookingId",
                table: "CommunicationLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationLogs_Guests_GuestId",
                table: "CommunicationLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationLogs_MessageTemplates_TemplateId",
                table: "CommunicationLogs");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationLogs_GuestId",
                table: "CommunicationLogs");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationLogs_IdempotencyKey",
                table: "CommunicationLogs");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "CommunicationLogs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "CommunicationLogs");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "CommunicationLogs");

            migrationBuilder.DropColumn(
                name: "GuestId",
                table: "CommunicationLogs");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "CommunicationLogs");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "CommunicationLogs");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "CommunicationLogs");

            migrationBuilder.DropColumn(
                name: "SentAtUtc",
                table: "CommunicationLogs");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                table: "CommunicationLogs");

            migrationBuilder.DropColumn(
                name: "ToAddress",
                table: "CommunicationLogs");

            migrationBuilder.RenameColumn(
                name: "TemplateId",
                table: "CommunicationLogs",
                newName: "MessageTemplateId");

            migrationBuilder.RenameIndex(
                name: "IX_CommunicationLogs_TemplateId",
                table: "CommunicationLogs",
                newName: "IX_CommunicationLogs_MessageTemplateId");

            migrationBuilder.AlterColumn<int>(
                name: "BookingId",
                table: "CommunicationLogs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "CommunicationLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "ProviderMessageId",
                table: "CommunicationLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "CommunicationLogs",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                table: "CommunicationLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "CommunicationLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recipient",
                table: "CommunicationLogs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationLogs_Bookings_BookingId",
                table: "CommunicationLogs",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationLogs_MessageTemplates_MessageTemplateId",
                table: "CommunicationLogs",
                column: "MessageTemplateId",
                principalTable: "MessageTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
