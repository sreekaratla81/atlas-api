namespace Atlas.Api.Services.Communication;

/// <summary>Sends notifications (Email, SMS, WhatsApp) for domain events with idempotency.</summary>
public interface ICommunicationSender
{
    /// <summary>
    /// Handles BookingConfirmedEvent: sends Email (SMTP), SMS (mock), WhatsApp (mock).
    /// Idempotent: skips if CommunicationLog already has a record for the same AggregateId + Channel.
    /// </summary>
    Task SendBookingConfirmedAsync(
        string aggregateId,
        string correlationId,
        int tenantId,
        Events.BookingConfirmedEvent payload,
        CancellationToken cancellationToken = default);
}
