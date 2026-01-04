using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;

namespace Atlas.Api.IntegrationTests;

[TestCaseOrderer(typeof(AlphabeticalOrderer))]
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

public class AlphabeticalOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
    {
        return testCases.OrderBy(tc => tc.TestMethod.Method.Name);
    }
}
