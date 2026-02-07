using System.Linq;
using Xunit;

namespace Atlas.Api.IntegrationTests;

public class CorsIntegrationTests : IntegrationTestBase
{
    public CorsIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task PreflightRequest_AllowsGuestPortalOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, ApiRoute("test-cors"));
        request.Headers.Add("Origin", "https://dev.atlashomestays.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await Client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("https://dev.atlashomestays.com", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }
}
