using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "Contract")]
public class UsersApiTests : IntegrationTestBase
{
    public UsersApiTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DataSeeder.SeedUserAsync(db);

        var response = await Client.GetAsync("/api/api/users");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.GetAsync("/api/api/users/1");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreatesUser()
    {
        var user = new User { Name = "User", Phone = "1", Email = "u@example.com", PasswordHash = "hash", Role = "admin" };
        var response = await Client.PostAsJsonAsync("/api/api/users", user);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Put_UpdatesUser()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await DataSeeder.SeedUserAsync(db);
        user.Name = "Updated";

        var response = await Client.PutAsJsonAsync($"/api/api/users/{user.Id}", user);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Updated", (await db2.Users.FindAsync(user.Id))!.Name);
    }

    [Fact]
    public async Task Put_ReturnsBadRequest_OnIdMismatch()
    {
        var user = new User { Id = 1, Name = "U", Phone = "1", Email = "e", PasswordHash = "p", Role = "r" };
        var response = await Client.PutAsJsonAsync("/api/api/users/2", user);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesUser()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await DataSeeder.SeedUserAsync(db);
        var id = user.Id;

        var response = await Client.DeleteAsync($"/api/api/users/{id}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db2.Users.AnyAsync(u => u.Id == id));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.DeleteAsync("/api/api/users/1");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
