using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Tests;

public class AdminCalendarControllerTests
{
    [Fact]
    public async Task GetAvailability_ReturnsJoinedCalendarData()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetAvailability_ReturnsJoinedCalendarData))
            .Options;

        using var context = new AppDbContext(options);
        var property = new Property { Id = 1, Name = "P", Address = "A", Type = "Villa", OwnerName = "O", ContactPhone = "1", Status = "Active" };
        var listing = new Listing { Id = 1, PropertyId = 1, Property = property, Name = "L", Floor = 1, Type = "Room", Status = "Active", WifiName = "w", WifiPassword = "p", MaxGuests = 2 };

        context.Properties.Add(property);
        context.Listings.Add(listing);
        context.ListingPricings.Add(new ListingPricing { ListingId = 1, Listing = listing, BaseNightlyRate = 100m, WeekendNightlyRate = 150m });
        context.ListingDailyRates.Add(new ListingDailyRate { ListingId = 1, Listing = listing, Date = new DateTime(2025, 1, 5), NightlyRate = 175m, Source = "Manual" });
        context.ListingDailyInventories.Add(new ListingDailyInventory { ListingId = 1, Listing = listing, Date = new DateTime(2025, 1, 5), RoomsAvailable = 3, Source = "Manual" });
        context.AvailabilityBlocks.Add(new AvailabilityBlock
        {
            ListingId = 1,
            Listing = listing,
            StartDate = new DateTime(2025, 1, 6),
            EndDate = new DateTime(2025, 1, 7),
            BlockType = "Booking",
            Source = "System",
            Status = "Active",
            Inventory = false
        });
        await context.SaveChangesAsync();

        var controller = new AdminCalendarController(context);
        var result = await controller.GetAvailability(1, new DateTime(2025, 1, 5), 2);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<AdminCalendarAvailabilityCellDto>>(ok.Value).ToList();
        Assert.Equal(2, payload.Count);

        var first = payload.Single(x => x.Date == new DateTime(2025, 1, 5));
        Assert.Equal(3, first.RoomsAvailable);
        Assert.Equal(175m, first.EffectivePrice);
        Assert.Equal(175m, first.PriceOverride);
        Assert.False(first.IsBlocked);

        var second = payload.Single(x => x.Date == new DateTime(2025, 1, 6));
        Assert.True(second.IsBlocked);
        Assert.Equal(0, second.RoomsAvailable);
    }

    [Fact]
    public async Task UpsertAvailability_ReturnsBadRequest_ForNegativeValues()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(UpsertAvailability_ReturnsBadRequest_ForNegativeValues))
            .Options;

        using var context = new AppDbContext(options);
        var controller = new AdminCalendarController(context);

        var result = await controller.UpsertAvailability(new AdminCalendarAvailabilityBulkUpsertRequestDto
        {
            Cells =
            [
                new AdminCalendarAvailabilityCellUpsertDto
                {
                    ListingId = 1,
                    Date = new DateTime(2025, 1, 1),
                    RoomsAvailable = -1,
                    PriceOverride = -2
                }
            ]
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpsertAvailability_DeduplicatesUsingIdempotencyKey()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(UpsertAvailability_DeduplicatesUsingIdempotencyKey))
            .Options;

        using var context = new AppDbContext(options);
        var property = new Property { Id = 1, Name = "P", Address = "A", Type = "Villa", OwnerName = "O", ContactPhone = "1", Status = "Active" };
        var listing = new Listing { Id = 1, PropertyId = 1, Property = property, Name = "L", Floor = 1, Type = "Room", Status = "Active", WifiName = "w", WifiPassword = "p", MaxGuests = 2 };
        context.Properties.Add(property);
        context.Listings.Add(listing);
        await context.SaveChangesAsync();

        var request = new AdminCalendarAvailabilityBulkUpsertRequestDto
        {
            Cells =
            [
                new AdminCalendarAvailabilityCellUpsertDto { ListingId = 1, Date = new DateTime(2025, 1, 2), RoomsAvailable = 2, PriceOverride = 250m }
            ]
        };

        var firstController = new AdminCalendarController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        firstController.Request.Headers["Idempotency-Key"] = "dup-key";

        var firstResult = await firstController.UpsertAvailability(request);
        var firstOk = Assert.IsType<OkObjectResult>(firstResult.Result);
        var firstPayload = Assert.IsType<AdminCalendarAvailabilityBulkUpsertResponseDto>(firstOk.Value);
        Assert.False(firstPayload.Deduplicated);

        var secondController = new AdminCalendarController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        secondController.Request.Headers["Idempotency-Key"] = "dup-key";

        var secondResult = await secondController.UpsertAvailability(request);
        var secondOk = Assert.IsType<OkObjectResult>(secondResult.Result);
        var secondPayload = Assert.IsType<AdminCalendarAvailabilityBulkUpsertResponseDto>(secondOk.Value);
        Assert.True(secondPayload.Deduplicated);
        Assert.Single(secondPayload.Cells);
    }

    [Fact]
    public async Task UpsertAvailability_DoesNotDeduplicateAcrossTenants()
    {
        var databaseName = nameof(UpsertAvailability_DoesNotDeduplicateAcrossTenants);

        await using var tenantOneContext = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName).Options,
            new StubTenantContextAccessor(1));
        await using var tenantTwoContext = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName).Options,
            new StubTenantContextAccessor(2));

        tenantOneContext.Properties.Add(new Property { Name = "P1", Address = "A", Type = "Villa", OwnerName = "O", ContactPhone = "1", Status = "Active" });
        await tenantOneContext.SaveChangesAsync();
        var tenantOneProperty = await tenantOneContext.Properties.SingleAsync();
        tenantOneContext.Listings.Add(new Listing { PropertyId = tenantOneProperty.Id, Property = tenantOneProperty, Name = "L1", Floor = 1, Type = "Room", Status = "Active", WifiName = "w", WifiPassword = "p", MaxGuests = 2 });
        await tenantOneContext.SaveChangesAsync();
        var tenantOneListing = await tenantOneContext.Listings.SingleAsync();

        tenantTwoContext.Properties.Add(new Property { Name = "P2", Address = "A", Type = "Villa", OwnerName = "O", ContactPhone = "1", Status = "Active" });
        await tenantTwoContext.SaveChangesAsync();
        var tenantTwoProperty = await tenantTwoContext.Properties.SingleAsync();
        tenantTwoContext.Listings.Add(new Listing { PropertyId = tenantTwoProperty.Id, Property = tenantTwoProperty, Name = "L2", Floor = 1, Type = "Room", Status = "Active", WifiName = "w", WifiPassword = "p", MaxGuests = 2 });
        await tenantTwoContext.SaveChangesAsync();
        var tenantTwoListing = await tenantTwoContext.Listings.SingleAsync();

        var tenantTwoController = new AdminCalendarController(tenantTwoContext)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        tenantTwoController.Request.Headers["Idempotency-Key"] = "shared-key";

        var tenantTwoResult = await tenantTwoController.UpsertAvailability(new AdminCalendarAvailabilityBulkUpsertRequestDto
        {
            Cells = [new AdminCalendarAvailabilityCellUpsertDto { ListingId = tenantTwoListing.Id, Date = new DateTime(2025, 1, 2), RoomsAvailable = 2, PriceOverride = 250m }]
        });
        var tenantTwoPayload = Assert.IsType<AdminCalendarAvailabilityBulkUpsertResponseDto>(Assert.IsType<OkObjectResult>(tenantTwoResult.Result).Value);
        Assert.False(tenantTwoPayload.Deduplicated);

        var tenantOneController = new AdminCalendarController(tenantOneContext)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        tenantOneController.Request.Headers["Idempotency-Key"] = "shared-key";

        var tenantOneResult = await tenantOneController.UpsertAvailability(new AdminCalendarAvailabilityBulkUpsertRequestDto
        {
            Cells = [new AdminCalendarAvailabilityCellUpsertDto { ListingId = tenantOneListing.Id, Date = new DateTime(2025, 1, 2), RoomsAvailable = 3, PriceOverride = 275m }]
        });
        var tenantOnePayload = Assert.IsType<AdminCalendarAvailabilityBulkUpsertResponseDto>(Assert.IsType<OkObjectResult>(tenantOneResult.Result).Value);
        Assert.False(tenantOnePayload.Deduplicated);
    }

    private sealed class StubTenantContextAccessor : ITenantContextAccessor
    {
        public StubTenantContextAccessor(int tenantId)
        {
            TenantId = tenantId;
        }

        public int? TenantId { get; }
    }
}
