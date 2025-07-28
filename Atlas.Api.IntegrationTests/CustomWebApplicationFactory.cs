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

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTest");

        // Create a unique database name for each test run to avoid
        // conflicts with leftover connections or data from previous runs.
        var dbName = $"AtlasHomestays_TestDb_{Guid.NewGuid()}";
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;";
        Environment.SetEnvironmentVariable("DEFAULT_CONNECTION", connectionString);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString));

            using (var scope = services.BuildServiceProvider().CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureDeleted();
                db.Database.Migrate(); // This applies all migrations and creates the schema

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
