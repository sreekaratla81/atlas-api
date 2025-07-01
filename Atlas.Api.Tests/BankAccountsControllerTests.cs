using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Api.Tests;

public class BankAccountsControllerTests
{
    private static AppDbContext GetContext(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAccount()
    {
        using var context = GetContext(nameof(Create_ReturnsCreatedAccount));
        var controller = new BankAccountsController(context, NullLogger<BankAccountsController>.Instance);
        var request = new BankAccountRequestDto
        {
            BankName = "Bank",
            AccountNumber = "123",
            IFSC = "IFSC",
            AccountType = "Savings"
        };

        var result = await controller.Create(request);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<BankAccountResponseDto>(created.Value);
        Assert.Equal("Bank", dto.BankName);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        using var context = GetContext(nameof(Get_ReturnsNotFound_WhenMissing));
        var controller = new BankAccountsController(context, NullLogger<BankAccountsController>.Instance);

        var result = await controller.Get(1);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenAccountMissing()
    {
        using var context = GetContext(nameof(Update_ReturnsNotFound_WhenAccountMissing));
        var controller = new BankAccountsController(context, NullLogger<BankAccountsController>.Instance);
        var dto = new BankAccountRequestDto { BankName = "b", AccountNumber = "1", IFSC = "i", AccountType = "s" };

        var result = await controller.Update(1, dto);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_RemovesAccount_WhenExists()
    {
        using var context = GetContext(nameof(Delete_RemovesAccount_WhenExists));
        context.BankAccounts.Add(new BankAccount { Id = 1, BankName = "B", AccountNumber = "1", IFSC = "I", AccountType = "S" });
        await context.SaveChangesAsync();
        var controller = new BankAccountsController(context, NullLogger<BankAccountsController>.Instance);

        var result = await controller.Delete(1);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(context.BankAccounts);
    }
}
