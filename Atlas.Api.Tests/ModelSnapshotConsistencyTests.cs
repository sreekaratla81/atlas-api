using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Linq;
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
        var runtimeInitializer = context.GetService<IModelRuntimeInitializer>();
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;

        var initializedCurrentModel = runtimeInitializer.Initialize(designTimeModel, designTime: true);
        var snapshotModelDefinition = migrationsAssembly.ModelSnapshot?.Model;
        var initializedSnapshotModel = snapshotModelDefinition == null
            ? null
            : runtimeInitializer.Initialize(snapshotModelDefinition, designTime: true);

        var currentModel = initializedCurrentModel.GetRelationalModel();
        var snapshotModel = initializedSnapshotModel?.GetRelationalModel();

        Assert.True(snapshotModel != null, "Model snapshot is missing. Add a migration to create it.");

        var hasDifferences = differ.HasDifferences(snapshotModel!, currentModel);

        Assert.False(
            hasDifferences,
            "Model snapshot does not match the current model. Add a new migration to update it.");
    }

    [Fact]
    public void ListingDailyRate_ShouldUseTenantScopedUniqueIndexOnly()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Dummy;Trusted_Connection=True;")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(ListingDailyRate));
        Assert.NotNull(entityType);

        var indexes = entityType!.GetIndexes().ToList();
        Assert.Contains(indexes, index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(ListingDailyRate.TenantId),
                nameof(ListingDailyRate.ListingId),
                nameof(ListingDailyRate.Date)
            }));

        Assert.DoesNotContain(indexes, index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(ListingDailyRate.ListingId),
                nameof(ListingDailyRate.Date)
            }));
    }

    [Fact]
    public void CommunicationLog_ShouldUseTenantScopedIdempotencyKeyUniqueIndex()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Dummy;Trusted_Connection=True;")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(CommunicationLog));
        Assert.NotNull(entityType);

        var indexes = entityType!.GetIndexes().ToList();
        Assert.Contains(indexes, index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(CommunicationLog.TenantId),
                nameof(CommunicationLog.IdempotencyKey)
            }));

        Assert.DoesNotContain(indexes, index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(CommunicationLog.IdempotencyKey)
            }));
    }

    [Fact]
    public void AutomationSchedule_ShouldHaveBookingForeignKeyAndTenantScopedUniqueIndex()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Dummy;Trusted_Connection=True;")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(AutomationSchedule));
        Assert.NotNull(entityType);

        var foreignKeys = entityType!.GetForeignKeys().ToList();
        Assert.Contains(foreignKeys, foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Booking) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(AutomationSchedule.BookingId)
            }));

        var indexes = entityType.GetIndexes().ToList();
        Assert.Contains(indexes, index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(AutomationSchedule.TenantId),
                nameof(AutomationSchedule.BookingId),
                nameof(AutomationSchedule.EventType),
                nameof(AutomationSchedule.DueAtUtc)
            }));
    }
}
