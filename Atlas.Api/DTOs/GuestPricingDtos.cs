using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

/// <summary>
/// Guest: Availability and nightly rate for a listing in a date range.
/// ListingDailyRate overrides base/weekend rate. Returns roomsAvailable and isAvailable per day.
/// </summary>
public class GuestAvailabilityRateResponseDto
{
    public int ListingId { get; set; }
    public string ListingName { get; set; } = string.Empty;
    public string Currency { get; set; } = "INR";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<GuestDayAvailabilityRateDto> Days { get; set; } = new();
}

/// <summary>Single day's availability and nightly rate for guest view.</summary>
public class GuestDayAvailabilityRateDto
{
    public string Date { get; set; } = string.Empty; // yyyy-MM-dd
    public int RoomsAvailable { get; set; }
    public bool IsAvailable { get; set; }
    public decimal NightlyRate { get; set; }
}

/// <summary>
/// Guest: Request for availability and nightly rate.
/// </summary>
public class GuestAvailabilityRateRequestDto
{
    public int ListingId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}
