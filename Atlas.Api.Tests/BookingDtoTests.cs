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
    public void BookingDefaults_AreSet()
    {
        var dto = new BookingDto();
        Assert.Equal("Lead", dto.BookingStatus);
        Assert.Equal("INR", dto.Currency);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        var dto = new BookingDto
        {
            ListingId = 1,
            GuestId = 2,
            BookingStatus = "Confirmed",
            Currency = "USD",
            TotalAmount = 150m
        };
        Assert.Equal(1, dto.ListingId);
        Assert.Equal(2, dto.GuestId);
        Assert.Equal("Confirmed", dto.BookingStatus);
        Assert.Equal("USD", dto.Currency);
        Assert.Equal(150m, dto.TotalAmount);
    }
}
