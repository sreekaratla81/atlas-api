using Atlas.Api.DTOs;

namespace Atlas.Api.Tests;

public class DtoMappingTests
{
    [Fact]
    public void PaymentResponseDto_ExcludesRazorpaySignature()
    {
        Assert.Null(typeof(PaymentResponseDto).GetProperty("RazorpaySignature"));
    }

    [Fact]
    public void UserResponseDto_ExcludesPasswordHash()
    {
        Assert.Null(typeof(UserResponseDto).GetProperty("PasswordHash"));
    }

    [Fact]
    public void ListingResponseDto_ExcludesWifiCredentials()
    {
        Assert.Null(typeof(ListingResponseDto).GetProperty("WifiName"));
        Assert.Null(typeof(ListingResponseDto).GetProperty("WifiPassword"));
    }

    [Fact]
    public void PropertyResponseDto_ExcludesTenantId()
    {
        Assert.Null(typeof(PropertyResponseDto).GetProperty("TenantId"));
    }
}
