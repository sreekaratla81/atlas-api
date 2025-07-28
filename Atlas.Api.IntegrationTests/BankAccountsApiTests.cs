using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public class BankAccountsApiTests : IntegrationTestBase
{
    public BankAccountsApiTests(CustomWebApplicationFactory factory) : base(factory) {}

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DataSeeder.SeedBankAccountAsync(db);

        var response = await Client.GetAsync("/api/bankaccounts");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.GetAsync("/api/bankaccounts/1");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreatesAccount()
    {
        var request = new BankAccount
        {
            BankName = "Bank",
            AccountNumber = "123",
            IFSC = "IFSC",
            AccountType = "Savings"
        };

        var response = await Client.PostAsJsonAsync("/api/bankaccounts", request);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.BankAccounts.CountAsync());
    }

    [Fact]
    public async Task Put_UpdatesAccount()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await DataSeeder.SeedBankAccountAsync(db);
        account.BankName = "Updated";

        var response = await Client.PutAsJsonAsync($"/api/bankaccounts/{account.Id}", account);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Updated", (await db2.BankAccounts.FindAsync(account.Id))!.BankName);
    }

    [Fact]
    public async Task Put_ReturnsNotFound_WhenMissing()
    {
        var account = new BankAccount
        {
            Id = 1,
            BankName = "B",
            AccountNumber = "1",
            IFSC = "I",
            AccountType = "S"
        };

        var response = await Client.PutAsJsonAsync("/api/bankaccounts/1", account);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesAccount()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await DataSeeder.SeedBankAccountAsync(db);
        var id = account.Id;

        var response = await Client.DeleteAsync($"/api/bankaccounts/{id}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db2.BankAccounts.AnyAsync(a => a.Id == id));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.DeleteAsync("/api/bankaccounts/1");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
