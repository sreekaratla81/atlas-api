namespace Atlas.Api.Services.WhatsApp;

/// <summary>Service for sending WhatsApp messages via Meta Cloud API.</summary>
public interface IWhatsAppService
{
    /// <summary>
    /// Sends a booking confirmation via an approved template (required for proactive messages).
    /// Uses TemplateName from config; hello_world = generic test, booking_confirmation = with params.
    /// </summary>
    /// <returns>Success, ProviderMessageId (if sent), Error (if failed, for logging).</returns>
    Task<(bool Success, string? ProviderMessageId, string? Error)> SendBookingConfirmationAsync(
        string toPhoneE164,
        int bookingId,
        DateTime checkinDate,
        DateTime checkoutDate,
        CancellationToken cancellationToken = default);
}
