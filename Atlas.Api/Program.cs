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
using Atlas.Api.Services.Auth;
using Atlas.Api.Services.Storage;
using Atlas.Api.Services.Tenancy;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

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
                        .AllowCredentials()
                        .WithExposedHeaders("X-Total-Count", "X-Page", "X-Page-Size", "X-Correlation-Id");
                });
            });

            builder.Services
                .AddControllers(options =>
                {
                    options.Filters.Add<Atlas.Api.Filters.ValidateModelAttribute>();
                    options.Filters.Add<Atlas.Api.Filters.ApiExceptionFilter>();
                    options.Filters.Add<Atlas.Api.Filters.BillingLockFilter>();
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
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
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
            builder.Services.AddScoped<IInvoiceService, InvoiceService>();
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
#pragma warning disable CS0618 // BL-011: NoOp retained for planned workflow integration
            builder.Services.AddScoped<Atlas.Api.Services.IBookingWorkflowPublisher, Atlas.Api.Services.NoOpBookingWorkflowPublisher>();
#pragma warning restore CS0618
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ITenantContextAccessor, HttpTenantContextAccessor>();
            builder.Services.AddScoped<ITenantProvider, TenantProvider>();
            builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
            builder.Services.AddScoped<Atlas.Api.Services.Onboarding.OnboardingService>();
            builder.Services.AddScoped<Atlas.Api.Services.Billing.IEntitlementsService, Atlas.Api.Services.Billing.EntitlementsService>();
            builder.Services.AddScoped<Atlas.Api.Services.Billing.CreditsService>();

            builder.Services.AddHttpClient<Atlas.Api.Services.ChannexService>();
            builder.Services.AddScoped<Atlas.Api.Services.IChannexService, Atlas.Api.Services.ChannexService>();

            if (env.IsDevelopment())
                builder.Services.AddScoped<Atlas.Api.Services.Channels.IChannelManagerProvider, Atlas.Api.Services.Channels.StubChannelManagerProvider>();
            else
                builder.Services.AddScoped<Atlas.Api.Services.Channels.IChannelManagerProvider, Atlas.Api.Services.Channels.ChannexAdapter>();

            var blobConnStr = builder.Configuration["AzureBlob:ConnectionString"];
            if (!string.IsNullOrWhiteSpace(blobConnStr) && !IsPlaceholderValue(blobConnStr))
            {
                builder.Services.AddSingleton(new BlobServiceClient(blobConnStr));
                builder.Services.AddSingleton<IFileStorageService, AzureBlobStorageService>();
            }
            else
            {
                builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
            }

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
            builder.Services.AddHostedService<Atlas.Api.Services.HoldCleanupHostedService>();
            builder.Services.AddHostedService<Atlas.Api.Services.Scheduling.AutomationSchedulerHostedService>();
            builder.Services.AddHttpClient("iCalSync", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AtlasPMS/1.0");
            });
            builder.Services.AddHostedService<Atlas.Api.Services.ICalSyncHostedService>();
            builder.Services.AddScoped<Atlas.Api.Services.Scheduling.BookingScheduleService>();

            builder.Services.Configure<Atlas.Api.Services.Msg91Settings>(builder.Configuration.GetSection("Msg91"));
            builder.Services.AddHttpClient("MSG91");
            builder.Services.AddScoped<Atlas.Api.Services.Notifications.Msg91NotificationProvider>();
            builder.Services.AddScoped<Atlas.Api.Services.Notifications.ConsoleNotificationProvider>();
            builder.Services.AddScoped<Atlas.Api.Services.Notifications.INotificationProvider>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<Atlas.Api.Services.Notifications.FallbackNotificationProvider>>();
                if (env.IsDevelopment())
                {
                    var primary = sp.GetRequiredService<Atlas.Api.Services.Notifications.ConsoleNotificationProvider>();
                    var fallback = sp.GetRequiredService<Atlas.Api.Services.Notifications.Msg91NotificationProvider>();
                    return new Atlas.Api.Services.Notifications.FallbackNotificationProvider(primary, fallback, logger);
                }
                else
                {
                    var primary = sp.GetRequiredService<Atlas.Api.Services.Notifications.Msg91NotificationProvider>();
                    var fallback = sp.GetRequiredService<Atlas.Api.Services.Notifications.ConsoleNotificationProvider>();
                    return new Atlas.Api.Services.Notifications.FallbackNotificationProvider(primary, fallback, logger);
                }
            });
            builder.Services.AddScoped<Atlas.Api.Services.Notifications.NotificationOrchestrator>();

            var jwtKey = builder.Configuration["Jwt:Key"];
            var jwtEnabled = !string.IsNullOrWhiteSpace(jwtKey) && !IsPlaceholderValue(jwtKey)
                && Encoding.UTF8.GetByteCount(jwtKey!) >= 32;

            if (!jwtEnabled && builder.Environment.IsProduction())
                throw new InvalidOperationException(
                    "JWT authentication is required in Production. " +
                    "Set Jwt__Key (or Jwt:Key) to a 32+ character secret in Azure App Service > Configuration > Application Settings.");

            if (jwtEnabled)
            {
                builder.Services.AddAuthentication("Bearer")
                    .AddJwtBearer("Bearer", options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = false,
                            ValidateAudience = false,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!))
                        };
                    });
                builder.Services.AddAuthorization();
            }
            else
            {
                builder.Services.AddAuthorization(options =>
                {
                    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                        .RequireAssertion(_ => true)
                        .Build();
                });
            }

            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });

            builder.Services.AddHttpLogging(options =>
            {
                options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod
                    | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath
                    | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode
                    | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.Duration;
                options.CombineLogs = true;
            });

            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddFixedWindowLimiter("mutations", opt =>
                {
                    opt.PermitLimit = 30;
                    opt.Window = TimeSpan.FromMinutes(1);
                    opt.QueueLimit = 0;
                });
                options.AddFixedWindowLimiter("payments", opt =>
                {
                    opt.PermitLimit = 10;
                    opt.Window = TimeSpan.FromMinutes(1);
                    opt.QueueLimit = 0;
                });
            });

            var app = builder.Build();

            // Exception handler for production: return JSON (not HTML) and add CORS headers
            // so the browser doesn't block error responses and clients don't get "Unexpected token '<'"
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler(exceptionHandlerApp =>
                {
                    exceptionHandlerApp.Run(async context =>
                    {
                        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                        if (feature?.Error != null)
                        {
                            var logger = context.RequestServices.GetService<ILogger<Program>>();
                            logger?.LogError(feature.Error, "Unhandled exception: {Path}", context.Request.Path);
                        }
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

            app.UseMiddleware<Atlas.Api.Middleware.CorrelationIdMiddleware>();
            app.UseResponseCompression();
            app.UseHttpLogging();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Atlas API v1");
                c.RoutePrefix = "swagger";
            });

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
            app.UseRateLimiter();

            app.UseMiddleware<TenantResolutionMiddleware>();

            // Optional: HTTPS redirect
            // app.UseHttpsRedirection();

            var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
            startupLogger.LogInformation("JWT authentication: {Status}", jwtEnabled ? "ENABLED" : "DISABLED (permissive auth — set Jwt__Key in Azure App Settings for production)");

            if (jwtEnabled)
            {
                app.UseAuthentication();
            }
            app.UseAuthorization();

            app.MapMethods("/test-cors", new[] { "OPTIONS" }, () => Results.Ok());
            app.MapGet("/health", async (IOptions<AzureServiceBusOptions> sbOpts, AppDbContext db) =>
            {
                var pipelineActive = !string.IsNullOrWhiteSpace(sbOpts.Value.ConnectionString);
                var outboxPending = await db.OutboxMessages.CountAsync(o => o.Status == "Pending");
                var outboxFailed = await db.OutboxMessages.CountAsync(o => o.Status == "Failed");

                return Results.Ok(new
                {
                    status = "healthy",
                    auth = new { jwtEnabled },
                    asyncPipeline = new
                    {
                        enabled = pipelineActive,
                        serviceBusConfigured = pipelineActive
                    },
                    outbox = new
                    {
                        pending = outboxPending,
                        failed = outboxFailed
                    }
                });
            });

            app.MapGet("/ops/outbox-stats", async (AppDbContext db) =>
            {
                var stats = await db.OutboxMessages
                    .GroupBy(o => o.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                var oldest = await db.OutboxMessages
                    .Where(o => o.Status == "Pending")
                    .OrderBy(o => o.CreatedAtUtc)
                    .Select(o => o.CreatedAtUtc)
                    .FirstOrDefaultAsync();

                return Results.Ok(new
                {
                    counts = stats.ToDictionary(s => s.Status, s => s.Count),
                    oldestPendingUtc = oldest == default ? (DateTime?)null : oldest,
                    queriedAtUtc = DateTime.UtcNow
                });
            });

            app.UseStaticFiles();

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
            if (string.IsNullOrWhiteSpace(value)) return true;
            var v = value.Trim();
            return string.Equals(v, "__SET_VIA_ENV_OR_AZURE__", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "__SET_VIA_ENV__", StringComparison.OrdinalIgnoreCase);
        }
    }
}
