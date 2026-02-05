using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Atlas.DbMigrator;

public interface IMigrationExecutor
{
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken);
    Task ApplyMigrationsAsync(CancellationToken cancellationToken);
}

public sealed class EfMigrationExecutor(AppDbContext dbContext) : IMigrationExecutor
{
    private readonly AppDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken)
    {
        var pending = await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        return pending.ToArray();
    }

    public Task ApplyMigrationsAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.MigrateAsync(cancellationToken);
    }
}
