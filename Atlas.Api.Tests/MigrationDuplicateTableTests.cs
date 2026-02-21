using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Atlas.Api.Tests;

/// <summary>
/// Prevents RestoreSnapshot-style migrations that recreate tables already created by InitialBaseline.
/// When snapshot is out of sync, EF can generate a migration with CreateTable for all tables—which fails
/// because InitialBaseline already created them. See docs/migrations-troubleshooting.md.
/// </summary>
public class MigrationDuplicateTableTests
{
    private static readonly HashSet<string> InitialBaselineTables = new(StringComparer.Ordinal)
    {
        "AutomationSchedule", "BankAccounts", "EnvironmentMarker", "Guests", "Incidents",
        "MessageTemplate", "OutboxMessage", "Properties", "Users", "Listings", "Bookings",
        "ListingPricing", "AvailabilityBlock", "CommunicationLog", "Payments", "ListingDailyRate"
    };

    private const string InitialBaselineId = "20250629080000";

    [Fact]
    public void NoMigrationAfterInitialBaseline_ShouldCreateTables_ThatInitialBaselineCreates()
    {
        var migrationsDir = GetMigrationsDirectory();
        if (!Directory.Exists(migrationsDir))
        {
            return; // Skip if path not resolvable (e.g. in minimal test env)
        }

        var duplicates = new List<(string MigrationFile, string Table)>();

        foreach (var file in Directory.EnumerateFiles(migrationsDir, "*.cs"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.EndsWith(".Designer") || name == "AppDbContextModelSnapshot")
                continue;

            var match = Regex.Match(name, @"^(\d{14})_");
            if (!match.Success)
                continue;

            var migrationId = match.Groups[1].Value;
            if (string.Compare(migrationId, InitialBaselineId, StringComparison.Ordinal) <= 0)
                continue; // InitialBaseline or earlier—allowed to create these tables

            var content = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(content, @"CreateTable\s*\(\s*name:\s*""([^""]+)"""))
            {
                var table = m.Groups[1].Value;
                if (InitialBaselineTables.Contains(table))
                    duplicates.Add((Path.GetFileName(file), table));
            }
        }

        Assert.False(
            duplicates.Count > 0,
            "Migrations must not CreateTable for tables already created by InitialBaseline. " +
            "If snapshot was out of sync, use an empty migration (see SyncModelSnapshot). " +
            "Duplicates found:\n" +
            string.Join("\n", duplicates.Select(d => $"  {d.MigrationFile}: CreateTable(name: \"{d.Table}\")")));
    }

    private static string GetMigrationsDirectory()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Atlas.Api", "Migrations"));
    }
}
