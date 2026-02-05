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

                    connectionString = args[i + 1];
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
}
