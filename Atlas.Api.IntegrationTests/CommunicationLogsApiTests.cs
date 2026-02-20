using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "Contract")]
public class CommunicationLogsApiTests : IntegrationTestBase
{
    public CommunicationLogsApiTests(SqlServerTestDatabase database) : base(database) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await Client.GetAsync(ApiControllerRoute("communication-logs"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithFilters_ReturnsOk()
    {
        var response = await Client.GetAsync(ApiControllerRoute("communication-logs?page=1&pageSize=10"));
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<CommunicationLogDto>>();
        Assert.NotNull(list);
    }
}
