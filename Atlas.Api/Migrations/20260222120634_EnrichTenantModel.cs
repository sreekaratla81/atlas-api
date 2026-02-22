using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnrichTenantModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("UPDATE [Tenants] SET [IsActive] = CASE WHEN [Status] = 'Active' THEN 1 ELSE 0 END");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Tenants");

            migrationBuilder.AddColumn<string>(
                name: "BrandColor",
                table: "Tenants",
                type: "varchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomDomain",
                table: "Tenants",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Tenants",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerEmail",
                table: "Tenants",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "Tenants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerPhone",
                table: "Tenants",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "Tenants",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "free");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Tenants",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.Sql("UPDATE [Tenants] SET [Status] = CASE WHEN [IsActive] = 1 THEN 'Active' ELSE 'Inactive' END");

            migrationBuilder.DropColumn(name: "BrandColor", table: "Tenants");
            migrationBuilder.DropColumn(name: "CustomDomain", table: "Tenants");
            migrationBuilder.DropColumn(name: "IsActive", table: "Tenants");
            migrationBuilder.DropColumn(name: "LogoUrl", table: "Tenants");
            migrationBuilder.DropColumn(name: "OwnerEmail", table: "Tenants");
            migrationBuilder.DropColumn(name: "OwnerName", table: "Tenants");
            migrationBuilder.DropColumn(name: "OwnerPhone", table: "Tenants");
            migrationBuilder.DropColumn(name: "Plan", table: "Tenants");
        }
    }
}
