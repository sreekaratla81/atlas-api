using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.DbMigrator;

public static class MigratorApp
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

        return await RunAsync(options, executor, logger, output, error, redactedTarget, cancellationToken);
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
            logger.LogError("Migration failed for {Target}. Error: {ErrorType}", redactedTarget, ex.GetType().Name);
            await error.WriteLineAsync($"Migration failed for {redactedTarget}.");
            return 1;
        }
    }
}
