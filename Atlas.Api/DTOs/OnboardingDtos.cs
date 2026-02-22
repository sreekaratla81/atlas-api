using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

public class OnboardingStartRequestDto
{
    [Required, MaxLength(200), EmailAddress]
    public string Email { get; set; } = null!;

    [Required, MaxLength(20)]
    public string Phone { get; set; } = null!;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = null!;

    [Required, MinLength(6)]
    public string Password { get; set; } = null!;

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(10)]
    public string? Pincode { get; set; }

    [MaxLength(30)]
    public string? PropertyType { get; set; }

    public int? RoomCount { get; set; }

    [MaxLength(500)]
    public string? AddressLine { get; set; }

    [MaxLength(500)]
    public string? AirbnbUrl { get; set; }
}

public class OnboardingStartResponseDto
{
    public string TenantSlug { get; set; } = null!;
    public int TenantId { get; set; }
    public int PropertyId { get; set; }
    public int ListingId { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = null!;
    public string NextStep { get; set; } = null!;
}

public class OnboardingStatusResponseDto
{
    public string OnboardingStatus { get; set; } = null!;
    public OnboardingProfileSummaryDto Profile { get; set; } = null!;
    public List<OnboardingChecklistItemDto> Checklist { get; set; } = new();
    public List<string> PublishBlockers { get; set; } = new();
}

public class OnboardingProfileSummaryDto
{
    public string? DisplayName { get; set; }
    public string? LegalName { get; set; }
    public string? BusinessType { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public bool HasPan { get; set; }
    public string? Gstin { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? PrimaryPhone { get; set; }
}

public class OnboardingChecklistItemDto
{
    public string Key { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Stage { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool Blocking { get; set; }
    public DateTime? DueAtUtc { get; set; }
}

public class OnboardingProfileUpdateDto
{
    [MaxLength(200)]
    public string? LegalName { get; set; }

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    [MaxLength(30)]
    public string? BusinessType { get; set; }

    [MaxLength(500)]
    public string? RegisteredAddressLine { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(50)]
    public string? State { get; set; }

    [MaxLength(10)]
    public string? Pincode { get; set; }

    /// <summary>Full PAN (ABCDE1234F). Stored as hash + last4 only.</summary>
    [RegularExpression(@"^[A-Z]{5}[0-9]{4}[A-Z]$", ErrorMessage = "Invalid PAN format.")]
    public string? Pan { get; set; }

    [MaxLength(15)]
    public string? Gstin { get; set; }

    [MaxLength(50)]
    public string? PlaceOfSupplyState { get; set; }

    [MaxLength(200), EmailAddress]
    public string? PrimaryEmail { get; set; }

    [MaxLength(20)]
    public string? PrimaryPhone { get; set; }
}

public class AirbnbPrefillRequestDto
{
    [MaxLength(500)]
    public string? AirbnbUrl { get; set; }

    public string? PastedText { get; set; }
}

public class AirbnbPrefillResponseDto
{
    public string? Title { get; set; }
    public string? LocationText { get; set; }
    public string? PropertyType { get; set; }
    public List<string> Amenities { get; set; } = new();
    public List<string> HouseRules { get; set; } = new();
    public int? MaxGuests { get; set; }
    public string? Description { get; set; }
    public string Source { get; set; } = null!;
    public List<string> Warnings { get; set; } = new();
}

public class HostKycDocumentDto
{
    public int Id { get; set; }
    public string DocType { get; set; } = null!;
    public string? FileUrl { get; set; }
    public string? OriginalFileName { get; set; }
    public string Status { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
