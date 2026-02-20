using Atlas.Api.Data;
using Atlas.Api.Data.Repositories;
using Atlas.Api.Options;
using Atlas.Api.Services.EventBus;
using Atlas.Api.Services.Outbox;
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
using Atlas.Api.Services.Tenancy;
using Microsoft.Extensions.Options;

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
                .AddJsonFile($"appsettings.{env.EnvironmentName}.local.json", optional: true)
                .AddEnvironmentVariables();

            const string CorsPolicy = "AtlasCorsPolicy";
            var allowedOrigins = BuildAllowedOrigins(builder.Configuration, env);

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: CorsPolicy, policy =>
                {
                    policy
                        .WithOrigins(allowedOrigins)
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

            // Configure Razorpay
            builder.Services.Configure<RazorpayConfig>(builder.Configuration.GetSection("Razorpay"));
            
            // Configure SMTP for email service
            builder.Services.Configure<Atlas.Api.Services.SmtpConfig>(builder.Configuration.GetSection("Smtp"));
            
            // Add HttpClient for Razorpay
            builder.Services.AddHttpClient("Razorpay", client =>
            {
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            });
            
            builder.Services.AddMemoryCache();
            builder.Services.Configure<QuoteOptions>(builder.Configuration.GetSection("Quotes"));
            builder.Services.AddScoped<IRazorpayPaymentService, RazorpayPaymentService>();
            builder.Services.AddScoped<ITenantPricingSettingsService, TenantPricingSettingsService>();
            builder.Services.AddScoped<IQuoteService, QuoteService>();
            builder.Services.AddScoped<Atlas.Api.Services.IEmailService, Atlas.Api.Services.EmailService>();
            
            builder.Services.AddScoped<Atlas.Api.Services.AvailabilityService>();
            builder.Services.AddScoped<Atlas.Api.Services.PricingService>();
            builder.Services.AddScoped<IListingPricingRepository, ListingPricingRepository>();
            builder.Services.AddScoped<IListingDailyRateRepository, ListingDailyRateRepository>();
            builder.Services.AddScoped<IListingDailyInventoryRepository, ListingDailyInventoryRepository>();
            builder.Services.AddScoped<Atlas.Api.Services.IAdminPricingService, Atlas.Api.Services.AdminPricingService>();
            builder.Services.AddScoped<Atlas.Api.Services.IGuestPricingService, Atlas.Api.Services.GuestPricingService>();
            builder.Services.AddScoped<Atlas.Api.Services.IBookingWorkflowPublisher, Atlas.Api.Services.NoOpBookingWorkflowPublisher>();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ITenantContextAccessor, HttpTenantContextAccessor>();
            builder.Services.AddScoped<ITenantProvider, TenantProvider>();
            // Do not register TenantResolutionMiddleware in DI; RequestDelegate is provided by the pipeline in UseMiddleware<T>()

            ValidateRequiredConfiguration(builder.Configuration, env);

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

            if (env.IsDevelopment())
            {
                if (IsPlaceholderValue(connectionString))
                {
                    Console.WriteLine("[WARN] Connection string is still using the placeholder value. Set it via environment variables for local development.");
                }

                var redactedConnectionString = ConnectionStringRedactor.Redact(connectionString);
                Console.WriteLine($"[DEBUG] Using connection string: {redactedConnectionString}");
            }

            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
                if (builder.Environment.IsDevelopment())
                {
                    options.LogTo(Console.WriteLine);
                }
            });

            builder.Services.Configure<AzureServiceBusOptions>(builder.Configuration.GetSection(AzureServiceBusOptions.SectionName));
            builder.Services.AddSingleton<InMemoryEventBusPublisher>();
            builder.Services.AddSingleton<AzureServiceBusPublisher>();
            builder.Services.AddSingleton<IEventBusPublisher>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<AzureServiceBusOptions>>();
                return !string.IsNullOrWhiteSpace(opts.Value.ConnectionString)
                    ? sp.GetRequiredService<AzureServiceBusPublisher>()
                    : sp.GetRequiredService<InMemoryEventBusPublisher>();
            });
            builder.Services.AddHostedService<OutboxDispatcherHostedService>();
            builder.Services.AddHostedService<Atlas.Api.Services.Consumers.BookingEventsNotificationConsumer>();
            builder.Services.AddHostedService<Atlas.Api.Services.Consumers.StayEventsNotificationConsumer>();

            builder.Services.Configure<Atlas.Api.Services.Msg91Settings>(builder.Configuration.GetSection("Msg91"));
            builder.Services.AddHttpClient("MSG91");
            builder.Services.AddScoped<Atlas.Api.Services.Notifications.INotificationProvider, Atlas.Api.Services.Notifications.Msg91NotificationProvider>();
            builder.Services.AddScoped<Atlas.Api.Services.Notifications.NotificationOrchestrator>();

            var jwtKey = builder.Configuration["Jwt:Key"];
            _ = jwtKey;

            // Auth disabled for local/dev; re-enable before prod (see ATLAS-HIGH-VALUE-BACKLOG execution rules)
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

            // Exception handler for production: return JSON (not HTML) and add CORS headers
            // so the browser doesn't block error responses and clients don't get "Unexpected token '<'"
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler(exceptionHandlerApp =>
                {
                    exceptionHandlerApp.Run(async context =>
                    {
                        var allowedOrigins = BuildAllowedOrigins(context.RequestServices.GetRequiredService<IConfiguration>(), context.RequestServices.GetRequiredService<IWebHostEnvironment>());
                        var origin = context.Request.Headers.Origin.ToString();
                        if (!string.IsNullOrEmpty(origin) && allowedOrigins.Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase)))
                        {
                            context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
                            context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
                        }
                        context.Response.StatusCode = 500;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new { error = "An error occurred. Please try again later." });
                    });
                });
            }
            else
            {
                app.UseDeveloperExceptionPage();
            }

            if (!app.Environment.IsProduction())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Atlas API v1");
                    c.RoutePrefix = "swagger";
                });
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

            app.UseMiddleware<TenantResolutionMiddleware>();

            // Optional: HTTPS redirect
            // app.UseHttpsRedirection();

            // Authentication is intentionally disabled until JWT is configured; then uncomment UseAuthentication/UseAuthorization.
            // app.UseAuthentication();
            // app.UseAuthorization();

            app.MapMethods("/test-cors", new[] { "OPTIONS" }, () => Results.Ok());
            app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

            // No path base: match dev URLs exactly — /listings/5, /availability/listing-availability, /api/Razorpay/order (only base URL differs between dev and prod).
            app.MapControllers();

            app.Run();
        }

        internal static string[] BuildAllowedOrigins(IConfiguration config, IWebHostEnvironment env)
        {
            // Guest portal (Razorpay/order) calls from www and apex; both must be allowed for prod.
            var requiredOrigins = new[]
            {
                "http://localhost:5173",
                "https://localhost:7018",
                "https://admin.atlashomestays.com",
                "https://dev.atlashomestays.com",
                "https://devadmin.atlashomestays.com",
                "https://www.atlashomestays.com",
                "https://atlashomestays.com",
                "https://*.pages.dev"
            };

            var origins = ImmutableArray.CreateBuilder<string>();
            origins.AddRange(requiredOrigins);

            if (env.IsDevelopment())
            {
                origins.Add("http://127.0.0.1:5173");
                origins.Add("https://127.0.0.1:7018");
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

        internal static void ValidateRequiredConfiguration(IConfiguration config, IWebHostEnvironment env)
        {
            if (ShouldSkipRequiredConfigValidation(env))
            {
                return;
            }

            var strictOptionalConfig = string.Equals(
                config["Startup:StrictRequiredConfig"],
                "true",
                StringComparison.OrdinalIgnoreCase);

            var connectionString = config.GetConnectionString("DefaultConnection");
            if (IsPlaceholderValue(connectionString))
            {
                throw new InvalidOperationException(
                    "Database connection string 'DefaultConnection' is not configured. " +
                    "Set it via environment variables or Azure App Service connection strings.");
            }

            var jwtKey = config["Jwt:Key"];
            if (IsPlaceholderValue(jwtKey))
            {
                HandleOptionalStartupConfig(
                    strictOptionalConfig,
                    "JWT signing key 'Jwt:Key' is not configured. " +
                    "Set it via environment variables or Azure App Service settings.");
            }

            var razorpayKeyId = config["Razorpay:KeyId"];
            var razorpayKeySecret = config["Razorpay:KeySecret"];
            if (IsPlaceholderValue(razorpayKeyId) || IsPlaceholderValue(razorpayKeySecret))
            {
                HandleOptionalStartupConfig(
                    strictOptionalConfig,
                    "Razorpay configuration 'Razorpay:KeyId' and 'Razorpay:KeySecret' are not configured. " +
                    "Set them via environment variables or Azure App Service settings.");
            }
        }

        private static void HandleOptionalStartupConfig(bool strictOptionalConfig, string message)
        {
            if (strictOptionalConfig)
            {
                throw new InvalidOperationException(message);
            }

            Console.WriteLine($"[WARN] {message} Continuing startup because Startup:StrictRequiredConfig is not enabled.");
        }

        private static bool ShouldSkipRequiredConfigValidation(IWebHostEnvironment env)
        {
            return env.IsDevelopment()
                || env.IsEnvironment("IntegrationTest")
                || env.IsEnvironment("Testing");
        }

        private static bool IsPlaceholderValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                || string.Equals(value.Trim(), "__SET_VIA_ENV_OR_AZURE__", StringComparison.OrdinalIgnoreCase);
        }
    }
}
