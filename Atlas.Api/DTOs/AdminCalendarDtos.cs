using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

/// <summary>Single calendar cell: availability and pricing for one listing-date.</summary>
public class AdminCalendarAvailabilityCellDto
{
    public DateTime Date { get; set; }
    public int ListingId { get; set; }
    public int RoomsAvailable { get; set; }
    public decimal EffectivePrice { get; set; }
    public decimal? PriceOverride { get; set; }
    public bool IsBlocked { get; set; }
}

/// <summary>Request body for bulk-upserting admin calendar availability cells.</summary>
public class AdminCalendarAvailabilityBulkUpsertRequestDto
{
    [Required]
    [MinLength(1)]
    public List<AdminCalendarAvailabilityCellUpsertDto> Cells { get; set; } = new();
}

/// <summary>Single cell within a bulk upsert request.</summary>
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

/// <summary>Response after bulk-upserting admin calendar availability cells.</summary>
public class AdminCalendarAvailabilityBulkUpsertResponseDto
{
    public int UpdatedCells { get; set; }
    public bool Deduplicated { get; set; }
    public List<AdminCalendarAvailabilityCellDto> Cells { get; set; } = new();
}
