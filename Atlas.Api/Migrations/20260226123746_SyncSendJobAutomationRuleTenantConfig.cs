using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class SyncSendJobAutomationRuleTenantConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AutomationRule_Tenants_TenantId",
                table: "AutomationRule");

            migrationBuilder.DropForeignKey(
                name: "FK_SendJob_Tenants_TenantId",
                table: "SendJob");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRule_TenantId",
                table: "AutomationRule",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_AutomationRule_Tenants_TenantId",
                table: "AutomationRule",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SendJob_Tenants_TenantId",
                table: "SendJob",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AutomationRule_Tenants_TenantId",
                table: "AutomationRule");

            migrationBuilder.DropForeignKey(
                name: "FK_SendJob_Tenants_TenantId",
                table: "SendJob");

            migrationBuilder.DropIndex(
                name: "IX_AutomationRule_TenantId",
                table: "AutomationRule");

            migrationBuilder.AddForeignKey(
                name: "FK_AutomationRule_Tenants_TenantId",
                table: "AutomationRule",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SendJob_Tenants_TenantId",
                table: "SendJob",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
