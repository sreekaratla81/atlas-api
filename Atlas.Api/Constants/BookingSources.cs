namespace Atlas.Api.Constants;

public static class BookingSources
{
    public const string Airbnb = "airbnb";
    public const string BookingDotCom = "booking.com";
    public const string Agoda = "agoda";
    public const string Direct = "direct";
    public const string Manual = "Manual";

    public static bool IsOta(string? source) =>
        string.Equals(source, Airbnb, StringComparison.OrdinalIgnoreCase)
        || string.Equals(source, BookingDotCom, StringComparison.OrdinalIgnoreCase)
        || string.Equals(source, Agoda, StringComparison.OrdinalIgnoreCase);
}
