using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Tests;

public class IdempotentOrderTests
{
    private static AppDbContext GetContext(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task PendingPayment_Query_ReturnsMostRecentForSameListingAndDates()
    {
        using var context = GetContext(nameof(PendingPayment_Query_ReturnsMostRecentForSameListingAndDates));

        var property = new Property
        {
            Name = "P", Address = "A", Type = "House", OwnerName = "O",
            ContactPhone = "0", CommissionPercent = 10, Status = "Active"
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var listing = new Listing
        {
            PropertyId = property.Id, Property = property, Name = "L",
            Floor = 1, Type = "Room", Status = "Available",
            WifiName = "w", WifiPassword = "p", MaxGuests = 2
        };
        context.Listings.Add(listing);
        await context.SaveChangesAsync();

        var checkin = new DateTime(2026, 4, 1);
        var checkout = new DateTime(2026, 4, 3);

        var booking = new Booking
        {
            ListingId = listing.Id, GuestId = 0, BookingSource = "Razorpay",
            PaymentStatus = "pending", CheckinDate = checkin, CheckoutDate = checkout,
            GuestsPlanned = 1, GuestsActual = 0, ExtraGuestCharge = 0, CommissionAmount = 0
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var payment = new Payment
        {
            BookingId = booking.Id, Amount = 5000, Method = "Razorpay", Type = "payment",
            ReceivedOn = DateTime.UtcNow, RazorpayOrderId = "order_dup_001", Status = "pending",
            Note = "Razorpay Order"
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var recentCutoff = DateTime.UtcNow.AddMinutes(-10);
        var found = await context.Payments
            .Where(p => p.Booking.ListingId == listing.Id
                     && p.Booking.CheckinDate == checkin
                     && p.Booking.CheckoutDate == checkout
                     && p.Status == "pending"
                     && p.RazorpayOrderId != null
                     && p.ReceivedOn > recentCutoff)
            .OrderByDescending(p => p.ReceivedOn)
            .FirstOrDefaultAsync();

        Assert.NotNull(found);
        Assert.Equal("order_dup_001", found!.RazorpayOrderId);
    }

    [Fact]
    public async Task PendingPayment_Query_IgnoresOldPayments()
    {
        using var context = GetContext(nameof(PendingPayment_Query_IgnoresOldPayments));

        var property = new Property
        {
            Name = "P", Address = "A", Type = "House", OwnerName = "O",
            ContactPhone = "0", CommissionPercent = 10, Status = "Active"
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var listing = new Listing
        {
            PropertyId = property.Id, Property = property, Name = "L",
            Floor = 1, Type = "Room", Status = "Available",
            WifiName = "w", WifiPassword = "p", MaxGuests = 2
        };
        context.Listings.Add(listing);
        await context.SaveChangesAsync();

        var checkin = new DateTime(2026, 5, 1);
        var checkout = new DateTime(2026, 5, 3);

        var booking = new Booking
        {
            ListingId = listing.Id, GuestId = 0, BookingSource = "Razorpay",
            PaymentStatus = "pending", CheckinDate = checkin, CheckoutDate = checkout,
            GuestsPlanned = 1, GuestsActual = 0, ExtraGuestCharge = 0, CommissionAmount = 0
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var payment = new Payment
        {
            BookingId = booking.Id, Amount = 5000, Method = "Razorpay", Type = "payment",
            ReceivedOn = DateTime.UtcNow.AddMinutes(-15),
            RazorpayOrderId = "order_expired_001", Status = "pending",
            Note = "Razorpay Order"
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var recentCutoff = DateTime.UtcNow.AddMinutes(-10);
        var found = await context.Payments
            .Where(p => p.Booking.ListingId == listing.Id
                     && p.Booking.CheckinDate == checkin
                     && p.Booking.CheckoutDate == checkout
                     && p.Status == "pending"
                     && p.RazorpayOrderId != null
                     && p.ReceivedOn > recentCutoff)
            .OrderByDescending(p => p.ReceivedOn)
            .FirstOrDefaultAsync();

        Assert.Null(found);
    }
}
