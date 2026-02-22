using Atlas.Api.Data;
using Atlas.Api.Models.Billing;
using Atlas.Api.Services.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services.Billing;

public record EntitlementsSnapshot(
    bool IsLocked,
    string? LockReason,
    int CreditsBalance,
    string SubscriptionStatus,
    bool IsWithinGracePeriod,
    string PlanCode,
    DateTime? PeriodEndUtc,
    Guid? OverdueInvoiceId
);

public interface IEntitlementsService
{
    Task<EntitlementsSnapshot> GetSnapshotAsync(int tenantId, CancellationToken ct = default);
}

public class EntitlementsService : IEntitlementsService
{
    private readonly AppDbContext _db;

    public EntitlementsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<EntitlementsSnapshot> GetSnapshotAsync(int tenantId, CancellationToken ct = default)
    {
        var sub = await _db.TenantSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (sub is null)
        {
            return new EntitlementsSnapshot(
                IsLocked: false, LockReason: null, CreditsBalance: 0,
                SubscriptionStatus: "None", IsWithinGracePeriod: false,
                PlanCode: "NONE", PeriodEndUtc: null, OverdueInvoiceId: null);
        }

        var balance = await _db.TenantCreditsLedger
            .Where(l => l.TenantId == tenantId)
            .SumAsync(l => l.CreditsDelta, ct);

        var isLocked = sub.LockedAtUtc.HasValue;
        var lockReason = sub.LockReason;

        var now = DateTime.UtcNow;
        var isWithinGrace = !isLocked
            && sub.CurrentPeriodEndUtc < now
            && sub.CurrentPeriodEndUtc.AddDays(sub.GracePeriodDays) >= now;

        Guid? overdueInvoiceId = null;
        if (isLocked && lockReason == LockReasons.InvoiceOverdue)
        {
            overdueInvoiceId = await _db.BillingInvoices
                .Where(i => i.TenantId == tenantId && i.Status == InvoiceStatuses.Overdue)
                .OrderByDescending(i => i.DueAtUtc)
                .Select(i => (Guid?)i.Id)
                .FirstOrDefaultAsync(ct);
        }

        return new EntitlementsSnapshot(
            IsLocked: isLocked,
            LockReason: lockReason,
            CreditsBalance: balance,
            SubscriptionStatus: sub.Status,
            IsWithinGracePeriod: isWithinGrace,
            PlanCode: sub.Plan.Code,
            PeriodEndUtc: sub.CurrentPeriodEndUtc,
            OverdueInvoiceId: overdueInvoiceId);
    }
}
