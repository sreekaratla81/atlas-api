using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameTablesToSingular : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropForeignKey(
                name: "FK_ListingDailyRates_Listings_ListingId",
                table: "ListingDailyRates");

            migrationBuilder.DropForeignKey(
                name: "FK_ListingPricings_Listings_ListingId",
                table: "ListingPricings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AutomationSchedules",
                table: "AutomationSchedules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CommunicationLogs",
                table: "CommunicationLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ListingDailyRates",
                table: "ListingDailyRates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ListingPricings",
                table: "ListingPricings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MessageTemplates",
                table: "MessageTemplates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OutboxMessages",
                table: "OutboxMessages");

            migrationBuilder.RenameTable(
                name: "AutomationSchedules",
                newName: "AutomationSchedule");

            migrationBuilder.RenameTable(
                name: "CommunicationLogs",
                newName: "CommunicationLog");

            migrationBuilder.RenameTable(
                name: "ListingDailyRates",
                newName: "ListingDailyRate");

            migrationBuilder.RenameTable(
                name: "ListingPricings",
                newName: "ListingPricing");

            migrationBuilder.RenameTable(
                name: "MessageTemplates",
                newName: "MessageTemplate");

            migrationBuilder.RenameTable(
                name: "OutboxMessages",
                newName: "OutboxMessage");

            migrationBuilder.RenameIndex(
                name: "IX_CommunicationLogs_BookingId",
                table: "CommunicationLog",
                newName: "IX_CommunicationLog_BookingId");

            migrationBuilder.RenameIndex(
                name: "IX_CommunicationLogs_GuestId",
                table: "CommunicationLog",
                newName: "IX_CommunicationLog_GuestId");

            migrationBuilder.RenameIndex(
                name: "IX_CommunicationLogs_IdempotencyKey",
                table: "CommunicationLog",
                newName: "IX_CommunicationLog_IdempotencyKey");

            migrationBuilder.RenameIndex(
                name: "IX_CommunicationLogs_TemplateId",
                table: "CommunicationLog",
                newName: "IX_CommunicationLog_TemplateId");

            migrationBuilder.RenameIndex(
                name: "IX_ListingDailyRates_ListingId_Date",
                table: "ListingDailyRate",
                newName: "IX_ListingDailyRate_ListingId_Date");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AutomationSchedule",
                table: "AutomationSchedule",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CommunicationLog",
                table: "CommunicationLog",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ListingDailyRate",
                table: "ListingDailyRate",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ListingPricing",
                table: "ListingPricing",
                column: "ListingId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MessageTemplate",
                table: "MessageTemplate",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OutboxMessage",
                table: "OutboxMessage",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationLog_Bookings_BookingId",
                table: "CommunicationLog",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationLog_Guests_GuestId",
                table: "CommunicationLog",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationLog_MessageTemplate_TemplateId",
                table: "CommunicationLog",
                column: "TemplateId",
                principalTable: "MessageTemplate",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ListingDailyRate_Listings_ListingId",
                table: "ListingDailyRate",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ListingPricing_Listings_ListingId",
                table: "ListingPricing",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationLog_Bookings_BookingId",
                table: "CommunicationLog");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationLog_Guests_GuestId",
                table: "CommunicationLog");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationLog_MessageTemplate_TemplateId",
                table: "CommunicationLog");

            migrationBuilder.DropForeignKey(
                name: "FK_ListingDailyRate_Listings_ListingId",
                table: "ListingDailyRate");

            migrationBuilder.DropForeignKey(
                name: "FK_ListingPricing_Listings_ListingId",
                table: "ListingPricing");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AutomationSchedule",
                table: "AutomationSchedule");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CommunicationLog",
                table: "CommunicationLog");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ListingDailyRate",
                table: "ListingDailyRate");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ListingPricing",
                table: "ListingPricing");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MessageTemplate",
                table: "MessageTemplate");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OutboxMessage",
                table: "OutboxMessage");

            migrationBuilder.RenameTable(
                name: "AutomationSchedule",
                newName: "AutomationSchedules");

            migrationBuilder.RenameTable(
                name: "CommunicationLog",
                newName: "CommunicationLogs");

            migrationBuilder.RenameTable(
                name: "ListingDailyRate",
                newName: "ListingDailyRates");

            migrationBuilder.RenameTable(
                name: "ListingPricing",
                newName: "ListingPricings");

            migrationBuilder.RenameTable(
                name: "MessageTemplate",
                newName: "MessageTemplates");

            migrationBuilder.RenameTable(
                name: "OutboxMessage",
                newName: "OutboxMessages");

            migrationBuilder.RenameIndex(
                name: "IX_CommunicationLog_BookingId",
                table: "CommunicationLogs",
                newName: "IX_CommunicationLogs_BookingId");

            migrationBuilder.RenameIndex(
                name: "IX_CommunicationLog_GuestId",
                table: "CommunicationLogs",
                newName: "IX_CommunicationLogs_GuestId");

            migrationBuilder.RenameIndex(
                name: "IX_CommunicationLog_IdempotencyKey",
                table: "CommunicationLogs",
                newName: "IX_CommunicationLogs_IdempotencyKey");

            migrationBuilder.RenameIndex(
                name: "IX_CommunicationLog_TemplateId",
                table: "CommunicationLogs",
                newName: "IX_CommunicationLogs_TemplateId");

            migrationBuilder.RenameIndex(
                name: "IX_ListingDailyRate_ListingId_Date",
                table: "ListingDailyRates",
                newName: "IX_ListingDailyRates_ListingId_Date");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AutomationSchedules",
                table: "AutomationSchedules",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CommunicationLogs",
                table: "CommunicationLogs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ListingDailyRates",
                table: "ListingDailyRates",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ListingPricings",
                table: "ListingPricings",
                column: "ListingId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MessageTemplates",
                table: "MessageTemplates",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OutboxMessages",
                table: "OutboxMessages",
                column: "Id");

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

            migrationBuilder.AddForeignKey(
                name: "FK_ListingDailyRates_Listings_ListingId",
                table: "ListingDailyRates",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ListingPricings_Listings_ListingId",
                table: "ListingPricings",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
