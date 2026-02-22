namespace Atlas.Api.DTOs;

/// <summary>Listing data returned by the API. Excludes WiFi credentials.</summary>
public class ListingResponseDto
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string Name { get; set; } = null!;
    public int Floor { get; set; }
    public string Type { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int MaxGuests { get; set; }
}
