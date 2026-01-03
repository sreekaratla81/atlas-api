using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Atlas.Api.IntegrationTests;

public class BookingWorkflowFailureTests : IClassFixture<FailingBookingWorkflowFactory>, IAsyncLifetime
{
    private readonly FailingBookingWorkflowFactory _factory;
    private HttpClient _client = null!;

    public BookingWorkflowFailureTests(FailingBookingWorkflowFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Post_CreatesBooking_WhenWorkflowPublisherFails()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = new Property
        {
            Name = "Test Property",
            Address = "123 Street",
            Type = "House",
            OwnerName = "Owner",
            ContactPhone = "555-0000",
            CommissionPercent = 10,
            Status = "Active"
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Test Listing",
            Floor = 1,
            Type = "Room",
            Status = "Available",
            WifiName = "wifi",
            WifiPassword = "pass",
            MaxGuests = 2
        };
        db.Listings.Add(listing);

        var guest = new Guest
        {
            Name = "Guest",
            Phone = "123456",
            Email = "guest@example.com",
            IdProofUrl = "N/A"
        };
        db.Guests.Add(guest);
        await db.SaveChangesAsync();

        var newBooking = new
        {
            listingId = listing.Id,
            guestId = guest.Id,
            checkinDate = DateTime.UtcNow.Date,
            checkoutDate = DateTime.UtcNow.Date.AddDays(2),
            bookingSource = "airbnb",
            bookingStatus = "Confirmed",
            paymentStatus = "Pending",
            amountReceived = 200m,
            guestsPlanned = 2,
            guestsActual = 2,
            extraGuestCharge = 0m,
            notes = "create"
        };

        var response = await _client.PostAsJsonAsync("/api/bookings", newBooking);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        var created = await db.Bookings.OrderByDescending(b => b.Id).FirstAsync();
        Assert.Equal("Confirmed", created.BookingStatus);

        var outbox = await db.OutboxMessages.SingleAsync();
        Assert.Equal("Failed", outbox.Status);
        Assert.Contains("Simulated publish failure", outbox.ErrorMessage);
    }

    private async Task ResetDatabaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }
}

public sealed class FailingBookingWorkflowFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddScoped<IBookingWorkflowPublisher, FailingBookingWorkflowPublisher>();
        });
    }
}

public sealed class FailingBookingWorkflowPublisher : IBookingWorkflowPublisher
{
    public Task PublishBookingConfirmedAsync(
        Booking booking,
        Guest guest,
        IReadOnlyCollection<CommunicationLog> communicationLogs,
        OutboxMessage outboxMessage)
    {
        throw new InvalidOperationException("Simulated publish failure");
    }
}
