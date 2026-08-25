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

    private const int InitialSetupBackoffMilliseconds = 500;
    private const int MaxSetupBackoffMilliseconds = 30_000;

    private readonly ConsumingOptions _options = consumingOptions.Value;

    protected abstract string QueueName { get; }

    protected abstract string RoutingKey { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var (connection, channel) = await StartConsumingAsync(stoppingToken);

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

    /// <summary>
    /// Establishes the connection and registers the consumer, retrying with
    /// exponential backoff so a transient broker failure during setup can
    /// never propagate out of ExecuteAsync (which would stop the host).
    /// </summary>
    private async Task<(IConnection Connection, IChannel Channel)> StartConsumingAsync(CancellationToken stoppingToken)
    {
        var backoffMilliseconds = InitialSetupBackoffMilliseconds;

        while (true)
        {
            IConnection? connection = null;
            IChannel? channel = null;

            try
            {
                connection = await connectionFactory.CreateConnectionAsync(stoppingToken);
                channel = await connection.CreateChannelAsync(new CreateChannelOptions(
                    publisherConfirmationsEnabled: false,
                    publisherConfirmationTrackingEnabled: false,
                    consumerDispatchConcurrency: _options.ConsumerDispatchConcurrency), stoppingToken);

                await DeclareTopologyAsync(channel, stoppingToken);

                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, message) => HandleMessageAsync(channel, message, stoppingToken);

                await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);

                return (connection, channel);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
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
                logger.LogWarning(ex,
                    "'{Consumer}' failed to start consuming '{Queue}', retrying in {BackoffMilliseconds} ms",
                    GetType().Name, QueueName, backoffMilliseconds);

                await Task.Delay(backoffMilliseconds, stoppingToken);
            }
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
