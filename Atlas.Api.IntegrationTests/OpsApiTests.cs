using System.Net.Http.Json;
using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public class OpsApiTests : IntegrationTestBase
{
    public OpsApiTests(SqlServerTestDatabase database) : base(database)
    {
    }

    [Fact]
    public async Task DbInfo_ReturnsDatabaseNameAndMarker()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = db.Database.GetDbConnection();

        var response = await Client.GetAsync(ApiRoute("/ops/db-info"));
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DbInfoResponse>();

        Assert.NotNull(payload);
        Assert.Equal(connection.Database, payload!.database);
        Assert.Equal("DEV", payload.marker);
    }

    private sealed record DbInfoResponse(string environment, string server, string database, string marker);
}
