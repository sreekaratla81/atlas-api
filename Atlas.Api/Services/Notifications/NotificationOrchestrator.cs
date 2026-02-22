using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services.Notifications;

/// <summary>Maps events to templates, enforces idempotency via CommunicationLog, sends via INotificationProvider.</summary>
public sealed class NotificationOrchestrator
{
    private readonly AppDbContext _db;
    private readonly INotificationProvider _provider;
    private readonly ILogger<NotificationOrchestrator> _logger;
    private const string ProviderName = "MSG91";

    public NotificationOrchestrator(AppDbContext db, INotificationProvider provider, ILogger<NotificationOrchestrator> logger)
    {
        _db = db;
        _provider = provider;
        _logger = logger;
    }

    /// <summary>Handle an event from the bus: load templates, ensure idempotent send per channel, send and log.</summary>
    public async Task HandleEventAsync(int tenantId, string eventType, string? entityId, string? correlationId, string eventId, string payloadJson, CancellationToken cancellationToken = default)
    {
        var bookingId = TryGetBookingIdFromPayload(payloadJson, entityId);
        var payload = FlattenPayload(payloadJson);
        var templates = await _db.MessageTemplates
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && m.EventType == eventType && m.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (templates.Count == 0)
        {
            _logger.LogWarning("No active templates for tenant {TenantId} event {EventType}; notification skipped. Configure templates to enable this channel.", tenantId, eventType);
            return;
        }

        correlationId ??= eventId;
        foreach (var template in templates)
        {
            var channel = template.Channel?.Trim() ?? "";
            if (string.IsNullOrEmpty(channel)) continue;

            var toAddress = ResolveToAddress(channel, payload);
            if (string.IsNullOrWhiteSpace(toAddress))
            {
                _logger.LogDebug("No recipient for channel {Channel} event {EventType} booking {BookingId}.", channel, eventType, bookingId);
                continue;
            }

            var idempotencyKey = $"{eventId}:{bookingId}:{channel}:{template.Id}";
            var log = await TryCreateLogAsync(tenantId, bookingId, template, eventType, toAddress, correlationId, idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (log == null)
                continue; // already sent (idempotent)

            var body = ReplacePlaceholders(template.Body, payload);
            var subject = template.Subject != null ? ReplacePlaceholders(template.Subject, payload) : null;

            try
            {
                SendResult result;
                if (channel.Equals("SMS", StringComparison.OrdinalIgnoreCase))
                    result = await _provider.SendSmsAsync(toAddress, body, template.TemplateKey, cancellationToken).ConfigureAwait(false);
                else if (channel.Equals("WhatsApp", StringComparison.OrdinalIgnoreCase))
                    result = await _provider.SendWhatsAppAsync(toAddress, body, template.TemplateKey, cancellationToken).ConfigureAwait(false);
                else if (channel.Equals("Email", StringComparison.OrdinalIgnoreCase))
                    result = await _provider.SendEmailAsync(toAddress, subject, body, cancellationToken).ConfigureAwait(false);
                else
                {
                    _logger.LogWarning("Unknown channel {Channel} for template {TemplateId}.", channel, template.Id);
                    continue;
                }

                log.AttemptCount++;
                if (result.Success)
                {
                    log.Status = "Sent";
                    log.ProviderMessageId = result.ProviderMessageId;
                    log.SentAtUtc = DateTime.UtcNow;
                }
                else
                {
                    log.Status = "Failed";
                    log.LastError = result.Error;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Send failed for template {TemplateId} channel {Channel}.", template.Id, channel);
                log.AttemptCount++;
                log.Status = "Failed";
                log.LastError = ex.Message;
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string? TryGetBookingIdFromPayload(string payloadJson, string? entityId)
    {
        if (!string.IsNullOrEmpty(entityId)) return entityId;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("bookingId", out var b)) return b.GetRawText();
        }
        catch { }
        return null;
    }

    private static Dictionary<string, string> FlattenPayload(string payloadJson)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            FlattenElement(doc.RootElement, "", d);
        }
        catch { }
        return d;
    }

    private static void FlattenElement(JsonElement el, string prefix, Dictionary<string, string> target)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                    FlattenElement(p.Value, prefix == "" ? p.Name : $"{prefix}.{p.Name}", target);
                break;
            case JsonValueKind.String:
                target[prefix] = el.GetString() ?? "";
                break;
            case JsonValueKind.Number:
                target[prefix] = el.GetRawText();
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                target[prefix] = el.GetBoolean() ? "true" : "false";
                break;
            default:
                break;
        }
    }

    private static string ResolveToAddress(string channel, IReadOnlyDictionary<string, string> payload)
    {
        if (channel.Equals("Email", StringComparison.OrdinalIgnoreCase))
            return payload.TryGetValue("guestEmail", out var e) ? e : (payload.TryGetValue("guest.Email", out var e2) ? e2 : "");
        if (channel.Equals("SMS", StringComparison.OrdinalIgnoreCase) || channel.Equals("WhatsApp", StringComparison.OrdinalIgnoreCase))
            return payload.TryGetValue("guestPhone", out var p) ? p : (payload.TryGetValue("guest.Phone", out var p2) ? p2 : "");
        return "";
    }

    private static string ReplacePlaceholders(string text, IReadOnlyDictionary<string, string> payload)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Regex.Replace(text, @"\{\{(\w+)\}\}", m =>
        {
            var key = m.Groups[1].Value;
            return payload.TryGetValue(key, out var v) ? v : m.Value;
        });
    }

    /// <summary>Insert CommunicationLog; returns the entity if inserted, null if unique violation (already sent).</summary>
    private async Task<CommunicationLog?> TryCreateLogAsync(int tenantId, string? bookingId, MessageTemplate template, string eventType, string toAddress, string correlationId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var bookingIdInt = int.TryParse(bookingId, out var b) ? (int?)b : null;
        var log = new CommunicationLog
        {
            TenantId = tenantId,
            BookingId = bookingIdInt,
            Channel = template.Channel,
            EventType = eventType,
            ToAddress = toAddress.Length <= 100 ? toAddress : toAddress[..100],
            TemplateId = template.Id,
            TemplateVersion = template.TemplateVersion,
            CorrelationId = correlationId.Length <= 100 ? correlationId : correlationId[..100],
            IdempotencyKey = idempotencyKey.Length <= 150 ? idempotencyKey : idempotencyKey[..150],
            Provider = ProviderName,
            Status = "Pending",
            AttemptCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.CommunicationLogs.Add(log);
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return log;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _logger.LogDebug("Idempotent skip: {IdempotencyKey} already sent.", idempotencyKey);
            return null;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("IX_CommunicationLog_TenantId_IdempotencyKey", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }
}
