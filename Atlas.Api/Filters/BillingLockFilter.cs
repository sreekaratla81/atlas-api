using Atlas.Api.Services.Billing;
using Atlas.Api.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Atlas.Api.Filters;

/// <summary>
/// Global action filter: blocks mutating requests (POST/PUT/PATCH/DELETE)
/// when the tenant is billing-locked. Returns 402 Payment Required.
/// Skips if: GET/HEAD/OPTIONS, [AllowAnonymous], [AllowWhenLocked], [BillingExempt], or no tenant resolved.
/// </summary>
public sealed class BillingLockFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> ReadOnlyMethods = new(StringComparer.OrdinalIgnoreCase)
        { "GET", "HEAD", "OPTIONS" };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (ReadOnlyMethods.Contains(context.HttpContext.Request.Method))
        {
            await next();
            return;
        }

        if (HasAttribute<AllowAnonymousAttribute>(context)
            || HasAttribute<AllowWhenLockedAttribute>(context)
            || HasAttribute<BillingExemptAttribute>(context))
        {
            await next();
            return;
        }

        var tenantAccessor = context.HttpContext.RequestServices.GetService<ITenantContextAccessor>();
        var tenantId = tenantAccessor?.TenantId;
        if (tenantId is null or 0)
        {
            await next();
            return;
        }

        var entitlements = context.HttpContext.RequestServices.GetRequiredService<IEntitlementsService>();
        var snapshot = await entitlements.GetSnapshotAsync(tenantId.Value, context.HttpContext.RequestAborted);

        if (!snapshot.IsLocked)
        {
            await next();
            return;
        }

        context.Result = new ObjectResult(new
        {
            code = "TENANT_LOCKED",
            reason = snapshot.LockReason,
            balance = snapshot.CreditsBalance,
            invoiceId = snapshot.OverdueInvoiceId,
            payUrl = snapshot.OverdueInvoiceId.HasValue
                ? $"/billing/invoices/{snapshot.OverdueInvoiceId}/pay-link"
                : null,
        })
        {
            StatusCode = StatusCodes.Status402PaymentRequired,
        };
    }

    private static bool HasAttribute<T>(ActionExecutingContext ctx) where T : Attribute
    {
        return ctx.ActionDescriptor.EndpointMetadata.Any(m => m is T);
    }
}
