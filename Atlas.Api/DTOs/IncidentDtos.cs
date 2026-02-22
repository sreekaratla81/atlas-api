using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

public class IncidentCreateDto
{
    [Required]
    public int ListingId { get; set; }

    public int? BookingId { get; set; }

    [Required]
    public string Description { get; set; } = null!;

    [Required]
    public string ActionTaken { get; set; } = null!;

    [Required]
    public string Status { get; set; } = null!;

    [Required]
    public string CreatedBy { get; set; } = null!;
}

public class IncidentResponseDto
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public int? BookingId { get; set; }
    public string Description { get; set; } = null!;
    public string ActionTaken { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedOn { get; set; }
}
