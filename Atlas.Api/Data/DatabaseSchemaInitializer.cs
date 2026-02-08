using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
        var migrationsAssembly = database.GetService<IMigrationsAssembly>();
        if (!migrationsAssembly.Migrations.Any() &&
            !HasMigrationsInContextAssembly(database, migrationsAssembly))
        {
            await database.EnsureCreatedAsync(cancellationToken);
            return true;
        }

        await database.MigrateAsync(cancellationToken);
        return false;
    }

    private static bool HasMigrationsInContextAssembly(
        DatabaseFacade database,
        IMigrationsAssembly migrationsAssembly)
    {
        var currentContext = database.GetService<ICurrentDbContext>().Context;
        var contextAssembly = currentContext.GetType().Assembly;

        if (!ReferenceEquals(migrationsAssembly.Assembly, contextAssembly))
        {
            return false;
        }

        return contextAssembly.GetExportedTypes()
            .Any(type => typeof(Migration).IsAssignableFrom(type) && !type.IsAbstract);
    }
}
