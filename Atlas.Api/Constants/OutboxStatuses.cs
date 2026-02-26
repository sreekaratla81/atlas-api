namespace Atlas.Api.Constants;

/// <summary>Outbox message lifecycle statuses.</summary>
public static class OutboxStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Published = "Published";
    public const string Failed = "Failed";
}
