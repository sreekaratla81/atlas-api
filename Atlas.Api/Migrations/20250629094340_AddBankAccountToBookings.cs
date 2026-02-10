using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Atlas.Api.Data;

#nullable disable

namespace Atlas.Api.Migrations
{
    [Migration("20250629094340_AddBankAccountToBookings")]
    [DbContext(typeof(AppDbContext))]
    public partial class AddBankAccountToBookings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Placeholder; will be backfilled from git history
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Placeholder; will be backfilled from git history
        }
    }
}
