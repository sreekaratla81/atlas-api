namespace Atlas.Api.Services.WhatsApp;

/// <summary>Configuration for Meta WhatsApp Cloud API.</summary>
public class WhatsAppConfig
{
    public const string SectionName = "WhatsApp";

    /// <summary>Meta API access token (from WhatsApp API Setup or System User).</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Phone number ID from WhatsApp API Setup (identifies the sender number).</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>Graph API base URL (default: https://graph.facebook.com/v22.0).</summary>
    public string BaseUrl { get; set; } = "https://graph.facebook.com/v22.0";

    /// <summary>Approved template name. Use "hello_world" for testing (no approval); "booking_confirmation" for custom template with {{1}} {{2}} {{3}}.</summary>
    public string TemplateName { get; set; } = "hello_world";

    /// <summary>Template language code (e.g. en, en_US).</summary>
    public string TemplateLanguage { get; set; } = "en";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(PhoneNumberId);
}
