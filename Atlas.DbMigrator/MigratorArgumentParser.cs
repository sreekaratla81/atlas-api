namespace Atlas.DbMigrator;

public static class MigratorArgumentParser
{
    public static bool TryParse(string[] args, out MigratorOptions options, out string? error)
    {
        options = new MigratorOptions(string.Empty, false);
        error = null;

        string? connectionString = null;
        var checkOnly = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--connection":
                    if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        error = "Missing value for --connection.";
                        return false;
                    }

                    var rawConnection = args[i + 1];
                    if (!TryResolveConnectionString(rawConnection, out connectionString, out error))
                    {
                        return false;
                    }

                    i++;
                    break;
                case "--check-only":
                    checkOnly = true;
                    break;
                default:
                    error = $"Unknown argument '{arg}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            error = "--connection is required.";
            return false;
        }

        options = new MigratorOptions(connectionString, checkOnly);
        return true;
    }

    private static bool TryResolveConnectionString(string rawConnection, out string? connectionString, out string? error)
    {
        connectionString = null;
        error = null;

        if (string.IsNullOrWhiteSpace(rawConnection))
        {
            error = "Missing value for --connection.";
            return false;
        }

        var trimmed = rawConnection.Trim();
        var envVarName = TryGetEnvironmentVariableName(trimmed);
        if (!string.IsNullOrWhiteSpace(envVarName))
        {
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            if (string.IsNullOrWhiteSpace(envValue))
            {
                error = $"Environment variable '{envVarName}' is not set.";
                return false;
            }

            connectionString = envValue;
            return true;
        }

        var expanded = Environment.ExpandEnvironmentVariables(rawConnection);
        connectionString = expanded;
        return true;
    }

    private static string? TryGetEnvironmentVariableName(string value)
    {
        if (value.Length > 2 && value.StartsWith('%') && value.EndsWith('%'))
        {
            return value[1..^1];
        }

        if (value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}') && value.Length > 3)
        {
            return value[2..^1];
        }

        if (value.StartsWith('$') && value.Length > 1)
        {
            var candidate = value[1..];
            foreach (var ch in candidate)
            {
                if (!char.IsLetterOrDigit(ch) && ch != '_')
                {
                    return null;
                }
            }

            return candidate;
        }

        return null;
    }
}
