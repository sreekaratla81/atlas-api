namespace Atlas.DbMigrator;

public sealed record MigratorOptions(string ConnectionString, bool CheckOnly);
