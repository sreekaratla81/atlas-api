using Atlas.DbMigrator;
using Xunit;

namespace Atlas.DbMigrator.Tests;

public class MigratorArgumentParserTests
{
    [Fact]
    public void TryParse_ReturnsFalse_WhenConnectionMissing()
    {
        var success = MigratorArgumentParser.TryParse(["--check-only"], out _, out var error);

        Assert.False(success);
        Assert.Equal("--connection is required.", error);
    }

    [Fact]
    public void TryParse_ReturnsFalse_WhenUnknownArgumentProvided()
    {
        var success = MigratorArgumentParser.TryParse(["--connection", "Server=.;Database=Atlas;", "--unexpected"], out _, out var error);

        Assert.False(success);
        Assert.Equal("Unknown argument '--unexpected'.", error);
    }

    [Fact]
    public void TryParse_ReturnsOptions_WhenArgumentsValid()
    {
        var success = MigratorArgumentParser.TryParse(["--connection", "Server=.;Database=Atlas;", "--check-only"], out var options, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("Server=.;Database=Atlas;", options.ConnectionString);
        Assert.True(options.CheckOnly);
    }
}
