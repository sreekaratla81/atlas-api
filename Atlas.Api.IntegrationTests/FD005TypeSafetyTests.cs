using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "FD005")]
public class FD005TypeSafetyTests : IntegrationTestBase
{
    public FD005TypeSafetyTests(SqlServerTestDatabase database) : base(database) {}

    [Fact]
    public async Task Property_Create_ResponseExcludesTenantId()
    {
        var payload = new { Name = "TypeSafe Prop", Address = "1 Main", Type = "Villa", OwnerName = "Owner", ContactPhone = "000" };
        var response = await Client.PostAsJsonAsync(ApiRoute("properties"), payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.TryGetProperty("tenantId", out _));
    }

    [Fact]
    public async Task User_Create_ResponseExcludesPasswordHash()
    {
        var payload = new { Name = "TestUser", Email = "u@t.com", Phone = "111", Role = "admin", Password = "secret123" };
        var response = await Client.PostAsJsonAsync(ApiControllerRoute("users"), payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.TryGetProperty("passwordHash", out _));
        Assert.True(doc.RootElement.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Listing_Get_ResponseExcludesWifiCredentials()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Atlas.Api.Data.AppDbContext>();
        var property = new Atlas.Api.Models.Property { Name = "P", Address = "A", Type = "H", OwnerName = "O", ContactPhone = "0", CommissionPercent = 10, Status = "Active" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        var listing = new Atlas.Api.Models.Listing { PropertyId = property.Id, Property = property, Name = "L", Floor = 1, Type = "Room", Status = "Available", WifiName = "secret-wifi", WifiPassword = "secret-pass", MaxGuests = 2 };
        db.Listings.Add(listing);
        await db.SaveChangesAsync();

        var response = await Client.GetAsync(ApiRoute($"listings/{listing.Id}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.TryGetProperty("wifiName", out _));
        Assert.False(doc.RootElement.TryGetProperty("wifiPassword", out _));
    }

    [Fact]
    public async Task Payments_GetAll_ReturnsTotalCountHeader()
    {
        var response = await Client.GetAsync(ApiControllerRoute("payments"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Total-Count"));
        Assert.True(response.Headers.Contains("X-Page"));
    }

    [Fact]
    public async Task Ops_Outbox_ReturnsTotalCountHeader()
    {
        var response = await Client.GetAsync(ApiRoute("ops/outbox"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Total-Count"));
    }
}
