using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public class GuestsApiTests : IntegrationTestBase
{
    public GuestsApiTests(CustomWebApplicationFactory factory) : base(factory) {}

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Guests.Add(new Guest { Name = "Guest", Phone = "1", Email = "g@example.com", IdProofUrl = "N/A" });
        await db.SaveChangesAsync();
        var response = await Client.GetAsync("/api/guests");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreatesGuest()
    {
        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com", IdProofUrl = "N/A" };
        var response = await Client.PostAsJsonAsync("/api/guests", guest);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Guests.CountAsync());
    }

    [Fact]
    public async Task Put_UpdatesGuest()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com", IdProofUrl = "N/A" };
        db.Guests.Add(guest);
        await db.SaveChangesAsync();
        guest.Name = "Updated";
        var response = await Client.PutAsJsonAsync($"/api/guests/{guest.Id}", guest);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Updated", (await db2.Guests.FindAsync(guest.Id))!.Name);
    }

    [Fact]
    public async Task Delete_RemovesGuest()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com", IdProofUrl = "N/A" };
        db.Guests.Add(guest);
        await db.SaveChangesAsync();
        var id = guest.Id;
        var response = await Client.DeleteAsync($"/api/guests/{id}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db2.Guests.AnyAsync(g => g.Id == id));
    }
}
