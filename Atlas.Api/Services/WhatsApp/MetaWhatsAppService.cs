using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Api.Services.WhatsApp;

/// <summary>Sends WhatsApp messages via Meta Cloud API.</summary>
public sealed class MetaWhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppConfig _config;
    private readonly ILogger<MetaWhatsAppService> _logger;

    public MetaWhatsAppService(
        IHttpClientFactory httpClientFactory,
        IOptions<WhatsAppConfig> config,
        ILogger<MetaWhatsAppService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("MetaWhatsApp");
        _config = config.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ProviderMessageId, string? Error)> SendBookingConfirmationAsync(
        string toPhoneE164,
        int bookingId,
        DateTime checkinDate,
        DateTime checkoutDate,
        CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured)
        {
            var msg = "WhatsApp not configured: missing AccessToken or PhoneNumberId.";
            _logger.LogWarning("{Msg}", msg);
            return (false, null, msg);
        }

        var to = NormalizePhone(toPhoneE164);
        if (string.IsNullOrEmpty(to))
        {
            var msg = $"Invalid phone number for WhatsApp: {toPhoneE164}";
            _logger.LogWarning("{Msg}", msg);
            return (false, null, msg);
        }

        var templateName = string.IsNullOrWhiteSpace(_config.TemplateName) ? "hello_world" : _config.TemplateName.Trim();
        var lang = string.IsNullOrWhiteSpace(_config.TemplateLanguage) ? "en_US" : _config.TemplateLanguage.Trim();
        // Meta's hello_world template requires en_US (not "en"); use en_US for hello_world.
        if (string.Equals(templateName, "hello_world", StringComparison.OrdinalIgnoreCase))
            lang = "en_US";

        object templatePayload;
        if (string.Equals(templateName, "hello_world", StringComparison.OrdinalIgnoreCase))
        {
            templatePayload = new
            {
                name = "hello_world",
                language = new { code = "en_US", policy = "deterministic" }
            };
        }
        else
        {
            var bodyParams = new[]
            {
                new { type = "text", text = bookingId.ToString() },
                new { type = "text", text = checkinDate.ToString("yyyy-MM-dd") },
                new { type = "text", text = checkoutDate.ToString("yyyy-MM-dd") }
            };
            templatePayload = new
            {
                name = templateName,
                language = new { code = lang },
                components = new[]
                {
                    new { type = "body", parameters = bodyParams }
                }
            };
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to,
            type = "template",
            template = templatePayload
        };

        var url = $"{_config.BaseUrl.TrimEnd('/')}/{_config.PhoneNumberId}/messages";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.AccessToken);
        request.Content = JsonContent.Create(payload);

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var msgId = doc.RootElement.TryGetProperty("messages", out var msgs)
                    && msgs.GetArrayLength() > 0
                    && msgs[0].TryGetProperty("id", out var id)
                    ? id.GetString()
                    : null;
                _logger.LogInformation("WhatsApp sent to {To}, messageId={MessageId}", to, msgId);
                return (true, msgId, null);
            }

            var error = $"WhatsApp API {response.StatusCode}: {content}";
            _logger.LogWarning("{Error}", error);
            return (false, null, error);
        }
        catch (Exception ex)
        {
            var error = $"{ex.GetType().Name}: {ex.Message}";
            _logger.LogError(ex, "WhatsApp send failed to {To}", to);
            return (false, null, error);
        }
    }

    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 10) return string.Empty;
        // Meta API expects E.164 without +. Indian 10-digit (6–9) → prepend 91.
        if (digits.Length == 10 && digits[0] >= '6' && digits[0] <= '9')
            return "91" + digits;
        return digits;
    }
}
