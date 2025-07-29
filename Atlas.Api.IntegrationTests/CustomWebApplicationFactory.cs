using Atlas.Api;
using Atlas.Api.Data;
using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Api.IntegrationTests;

// ⚠️ NOTE:
// If SQL Server connection fails, tests will fallback to an in-memory database.
// This allows Codex Agent and CI environments to run integration tests without
// needing a local SQL Server setup.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTest");

        // Create a unique LocalDb name for each run. Will fall back to InMemory
        // if SQL Server/LocalDb is unavailable.
        var dbName = $"AtlasHomestays_TestDb_{Guid.NewGuid()}";
        var connectionString =
            Environment.GetEnvironmentVariable("Atlas_TestDb") ??
            $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;";

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();

            bool useInMemory = false;
            try
            {
                var testOptions = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlServer(connectionString)
                    .Options;
                using var testContext = new AppDbContext(testOptions);
                testContext.Database.OpenConnection();
                testContext.Database.CloseConnection();
            }
            catch
            {
                useInMemory = true;
            }

            if (useInMemory)
            {
                var provider = new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();
                services.AddDbContext<AppDbContext>(o =>
                {
                    o.UseInMemoryDatabase("CodexFallbackDb");
                    o.UseInternalServiceProvider(provider);
                });
            }
            else
            {
                Environment.SetEnvironmentVariable("DEFAULT_CONNECTION", connectionString);
                services.AddDbContext<AppDbContext>(o =>
                    o.UseSqlServer(connectionString));
            }

            using (var scope = services.BuildServiceProvider().CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (useInMemory)
                {
                    db.Database.EnsureCreated();
                }
                else
                {
                    db.Database.Migrate();
                }

                if (!db.Properties.Any())
                {
                    db.Properties.Add(new Atlas.Api.Models.Property
                    {
                        Name = "Test Villa",
                        Address = "Seed Address",
                        Type = "Villa",
                        OwnerName = "Owner",
                        ContactPhone = "000",
                        CommissionPercent = 10,
                        Status = "Active"
                    });
                    db.SaveChanges();
                }
            }
        });
    }
}
