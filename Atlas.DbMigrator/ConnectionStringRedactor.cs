using System.Data.Common;

namespace Atlas.DbMigrator;

public static class ConnectionStringRedactor
{
    private static readonly string[] ServerKeys =
        ["Server", "Data Source", "Address", "Addr", "Network Address"];

    private static readonly string[] DatabaseKeys =
        ["Database", "Initial Catalog"];

    public static string Redact(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "<redacted>";
        }

        try
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            var server = TryGetValue(builder, ServerKeys);
            var database = TryGetValue(builder, DatabaseKeys);

            if (string.IsNullOrWhiteSpace(server) && string.IsNullOrWhiteSpace(database))
            {
                return "<redacted>";
            }

            var parts = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(server))
            {
                parts.Add($"Server={server}");
            }

            if (!string.IsNullOrWhiteSpace(database))
            {
                parts.Add($"Database={database}");
            }

            return string.Join(";", parts);
        }
        catch (ArgumentException)
        {
            return "<redacted>";
        }
    }

    private static string? TryGetValue(DbConnectionStringBuilder builder, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var value) && value is not null)
            {
                return value.ToString();
            }
        }

        return null;
    }
}
