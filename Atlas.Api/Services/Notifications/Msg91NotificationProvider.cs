using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Atlas.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Api.Services.Notifications;

/// <summary>MSG91 for SMS and WhatsApp; Email via SMTP (unified interface).</summary>
public sealed class Msg91NotificationProvider : INotificationProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Msg91Settings _msg91;
    private readonly SmtpConfig? _smtpConfig;
    private readonly ILogger<Msg91NotificationProvider> _logger;
    private const string ProviderName = "MSG91";

    public Msg91NotificationProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<Msg91Settings> msg91,
        IOptions<SmtpConfig>? smtpConfig,
        ILogger<Msg91NotificationProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _msg91 = msg91.Value;
        _smtpConfig = smtpConfig?.Value;
        _logger = logger;
    }

    public async Task<SendResult> SendSmsAsync(string to, string body, string? templateId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_msg91.AuthKey))
        {
            _logger.LogWarning("MSG91 AuthKey not configured; skipping SMS to {To}.", Mask(to));
            return SendResult.Fail("MSG91 not configured");
        }

        var mobile = NormalizeMobile(to);
        var template = templateId ?? _msg91.TemplateId;
        try
        {
            // MSG91 Flow API v5: https://docs.msg91.com/p/5ffa5810send-sms
            var payload = new
            {
                template_id = template,
                short_url = "0",
                recipients = new[] { new { mobiles = mobile, var = body } }
            };
            var client = _httpClientFactory.CreateClient(ProviderName);
            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.msg91.com/api/v5/flow/");
            req.Headers.Add("authkey", _msg91.AuthKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MSG91 SMS failed for {To}: {Status} {Body}.", Mask(to), response.StatusCode, responseText);
                return SendResult.Fail($"{response.StatusCode}: {responseText}");
            }
            var id = TryParseMessageId(responseText);
            _logger.LogInformation("MSG91 SMS sent to {To}.", Mask(to));
            return SendResult.Ok(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MSG91 SMS error for {To}.", Mask(to));
            return SendResult.Fail(ex.Message);
        }
    }

    public async Task<SendResult> SendWhatsAppAsync(string to, string body, string? templateId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_msg91.AuthKey))
        {
            _logger.LogWarning("MSG91 AuthKey not configured; skipping WhatsApp to {To}.", Mask(to));
            return SendResult.Fail("MSG91 not configured");
        }

        var mobile = NormalizeMobile(to);
        try
        {
            // MSG91 WhatsApp send message (text): https://docs.msg91.com/whatsapp/send-message-in-text
            var baseUrl = _msg91.WhatsAppBaseUrl?.TrimEnd('/') ?? "https://api.msg91.com/api/v5/whatsapp";
            var payload = new { to = mobile, body };
            var client = _httpClientFactory.CreateClient(ProviderName);
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/sendMessage");
            req.Headers.Add("authkey", _msg91.AuthKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MSG91 WhatsApp failed for {To}: {Status} {Body}.", Mask(to), response.StatusCode, responseText);
                return SendResult.Fail($"{response.StatusCode}: {responseText}");
            }
            var id = TryParseMessageId(responseText);
            _logger.LogInformation("MSG91 WhatsApp sent to {To}.", Mask(to));
            return SendResult.Ok(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MSG91 WhatsApp error for {To}.", Mask(to));
            return SendResult.Fail(ex.Message);
        }
    }

    public async Task<SendResult> SendEmailAsync(string to, string? subject, string body, CancellationToken cancellationToken = default)
    {
        if (_smtpConfig == null || string.IsNullOrWhiteSpace(_smtpConfig.Host))
        {
            _logger.LogWarning("SMTP not configured; skipping email to {To}.", Mask(to));
            return SendResult.Fail("SMTP not configured");
        }
        try
        {
            using var client = new SmtpClient(_smtpConfig.Host, _smtpConfig.Port)
            {
                EnableSsl = _smtpConfig.EnableSsl,
                Credentials = string.IsNullOrEmpty(_smtpConfig.Username) ? null : new NetworkCredential(_smtpConfig.Username, _smtpConfig.Password)
            };
            var message = new MailMessage(_smtpConfig.FromEmail, to, subject ?? "Notification", body) { IsBodyHtml = true };
            message.From = new MailAddress(_smtpConfig.FromEmail, _smtpConfig.FromName);
            await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Email sent to {To}.", Mask(to));
            return SendResult.Ok(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email error for {To}.", Mask(to));
            return SendResult.Fail(ex.Message);
        }
    }

    private static string NormalizeMobile(string phone)
    {
        var p = phone.Trim().Replace(" ", "").Replace("-", "");
        if (p.StartsWith("+91", StringComparison.Ordinal)) return p.Substring(3).TrimStart();
        if (p.StartsWith("91", StringComparison.Ordinal) && p.Length >= 12) return p;
        if (p.Length == 10 && p.All(char.IsDigit)) return "91" + p;
        return p;
    }

    private static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "***";
        if (value.Length <= 4) return "***";
        return value[^4..].PadLeft(value.Length, '*');
    }

    private static string? TryParseMessageId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("request_id", out var id)) return id.GetString();
            if (doc.RootElement.TryGetProperty("id", out var id2)) return id2.GetString();
        }
        catch { }
        return null;
    }
}
