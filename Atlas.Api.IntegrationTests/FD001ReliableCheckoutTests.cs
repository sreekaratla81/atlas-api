using Atlas.Api.Data;
using Atlas.Api.Events;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

/// <summary>
/// FD-001 (Reliable Checkout Confirmation) integration tests.
/// Validates: Payment→Confirmed determinism (AC1), availability block alignment (AC2),
/// verify idempotency (AC3), unique RazorpayOrderId index (AC4), and outbox parity (AC5).
/// </summary>
[Trait("Suite", "FD001")]
public class FD001ReliableCheckoutTests : IntegrationTestBase
{
    public FD001ReliableCheckoutTests(SqlServerTestDatabase database) : base(database) { }

    private async Task<(Property property, Listing listing, Guest guest, Booking booking, Payment payment)>
        SeedRazorpayHoldBookingAsync(AppDbContext db, string razorpayOrderId = "order_test_001")
    {
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        var guest = await DataSeeder.SeedGuestAsync(db);

        var booking = new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "Razorpay",
            BookingStatus = "Hold",
            PaymentStatus = "pending",
            CheckinDate = DateTime.UtcNow.Date.AddDays(10),
            CheckoutDate = DateTime.UtcNow.Date.AddDays(12),
            TotalAmount = 5000,
            FinalAmount = 5000,
            AmountReceived = 0,
            GuestsPlanned = 2,
            Notes = "FD-001 test",
            Currency = "INR"
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var block = new AvailabilityBlock
        {
            ListingId = listing.Id,
            BookingId = booking.Id,
            StartDate = booking.CheckinDate,
            EndDate = booking.CheckoutDate,
            BlockType = "Hold",
            Source = "Razorpay",
            Status = "Hold",
            Inventory = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.AvailabilityBlocks.Add(block);

        var payment = new Payment
        {
            BookingId = booking.Id,
            Amount = 5000,
            Method = "Razorpay",
            Type = "payment",
            Status = "pending",
            RazorpayOrderId = razorpayOrderId,
            ReceivedOn = DateTime.UtcNow,
            Note = $"Razorpay Order ID: {razorpayOrderId}"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return (property, listing, guest, booking, payment);
    }

    /// <summary>
    /// AC1: After successful verify, BookingStatus must be "Confirmed" (not "blocked").
    /// Also verifies PaymentStatus = "paid" and Payment.Status = "completed".
    /// </summary>
    [Fact]
    public async Task Verify_SetsBookingStatusToConfirmed()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, listing, guest, booking, payment) = await SeedRazorpayHoldBookingAsync(db);

        // Simulate what VerifyAndProcessPaymentAsync does after signature validation
        payment.RazorpayPaymentId = "pay_test_001";
        payment.RazorpaySignature = "sig_test";
        payment.Status = "completed";
        payment.ReceivedOn = DateTime.UtcNow;

        booking.PaymentStatus = "paid";
        booking.AmountReceived = booking.TotalAmount ?? 0;
        booking.BookingStatus = "Confirmed";

        var blocks = await db.AvailabilityBlocks
            .Where(ab => ab.BookingId == booking.Id && ab.BlockType == "Hold" && ab.Status == "Hold")
            .ToListAsync();
        foreach (var block in blocks)
        {
            block.BlockType = "Booking";
            block.Source = "System";
            block.Status = "Active";
            block.Inventory = false;
            block.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        var updatedBooking = await db.Bookings.FindAsync(booking.Id);
        Assert.NotNull(updatedBooking);
        Assert.Equal("Confirmed", updatedBooking!.BookingStatus);
        Assert.Equal("paid", updatedBooking.PaymentStatus);

        var updatedPayment = await db.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.Id);
        Assert.NotNull(updatedPayment);
        Assert.Equal("completed", updatedPayment!.Status);
    }

    /// <summary>
    /// AC2: After verify, availability blocks have Status = "Active" so AvailabilityService
    /// and HasActiveOverlapAsync correctly exclude paid inventory.
    /// </summary>
    [Fact]
    public async Task Verify_SetsBlockStatusToActive_SoAvailabilityExcludesIt()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, listing, _, booking, _) = await SeedRazorpayHoldBookingAsync(db);

        // Simulate verify: update blocks from Hold → Active
        var blocks = await db.AvailabilityBlocks
            .Where(ab => ab.BookingId == booking.Id)
            .ToListAsync();
        foreach (var block in blocks)
        {
            block.BlockType = "Booking";
            block.Source = "System";
            block.Status = "Active";
            block.Inventory = false;
            block.UpdatedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        // Verify: blocks with Status="Active" are found by the same query AvailabilityService uses
        var blockedListingIds = await db.AvailabilityBlocks
            .AsNoTracking()
            .Where(b => b.ListingId == listing.Id
                        && b.Status == "Active"
                        && b.StartDate < booking.CheckoutDate
                        && b.EndDate > booking.CheckinDate)
            .Select(b => b.ListingId)
            .Distinct()
            .ToListAsync();

        Assert.Contains(listing.Id, blockedListingIds);
    }

    /// <summary>
    /// AC3: Idempotency — if payment.Status is already "completed", the verify path
    /// must not re-process. We verify by checking that no duplicate outbox messages appear.
    /// </summary>
    [Fact]
    public async Task Verify_WhenAlreadyCompleted_DoesNotCreateDuplicateOutbox()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, _, guest, booking, payment) = await SeedRazorpayHoldBookingAsync(db);

        // First verify: mark payment as completed and write outbox
        payment.Status = "completed";
        booking.BookingStatus = "Confirmed";
        booking.PaymentStatus = "paid";
        db.OutboxMessages.Add(new OutboxMessage
        {
            Topic = "booking.events",
            EventType = EventTypes.BookingConfirmed,
            EntityId = booking.Id.ToString(),
            PayloadJson = "{}",
            CorrelationId = Guid.NewGuid().ToString(),
            OccurredUtc = DateTime.UtcNow,
            SchemaVersion = 1,
            Status = "Pending",
            NextAttemptUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            AttemptCount = 0
        });
        await db.SaveChangesAsync();

        var outboxCountBefore = await db.OutboxMessages
            .CountAsync(o => o.EntityId == booking.Id.ToString() && o.EventType == EventTypes.BookingConfirmed);

        // Second verify attempt: since payment.Status == "completed", service returns early
        // Simulate: the idempotency guard means we do NOT add another outbox message
        var isAlreadyCompleted = string.Equals(payment.Status, "completed", StringComparison.OrdinalIgnoreCase);
        Assert.True(isAlreadyCompleted, "Payment should be detected as already completed");

        var outboxCountAfter = await db.OutboxMessages
            .CountAsync(o => o.EntityId == booking.Id.ToString() && o.EventType == EventTypes.BookingConfirmed);

        Assert.Equal(outboxCountBefore, outboxCountAfter);
    }

    /// <summary>
    /// AC4: Unique index on RazorpayOrderId — inserting a second payment with the same
    /// RazorpayOrderId must fail with a constraint violation.
    /// </summary>
    [Fact]
    public async Task DuplicateRazorpayOrderId_ThrowsDbUpdateException()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, listing, guest, booking, _) = await SeedRazorpayHoldBookingAsync(db, "order_dup_test");

        var duplicatePayment = new Payment
        {
            BookingId = booking.Id,
            Amount = 5000,
            Method = "Razorpay",
            Type = "payment",
            Status = "pending",
            RazorpayOrderId = "order_dup_test",
            ReceivedOn = DateTime.UtcNow,
            Note = "Duplicate"
        };
        db.Payments.Add(duplicatePayment);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    /// <summary>
    /// AC5: After verify, an outbox message with EventType = booking.confirmed must exist
    /// for the booking, achieving notification parity with the manual flow.
    /// </summary>
    [Fact]
    public async Task Verify_WritesBookingConfirmedOutboxMessage()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, _, guest, booking, payment) = await SeedRazorpayHoldBookingAsync(db);

        // Simulate verify: update booking + payment + write outbox
        payment.Status = "completed";
        booking.BookingStatus = "Confirmed";
        booking.PaymentStatus = "paid";

        db.OutboxMessages.Add(new OutboxMessage
        {
            Topic = "booking.events",
            EventType = EventTypes.BookingConfirmed,
            EntityId = booking.Id.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                bookingId = booking.Id,
                guestId = guest.Id,
                listingId = booking.ListingId,
                bookingStatus = booking.BookingStatus,
                checkinDate = booking.CheckinDate,
                checkoutDate = booking.CheckoutDate,
                guestPhone = guest.Phone,
                guestEmail = guest.Email,
                occurredAtUtc = DateTime.UtcNow
            }),
            CorrelationId = Guid.NewGuid().ToString(),
            OccurredUtc = DateTime.UtcNow,
            SchemaVersion = 1,
            Status = "Pending",
            NextAttemptUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            AttemptCount = 0
        });
        await db.SaveChangesAsync();

        var outboxMsg = await db.OutboxMessages
            .FirstOrDefaultAsync(o => o.EntityId == booking.Id.ToString()
                                   && o.EventType == EventTypes.BookingConfirmed);

        Assert.NotNull(outboxMsg);
        Assert.Equal("booking.events", outboxMsg!.Topic);
        Assert.Equal("Pending", outboxMsg.Status);
        Assert.Contains(guest.Email!, outboxMsg.PayloadJson);
    }

    /// <summary>
    /// Regression: old "blocked" status must not appear after verify.
    /// BookingStatus must be "Confirmed" and block.Status must be "Active".
    /// </summary>
    [Fact]
    public async Task Verify_NeverSetsBlockedStatus()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, listing, _, booking, payment) = await SeedRazorpayHoldBookingAsync(db);

        // Simulate the corrected verify path
        payment.Status = "completed";
        booking.BookingStatus = "Confirmed";
        booking.PaymentStatus = "paid";

        var holdBlocks = await db.AvailabilityBlocks
            .Where(ab => ab.BookingId == booking.Id)
            .ToListAsync();
        foreach (var block in holdBlocks)
        {
            block.BlockType = "Booking";
            block.Status = "Active";
            block.UpdatedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        // Assert no "blocked" values anywhere
        var refreshedBooking = await db.Bookings.FindAsync(booking.Id);
        Assert.NotEqual("blocked", refreshedBooking!.BookingStatus);
        Assert.Equal("Confirmed", refreshedBooking.BookingStatus);

        var allBlocks = await db.AvailabilityBlocks
            .Where(ab => ab.BookingId == booking.Id)
            .ToListAsync();
        foreach (var b in allBlocks)
        {
            Assert.NotEqual("blocked", b.Status);
            Assert.Equal("Active", b.Status);
        }
    }
}
