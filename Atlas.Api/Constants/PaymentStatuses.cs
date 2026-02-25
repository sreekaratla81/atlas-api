namespace Atlas.Api.Constants;

/// <summary>Canonical payment status values.</summary>
public static class PaymentStatuses
{
    public const string Pending = "pending";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Refunded = "refunded";
    public const string PartiallyRefunded = "partially_refunded";

    public static bool IsCompleted(string status) =>
        string.Equals(status, Completed, StringComparison.OrdinalIgnoreCase);

    public static bool IsRefunded(string status) =>
        string.Equals(status, Refunded, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, PartiallyRefunded, StringComparison.OrdinalIgnoreCase);
}
