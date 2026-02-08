using Atlas.Api;
using Atlas.Api.Data;
using Microsoft.AspNetCore.Http;
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
    public CustomWebApplicationFactory(string? connectionString = null)
    {
        ConnectionString = connectionString;
    }

    public PathString PathBase { get; } = new("/");
    public string? ConnectionString { get; set; }

    public string ApiRoute(string relativePath)
    {
        var trimmedPath = relativePath.TrimStart('/');
        var basePath = PathBase.HasValue ? PathBase.Value!.TrimEnd('/') : string.Empty;

        if (string.IsNullOrEmpty(basePath))
        {
            return $"/{trimmedPath}";
        }

        return $"{basePath}/{trimmedPath}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTest");
        Environment.SetEnvironmentVariable("ATLAS_DELETE_BEHAVIOR", "Cascade");

        var dbName = $"AtlasHomestays_TestDb_{TestRunId.Value}";
        var connectionString = ConnectionString ??
            Environment.GetEnvironmentVariable("Atlas_TestDb") ??
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
            $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;";

        TestConnectionStringGuard.Validate(connectionString, nameof(CustomWebApplicationFactory));
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connectionString);

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
