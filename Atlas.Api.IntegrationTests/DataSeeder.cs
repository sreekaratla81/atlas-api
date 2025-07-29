using Atlas.Api.Data;
using Atlas.Api.Models;

namespace Atlas.Api.IntegrationTests;

public static class DataSeeder
{
    public static async Task<Property> SeedPropertyAsync(AppDbContext db)
    {
        var property = new Property
        {
            Name = "Property",
            Address = "Addr",
            Type = "House",
            OwnerName = "Owner",
            ContactPhone = "000",
            CommissionPercent = 10,
            Status = "Active"
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        return property;
    }

    public static async Task<Listing> SeedListingAsync(AppDbContext db, Property property)
    {
        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing",
            Floor = 1,
            Type = "Room",
            Status = "Available",
            WifiName = "wifi",
            WifiPassword = "pass",
            MaxGuests = 2
        };
        db.Listings.Add(listing);
        await db.SaveChangesAsync();
        return listing;
    }

    public static async Task<Guest> SeedGuestAsync(AppDbContext db)
    {
        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com", IdProofUrl = "N/A" };
        db.Guests.Add(guest);
        await db.SaveChangesAsync();
        return guest;
    }

    public static async Task<Booking> SeedBookingAsync(AppDbContext db, Property property, Listing listing, Guest guest)
    {
        var booking = new Booking
        {
            ListingId = listing.Id,
            Listing = listing,
            GuestId = guest.Id,
            Guest = guest,
            BookingSource = "airbnb",
            PaymentStatus = "Pending",
            CheckinDate = DateTime.UtcNow.Date,
            CheckoutDate = DateTime.UtcNow.Date.AddDays(1),
            AmountReceived = 100,
            GuestsPlanned = 1,
            GuestsActual = 1,
            ExtraGuestCharge = 0,
            AmountGuestPaid = 100,
            CommissionAmount = 0,
            Notes = "note"
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }

    public static async Task<Payment> SeedPaymentAsync(AppDbContext db, Booking booking)
    {
        var payment = new Payment
        {
            BookingId = booking.Id,
            Amount = 50,
            Method = "cash",
            Type = "partial",
            ReceivedOn = DateTime.UtcNow,
            Note = "first"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return payment;
    }

    public static async Task<BankAccount> SeedBankAccountAsync(AppDbContext db)
    {
        var account = new BankAccount
        {
            BankName = "Bank",
            AccountNumber = "123",
            IFSC = "IFSC",
            AccountType = "Savings"
        };
        db.BankAccounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    public static async Task<Incident> SeedIncidentAsync(AppDbContext db, Listing listing, Booking? booking = null)
    {
        var incident = new Incident
        {
            ListingId = listing.Id,
            BookingId = booking?.Id,
            Description = "desc",
            ActionTaken = "none",
            Status = "open",
            CreatedBy = "tester",
            CreatedOn = DateTime.UtcNow
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();
        return incident;
    }

    public static async Task<User> SeedUserAsync(AppDbContext db)
    {
        var user = new User
        {
            Name = "User",
            Phone = "123",
            Email = "user@example.com",
            PasswordHash = "hash",
            Role = "admin"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
