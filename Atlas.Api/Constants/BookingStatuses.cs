namespace Atlas.Api.Constants;

/// <summary>Canonical booking status values. Use these instead of magic strings.</summary>
public static class BookingStatuses
{
    public const string Lead = "Lead";
    public const string Hold = "Hold";

    /// <summary>Draft created during Razorpay order. No inventory blocked yet.
    /// Transitions to Confirmed on payment success, or is hard-deleted on failure.</summary>
    public const string PaymentPending = "PaymentPending";

    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
    public const string CheckedIn = "CheckedIn";
    public const string CheckedOut = "CheckedOut";

    public static bool IsConfirmed(string status) =>
        string.Equals(status, Confirmed, StringComparison.OrdinalIgnoreCase);

    public static bool IsCancelled(string status) =>
        string.Equals(status, Cancelled, StringComparison.OrdinalIgnoreCase);

    public static bool IsPaymentPending(string status) =>
        string.Equals(status, PaymentPending, StringComparison.OrdinalIgnoreCase);

    public static bool IsTerminal(string status) =>
        IsCancelled(status) || string.Equals(status, Expired, StringComparison.OrdinalIgnoreCase);
}
