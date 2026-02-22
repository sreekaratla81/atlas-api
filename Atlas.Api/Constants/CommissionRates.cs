namespace Atlas.Api.Constants;

/// <summary>OTA commission rate constants. Used for revenue calculations in BookingsController and AdminReportsController.</summary>
public static class CommissionRates
{
    public const decimal Airbnb = 0.16m;
    public const decimal BookingDotCom = 0.15m;
    public const decimal Agoda = 0.18m;
    public const decimal Default = 0m;

    public static decimal ForSource(string? source) => source?.ToLowerInvariant() switch
    {
        "airbnb" => Airbnb,
        "booking.com" => BookingDotCom,
        "agoda" => Agoda,
        _ => Default
    };
}
