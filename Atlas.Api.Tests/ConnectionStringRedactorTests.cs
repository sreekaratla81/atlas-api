namespace Atlas.Api.Tests;

public class ConnectionStringRedactorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Redact_ReturnsRedacted_WhenConnectionStringMissing(string? connectionString)
    {
        var redacted = ConnectionStringRedactor.Redact(connectionString);

        Assert.Equal("<redacted>", redacted);
    }

    [Fact]
    public void Redact_ReturnsServerAndDatabase_WhenPresent()
    {
        const string connectionString =
            "Server=tcp:atlas.database.windows.net,1433;Database=AtlasDb;User ID=atlas;Password=secret;Encrypt=True;";

        var redacted = ConnectionStringRedactor.Redact(connectionString);

        Assert.Equal("Server=tcp:atlas.database.windows.net,1433;Database=AtlasDb", redacted);
    }

    [Fact]
    public void Redact_ReturnsRedacted_WhenServerAndDatabaseMissing()
    {
        const string connectionString = "User ID=atlas;Password=secret;";

        var redacted = ConnectionStringRedactor.Redact(connectionString);

        Assert.Equal("<redacted>", redacted);
    }
}
