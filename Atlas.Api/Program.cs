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


            // Add services
            builder.Services.AddCors(options =>
            {
                // TODO: Restrict CORS to specific domains after 1 month (e.g., by July 26, 2025)
                options.AddPolicy("AllowAllOriginsTemp", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });
            // Add services to the container.

            builder.Services
                .AddControllers()
                .AddJsonOptions(opts =>
                {
                    opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                });
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Atlas API", Version = "v1" });
                c.CustomSchemaIds(type => type.FullName);
                c.IgnoreObsoleteProperties();
            });
            // Connection string can come from appsettings.json, appsettings.{Environment}.json
            // or environment variables. Azure App Service typically injects the
            // connection string as `ConnectionStrings__DefaultConnection` so we
            // read from configuration first which already checks that variable.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

            // Fall back to older environment variable name if provided
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = Environment.GetEnvironmentVariable("DEFAULT_CONNECTION");
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

            // Enable Swagger even in production
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Atlas API v1");
                c.RoutePrefix = "swagger"; // ensures URL ends with /swagger
            });

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage(); // Use detailed error page only in development
            }
            // app.UseHttpsRedirection();
            app.UseCors("AllowAllOriginsTemp");
            // app.UseAuthentication();
            // app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
