using Atlas.Api.DTOs;

namespace Atlas.Api.Tests;

public class BookingDtoTests
{
    [Fact]
    public void PaymentStatus_DefaultsToPaid()
    {
        var dto = new BookingDto();
        Assert.Equal("Paid", dto.PaymentStatus);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        var dto = new BookingDto { ListingId = 1, GuestId = 2 };
        Assert.Equal(1, dto.ListingId);
        Assert.Equal(2, dto.GuestId);
    }
}
