using Atlas.DbMigrator;

var exitCode = await MigratorApp.RunAsync(args, Console.Out, Console.Error, CancellationToken.None);
Environment.Exit(exitCode);
