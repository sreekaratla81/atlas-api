using Atlas.Api;
using Atlas.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;

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

        Environment.SetEnvironmentVariable("JWT_KEY", "testkey123");
        Environment.SetEnvironmentVariable("DEFAULT_CONNECTION", connectionString);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o =>
                o.UseSqlServer(connectionString, sqlOptions =>
                    sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                 .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
        });
    }
}
