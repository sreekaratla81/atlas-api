using Atlas.Api.Data;
using Atlas.Api.Events;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Api.Services.Communication;

/// <summary>Sends Email (SMTP), SMS (mock), WhatsApp (mock) for BookingConfirmedEvent with idempotency.</summary>
public sealed class CommunicationSender : ICommunicationSender
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ILogger<CommunicationSender> _logger;

    public CommunicationSender(
        AppDbContext db,
        IEmailService emailService,
        ILogger<CommunicationSender> logger)
    {
        _db = db;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task SendBookingConfirmedAsync(
        string aggregateId,
        string correlationId,
        int tenantId,
        BookingConfirmedEvent payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing BookingConfirmed for aggregate {AggregateId}, tenant {TenantId}.", aggregateId, tenantId);

        var channels = new[] { "Email", "SMS", "WhatsApp" };
        foreach (var channel in channels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SendForChannelAsync(aggregateId, correlationId, tenantId, channel, payload, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendForChannelAsync(
        string aggregateId,
        string correlationId,
        int tenantId,
        string channel,
        BookingConfirmedEvent payload,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = $"{EventTypes.BookingConfirmed}:{aggregateId}:{channel}";

        var exists = await _db.CommunicationLogs
            .IgnoreQueryFilters()
            .AnyAsync(
                c => c.TenantId == tenantId && c.IdempotencyKey == idempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            _logger.LogDebug("Skipping {Channel} for aggregate {AggregateId} (already sent).", channel, aggregateId);
            return;
        }

        bool success;
        string? providerMessageId = null;

        switch (channel)
        {
            case "Email":
                success = await SendEmailAsync(payload, cancellationToken).ConfigureAwait(false);
                break;
            case "SMS":
                (success, providerMessageId) = await SendSmsMockAsync(payload, cancellationToken).ConfigureAwait(false);
                break;
            case "WhatsApp":
                (success, providerMessageId) = await SendWhatsAppMockAsync(payload, cancellationToken).ConfigureAwait(false);
                break;
            default:
                _logger.LogWarning("Unknown channel {Channel}, skipping.", channel);
                return;
        }

        var log = new CommunicationLog
        {
            TenantId = tenantId,
            BookingId = payload.BookingId,
            GuestId = payload.GuestId,
            Channel = channel,
            EventType = EventTypes.BookingConfirmed,
            ToAddress = channel == "Email" ? (payload.GuestEmail ?? "") : (payload.GuestPhone ?? ""),
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
            Provider = channel == "Email" ? "SMTP" : "Mock",
            ProviderMessageId = providerMessageId,
            Status = success ? "Sent" : "Failed",
            AttemptCount = 1,
            SentAtUtc = success ? DateTime.UtcNow : null
        };

        _db.CommunicationLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (success)
            _logger.LogInformation("Sent {Channel} for booking {BookingId}.", channel, payload.BookingId);
        else
            _logger.LogWarning("Failed to send {Channel} for booking {BookingId}.", channel, payload.BookingId);
    }

    private async Task<bool> SendEmailAsync(BookingConfirmedEvent payload, CancellationToken cancellationToken)
    {
        var email = payload.GuestEmail?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogDebug("Skipping Email for booking {BookingId}: no guest email.", payload.BookingId);
            return true; // Not a failure, just nothing to send
        }

        var guestName = "Guest";
        try
        {
            var booking = await _db.Bookings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(b => b.Listing)
                .ThenInclude(l => l!.Property)
                .FirstOrDefaultAsync(b => b.Id == payload.BookingId, cancellationToken)
                .ConfigureAwait(false);

            if (booking?.Guest != null)
            {
                guestName = booking.Guest.Name ?? guestName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load guest name for booking {BookingId}, using default.", payload.BookingId);
        }

        var propertyName = "Property";
        try
        {
            var listing = await _db.Listings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l => l.Id == payload.ListingId, cancellationToken)
                .ConfigureAwait(false);
            propertyName = listing?.Property?.Name ?? listing?.Name ?? propertyName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load listing for {ListingId}, using default property name.", payload.ListingId);
        }

        return await _emailService.SendBookingConfirmationEmailAsync(
            guestName,
            email,
            payload.BookingId.ToString(),
            propertyName,
            adults: 1,
            payload.CheckinDate,
            payload.CheckoutDate,
            totalAmount: 0,
            "INR",
            paymentId: "").ConfigureAwait(false);
    }

    private Task<(bool Success, string? ProviderMessageId)> SendSmsMockAsync(BookingConfirmedEvent payload, CancellationToken cancellationToken)
    {
        var phone = payload.GuestPhone?.Trim();
        if (string.IsNullOrWhiteSpace(phone))
        {
            _logger.LogDebug("Skipping SMS for booking {BookingId}: no guest phone.", payload.BookingId);
            return Task.FromResult((true, (string?)null)); // No-op, consider success
        }

        var body = $"Your booking {payload.BookingId} is confirmed. Check-in: {payload.CheckinDate:yyyy-MM-dd}, Check-out: {payload.CheckoutDate:yyyy-MM-dd}.";
        _logger.LogInformation("SMS (mock): To={Phone}, Body={Body}", phone, body);
        return Task.FromResult<(bool Success, string? ProviderMessageId)>((true, "mock-sms-" + Guid.NewGuid().ToString("N")[..12]));
    }

    private Task<(bool Success, string? ProviderMessageId)> SendWhatsAppMockAsync(BookingConfirmedEvent payload, CancellationToken cancellationToken)
    {
        var phone = payload.GuestPhone?.Trim();
        if (string.IsNullOrWhiteSpace(phone))
        {
            _logger.LogDebug("Skipping WhatsApp for booking {BookingId}: no guest phone.", payload.BookingId);
            return Task.FromResult((true, (string?)null));
        }

        var body = $"Your booking {payload.BookingId} is confirmed. Check-in: {payload.CheckinDate:yyyy-MM-dd}, Check-out: {payload.CheckoutDate:yyyy-MM-dd}.";
        _logger.LogInformation("WhatsApp (mock): To={Phone}, Body={Body}", phone, body);
        return Task.FromResult<(bool Success, string? ProviderMessageId)>((true, "mock-wa-" + Guid.NewGuid().ToString("N")[..12]));
    }
}
