using Atlas.Api.Data;
using Atlas.Api.Models;
using Application.Guests.Queries.SearchGuests;
using Infrastructure.Phone;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

namespace Atlas.Api.IntegrationTests;

public class GuestsSearchApiTests : IntegrationTestBase
{
    public GuestsSearchApiTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task SeedGuestsAsync(int count = 200)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var normalizer = new PhoneNormalizer();
        for (int i = 0; i < count; i++)
        {
            var phone = $"990000{i:D3}";
            var guest = new Guest { Name = $"Raj{i}", Phone = phone, Email = $"raj{i}@example.com" };
            guest.NameSearch = guest.Name.ToLowerInvariant();
            guest.PhoneE164 = normalizer.Normalize(phone);
            db.Guests.Add(guest);
        }
        await db.SaveChangesAsync();
    }

    private void Authenticate()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("testkey123"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: new[] { new Claim("sub", "test") }, expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: creds);
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
    }

    [Fact]
    public async Task Search_ByPhone_ReturnsExact()
    {
        await SeedGuestsAsync();
        Authenticate();
        var response = await Client.GetFromJsonAsync<SearchGuestsResponse>("/api/guests/search?query=990000050");
        Assert.Equal(1, response!.Items.Count);
        Assert.Equal("Raj50", response.Items[0].Name);
    }

    [Fact]
    public async Task Search_ByEmail_ReturnsExact()
    {
        await SeedGuestsAsync();
        Authenticate();
        var response = await Client.GetFromJsonAsync<SearchGuestsResponse>("/api/guests/search?query=raj10@example.com");
        Assert.Equal("Raj10", response!.Items[0].Name);
    }

    [Fact]
    public async Task Search_ByNamePrefix_Returns()
    {
        await SeedGuestsAsync();
        Authenticate();
        var response = await Client.GetFromJsonAsync<SearchGuestsResponse>("/api/guests/search?query=Raj1");
        Assert.True(response!.Items.Count > 0);
    }

    [Fact]
    public async Task Search_ByNameContains_Returns()
    {
        await SeedGuestsAsync();
        Authenticate();
        var response = await Client.GetFromJsonAsync<SearchGuestsResponse>("/api/guests/search?query=aj1");
        Assert.True(response!.Items.Count > 0);
    }

    [Fact]
    public async Task Search_Paging_Works()
    {
        await SeedGuestsAsync();
        Authenticate();
        var response = await Client.GetFromJsonAsync<SearchGuestsResponse>("/api/guests/search?query=Raj&page=2&pageSize=5");
        Assert.Equal(2, response!.Page);
        Assert.Equal(5, response.Items.Count);
    }

    [Fact]
    public async Task Search_MinLengthValidation()
    {
        await SeedGuestsAsync();
        Authenticate();
        var resp = await Client.GetAsync("/api/guests/search?query=r");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
    }
    

    [Fact]
    public async Task Search_RequiresAuth()
    {
        await SeedGuestsAsync();
        var resp = await Client.GetAsync("/api/guests/search?query=Raj");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}


