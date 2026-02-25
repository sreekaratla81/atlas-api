namespace Atlas.Api.Services.Notifications;

/// <summary>Stub provider that logs to console/ILogger. Used in dev/test or as fallback.</summary>
public sealed class ConsoleNotificationProvider : INotificationProvider
{
    private readonly ILogger<ConsoleNotificationProvider> _logger;

    public ConsoleNotificationProvider(ILogger<ConsoleNotificationProvider> logger) => _logger = logger;

    public Task<SendResult> SendSmsAsync(string to, string body, string? templateId = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[CONSOLE-SMS] To: {To} | Body: {Body}", to, Truncate(body));
        return Task.FromResult(SendResult.Ok($"console-sms-{Guid.NewGuid():N}"));
    }

    public Task<SendResult> SendWhatsAppAsync(string to, string body, string? templateId = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[CONSOLE-WA] To: {To} | Body: {Body}", to, Truncate(body));
        return Task.FromResult(SendResult.Ok($"console-wa-{Guid.NewGuid():N}"));
    }

    public Task<SendResult> SendEmailAsync(string to, string? subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[CONSOLE-EMAIL] To: {To} | Subject: {Subject} | Body: {Body}", to, subject, Truncate(body));
        return Task.FromResult(SendResult.Ok($"console-email-{Guid.NewGuid():N}"));
    }

    private static string Truncate(string s) => s.Length > 200 ? s[..200] + "..." : s;
}
