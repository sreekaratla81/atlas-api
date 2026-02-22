namespace Atlas.Api.Options;

/// <summary>Azure Service Bus configuration. Single topics; multi-tenant via application properties.</summary>
public class AzureServiceBusOptions
{
    public const string SectionName = "AzureServiceBus";

    public string ConnectionString { get; set; } = string.Empty;
    public bool EnableSessions { get; set; } = true;

    public string TopicBookingEvents { get; set; } = "booking.events";
    public string TopicStayEvents { get; set; } = "stay.events";
    public string TopicWhatsAppInbound { get; set; } = "whatsapp.inbound";

    public string SubscriptionNotifications { get; set; } = "notifications";
    public string SubscriptionScheduler { get; set; } = "scheduler";
    public string SubscriptionConversationOrchestrator { get; set; } = "conversation-orchestrator";

    public int MaxConcurrentSessions { get; set; } = 4;

    public int OutboxBatchSize { get; set; } = 50;
    public int OutboxPollIntervalSeconds { get; set; } = 15;
    public int OutboxMaxAttempts { get; set; } = 5;
}
