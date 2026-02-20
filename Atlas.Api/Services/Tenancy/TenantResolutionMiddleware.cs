namespace Atlas.Api.Services.Tenancy;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider, IWebHostEnvironment environment)
    {
        if (SkipTenantResolution(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var tenant = await tenantProvider.ResolveTenantAsync(context, context.RequestAborted);
        if (tenant is null)
        {
            if (!IsLocalOrDevelopment(environment))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "Tenant could not be resolved." }, context.RequestAborted);
                return;
            }

            await _next(context);
            return;
        }

        context.Items[typeof(Atlas.Api.Models.Tenant)] = tenant;
        await _next(context);
    }

    /// <summary>Paths that do not require tenant (health, Swagger UI). No subdomain or host-based tenant.</summary>
    private static bool SkipTenantResolution(PathString path)
    {
        var value = path.Value ?? "";
        return value.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalOrDevelopment(IWebHostEnvironment environment)
    {
        return environment.IsDevelopment()
            || environment.IsEnvironment("IntegrationTest")
            || environment.IsEnvironment("Testing")
            || environment.IsEnvironment("Local");
    }
}
