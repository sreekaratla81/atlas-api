using Atlas.Api.Constants;

namespace Atlas.Api.Tests;

public class ConstantsTests
{
    [Fact]
    public void BookingSources_IsOta_ReturnsTrue_ForAirbnb()
    {
        Assert.True(BookingSources.IsOta("airbnb"));
    }

    [Fact]
    public void BookingSources_IsOta_ReturnsFalse_ForDirect()
    {
        Assert.False(BookingSources.IsOta("direct"));
    }

    [Fact]
    public void CommissionRates_ForSource_ReturnsCorrectRate_ForAirbnb()
    {
        Assert.Equal(0.16m, CommissionRates.ForSource("airbnb"));
    }

    [Fact]
    public void CommissionRates_ForSource_ReturnsZero_ForUnknown()
    {
        Assert.Equal(0m, CommissionRates.ForSource("unknown-ota"));
    }

    [Fact]
    public void BlockStatuses_IsBlocking_ReturnsTrue_ForActive()
    {
        Assert.True(BlockStatuses.IsBlocking("Active"));
    }

    [Fact]
    public void BlockStatuses_IsBlocking_ReturnsFalse_ForExpired()
    {
        Assert.False(BlockStatuses.IsBlocking("Expired"));
    }
}
