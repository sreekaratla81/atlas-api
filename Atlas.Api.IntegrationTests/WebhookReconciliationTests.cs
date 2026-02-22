using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.Events;
using Atlas.Api.Models;
using Atlas.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

/// <summary>
/// BL-001 Webhook reconciliation integration tests.
/// Validates: server-side reconciliation via ReconcileWebhookPaymentAsync correctly
/// transitions Hold→Confirmed, writes outbox, and is idempotent.
/// </summary>
[Trait("Suite", "Webhook")]
public class WebhookReconciliationTests : IntegrationTestBase
{
    public WebhookReconciliationTests(SqlServerTestDatabase database) : base(database) { }

    private async Task<(Booking booking, Payment payment, Guest guest)>
        SeedHoldBookingAsync(AppDbContext db, string orderId = "order_webhook_001")
    {
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        var guest = await DataSeeder.SeedGuestAsync(db);

        var booking = new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "Razorpay",
            BookingStatus = BookingStatuses.Hold,
            PaymentStatus = "pending",
            CheckinDate = DateTime.UtcNow.Date.AddDays(15),
            CheckoutDate = DateTime.UtcNow.Date.AddDays(17),
            TotalAmount = 8000,
            FinalAmount = 8000,
            AmountReceived = 0,
            GuestsPlanned = 2,
            Notes = "Webhook test",
            Currency = "INR"
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        db.AvailabilityBlocks.Add(new AvailabilityBlock
        {
            ListingId = listing.Id,
            BookingId = booking.Id,
            StartDate = booking.CheckinDate,
            EndDate = booking.CheckoutDate,
            BlockType = BlockStatuses.Hold,
            Source = "Razorpay",
            Status = BlockStatuses.Hold,
            Inventory = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        var payment = new Payment
        {
            BookingId = booking.Id,
            Amount = 8000,
            Method = "Razorpay",
            Type = "payment",
            Status = "pending",
            RazorpayOrderId = orderId,
            ReceivedOn = DateTime.UtcNow,
            Note = $"Razorpay Order ID: {orderId}"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return (booking, payment, guest);
    }

    [Fact]
    public async Task Reconcile_SetsBookingToConfirmed_AndWritesOutbox()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (booking, payment, _) = await SeedHoldBookingAsync(db, "order_wh_001");

        var service = scope.ServiceProvider.GetRequiredService<IRazorpayPaymentService>();
        var result = await service.ReconcileWebhookPaymentAsync("order_wh_001", "pay_wh_001");

        Assert.True(result);

        await db.Entry(booking).ReloadAsync();
        await db.Entry(payment).ReloadAsync();

        Assert.Equal(BookingStatuses.Confirmed, booking.BookingStatus);
        Assert.Equal("paid", booking.PaymentStatus);
        Assert.Equal("completed", payment.Status);
        Assert.Equal("pay_wh_001", payment.RazorpayPaymentId);

        var outbox = await db.OutboxMessages
            .Where(o => o.EntityId == booking.Id.ToString() && o.EventType == EventTypes.BookingConfirmed)
            .ToListAsync();
        Assert.Single(outbox);
        Assert.Contains("webhook", outbox[0].PayloadJson);
    }

    [Fact]
    public async Task Reconcile_WhenAlreadyCompleted_IsIdempotent()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (booking, payment, _) = await SeedHoldBookingAsync(db, "order_wh_idem");

        payment.Status = "completed";
        booking.BookingStatus = BookingStatuses.Confirmed;
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IRazorpayPaymentService>();
        var result = await service.ReconcileWebhookPaymentAsync("order_wh_idem", "pay_wh_idem");

        Assert.True(result);

        var outboxCount = await db.OutboxMessages
            .CountAsync(o => o.EntityId == booking.Id.ToString() && o.EventType == EventTypes.BookingConfirmed);
        Assert.Equal(0, outboxCount);
    }

    [Fact]
    public async Task Reconcile_WhenNoPaymentFound_ReturnsFalse()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRazorpayPaymentService>();

        var result = await service.ReconcileWebhookPaymentAsync("order_nonexistent", "pay_nonexistent");

        Assert.False(result);
    }

    [Fact]
    public async Task Reconcile_SetsBlockStatusToActive()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (booking, _, _) = await SeedHoldBookingAsync(db, "order_wh_block");

        var service = scope.ServiceProvider.GetRequiredService<IRazorpayPaymentService>();
        await service.ReconcileWebhookPaymentAsync("order_wh_block", "pay_wh_block");

        var blocks = await db.AvailabilityBlocks
            .Where(ab => ab.BookingId == booking.Id)
            .ToListAsync();

        Assert.All(blocks, b => Assert.Equal(BlockStatuses.Active, b.Status));
        Assert.All(blocks, b => Assert.Equal("Booking", b.BlockType));
    }
}
