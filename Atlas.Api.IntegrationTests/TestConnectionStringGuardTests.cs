using Xunit;

namespace Atlas.Api.IntegrationTests;

public class TestConnectionStringGuardTests
{
    [Fact]
    public void Validate_AllowsLocalDbWithTestPrefix()
    {
        var connectionString =
            $"Server=(localdb)\\MSSQLLocalDB;Database={TestConnectionStringGuard.TestDatabasePrefix}123;Trusted_Connection=True;";

        var exception = Record.Exception(() => TestConnectionStringGuard.Validate(connectionString, "test"));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_RejectsNonLocalDbDataSource()
    {
        var connectionString =
            $"Server=.;Database={TestConnectionStringGuard.TestDatabasePrefix}123;Trusted_Connection=True;";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TestConnectionStringGuard.Validate(connectionString, "test"));

        Assert.Contains("LocalDb", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsDatabaseWithoutTestPrefix()
    {
        var connectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=AtlasHomestays;Trusted_Connection=True;";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TestConnectionStringGuard.Validate(connectionString, "test"));

        Assert.Contains(TestConnectionStringGuard.TestDatabasePrefix, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AllowsOverrideForNonLocalDb()
    {
        var connectionString = "Server=.;Database=AtlasHomestays;Trusted_Connection=True;";

        using var scope = new EnvironmentVariableScope(TestConnectionStringGuard.AllowNonLocalDbEnvVar, "true");

        var exception = Record.Exception(() => TestConnectionStringGuard.Validate(connectionString, "test"));

        Assert.Null(exception);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _originalValue);
        }
    }
}
