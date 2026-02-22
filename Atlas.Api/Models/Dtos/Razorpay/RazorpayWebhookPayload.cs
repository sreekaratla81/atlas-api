using System.Text.Json.Serialization;

namespace Atlas.Api.Models.Dtos.Razorpay;

/// <summary>Razorpay webhook payload shape (subset needed for reconciliation).</summary>
public class RazorpayWebhookPayload
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public RazorpayWebhookPayloadData? Payload { get; set; }

    [JsonPropertyName("account_id")]
    public string? AccountId { get; set; }
}

public class RazorpayWebhookPayloadData
{
    [JsonPropertyName("payment")]
    public RazorpayWebhookEntity<RazorpayPaymentEntity>? Payment { get; set; }
}

public class RazorpayWebhookEntity<T>
{
    [JsonPropertyName("entity")]
    public T? Entity { get; set; }
}

public class RazorpayPaymentEntity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("order_id")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string? Method { get; set; }
}
