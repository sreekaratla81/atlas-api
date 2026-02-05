using Atlas.Api.Data;
using System.Net.Http.Json;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "Contract")]
public class PropertiesApiTests : IntegrationTestBase
{
    public PropertiesApiTests(CustomWebApplicationFactory factory) : base(factory) {}

    private static async Task<(Property property, Listing listing, Guest guest, Booking booking)> SeedDataAsync(AppDbContext db)
    {
        var property = new Property
        {
            Name = "SeedProp",
            Address = "Addr",
            Type = "House",
            OwnerName = "Owner",
            ContactPhone = "000",
            CommissionPercent = 10,
            Status = "Active"
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing",
            Floor = 1,
            Type = "Room",
            Status = "Available",
            WifiName = "wifi",
            WifiPassword = "pass",
            MaxGuests = 2
        };
        db.Listings.Add(listing);

        var guest = new Guest
        {
            Name = "Guest",
            Phone = "1",
            Email = "g@example.com",
            IdProofUrl = "N/A"
        };
        db.Guests.Add(guest);
        await db.SaveChangesAsync();

        var booking = new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            PaymentStatus = "Pending",
            CheckinDate = DateTime.UtcNow.Date,
            CheckoutDate = DateTime.UtcNow.Date.AddDays(1),
            AmountReceived = 100,
            Notes = "seed"
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        return (property, listing, guest, booking);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await Client.GetAsync(ApiRoute("properties"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var properties = await response.Content.ReadFromJsonAsync<List<Property>>();
        Assert.NotNull(properties);
        Assert.Contains(properties, p => p.Name == "Test Villa");
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        // Id 1 is seeded in every test run. Use a high value that will not exist
        // to verify the NotFound response.
        var response = await Client.GetAsync(ApiRoute("properties/999"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreatesProperty()
    {
        var property = new Property
        {
            Name = "Prop",
            Address = "Addr",
            Type = "House",
            OwnerName = "Owner",
            ContactPhone = "123",
            CommissionPercent = 10,
            Status = "Active"
        };
        var response = await Client.PostAsJsonAsync(ApiRoute("properties"), property);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Properties.CountAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Put_UpdatesProperty()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = new Property
        {
            Name = "Prop",
            Address = "Addr",
            Type = "House",
            OwnerName = "Owner",
            ContactPhone = "123",
            CommissionPercent = 10,
            Status = "Active"
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        property.Name = "Updated";
        var response = await Client.PutAsJsonAsync(ApiRoute($"properties/{property.Id}"), property);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await db2.Properties.FindAsync(property.Id);
        Assert.Equal("Updated", updated!.Name);
    }

    [Fact]
    public async Task Put_ReturnsBadRequest_OnIdMismatch()
    {
        var property = new Property { Id = 1, Name = "P", Address = "A", Type = "T", OwnerName = "O", ContactPhone = "0", CommissionPercent = 10, Status = "A" };
        var response = await Client.PutAsJsonAsync(ApiRoute("properties/2"), property);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesProperty()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = new Property
        {
            Name = "Prop",
            Address = "Addr",
            Type = "House",
            OwnerName = "Owner",
            ContactPhone = "123",
            CommissionPercent = 10,
            Status = "Active"
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        var id = property.Id;

        var response = await Client.DeleteAsync(ApiRoute($"properties/{id}"));
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await db2.Properties.AnyAsync(p => p.Id == id);
        Assert.False(exists);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        // The database always contains a seed property with Id 1. Use a
        // non-existent id to ensure the API returns NotFound.
        var response = await Client.DeleteAsync(ApiRoute("properties/999"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
