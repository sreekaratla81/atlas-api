using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAccountToBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BankAccountId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BankAccountId",
                table: "Bookings",
                column: "BankAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_BankAccounts_BankAccountId",
                table: "Bookings",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_BankAccounts_BankAccountId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_BankAccountId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "Bookings");
        }
    }
}
