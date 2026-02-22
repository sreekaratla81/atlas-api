namespace Atlas.Api.Services.Tenancy;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
                _logger.LogWarning("Tenant resolution failed for {Method} {Path}. Header X-Tenant-Slug: {Slug}",
                    context.Request.Method, context.Request.Path,
                    context.Request.Headers["X-Tenant-Slug"].ToString());
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "Tenant could not be resolved." }, context.RequestAborted);
                return;
            }

            _logger.LogDebug("Tenant not resolved for {Path} (dev/local — proceeding without tenant).", context.Request.Path);
            await _next(context);
            return;
        }

        context.Items[typeof(Atlas.Api.Models.Tenant)] = tenant;
        await _next(context);
    }

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
