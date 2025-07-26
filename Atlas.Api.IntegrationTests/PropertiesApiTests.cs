using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public class PropertiesApiTests : IntegrationTestBase
{
    public PropertiesApiTests(CustomWebApplicationFactory factory) : base(factory) {}

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Properties.Add(new Property
        {
            Name = "Prop1",
            Address = "Addr",
            Type = "House",
            OwnerName = "Owner",
            ContactPhone = "111",
            CommissionPercent = 10,
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/properties");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
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
        var response = await Client.PostAsJsonAsync("/api/properties", property);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Properties.CountAsync();
        Assert.Equal(1, count);
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
        var response = await Client.PutAsJsonAsync($"/api/properties/{property.Id}", property);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await db2.Properties.FindAsync(property.Id);
        Assert.Equal("Updated", updated!.Name);
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

        var response = await Client.DeleteAsync($"/api/properties/{id}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await db2.Properties.AnyAsync(p => p.Id == id);
        Assert.False(exists);
    }
}
