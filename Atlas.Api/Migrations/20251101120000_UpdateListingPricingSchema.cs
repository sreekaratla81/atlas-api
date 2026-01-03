using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateListingPricingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ListingPricings",
                table: "ListingPricings");

            migrationBuilder.DropIndex(
                name: "IX_ListingPricings_ListingId",
                table: "ListingPricings");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ListingPricings");

            migrationBuilder.DropColumn(
                name: "WeekdayRate",
                table: "ListingPricings");

            migrationBuilder.RenameColumn(
                name: "WeekendRate",
                table: "ListingPricings",
                newName: "WeekendNightlyRate");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "ListingPricings",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "BaseRate",
                table: "ListingPricings",
                newName: "BaseNightlyRate");

            migrationBuilder.AddColumn<decimal>(
                name: "ExtraGuestRate",
                table: "ListingPricings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "ListingPricings",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "INR",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "INR");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "ListingPricings",
                type: "datetime",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ListingPricings",
                table: "ListingPricings",
                column: "ListingId");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "ListingDailyRates",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.RenameColumn(
                name: "Rate",
                table: "ListingDailyRates",
                newName: "NightlyRate");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "ListingDailyRates",
                newName: "UpdatedAtUtc");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "ListingDailyRates",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "ListingDailyRates",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ListingDailyRates",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "ListingDailyRates",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "ListingDailyRates",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "ListingDailyRates",
                type: "datetime",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.Sql(
                """
                UPDATE ListingDailyRates
                SET Source = 'Manual'
                WHERE Source IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "ListingDailyRates",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.Sql(
                """
                INSERT INTO ListingPricings (ListingId, BaseNightlyRate, WeekendNightlyRate, ExtraGuestRate, Currency, UpdatedAtUtc)
                SELECT bp.ListingId, bp.BasePrice, NULL, NULL, bp.Currency, GETUTCDATE()
                FROM ListingBasePrices bp
                WHERE NOT EXISTS (
                    SELECT 1 FROM ListingPricings lp WHERE lp.ListingId = bp.ListingId
                );
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO ListingDailyRates (ListingId, Date, NightlyRate, Currency, Source, Reason, UpdatedByUserId, UpdatedAtUtc)
                SELECT o.ListingId,
                       o.Date,
                       o.Price,
                       COALESCE(bp.Currency, 'INR'),
                       'Manual',
                       NULL,
                       NULL,
                       GETUTCDATE()
                FROM ListingDailyOverrides o
                LEFT JOIN ListingBasePrices bp ON bp.ListingId = o.ListingId
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM ListingDailyRates dr
                    WHERE dr.ListingId = o.ListingId AND dr.Date = o.Date
                );
                """);

            migrationBuilder.DropTable(
                name: "ListingBasePrices");

            migrationBuilder.DropTable(
                name: "ListingDailyOverrides");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListingBasePrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "INR")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingBasePrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingBasePrices_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingDailyOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingDailyOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingDailyOverrides_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.DropPrimaryKey(
                name: "PK_ListingPricings",
                table: "ListingPricings");

            migrationBuilder.DropColumn(
                name: "ExtraGuestRate",
                table: "ListingPricings");

            migrationBuilder.RenameColumn(
                name: "WeekendNightlyRate",
                table: "ListingPricings",
                newName: "WeekendRate");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "ListingPricings",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "BaseNightlyRate",
                table: "ListingPricings",
                newName: "BaseRate");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "ListingPricings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "INR",
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldMaxLength: 10,
                oldDefaultValue: "INR");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ListingPricings",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "ListingPricings",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<decimal>(
                name: "WeekdayRate",
                table: "ListingPricings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ListingPricings",
                table: "ListingPricings",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ListingPricings_ListingId",
                table: "ListingPricings",
                column: "ListingId",
                unique: true);

            migrationBuilder.RenameColumn(
                name: "NightlyRate",
                table: "ListingDailyRates",
                newName: "Rate");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "ListingDailyRates",
                newName: "CreatedAt");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "ListingDailyRates");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "ListingDailyRates");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ListingDailyRates");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "ListingDailyRates");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "ListingDailyRates",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "ListingDailyRates",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ListingDailyRates",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.CreateIndex(
                name: "IX_ListingBasePrices_ListingId",
                table: "ListingBasePrices",
                column: "ListingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingDailyOverrides_ListingId_Date",
                table: "ListingDailyOverrides",
                columns: new[] { "ListingId", "Date" },
                unique: true);
        }
    }
}
