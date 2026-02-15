using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    public partial class AddWhatsAppInboundMessage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppInboundMessage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    FromNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ToNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    GuestId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppInboundMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppInboundMessage_Booking_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Booking",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppInboundMessage_Guest_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppInboundMessage_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInboundMessage_BookingId",
                table: "WhatsAppInboundMessage",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInboundMessage_GuestId",
                table: "WhatsAppInboundMessage",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInboundMessage_TenantId",
                table: "WhatsAppInboundMessage",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInboundMessage_TenantId_Provider_ProviderMessageId",
                table: "WhatsAppInboundMessage",
                columns: new[] { "TenantId", "Provider", "ProviderMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInboundMessage_TenantId_ReceivedAtUtc",
                table: "WhatsAppInboundMessage",
                columns: new[] { "TenantId", "ReceivedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppInboundMessage");
        }
    }
}
