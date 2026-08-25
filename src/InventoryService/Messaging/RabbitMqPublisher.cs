using System.Text.Json;
using RabbitMQ.Client;
using Shared.Messaging;

namespace InventoryService.Messaging;

internal static class InventoryTopology
{
    public const string TopicExchange = "orders.topic";
    public const string DeadLetterExchange = "orders.dlx";
    public const string DeadLetterQueue = "orders.dead-letter";
    public const string OrderCreatedQueue = "inventory.order-created";
}

public interface IRabbitMqPublisher
{
    bool IsConnected { get; }

    Task InitializeAsync(CancellationToken cancellationToken);

    Task PublishAsync<TEvent>(TEvent @event, string routingKey, CancellationToken cancellationToken)
        where TEvent : EventEnvelope;
}

public sealed class RabbitMqPublisher(IRabbitMqConnectionFactory connectionFactory, ILogger<RabbitMqPublisher> logger)
    : IRabbitMqPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IRabbitMqConnectionFactory _connectionFactory = connectionFactory;
    private readonly ILogger<RabbitMqPublisher> _logger = logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public bool IsConnected => _channel is { IsOpen: true };

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return;
        }

        _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        _channel = await _connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true), cancellationToken);

        await DeclareTopologyAsync(_channel, cancellationToken);
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(InventoryTopology.TopicExchange, ExchangeType.Topic,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(InventoryTopology.DeadLetterExchange, ExchangeType.Fanout,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);

        _logger.LogInformation("Declared exchanges '{TopicExchange}' (topic) and '{DeadLetterExchange}' (fanout)",
            InventoryTopology.TopicExchange, InventoryTopology.DeadLetterExchange);
    }

    public async Task PublishAsync<TEvent>(TEvent @event, string routingKey, CancellationToken cancellationToken)
        where TEvent : EventEnvelope
    {
        var channel = _channel ?? throw new InvalidOperationException("RabbitMQ publisher has not been initialized");

        var body = JsonSerializer.SerializeToUtf8Bytes(@event, SerializerOptions);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = @event.EventId.ToString(),
            Type = @event.EventType,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        };

        await channel.BasicPublishAsync(InventoryTopology.TopicExchange, routingKey,
            mandatory: false, properties, body, cancellationToken);

        _logger.LogInformation(
            "Published {EventType} event {EventId} to exchange '{Exchange}' with routing key '{RoutingKey}'",
            @event.EventType, @event.EventId, InventoryTopology.TopicExchange, routingKey);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
