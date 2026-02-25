namespace Atlas.Api.Services.Notifications;

/// <summary>Wraps a primary and fallback INotificationProvider. If primary fails, tries fallback.</summary>
public sealed class FallbackNotificationProvider : INotificationProvider
{
    private readonly INotificationProvider _primary;
    private readonly INotificationProvider _fallback;
    private readonly ILogger<FallbackNotificationProvider> _logger;

    public FallbackNotificationProvider(
        INotificationProvider primary,
        INotificationProvider fallback,
        ILogger<FallbackNotificationProvider> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<SendResult> SendSmsAsync(string to, string body, string? templateId = null, CancellationToken cancellationToken = default)
    {
        var result = await _primary.SendSmsAsync(to, body, templateId, cancellationToken);
        if (result.Success) return result;
        _logger.LogWarning("Primary SMS failed for {To}: {Error}. Trying fallback.", to, result.Error);
        return await _fallback.SendSmsAsync(to, body, templateId, cancellationToken);
    }

    public async Task<SendResult> SendWhatsAppAsync(string to, string body, string? templateId = null, CancellationToken cancellationToken = default)
    {
        var result = await _primary.SendWhatsAppAsync(to, body, templateId, cancellationToken);
        if (result.Success) return result;
        _logger.LogWarning("Primary WhatsApp failed for {To}: {Error}. Trying fallback.", to, result.Error);
        return await _fallback.SendWhatsAppAsync(to, body, templateId, cancellationToken);
    }

    public async Task<SendResult> SendEmailAsync(string to, string? subject, string body, CancellationToken cancellationToken = default)
    {
        var result = await _primary.SendEmailAsync(to, subject, body, cancellationToken);
        if (result.Success) return result;
        _logger.LogWarning("Primary email failed for {To}: {Error}. Trying fallback.", to, result.Error);
        return await _fallback.SendEmailAsync(to, subject, body, cancellationToken);
    }
}
