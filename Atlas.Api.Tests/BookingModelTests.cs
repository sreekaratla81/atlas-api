using Atlas.Api.Models;

namespace Atlas.Api.Tests;

public class BookingModelTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var booking = new Booking
        {
            ListingId = 1,
            GuestId = 1,
            BookingSource = "b",
            Notes = "n",
            PaymentStatus = "Pending"
        };

        Assert.Equal("Pending", booking.PaymentStatus);
        Assert.True((DateTime.UtcNow - booking.CreatedAt).TotalSeconds < 5);
    }
}
