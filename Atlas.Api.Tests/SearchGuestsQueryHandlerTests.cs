using Application.Guests.Queries.SearchGuests;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Infrastructure.Phone;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Api.Tests;

public class SearchGuestsQueryHandlerTests
{
    private static (SearchGuestsQueryHandler handler, AppDbContext ctx) GetHandler(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        var ctx = new AppDbContext(options);
        var handler = new SearchGuestsQueryHandler(ctx, new PhoneNormalizer(), new LoggerFactory().CreateLogger<SearchGuestsQueryHandler>());
        return (handler, ctx);
    }

    [Fact]
    public async Task Handle_FindsByPhone()
    {
        var (handler, ctx) = GetHandler(nameof(Handle_FindsByPhone));
        var guest = new Guest { Name = "Raj", Phone = "990198734", Email = "raj@example.com" };
        guest.NameSearch = guest.Name.ToLowerInvariant();
        guest.PhoneE164 = new PhoneNormalizer().Normalize(guest.Phone);
        ctx.Guests.Add(guest);
        ctx.SaveChanges();
        var result = await handler.Handle(new SearchGuestsRequest("990198734"), CancellationToken.None);
        Assert.Single(result.Items);
        Assert.Equal(guest.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task Handle_FindsByEmail()
    {
        var (handler, ctx) = GetHandler(nameof(Handle_FindsByEmail));
        var guest = new Guest { Name = "Raj", Phone = "1", Email = "raj@example.com" };
        guest.NameSearch = guest.Name.ToLowerInvariant();
        guest.PhoneE164 = new PhoneNormalizer().Normalize(guest.Phone);
        ctx.Guests.Add(guest);
        ctx.SaveChanges();
        var result = await handler.Handle(new SearchGuestsRequest("raj@example.com"), CancellationToken.None);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Handle_FindsByNamePrefix()
    {
        var (handler, ctx) = GetHandler(nameof(Handle_FindsByNamePrefix));
        var guest = new Guest { Name = "Rajesh", Phone = "1", Email = "r@example.com" };
        guest.NameSearch = guest.Name.ToLowerInvariant();
        ctx.Guests.Add(guest);
        ctx.SaveChanges();
        var result = await handler.Handle(new SearchGuestsRequest("ra"), CancellationToken.None);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Handle_FindsByNameContains()
    {
        var (handler, ctx) = GetHandler(nameof(Handle_FindsByNameContains));
        var guest = new Guest { Name = "Kiran Raj", Phone = "1", Email = "k@example.com" };
        guest.NameSearch = guest.Name.ToLowerInvariant();
        ctx.Guests.Add(guest);
        ctx.SaveChanges();
        var result = await handler.Handle(new SearchGuestsRequest("raj"), CancellationToken.None);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Handle_PagingWorks()
    {
        var (handler, ctx) = GetHandler(nameof(Handle_PagingWorks));
        for (int i = 0; i < 15; i++)
        {
            var g = new Guest { Name = $"Raj{i}", Phone = $"{i}", Email = $"r{i}@e.com" };
            g.NameSearch = g.Name.ToLowerInvariant();
            ctx.Guests.Add(g);
        }
        ctx.SaveChanges();
        var result = await handler.Handle(new SearchGuestsRequest("raj", Page:2, PageSize:5), CancellationToken.None);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.Items.Count);
    }
}
