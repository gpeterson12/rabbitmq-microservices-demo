using System.Text.Json;
using RabbitMQ.Client;
using Shared.Messaging;

namespace OrderService.Messaging;

internal static class OrdersTopology
{
    public const string TopicExchange = "orders.topic";
    public const string DeadLetterExchange = "orders.dlx";
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

    private const int InitialSetupBackoffMilliseconds = 500;
    private const int MaxSetupBackoffMilliseconds = 30_000;

    private readonly IRabbitMqConnectionFactory _connectionFactory = connectionFactory;
    private readonly ILogger<RabbitMqPublisher> _logger = logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public bool IsConnected => _channel is { IsOpen: true };

    /// <summary>
    /// Establishes the connection and confirm-enabled publish channel,
    /// retrying with exponential backoff so a transient broker failure
    /// during setup can never propagate out of the hosted initializer
    /// (which would stop the host). Connection/channel fields are assigned
    /// only after setup fully succeeds, so a failed attempt can never leave
    /// a partially initialized publisher behind.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return;
        }

        var backoffMilliseconds = InitialSetupBackoffMilliseconds;

        while (true)
        {
            IConnection? connection = null;
            IChannel? channel = null;

            try
            {
                connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

                channel = await connection.CreateChannelAsync(new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true), cancellationToken);

                await DeclareTopologyAsync(channel, cancellationToken);

                _connection = connection;
                _channel = channel;
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                if (channel is not null)
                {
                    await channel.DisposeAsync();
                }

                if (connection is not null)
                {
                    await connection.DisposeAsync();
                }

                backoffMilliseconds = Math.Min(backoffMilliseconds * 2, MaxSetupBackoffMilliseconds);
                _logger.LogWarning(ex,
                    "Failed to initialize RabbitMQ publisher, retrying in {BackoffMilliseconds} ms",
                    backoffMilliseconds);

                await Task.Delay(backoffMilliseconds, cancellationToken);
            }
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(OrdersTopology.TopicExchange, ExchangeType.Topic,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(OrdersTopology.DeadLetterExchange, ExchangeType.Fanout,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);

        _logger.LogInformation("Declared exchanges '{TopicExchange}' (topic) and '{DeadLetterExchange}' (fanout)",
            OrdersTopology.TopicExchange, OrdersTopology.DeadLetterExchange);
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

        await channel.BasicPublishAsync(OrdersTopology.TopicExchange, routingKey,
            mandatory: false, properties, body, cancellationToken);

        _logger.LogInformation(
            "Published {EventType} event {EventId} to exchange '{Exchange}' with routing key '{RoutingKey}'",
            @event.EventType, @event.EventId, OrdersTopology.TopicExchange, routingKey);
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
