using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Linq;
using System.Reflection;
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
        var currentContextType = database.GetService<ICurrentDbContext>().Context.GetType();

        return migrationsAssembly.Assembly.GetExportedTypes()
            .Where(type => typeof(Migration).IsAssignableFrom(type) && !type.IsAbstract)
            .Any(type => type.GetCustomAttributes<DbContextAttribute>(inherit: true)
                .Any(attribute => attribute.ContextType == currentContextType));
    }
}
