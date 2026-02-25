using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.Events;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services.Scheduling;

/// <summary>
/// Creates and cancels AutomationSchedule entries for a booking's lifecycle:
/// welcome reminder, pre-checkout, post-checkout, and invoice due.
/// </summary>
public sealed class BookingScheduleService
{
    private readonly AppDbContext _db;
    private readonly ILogger<BookingScheduleService> _logger;

    public BookingScheduleService(AppDbContext db, ILogger<BookingScheduleService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Create all lifecycle schedules for a confirmed booking.</summary>
    public void CreateSchedulesForConfirmedBooking(Booking booking)
    {
        var checkin = booking.CheckinDate;
        var checkout = booking.CheckoutDate;

        if (checkin == default || checkout == default)
        {
            _logger.LogWarning("Skipping schedule creation for booking {BookingId}: missing checkin/checkout dates.", booking.Id);
            return;
        }

        AddSchedule(booking, EventTypes.StayWelcomeDue, SafeAddHours(checkin, -24));
        AddSchedule(booking, EventTypes.StayPrecheckoutDue, SafeAddHours(checkout, -24));
        AddSchedule(booking, EventTypes.StayPostcheckoutDue, SafeAddHours(checkout, 1));
        AddSchedule(booking, EventTypes.InvoiceDue, SafeAddHours(checkout, 2));

        _logger.LogInformation("Created 4 lifecycle schedules for booking {BookingId} (tenant {TenantId}).",
            booking.Id, booking.TenantId);
    }

    private static DateTime SafeAddHours(DateTime dt, double hours)
    {
        try { return dt.AddHours(hours); }
        catch (ArgumentOutOfRangeException) { return DateTime.UtcNow.AddSeconds(10); }
    }

    /// <summary>Cancel all pending schedules for a booking (e.g. on cancellation).</summary>
    public async Task CancelSchedulesForBookingAsync(int bookingId, CancellationToken ct = default)
    {
        var pending = await _db.AutomationSchedules
            .Where(s => s.BookingId == bookingId && s.Status == ScheduleStatuses.Pending)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var schedule in pending)
        {
            schedule.Status = ScheduleStatuses.Cancelled;
            schedule.CompletedAtUtc = DateTime.UtcNow;
        }

        if (pending.Count > 0)
            _logger.LogInformation("Cancelled {Count} pending schedules for booking {BookingId}.", pending.Count, bookingId);
    }

    private void AddSchedule(Booking booking, string eventType, DateTime dueAtUtc)
    {
        if (dueAtUtc <= DateTime.UtcNow)
        {
            dueAtUtc = DateTime.UtcNow.AddSeconds(10);
        }

        _db.AutomationSchedules.Add(new AutomationSchedule
        {
            TenantId = booking.TenantId,
            Booking = booking,
            EventType = eventType,
            DueAtUtc = dueAtUtc,
            Status = ScheduleStatuses.Pending,
            AttemptCount = 0
        });
    }
}
