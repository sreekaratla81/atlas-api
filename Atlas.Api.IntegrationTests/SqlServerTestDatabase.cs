using Atlas.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.IntegrationTests;

public class SqlServerTestDatabase : IAsyncLifetime, IDisposable
{
    private readonly string _databaseName;
    private readonly string _connectionString;
    private readonly string _masterConnectionString;

    public SqlServerTestDatabase()
    {
        _databaseName = $"AtlasHomestays_TestDb_{TestRunId.Value}";
        _connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;";
        _masterConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;";
    }

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        await EnsureDatabaseExistsAsync();
        await ApplyMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        await DropDatabaseAsync();
    }

    public void Dispose()
    {
        DropDatabaseAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    private async Task EnsureDatabaseExistsAsync()
    {
        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID('{_databaseName}') IS NULL CREATE DATABASE [{_databaseName}];";
        await command.ExecuteNonQueryAsync();
    }

    private async Task ApplyMigrationsAsync()
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        builder.UseSqlServer(_connectionString, sqlOptions =>
            sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

        await using var context = new AppDbContext(builder.Options);
        await context.Database.MigrateAsync();
    }

    private async Task DropDatabaseAsync()
    {
        if (bool.TryParse(Environment.GetEnvironmentVariable("ATLAS_TEST_KEEP_DB"), out var keepDb) && keepDb)
        {
            return;
        }

        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"IF DB_ID('{_databaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{_databaseName}];
END";

        await command.ExecuteNonQueryAsync();
    }
}
