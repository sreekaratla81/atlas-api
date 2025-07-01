using Atlas.Api.Models;

namespace Atlas.Api.Tests;

public class BankAccountModelTests
{
    [Fact]
    public void Constructor_SetsCreatedAt()
    {
        var account = new BankAccount
        {
            BankName = "b",
            AccountNumber = "a",
            IFSC = "i",
            AccountType = "s"
        };

        Assert.True((DateTime.UtcNow - account.CreatedAt).TotalSeconds < 5);
    }
}
