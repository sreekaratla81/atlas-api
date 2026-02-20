namespace Atlas.Api.DTOs;

/// <summary>
/// Safe listing shape for public/guest discovery. Excludes WifiName, WifiPassword, TenantId, and internal-only fields.
/// </summary>
public class PublicListingDto
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string? PropertyAddress { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Floor { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? CheckInTime { get; set; }
    public string? CheckOutTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public int MaxGuests { get; set; }
}
