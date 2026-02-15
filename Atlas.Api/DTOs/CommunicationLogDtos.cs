namespace Atlas.Api.DTOs;

public class CommunicationLogDto
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public int? BookingId { get; set; }
    public int? GuestId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public int? TemplateId { get; set; }
    public int TemplateVersion { get; set; }
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? LastError { get; set; }
}
