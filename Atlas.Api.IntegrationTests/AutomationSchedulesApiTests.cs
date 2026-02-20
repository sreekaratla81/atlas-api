using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "Contract")]
public class AutomationSchedulesApiTests : IntegrationTestBase
{
    public AutomationSchedulesApiTests(SqlServerTestDatabase database) : base(database) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await Client.GetAsync(ApiControllerRoute("automation-schedules"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithFilters_ReturnsOk()
    {
        var response = await Client.GetAsync(ApiControllerRoute("automation-schedules?page=1&pageSize=10"));
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<AutomationScheduleDto>>();
        Assert.NotNull(list);
    }
}
