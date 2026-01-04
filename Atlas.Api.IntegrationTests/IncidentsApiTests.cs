using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public class IncidentsApiTests : IntegrationTestBase
{
    public IncidentsApiTests(CustomWebApplicationFactory factory) : base(factory) {}

    private async Task<(Listing listing, Booking booking)> SeedIncidentDataAsync(AppDbContext db)
    {
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        var guest = await DataSeeder.SeedGuestAsync(db);
        var booking = await DataSeeder.SeedBookingAsync(db, property, listing, guest);
        return (listing, booking);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedIncidentDataAsync(db);
        await DataSeeder.SeedIncidentAsync(db, data.listing, data.booking);

        var response = await Client.GetAsync(ApiRoute("api/incidents"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.GetAsync(ApiRoute("api/incidents/1"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreatesIncident()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedIncidentDataAsync(db);
        var incident = new Incident
        {
            ListingId = data.listing.Id,
            BookingId = data.booking.Id,
            Description = "desc",
            ActionTaken = "none",
            Status = "open",
            CreatedBy = "tester",
            CreatedOn = DateTime.UtcNow
        };
        var response = await Client.PostAsJsonAsync(ApiRoute("api/incidents"), incident);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db2.Incidents.CountAsync());
    }

    [Fact]
    public async Task Put_UpdatesIncident()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedIncidentDataAsync(db);
        var incident = await DataSeeder.SeedIncidentAsync(db, data.listing, data.booking);
        incident.Status = "closed";

        var response = await Client.PutAsJsonAsync(ApiRoute($"api/incidents/{incident.Id}"), incident);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("closed", (await db2.Incidents.FindAsync(incident.Id))!.Status);
    }

    [Fact]
    public async Task Put_ReturnsBadRequest_OnIdMismatch()
    {
        var incident = new Incident
        {
            Id = 1,
            ListingId = 1,
            BookingId = null,
            Description = "d",
            ActionTaken = "a",
            Status = "open",
            CreatedBy = "c",
            CreatedOn = DateTime.UtcNow
        };

        var response = await Client.PutAsJsonAsync(ApiRoute("api/incidents/2"), incident);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesIncident()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedIncidentDataAsync(db);
        var incident = await DataSeeder.SeedIncidentAsync(db, data.listing, data.booking);
        var id = incident.Id;

        var response = await Client.DeleteAsync(ApiRoute($"api/incidents/{id}"));
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db2.Incidents.AnyAsync(i => i.Id == id));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.DeleteAsync(ApiRoute("api/incidents/1"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
