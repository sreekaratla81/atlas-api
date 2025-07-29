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

        var dbName = $"AtlasHomestays_TestDb_{Guid.NewGuid()}";
        var connectionString =
            Environment.GetEnvironmentVariable("Atlas_TestDb") ??
            $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;";

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            Environment.SetEnvironmentVariable("DEFAULT_CONNECTION", connectionString);
            services.AddDbContext<AppDbContext>(o =>
                o.UseSqlServer(connectionString));

            using (var scope = services.BuildServiceProvider().CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.Database.EnsureDeleted();   // Clean test schema
                db.Database.Migrate();         // Apply EF Core migrations

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
