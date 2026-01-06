using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Collections.Immutable;
using System.Linq;
using Atlas.Api.Models;

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

            const string CorsPolicy = "AtlasCorsPolicy";
            var allowedOrigins = BuildAllowedOrigins(builder.Configuration, env);

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: CorsPolicy, policy =>
                {
                    policy
                        .WithOrigins(allowedOrigins)
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
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

            builder.Services.AddScoped<Atlas.Api.Services.AvailabilityService>();
            builder.Services.AddScoped<Atlas.Api.Services.PricingService>();
            builder.Services.AddScoped<Atlas.Api.Services.IBookingWorkflowPublisher, Atlas.Api.Services.NoOpBookingWorkflowPublisher>();

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

            using (var scope = app.Services.CreateScope())
            {
                var scopedEnv = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Guard production migrations to avoid unintended schema changes unless explicitly enabled.
                if (ShouldRunMigrations(scopedEnv, config))
                {
                    db.Database.Migrate();
                }

                ValidateEnvironmentMarker(db, scopedEnv);
            }

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

            if (app.Environment.IsDevelopment())
            {
                app.Use(async (context, next) =>
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    var origin = context.Request.Headers.Origin.ToString();

                    logger.LogInformation("CORS Origin header: {Origin}", string.IsNullOrWhiteSpace(origin) ? "<none>" : origin);
                    logger.LogInformation("Applying CORS policy: {CorsPolicy}", CorsPolicy);

                    await next();
                });
            }

            app.UseCors(CorsPolicy);

            // Optional: HTTPS redirect
            // app.UseHttpsRedirection();

            // app.UseAuthentication();
            // app.UseAuthorization();

            app.MapMethods("/test-cors", new[] { "OPTIONS" }, () => Results.Ok());

            if (!app.Environment.IsProduction()
                && !app.Environment.IsEnvironment("IntegrationTest")
                && !app.Environment.IsEnvironment("Testing"))
            {
                app.UsePathBase("/api");
            }

            app.MapControllers();

            app.Run();
        }

        internal static bool ShouldRunMigrations(IWebHostEnvironment env, IConfiguration config)
        {
            if (env.IsDevelopment() || env.IsEnvironment("Test"))
            {
                return true;
            }

            return config.GetValue<bool>("RunMigrations");
        }

        internal static void ValidateEnvironmentMarker(AppDbContext db, IWebHostEnvironment env)
        {
            if (!env.IsDevelopment() && !env.IsProduction())
            {
                return;
            }

            EnvironmentMarker? marker;
            try
            {
                marker = db.EnvironmentMarkers
                    .AsNoTracking()
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Environment marker validation failed. Ensure migrations are applied before starting the application.",
                    ex);
            }

            if (marker is null)
            {
                throw new InvalidOperationException(
                    "Environment marker is missing. Apply the EnvironmentMarker migration to seed the marker value.");
            }

            if (env.IsDevelopment() && string.Equals(marker.Marker, "PROD", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Development environment is configured with a production database (EnvironmentMarker=PROD). Abort startup.");
            }

            if (env.IsProduction() && string.Equals(marker.Marker, "DEV", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Production environment is configured with a development database (EnvironmentMarker=DEV). Abort startup.");
            }
        }

        internal static string[] BuildAllowedOrigins(IConfiguration config, IWebHostEnvironment env)
        {
            var requiredOrigins = new[]
            {
                "http://localhost:5173",
                "https://admin.atlashomestays.com",
                "https://devadmin.atlashomestays.com",
                "https://www.atlashomestays.com",
                "https://*.pages.dev"
            };

            var origins = ImmutableArray.CreateBuilder<string>();
            origins.AddRange(requiredOrigins);

            if (env.IsDevelopment())
            {
                origins.Add("http://127.0.0.1:5173");
            }

            var additionalOrigins = config.GetSection("Cors:AdditionalOrigins").Get<string[]>();
            if (additionalOrigins is { Length: > 0 })
            {
                origins.AddRange(additionalOrigins.Where(origin => !string.IsNullOrWhiteSpace(origin)));
            }

            return origins
                .Select(origin => origin.Trim())
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
