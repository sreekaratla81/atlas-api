using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Atlas.Api.Data;

#nullable disable

namespace Atlas.Api.Migrations
{
    [Migration("20251109120000_RenameTablesToSingular")]
    [DbContext(typeof(AppDbContext))]
    public partial class RenameTablesToSingular : Migration
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
