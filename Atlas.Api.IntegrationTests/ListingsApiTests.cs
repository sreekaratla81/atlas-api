using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "Contract")]
public class ListingsApiTests : IntegrationTestBase
{
    public ListingsApiTests(SqlServerTestDatabase database) : base(database) { }

    private async Task<(Property property, Listing listing)> SeedListingAsync(AppDbContext db)
    {
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        return (property, listing);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedListingAsync(db);

        var response = await Client.GetAsync(ApiRoute("listings"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.GetAsync(ApiRoute("listings/1"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreatesListing()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (property, _) = await SeedListingAsync(db);
        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "L",
            Floor = 1,
            Type = "Room",
            Status = "Available",
            WifiName = "wifi",
            WifiPassword = "pass",
            MaxGuests = 2
        };

        var response = await Client.PostAsJsonAsync(ApiRoute("listings"), listing);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db2.Listings.CountAsync());
    }

    [Fact]
    public async Task Put_UpdatesListing()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, listing) = await SeedListingAsync(db);
        listing.Name = "Updated";

        var response = await Client.PutAsJsonAsync(ApiRoute($"listings/{listing.Id}"), listing);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Updated", (await db2.Listings.FindAsync(listing.Id))!.Name);
    }

    [Fact]
    public async Task Put_ReturnsBadRequest_OnIdMismatch()
    {
        var listing = new Listing { Id = 1, PropertyId = 1, Property = new Property { Id = 1, Name = "P", Address = "A", Type = "T", OwnerName = "O", ContactPhone = "0", CommissionPercent = 10, Status = "Active" }, Name = "N", Floor = 1, Type = "T", Status = "A", WifiName = "w", WifiPassword = "p", MaxGuests = 1 };
        var response = await Client.PutAsJsonAsync(ApiRoute("listings/2"), listing);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesListing()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, listing) = await SeedListingAsync(db);
        var id = listing.Id;

        var response = await Client.DeleteAsync(ApiRoute($"listings/{id}"));
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db2.Listings.AnyAsync(l => l.Id == id));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.DeleteAsync(ApiRoute("listings/1"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
