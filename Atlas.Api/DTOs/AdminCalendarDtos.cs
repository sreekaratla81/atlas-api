using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

public class AdminCalendarAvailabilityCellDto
{
    public DateTime Date { get; set; }
    public int ListingId { get; set; }
    public int RoomsAvailable { get; set; }
    public decimal EffectivePrice { get; set; }
    public decimal? PriceOverride { get; set; }
    public bool IsBlocked { get; set; }
}

public class AdminCalendarAvailabilityBulkUpsertRequestDto
{
    [Required]
    [MinLength(1)]
    public List<AdminCalendarAvailabilityCellUpsertDto> Cells { get; set; } = new();
}

public class AdminCalendarAvailabilityCellUpsertDto
{
    [Required]
    public int ListingId { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Range(0, int.MaxValue)]
    public int RoomsAvailable { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? PriceOverride { get; set; }
}

public class AdminCalendarAvailabilityBulkUpsertResponseDto
{
    public int UpdatedCells { get; set; }
    public bool Deduplicated { get; set; }
    public List<AdminCalendarAvailabilityCellDto> Cells { get; set; } = new();
}
