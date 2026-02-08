using Microsoft.Data.SqlClient;

namespace Atlas.Api.IntegrationTests;

public static class TestConnectionStringGuard
{
    public const string TestDatabasePrefix = "AtlasHomestays_TestDb_";
    public const string AllowNonLocalDbEnvVar = "ATLAS_ALLOW_NON_LOCALDB_TESTS";

    public static void Validate(string connectionString, string source)
    {
        if (IsOverrideEnabled())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Integration tests require a LocalDb connection string, but '{source}' was empty. " +
                $"Set Atlas_TestDb to a LocalDb test database starting with '{TestDatabasePrefix}'.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource?.Trim();
        var databaseName = builder.InitialCatalog?.Trim();

        var isLocalDb = !string.IsNullOrWhiteSpace(dataSource) &&
            dataSource.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase);
        var hasTestPrefix = !string.IsNullOrWhiteSpace(databaseName) &&
            databaseName.StartsWith(TestDatabasePrefix, StringComparison.OrdinalIgnoreCase);

        if (isLocalDb && hasTestPrefix)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Integration tests must run against LocalDb with a test database name. " +
            $"Set Atlas_TestDb to a LocalDb connection string like " +
            $"\"Server=(localdb)\\\\MSSQLLocalDB;Database={TestDatabasePrefix}<id>;Trusted_Connection=True;\". " +
            $"Current data source: '{dataSource ?? "<empty>"}', database: '{databaseName ?? "<empty>"}'. " +
            $"To override (not recommended), set {AllowNonLocalDbEnvVar}=true.");
    }

    private static bool IsOverrideEnabled()
    {
        return bool.TryParse(Environment.GetEnvironmentVariable(AllowNonLocalDbEnvVar), out var allow) && allow;
    }
}
