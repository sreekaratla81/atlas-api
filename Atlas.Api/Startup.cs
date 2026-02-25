using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Atlas.Api.Services.Tenancy;

namespace Atlas.Api;

/// <summary>
/// Exists so Swashbuckle.AspNetCore.Cli (swagger tofile) can find a type named Startup
/// when building an in-process host. The main app runs from Program.cs; this class
/// is only used by the CLI for OpenAPI generation.
/// </summary>
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddControllers()
            .AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Atlas API", Version = "v1" });
            c.CustomSchemaIds(type => type.FullName);
            c.IgnoreObsoleteProperties();
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                c.IncludeXmlComments(xmlPath);
            c.AddSecurityDefinition(TenantProvider.TenantSlugHeaderName, new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Name = TenantProvider.TenantSlugHeaderName,
                Description = "Tenant slug (e.g. atlas). Required in Production.",
                Type = SecuritySchemeType.ApiKey
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = TenantProvider.TenantSlugHeaderName } }, Array.Empty<string>() }
            });
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Atlas API v1");
            c.RoutePrefix = "swagger";
        });
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}
