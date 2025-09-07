using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestPortalAndStorageMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Guests_GuestId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ListingId",
                table: "Bookings");

            migrationBuilder.AddColumn<string>(
                name: "BlobContainer",
                table: "Listings",
                type: "nvarchar(63)",
                maxLength: 63,
                nullable: false,
                defaultValue: "listing-images");

            migrationBuilder.AddColumn<string>(
                name: "BlobPrefix",
                table: "Listings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CoverImage",
                table: "Listings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NightlyPrice",
                table: "Listings",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                table: "Listings",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Listings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE Listings SET Slug = LOWER(REPLACE(Name,' ','-')) WHERE Slug = '' OR Slug IS NULL");
            migrationBuilder.Sql("UPDATE Listings SET BlobContainer = 'listing-images' WHERE BlobContainer IS NULL OR BlobContainer = ''");
            migrationBuilder.Sql("UPDATE Listings SET BlobPrefix = CAST(Id AS nvarchar(10)) + '/' WHERE BlobPrefix IS NULL OR BlobPrefix = ''");

            migrationBuilder.CreateTable(
                name: "ListingMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsCover = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingMedia_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Slug",
                table: "Listings",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ListingId_CheckinDate_CheckoutDate",
                table: "Bookings",
                columns: new[] { "ListingId", "CheckinDate", "CheckoutDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ListingMedia_ListingId",
                table: "ListingMedia",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingMedia_ListingId_BlobName",
                table: "ListingMedia",
                columns: new[] { "ListingId", "BlobName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingMedia_ListingId_IsCover",
                table: "ListingMedia",
                columns: new[] { "ListingId", "IsCover" },
                unique: true,
                filter: "[IsCover] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Guests_GuestId",
                table: "Bookings",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Guests_GuestId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "ListingMedia");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Slug",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ListingId_CheckinDate_CheckoutDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "BlobContainer",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "BlobPrefix",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "CoverImage",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "NightlyPrice",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ShortDescription",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Listings");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ListingId",
                table: "Bookings",
                column: "ListingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Guests_GuestId",
                table: "Bookings",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
