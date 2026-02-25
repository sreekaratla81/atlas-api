using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services;

public interface IInvoiceService
{
    Task<BookingInvoice> GenerateInvoiceAsync(int bookingId, CancellationToken ct = default);
    Task<BookingInvoice?> GetInvoiceByBookingIdAsync(int bookingId, CancellationToken ct = default);
}

public class InvoiceService : IInvoiceService
{
    private readonly AppDbContext _db;
    private readonly ILogger<InvoiceService> _log;

    public InvoiceService(AppDbContext db, ILogger<InvoiceService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<BookingInvoice?> GetInvoiceByBookingIdAsync(int bookingId, CancellationToken ct = default)
    {
        return await _db.BookingInvoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.BookingId == bookingId, ct);
    }

    public async Task<BookingInvoice> GenerateInvoiceAsync(int bookingId, CancellationToken ct = default)
    {
        var existing = await _db.BookingInvoices
            .FirstOrDefaultAsync(i => i.BookingId == bookingId, ct);
        if (existing is not null) return existing;

        var booking = await _db.Bookings
            .Include(b => b.Listing)
                .ThenInclude(l => l!.Property)
            .Include(b => b.Guest)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct)
            ?? throw new InvalidOperationException($"Booking {bookingId} not found.");

        var tenantProfile = await _db.TenantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(tp => tp.TenantId == booking.TenantId, ct);

        var nights = (booking.CheckoutDate - booking.CheckinDate).Days;
        if (nights < 1) nights = 1;

        var baseAmount = booking.TotalAmount ?? 0m;
        var perNight = nights > 0 ? baseAmount / nights : baseAmount;
        // Indian GST slab: 12% when per-night tariff > 7500, else 5%
        var gstRate = perNight > 7500 ? 0.12m : 0.05m;
        var gstAmount = Math.Round(baseAmount * gstRate, 2);
        var totalWithGst = baseAmount + gstAmount;

        var invoiceNumber = $"ATL-INV-{booking.Id:D6}-{DateTime.UtcNow:yyyyMMdd}";

        var invoice = new BookingInvoice
        {
            BookingId = bookingId,
            TenantId = booking.TenantId,
            InvoiceNumber = invoiceNumber,
            GuestName = booking.Guest?.Name ?? "Guest",
            GuestEmail = booking.Guest?.Email,
            GuestPhone = booking.Guest?.Phone,
            PropertyName = booking.Listing?.Property?.Name ?? "",
            ListingName = booking.Listing?.Name ?? "",
            CheckinDate = booking.CheckinDate,
            CheckoutDate = booking.CheckoutDate,
            Nights = nights,
            BaseAmount = baseAmount,
            GstRate = gstRate,
            GstAmount = gstAmount,
            TotalAmount = totalWithGst,
            SupplierGstin = tenantProfile?.Gstin,
            SupplierLegalName = tenantProfile?.LegalName,
            SupplierAddress = tenantProfile?.RegisteredAddressLine,
            PlaceOfSupply = tenantProfile?.PlaceOfSupplyState ?? tenantProfile?.State,
            GeneratedAt = DateTime.UtcNow,
            Status = "generated"
        };

        _db.BookingInvoices.Add(invoice);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Generated invoice {InvoiceNumber} for booking {BookingId}", invoiceNumber, bookingId);

        return invoice;
    }
}
