using Atlas.Api;
using Atlas.Api.Data;
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
        builder.UseEnvironment("Test");
        Environment.SetEnvironmentVariable("DEFAULT_CONNECTION", "Server=(localdb)\\MSSQLLocalDB;Database=AtlasHomestays_TestDb;Trusted_Connection=True;");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=AtlasHomestays_TestDb;Trusted_Connection=True;"));

            using (var scope = services.BuildServiceProvider().CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureDeleted();
                db.Database.Migrate();

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
