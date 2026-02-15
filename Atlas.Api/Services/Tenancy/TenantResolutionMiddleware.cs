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

    private static bool IsLocalOrDevelopment(IWebHostEnvironment environment)
    {
        return environment.IsDevelopment()
            || environment.IsEnvironment("IntegrationTest")
            || environment.IsEnvironment("Testing")
            || environment.IsEnvironment("Local");
    }
}
