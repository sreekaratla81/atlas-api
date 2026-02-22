using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.DbMigrator;

public sealed class MigratorApp
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        if (!MigratorArgumentParser.TryParse(args, out var options, out var parseError))
        {
            await error.WriteLineAsync(parseError ?? "Invalid arguments.");
            return 1;
        }

        var redactedTarget = ConnectionStringRedactor.Redact(options.ConnectionString);

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddDbContext<AppDbContext>(dbOptions => dbOptions.UseSqlServer(options.ConnectionString));
        services.AddScoped<IMigrationExecutor, EfMigrationExecutor>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MigratorApp>>();
        var executor = scope.ServiceProvider.GetRequiredService<IMigrationExecutor>();

        var result = await RunAsync(options, executor, logger, output, error, redactedTarget, cancellationToken);
        if (result == 0 && !options.CheckOnly)
        {
            try
            {
                using var seedScope = provider.CreateScope();
                var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (!await db.EnvironmentMarkers.AnyAsync(cancellationToken))
                {
                    db.EnvironmentMarkers.Add(new EnvironmentMarker { Marker = "DEV" });
                    await db.SaveChangesAsync(cancellationToken);
                    logger.LogInformation("Seeded EnvironmentMarker for {Target}.", redactedTarget);
                }

                await SeedPlatformAdminAsync(db, logger, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Post-migration seeding failed (non-fatal). Tables may not exist yet in {Target}.", redactedTarget);
            }
        }
        return result;
    }

    private static async Task SeedPlatformAdminAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        const string email = "sreekar.atla@gmail.com";
        var exists = await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct);
        if (exists) return;

        var atlas = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == "atlas", ct);
        if (atlas is null) return;

        db.Users.Add(new User
        {
            TenantId = atlas.Id,
            Name = "Sreekar (Platform Admin)",
            Email = email,
            Phone = "0000000000",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("AtlasAdmin!2026"),
            Role = "platform-admin",
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded platform-admin user {Email} on tenant {Slug}.", email, atlas.Slug);
    }

    internal static async Task<int> RunAsync(
        MigratorOptions options,
        IMigrationExecutor executor,
        ILogger logger,
        TextWriter output,
        TextWriter error,
        string redactedTarget,
        CancellationToken cancellationToken)
    {
        try
        {
            var pending = await executor.GetPendingMigrationsAsync(cancellationToken);

            if (options.CheckOnly)
            {
                if (pending.Count > 0)
                {
                    foreach (var migration in pending)
                    {
                        await output.WriteLineAsync(migration);
                    }

                    return 2;
                }

                logger.LogInformation("No pending migrations for {Target}.", redactedTarget);
                return 0;
            }

            logger.LogInformation("Applying migrations for {Target}.", redactedTarget);
            await executor.ApplyMigrationsAsync(cancellationToken);
            logger.LogInformation("Migrations applied for {Target}.", redactedTarget);
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError("Migration failed for {Target}. Error: {ErrorType} — {ErrorMessage}", redactedTarget, ex.GetType().Name, ex.Message);
            await error.WriteLineAsync($"Migration failed for {redactedTarget}.");
            return 1;
        }
    }
}
