using Atlas.Api.Data;
using Atlas.Api.Models.Billing;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services.Billing;

public class CreditsService
{
    public const int OnboardingGrantAmount = 500;

    private readonly AppDbContext _db;
    private readonly ILogger<CreditsService> _logger;

    public CreditsService(AppDbContext db, ILogger<CreditsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> GetBalanceAsync(int tenantId, CancellationToken ct = default)
    {
        return await _db.TenantCreditsLedger
            .Where(l => l.TenantId == tenantId)
            .SumAsync(l => l.CreditsDelta, ct);
    }

    /// <summary>Grant initial free credits and create trial subscription on onboarding.</summary>
    public async Task ProvisionTrialAsync(int tenantId, CancellationToken ct = default)
    {
        var freePlan = await _db.BillingPlans
            .FirstOrDefaultAsync(p => p.Code == "FREE_TRIAL" && p.IsActive, ct);

        if (freePlan is null)
        {
            freePlan = new BillingPlan
            {
                Code = "FREE_TRIAL",
                Name = "Free Trial",
                MonthlyPriceInr = 0,
                CreditsIncluded = OnboardingGrantAmount,
                IsActive = true,
            };
            _db.BillingPlans.Add(freePlan);
            await _db.SaveChangesAsync(ct);
        }

        var now = DateTime.UtcNow;
        var trialEnd = now.AddDays(30);

        _db.TenantSubscriptions.Add(new TenantSubscription
        {
            TenantId = tenantId,
            PlanId = freePlan.Id,
            Status = SubscriptionStatuses.Trial,
            TrialEndsAtUtc = trialEnd,
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = trialEnd,
            AutoRenew = false,
            GracePeriodDays = 7,
        });

        _db.TenantCreditsLedger.Add(new TenantCreditsLedger
        {
            TenantId = tenantId,
            Type = LedgerTypes.Grant,
            CreditsDelta = OnboardingGrantAmount,
            Reason = LedgerReasons.OnboardingGrant,
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Provisioned trial for tenant {TenantId}: {Credits} credits, ends {TrialEnd:u}",
            tenantId, OnboardingGrantAmount, trialEnd);
    }

    /// <summary>
    /// Debit one credit for a booking. If balance would go to zero, lock the tenant.
    /// Returns remaining balance. Throws if already locked.
    /// </summary>
    public async Task<int> DebitForBookingAsync(int tenantId, int bookingId, CancellationToken ct = default)
    {
        var balance = await GetBalanceAsync(tenantId, ct);

        if (balance <= 0)
        {
            await LockTenantAsync(tenantId, LockReasons.CreditsExhausted, ct);
            throw new TenantLockedException(LockReasons.CreditsExhausted, 0);
        }

        _db.TenantCreditsLedger.Add(new TenantCreditsLedger
        {
            TenantId = tenantId,
            Type = LedgerTypes.Debit,
            CreditsDelta = -1,
            Reason = LedgerReasons.BookingCreated,
            ReferenceType = "Booking",
            ReferenceId = bookingId.ToString(),
        });
        await _db.SaveChangesAsync(ct);

        var newBalance = balance - 1;

        if (newBalance <= 0)
        {
            await LockTenantAsync(tenantId, LockReasons.CreditsExhausted, ct);
            _logger.LogWarning("Tenant {TenantId} credits exhausted after booking {BookingId}", tenantId, bookingId);
        }

        return newBalance;
    }

    public async Task LockTenantAsync(int tenantId, string reason, CancellationToken ct = default)
    {
        var sub = await _db.TenantSubscriptions
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (sub is null) return;
        if (sub.LockedAtUtc.HasValue) return;

        sub.LockedAtUtc = DateTime.UtcNow;
        sub.LockReason = reason;
        sub.Status = SubscriptionStatuses.Suspended;
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Tenant {TenantId} locked: {Reason}", tenantId, reason);
    }

    public async Task UnlockTenantAsync(int tenantId, CancellationToken ct = default)
    {
        var sub = await _db.TenantSubscriptions
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (sub is null) return;

        sub.LockedAtUtc = null;
        sub.LockReason = null;
        sub.Status = SubscriptionStatuses.Active;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Tenant {TenantId} unlocked", tenantId);
    }
}

public class TenantLockedException : Exception
{
    public string Reason { get; }
    public int Balance { get; }

    public TenantLockedException(string reason, int balance)
        : base($"Tenant locked: {reason}")
    {
        Reason = reason;
        Balance = balance;
    }
}
