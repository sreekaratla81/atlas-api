namespace Atlas.Api.Constants;

/// <summary>Canonical availability block status values. Use these instead of magic strings.</summary>
public static class BlockStatuses
{
    /// <summary>Active block — excludes the listing from guest availability queries.</summary>
    public const string Active = "Active";

    /// <summary>Temporary hold during Razorpay checkout. Expires after a configurable timeout.</summary>
    public const string Hold = "Hold";

    /// <summary>Admin-created block (manual calendar block).</summary>
    public const string Blocked = "Blocked";

    /// <summary>Hold that timed out without payment completion.</summary>
    public const string Expired = "Expired";

    /// <summary>Inventory is open/available.</summary>
    public const string Open = "Open";

    /// <summary>Returns true if this status should exclude the listing from availability.</summary>
    public static bool IsBlocking(string status) =>
        string.Equals(status, Active, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Blocked, StringComparison.OrdinalIgnoreCase);
}
