using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Api.Data;

internal static class DatabaseSchemaInitializer
{
    internal static async Task<bool> EnsureSchemaAsync(
        DatabaseFacade database,
        CancellationToken cancellationToken = default)
    {
        var migrations = await database.GetMigrationsAsync(cancellationToken);

        if (!migrations.Any())
        {
            await database.EnsureCreatedAsync(cancellationToken);
            return true;
        }

        await database.MigrateAsync(cancellationToken);
        return false;
    }
}
