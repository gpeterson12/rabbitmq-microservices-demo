using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Messaging;

internal static class NotificationsTopology
{
    public const string TopicExchange = "orders.topic";
    public const string OrderReservedQueue = "notification.order-reserved";
    public const string OrderRejectedQueue = "notification.order-rejected";
}

public abstract class RabbitMqConsumerBase(
    IRabbitMqConnectionFactory connectionFactory,
    IOptions<ConsumingOptions> consumingOptions,
    ILogger logger)
    : BackgroundService
{
    protected static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ConsumingOptions _options = consumingOptions.Value;

    protected abstract string QueueName { get; }

    protected abstract string RoutingKey { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await connectionFactory.CreateConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            consumerDispatchConcurrency: _options.ConsumerDispatchConcurrency), stoppingToken);

        await DeclareTopologyAsync(channel, stoppingToken);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, message) => HandleMessageAsync(channel, message, stoppingToken);

        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("'{Consumer}' consuming '{Queue}' with prefetch count {PrefetchCount} and dispatch concurrency {DispatchConcurrency}",
            GetType().Name, QueueName, _options.PrefetchCount, _options.ConsumerDispatchConcurrency);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await channel.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken stoppingToken)
    {
        await channel.ExchangeDeclareAsync(NotificationsTopology.TopicExchange, ExchangeType.Topic,
            durable: true, autoDelete: false, cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false,
            autoDelete: false, cancellationToken: stoppingToken);

        await channel.QueueBindAsync(QueueName, NotificationsTopology.TopicExchange, RoutingKey,
            cancellationToken: stoppingToken);

        logger.LogInformation("Declared queue '{Queue}' bound to '{Exchange}' on routing key '{RoutingKey}'",
            QueueName, NotificationsTopology.TopicExchange, RoutingKey);
    }

    protected abstract Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs message,
        CancellationToken stoppingToken);
}
