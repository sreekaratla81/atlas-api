using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Atlas.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var env = builder.Environment;

            builder.Configuration
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            const string CorsPolicy = "AtlasCors";

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: CorsPolicy, policy =>
                {
                    policy
                        .SetIsOriginAllowed(origin =>
                        {
                            try
                            {
                                var uri = new Uri(origin);
                                var host = uri.Host.ToLowerInvariant();
                                return
                                    // Local dev
                                    (origin == "http://localhost:5173") ||
                                    (origin == "http://127.0.0.1:5173") ||
                                    // Production
                                    (origin == "https://admin.atlashomestays.com") ||
                                    // Cloudflare Pages previews (optional)
                                    host.EndsWith(".pages.dev");
                            }
                            catch { return false; }
                        })
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            builder.Services
                .AddControllers(options =>
                {
                    options.Filters.Add<Atlas.Api.Filters.ValidateModelAttribute>();
                })
                .AddJsonOptions(opts =>
                {
                    opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Atlas API", Version = "v1" });
                c.CustomSchemaIds(type => type.FullName);
                c.IgnoreObsoleteProperties();
            });

            var connectionString = Environment.GetEnvironmentVariable("DEFAULT_CONNECTION")
                ?? builder.Configuration.GetConnectionString("DefaultConnection");

            Console.WriteLine($"[DEBUG] Using connection string: {connectionString}");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is not configured.");
            }
            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
                if (builder.Environment.IsDevelopment())
                {
                    options.LogTo(Console.WriteLine);
                }
            });

            var jwtKey = builder.Configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
            }

            // TODO: Re-enable authentication before going to production
            // builder.Services.AddAuthentication("Bearer")
            //     .AddJwtBearer("Bearer", options =>
            //     {
            //         options.TokenValidationParameters = new TokenValidationParameters
            //         {
            //             ValidateIssuer = false,
            //             ValidateAudience = false,
            //             ValidateLifetime = true,
            //             ValidateIssuerSigningKey = true,
            //             IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey ?? string.Empty))
            //         };
            //     });

            var app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Atlas API v1");
                c.RoutePrefix = "swagger";
            });

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseCors(CorsPolicy);

            // Optional: HTTPS redirect
            // app.UseHttpsRedirection();

            // app.UseAuthentication();
            // app.UseAuthorization();

            app.MapMethods("/test-cors", new[] { "OPTIONS" }, () => Results.Ok());

            if (!app.Environment.IsProduction())
            {
                app.UsePathBase("/api");
            }

            app.MapControllers();

            app.Run();
        }
    }
}
