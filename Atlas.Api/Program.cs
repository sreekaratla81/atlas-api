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
using Atlas.Api.Services;
using Atlas.Api.Models.Dtos.Razorpay;

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

            // Configure Razorpay
            builder.Services.Configure<RazorpayConfig>(builder.Configuration.GetSection("Razorpay"));
            
            // Add HttpClient for Razorpay
            builder.Services.AddHttpClient("Razorpay", client =>
            {
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            });
            
            builder.Services.AddScoped<IRazorpayPaymentService, RazorpayPaymentService>();
            
            builder.Services.AddScoped<Atlas.Api.Services.AvailabilityService>();
            builder.Services.AddScoped<Atlas.Api.Services.PricingService>();
            builder.Services.AddScoped<Atlas.Api.Services.IBookingWorkflowPublisher, Atlas.Api.Services.NoOpBookingWorkflowPublisher>();

            var connectionString = Environment.GetEnvironmentVariable("DEFAULT_CONNECTION")
                ?? builder.Configuration.GetConnectionString("DefaultConnection");

            if (env.IsDevelopment())
            {
                var redactedConnectionString = ConnectionStringRedactor.Redact(connectionString);
                Console.WriteLine($"[DEBUG] Using connection string: {redactedConnectionString}");
            }

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
