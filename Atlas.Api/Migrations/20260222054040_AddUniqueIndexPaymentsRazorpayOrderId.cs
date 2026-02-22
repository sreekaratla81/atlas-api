using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexPaymentsRazorpayOrderId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_RazorpayOrderId_Unique",
                table: "Payments",
                column: "RazorpayOrderId",
                unique: true,
                filter: "[RazorpayOrderId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_RazorpayOrderId_Unique",
                table: "Payments");
        }
    }
}
