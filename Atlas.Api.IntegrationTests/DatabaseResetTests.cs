using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Api.IntegrationTests;

public class DatabaseResetTests : IntegrationTestBase
{
    public DatabaseResetTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Test1_AddGuestForVerification()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Guests.Add(new Guest
        {
            Name = "Reset Guest",
            Phone = "123",
            Email = "reset@example.com",
            IdProofUrl = "none"
        });

        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Guests.CountAsync());
    }

    [Fact]
    public async Task Test2_DataIsClearedBetweenTests()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(0, await db.Guests.CountAsync());
    }
}
