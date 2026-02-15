using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "Contract")]
public class MessageTemplatesApiTests : IntegrationTestBase
{
    public MessageTemplatesApiTests(SqlServerTestDatabase database) : base(database) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await Client.GetAsync(ApiControllerRoute("message-templates"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_Returns404_WhenMissing()
    {
        var response = await Client.GetAsync(ApiControllerRoute("message-templates/99999"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreatesMessageTemplate()
    {
        var dto = new MessageTemplateCreateUpdateDto
        {
            EventType = "BookingConfirmed",
            Channel = "Email",
            ScopeType = "Tenant",
            Language = "en",
            Body = "Hello {{GuestName}}",
            IsActive = true
        };
        var response = await Client.PostAsJsonAsync(ApiControllerRoute("message-templates"), dto);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.MessageTemplates.CountAsync());
    }

    [Fact]
    public async Task Put_UpdatesMessageTemplate()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.MessageTemplates.Add(new MessageTemplate
        {
            EventType = "Test",
            Channel = "Email",
            ScopeType = "Tenant",
            Language = "en",
            Body = "Original",
            IsActive = true
        });
        await db.SaveChangesAsync();
        var id = (await db.MessageTemplates.FirstAsync()).Id;

        var dto = new MessageTemplateCreateUpdateDto
        {
            EventType = "Test",
            Channel = "Email",
            ScopeType = "Tenant",
            Language = "en",
            Body = "Updated",
            IsActive = true
        };
        var response = await Client.PutAsJsonAsync(ApiControllerRoute($"message-templates/{id}"), dto);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<MessageTemplateResponseDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Body);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.MessageTemplates.Add(new MessageTemplate
        {
            EventType = "Test",
            Channel = "Email",
            ScopeType = "Tenant",
            Language = "en",
            Body = "ToDelete",
            IsActive = true
        });
        await db.SaveChangesAsync();
        var id = (await db.MessageTemplates.FirstAsync()).Id;

        var response = await Client.DeleteAsync(ApiControllerRoute($"message-templates/{id}"));
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await Client.GetAsync(ApiControllerRoute($"message-templates/{id}"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
