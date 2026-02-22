using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Filters;
using Atlas.Api.Models.Billing;
using Atlas.Api.Services.Billing;
using Atlas.Api.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Controllers;

[ApiController]
[Route("billing")]
[Authorize]
[Produces("application/json")]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IEntitlementsService _entitlements;
    private readonly CreditsService _credits;

    public BillingController(AppDbContext db, ITenantContextAccessor tenantAccessor, IEntitlementsService entitlements, CreditsService credits)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _entitlements = entitlements;
        _credits = credits;
    }

    private int TenantId => _tenantAccessor.TenantId ?? 0;

    /// <summary>Current tenant entitlements (lock state, credits, plan).</summary>
    [HttpGet("entitlements")]
    [AllowWhenLocked]
    public async Task<IActionResult> GetEntitlements(CancellationToken ct)
    {
        var snap = await _entitlements.GetSnapshotAsync(TenantId, ct);
        return Ok(new EntitlementsResponseDto
        {
            IsLocked = snap.IsLocked,
            LockReason = snap.LockReason,
            CreditsBalance = snap.CreditsBalance,
            SubscriptionStatus = snap.SubscriptionStatus,
            IsWithinGracePeriod = snap.IsWithinGracePeriod,
            PlanCode = snap.PlanCode,
            PeriodEndUtc = snap.PeriodEndUtc,
            OverdueInvoiceId = snap.OverdueInvoiceId,
        });
    }

    /// <summary>All active billing plans.</summary>
    [HttpGet("plans")]
    [AllowWhenLocked]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        var plans = await _db.BillingPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.MonthlyPriceInr)
            .Select(p => new BillingPlanDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                MonthlyPriceInr = p.MonthlyPriceInr,
                CreditsIncluded = p.CreditsIncluded,
                SeatLimit = p.SeatLimit,
                ListingLimit = p.ListingLimit,
            })
            .ToListAsync(ct);
        return Ok(plans);
    }

    /// <summary>Subscribe to a plan (creates/updates subscription + issues invoice).</summary>
    [HttpPost("subscribe")]
    [AllowWhenLocked]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequestDto req, CancellationToken ct)
    {
        var plan = await _db.BillingPlans
            .FirstOrDefaultAsync(p => p.Code == req.PlanCode && p.IsActive, ct);
        if (plan is null) return NotFound(new { error = "Plan not found." });

        var now = DateTime.UtcNow;
        var periodEnd = now.AddMonths(1);

        var existing = await _db.TenantSubscriptions
            .Where(s => s.TenantId == TenantId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            existing.PlanId = plan.Id;
            existing.Status = SubscriptionStatuses.Active;
            existing.CurrentPeriodStartUtc = now;
            existing.CurrentPeriodEndUtc = periodEnd;
            existing.AutoRenew = req.AutoRenew;
            existing.LockedAtUtc = null;
            existing.LockReason = null;
            existing.NextInvoiceAtUtc = periodEnd;
        }
        else
        {
            _db.TenantSubscriptions.Add(new TenantSubscription
            {
                TenantId = TenantId,
                PlanId = plan.Id,
                Status = SubscriptionStatuses.Active,
                CurrentPeriodStartUtc = now,
                CurrentPeriodEndUtc = periodEnd,
                AutoRenew = req.AutoRenew,
                NextInvoiceAtUtc = periodEnd,
            });
        }

        if (plan.CreditsIncluded > 0)
        {
            _db.TenantCreditsLedger.Add(new TenantCreditsLedger
            {
                TenantId = TenantId,
                Type = LedgerTypes.Grant,
                CreditsDelta = plan.CreditsIncluded,
                Reason = LedgerReasons.PlanGrant,
            });
        }

        var taxRate = 18m;
        var taxAmount = plan.MonthlyPriceInr * taxRate / 100m;
        var total = plan.MonthlyPriceInr + taxAmount;

        var invoice = new BillingInvoice
        {
            TenantId = TenantId,
            PeriodStartUtc = now,
            PeriodEndUtc = periodEnd,
            AmountInr = plan.MonthlyPriceInr,
            TaxGstRate = taxRate,
            TaxAmountInr = taxAmount,
            TotalInr = total,
            Status = plan.MonthlyPriceInr == 0 ? InvoiceStatuses.Paid : InvoiceStatuses.Issued,
            DueAtUtc = now.AddDays(7),
            PaidAtUtc = plan.MonthlyPriceInr == 0 ? now : null,
        };
        _db.BillingInvoices.Add(invoice);
        await _db.SaveChangesAsync(ct);

        return Ok(new { subscriptionStatus = existing?.Status ?? SubscriptionStatuses.Active, invoiceId = invoice.Id });
    }

    /// <summary>List invoices for this tenant.</summary>
    [HttpGet("invoices")]
    [AllowWhenLocked]
    public async Task<IActionResult> GetInvoices(CancellationToken ct)
    {
        var invoices = await _db.BillingInvoices
            .Where(i => i.TenantId == TenantId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new InvoiceDto
            {
                Id = i.Id,
                PeriodStartUtc = i.PeriodStartUtc,
                PeriodEndUtc = i.PeriodEndUtc,
                AmountInr = i.AmountInr,
                TaxGstRate = i.TaxGstRate,
                TaxAmountInr = i.TaxAmountInr,
                TotalInr = i.TotalInr,
                Status = i.Status,
                DueAtUtc = i.DueAtUtc,
                PaidAtUtc = i.PaidAtUtc,
                PaymentLinkId = i.PaymentLinkId,
                PdfUrl = i.PdfUrl,
                CreatedAtUtc = i.CreatedAtUtc,
            })
            .ToListAsync(ct);
        return Ok(invoices);
    }

    /// <summary>
    /// Create a Razorpay payment link for an invoice.
    /// TODO: Replace stub with actual Razorpay Payment Link API when keys are ready.
    /// </summary>
    [HttpPost("invoices/{id}/pay-link")]
    [AllowWhenLocked]
    public async Task<IActionResult> CreatePayLink(Guid id, CancellationToken ct)
    {
        var invoice = await _db.BillingInvoices
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == TenantId, ct);
        if (invoice is null) return NotFound(new { error = "Invoice not found." });
        if (invoice.Status == InvoiceStatuses.Paid) return Ok(new { message = "Invoice already paid." });

        // TODO: Call Razorpay Payment Links API to create a real payment link.
        // For now, generate a stub URL. Replace once Razorpay subscription keys are configured.
        var stubLinkId = $"pl_stub_{invoice.Id:N}";
        var stubUrl = $"https://rzp.io/i/{stubLinkId}";

        invoice.PaymentLinkId = stubLinkId;
        invoice.Provider = "Razorpay";
        await _db.SaveChangesAsync(ct);

        return Ok(new PayLinkResponseDto { PaymentLinkUrl = stubUrl, PaymentLinkId = stubLinkId });
    }

    /// <summary>
    /// Razorpay webhook for billing payments. Marks invoice paid and unlocks tenant.
    /// TODO: Verify Razorpay signature before processing.
    /// </summary>
    [HttpPost("webhooks/razorpay")]
    [AllowAnonymous]
    [BillingExempt]
    public async Task<IActionResult> RazorpayWebhook(CancellationToken ct)
    {
        // TODO: Implement Razorpay signature verification using WebhookSecret.
        // For now, accept raw JSON with { paymentLinkId, paymentId, status }.
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var payload = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(body);

        var linkId = payload.TryGetProperty("paymentLinkId", out var lid) ? lid.GetString() : null;
        var paymentId = payload.TryGetProperty("paymentId", out var pid) ? pid.GetString() : null;
        var status = payload.TryGetProperty("status", out var st) ? st.GetString() : null;

        if (string.IsNullOrWhiteSpace(linkId)) return BadRequest(new { error = "Missing paymentLinkId." });

        var invoice = await _db.BillingInvoices
            .FirstOrDefaultAsync(i => i.PaymentLinkId == linkId, ct);
        if (invoice is null) return NotFound(new { error = "Invoice not found for payment link." });

        if (status == "captured" || status == "paid")
        {
            if (invoice.Status == InvoiceStatuses.Paid) return Ok(new { message = "Already processed." });

            invoice.Status = InvoiceStatuses.Paid;
            invoice.PaidAtUtc = DateTime.UtcNow;

            _db.BillingPayments.Add(new BillingPayment
            {
                InvoiceId = invoice.Id,
                ProviderPaymentId = paymentId,
                Status = "Captured",
                AmountInr = invoice.TotalInr,
            });

            await _db.SaveChangesAsync(ct);
            await _credits.UnlockTenantAsync(invoice.TenantId, ct);
        }

        return Ok(new { message = "Processed." });
    }

    /// <summary>Manual credit adjustment (platform-admin only).</summary>
    [HttpPost("credits/adjust")]
    [Authorize(Roles = "platform-admin")]
    [BillingExempt]
    public async Task<IActionResult> AdjustCredits([FromBody] CreditAdjustRequestDto req, CancellationToken ct)
    {
        _db.TenantCreditsLedger.Add(new TenantCreditsLedger
        {
            TenantId = TenantId,
            Type = req.CreditsDelta >= 0 ? LedgerTypes.Adjust : LedgerTypes.Debit,
            CreditsDelta = req.CreditsDelta,
            Reason = LedgerReasons.ManualAdjust,
            ReferenceType = "Admin",
            ReferenceId = req.Reason,
        });
        await _db.SaveChangesAsync(ct);
        var balance = await _credits.GetBalanceAsync(TenantId, ct);

        if (req.CreditsDelta > 0 && balance > 0)
        {
            var snap = await _entitlements.GetSnapshotAsync(TenantId, ct);
            if (snap.IsLocked && snap.LockReason == LockReasons.CreditsExhausted)
                await _credits.UnlockTenantAsync(TenantId, ct);
        }

        return Ok(new { balance });
    }
}
