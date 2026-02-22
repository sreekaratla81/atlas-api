using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Transactions;

namespace Atlas.Api.IntegrationTests;

[Collection("IntegrationTests")]
public class BookingWorkflowFailureTests : IClassFixture<SqlServerTestDatabase>, IAsyncLifetime
{
    private readonly SqlServerTestDatabase _database;
    private FailingBookingWorkflowFactory _factory = null!;
    private HttpClient _client = null!;
    private TransactionScope? _testTransaction;

    public BookingWorkflowFailureTests(SqlServerTestDatabase database)
    {
        _database = database;
    }

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("Atlas_TestDb", _database.ConnectionString);
        _factory = new FailingBookingWorkflowFactory(_database.ConnectionString);
        _client = _factory.CreateClient();
        await IntegrationTestDatabase.ResetAsync(_factory);
        _testTransaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
    }

    public Task DisposeAsync()
    {
        _testTransaction?.Dispose();
        _testTransaction = null;
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact(Skip = "Obsolete: booking workflow now uses async outbox/Service Bus; test expected sync IBookingWorkflowPublisher flow and AttemptCount/CommunicationLogs.")]
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

        var response = await _client.PostAsJsonAsync(_factory.ApiRoute("bookings"), newBooking);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        var created = await db.Bookings.OrderByDescending(b => b.Id).FirstAsync();
        Assert.Equal("Confirmed", created.BookingStatus);

        var outbox = await db.OutboxMessages.SingleAsync();
        Assert.Equal(1, outbox.AttemptCount);
        Assert.Contains("Simulated publish failure", outbox.LastError);

        var logs = await db.CommunicationLogs.OrderBy(l => l.Id).ToListAsync();
        Assert.NotEmpty(logs);
        Assert.All(logs, log =>
        {
            Assert.Equal("Failed", log.Status);
            Assert.Equal("booking-confirmed", log.EventType);
            Assert.Equal("System", log.Provider);
            Assert.Equal(created.Id, log.BookingId);
            Assert.Equal(guest.Id, log.GuestId);
            Assert.False(string.IsNullOrWhiteSpace(log.ToAddress));
            Assert.False(string.IsNullOrWhiteSpace(log.CorrelationId));
            Assert.False(string.IsNullOrWhiteSpace(log.IdempotencyKey));
            Assert.Equal(1, log.AttemptCount);
            Assert.Contains("Simulated publish failure", log.LastError);
            Assert.Equal(0, log.TemplateVersion);
        });
    }

}

public sealed class FailingBookingWorkflowFactory : CustomWebApplicationFactory
{
    public FailingBookingWorkflowFactory(string? connectionString = null) : base(connectionString)
    {
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
#pragma warning disable CS0618
            services.AddScoped<IBookingWorkflowPublisher, FailingBookingWorkflowPublisher>();
#pragma warning restore CS0618
        });
    }
}

#pragma warning disable CS0618
public sealed class FailingBookingWorkflowPublisher : IBookingWorkflowPublisher
#pragma warning restore CS0618
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
