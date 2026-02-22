using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Tests;

public static class TestHelpers
{
    public static AppDbContext CreateInMemoryDb(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }
}
