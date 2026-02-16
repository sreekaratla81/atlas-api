using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using Atlas.Api.Options;

namespace Atlas.Api.Services.EventBus;

public sealed class AzureServiceBusPublisher : IEventBusPublisher
{
    private readonly AzureServiceBusOptions _options;
    private readonly ServiceBusClient _client;

    public AzureServiceBusPublisher(IOptions<AzureServiceBusOptions> options)
    {
        _options = options.Value;
        _client = new ServiceBusClient(_options.ConnectionString);
    }

    public async Task PublishAsync(string topic, string messageId, string? sessionId, IReadOnlyDictionary<string, object> applicationProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
    {
        var sender = _client.CreateSender(topic);
        try
        {
            var message = new ServiceBusMessage(body)
            {
                MessageId = messageId,
                CorrelationId = applicationProperties.TryGetValue("CorrelationId", out var c) ? c?.ToString() : null,
                SessionId = sessionId,
            };
            foreach (var kv in applicationProperties)
                message.ApplicationProperties[kv.Key] = kv.Value;

            await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await sender.DisposeAsync().ConfigureAwait(false);
        }
    }
}
