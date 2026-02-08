using Atlas.Api;
using Atlas.Api.Data;
using Microsoft.Data.SqlClient;
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
    private readonly string _connectionString;
    public PathString PathBase { get; } = new("/");

    public CustomWebApplicationFactory(string connectionString)
    {
        _connectionString = EnsureLocalDbConnectionString(connectionString);
    }

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
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _connectionString);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o =>
                o.UseSqlServer(_connectionString, sqlOptions =>
                    sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                 .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
        });
    }

    private static string EnsureLocalDbConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource ?? string.Empty;
        if (!dataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Integration tests must use LocalDb. Refusing to use data source '{dataSource}'.");
        }

        return builder.ConnectionString;
    }
}
