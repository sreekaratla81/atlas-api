using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <summary>No-op migration to sync AppDbContextModelSnapshot with current model (fixes PendingModelChangesWarning).</summary>
    public partial class SyncModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty - snapshot was out of sync with model; this migration exists to restore snapshot only.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty
        }
    }
}
