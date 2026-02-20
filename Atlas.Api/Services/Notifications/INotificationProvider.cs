namespace Atlas.Api.Services.Notifications;

/// <summary>Unified provider for SMS, WhatsApp, and Email (MSG91 or composite).</summary>
public interface INotificationProvider
{
    Task<SendResult> SendSmsAsync(string to, string body, string? templateId = null, CancellationToken cancellationToken = default);
    Task<SendResult> SendWhatsAppAsync(string to, string body, string? templateId = null, CancellationToken cancellationToken = default);
    Task<SendResult> SendEmailAsync(string to, string? subject, string body, CancellationToken cancellationToken = default);
}

public sealed class SendResult
{
    public bool Success { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? Error { get; init; }

    public static SendResult Ok(string? providerMessageId = null) => new() { Success = true, ProviderMessageId = providerMessageId };
    public static SendResult Fail(string error) => new() { Success = false, Error = error };
}
