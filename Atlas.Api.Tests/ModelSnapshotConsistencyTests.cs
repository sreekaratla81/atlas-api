using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Atlas.Api.Tests;

public class ModelSnapshotConsistencyTests
{
    [Fact]
    public void ModelSnapshot_ShouldMatch_CurrentModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Dummy;Trusted_Connection=True;")
            .Options;

        using var context = new AppDbContext(options);
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var differ = context.GetService<IMigrationsModelDiffer>();

        var currentModel = context.Model.GetRelationalModel();
        var snapshotModel = migrationsAssembly.ModelSnapshot?.Model.GetRelationalModel();

        Assert.NotNull(snapshotModel, "Model snapshot is missing. Add a migration to create it.");

        var hasDifferences = differ.HasDifferences(snapshotModel!, currentModel);

        Assert.False(
            hasDifferences,
            "Model snapshot does not match the current model. Add a new migration to update it.");
    }
}
