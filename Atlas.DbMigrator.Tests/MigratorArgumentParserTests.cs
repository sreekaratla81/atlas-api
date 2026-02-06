using Atlas.DbMigrator;
using Xunit;

namespace Atlas.DbMigrator.Tests;

public class MigratorArgumentParserTests
{
    [Fact]
    public void TryParse_ReturnsFalse_WhenConnectionMissing()
    {
        const string envVarName = "MIGRATOR_CONNECTION";
        var original = Environment.GetEnvironmentVariable(envVarName);

        try
        {
            Environment.SetEnvironmentVariable(envVarName, null);
            var success = MigratorArgumentParser.TryParse(["--check-only"], out _, out var error);

            Assert.False(success);
            Assert.Equal("--connection is required.", error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, original);
        }
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

    [Fact]
    public void TryParse_UsesEnvironmentVariable_WhenConnectionArgumentMissing()
    {
        const string envVarName = "MIGRATOR_CONNECTION";
        var original = Environment.GetEnvironmentVariable(envVarName);

        try
        {
            Environment.SetEnvironmentVariable(envVarName, "Server=.;Database=Atlas;");

            var success = MigratorArgumentParser.TryParse(["--check-only"], out var options, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Equal("Server=.;Database=Atlas;", options.ConnectionString);
            Assert.True(options.CheckOnly);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, original);
        }
    }

    [Fact]
    public void TryParse_ExpandsEnvironmentVariableConnectionString()
    {
        const string envVarName = "ATLAS_DB_CONNECTION";
        var original = Environment.GetEnvironmentVariable(envVarName);

        try
        {
            Environment.SetEnvironmentVariable(envVarName, "Server=.;Database=Atlas;");

            var success = MigratorArgumentParser.TryParse(["--connection", $"%{envVarName}%"], out var options, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Equal("Server=.;Database=Atlas;", options.ConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, original);
        }
    }

    [Fact]
    public void TryParse_ReturnsFalse_WhenEnvironmentVariableMissing()
    {
        const string envVarName = "ATLAS_DB_CONNECTION_MISSING";
        var original = Environment.GetEnvironmentVariable(envVarName);

        try
        {
            Environment.SetEnvironmentVariable(envVarName, null);

            var success = MigratorArgumentParser.TryParse(["--connection", $"%{envVarName}%"], out _, out var error);

            Assert.False(success);
            Assert.Equal($"Environment variable '{envVarName}' is not set.", error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, original);
        }
    }
}
