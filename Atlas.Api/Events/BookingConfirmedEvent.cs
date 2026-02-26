using System.Text.Json.Serialization;

namespace Atlas.Api.Events;

/// <summary>DTO for BookingConfirmed outbox event payload.</summary>
public sealed class BookingConfirmedEvent
{
    [JsonPropertyName("bookingId")]
    public int BookingId { get; set; }

    [JsonPropertyName("guestId")]
    public int GuestId { get; set; }

    [JsonPropertyName("listingId")]
    public int ListingId { get; set; }

    [JsonPropertyName("bookingStatus")]
    public string? BookingStatus { get; set; }

    [JsonPropertyName("checkinDate")]
    public DateTime CheckinDate { get; set; }

    [JsonPropertyName("checkoutDate")]
    public DateTime CheckoutDate { get; set; }

    [JsonPropertyName("guestPhone")]
    public string? GuestPhone { get; set; }

    [JsonPropertyName("guestEmail")]
    public string? GuestEmail { get; set; }

    [JsonPropertyName("occurredAtUtc")]
    public DateTime OccurredAtUtc { get; set; }
}
