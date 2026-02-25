using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

/// <summary>Automation schedule data returned by the API.</summary>
public class AutomationScheduleDto
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public int BookingId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime DueAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Request to create a manual automation schedule.</summary>
public class CreateAutomationScheduleDto
{
    [Required]
    public int BookingId { get; set; }

    [Required]
    public string EventType { get; set; } = string.Empty;

    [Required]
    public DateTime DueAtUtc { get; set; }
}
