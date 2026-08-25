using System.Text.Json;
using InventoryService.Models;
using InventoryService.Services;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace InventoryService.Messaging;

public sealed class OrderCreatedConsumer(
    IRabbitMqConnectionFactory connectionFactory,
    IRabbitMqPublisher publisher,
    IStockTable stockTable,
    IProcessedOrderTable processedOrders,
    IOptions<ConsumingOptions> consumingOptions,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private const int InitialSetupBackoffMilliseconds = 500;
    private const int MaxSetupBackoffMilliseconds = 30_000;

    private readonly ConsumingOptions _options = consumingOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var (connection, channel) = await StartConsumingAsync(stoppingToken);

        logger.LogInformation("Consuming '{Queue}' with prefetch count {PrefetchCount} and dispatch concurrency {DispatchConcurrency}",
            InventoryTopology.OrderCreatedQueue, _options.PrefetchCount, _options.ConsumerDispatchConcurrency);

        if (_options.SimulatedProcessingDelayEnabled)
        {
            logger.LogInformation(
                "Simulated processing delay is ENABLED (demo mode): {Min}-{Max} ms per message; set Consuming__SimulatedProcessingDelayEnabled=false for load testing",
                _options.MinProcessingDelayMilliseconds, _options.MaxProcessingDelayMilliseconds);
        }

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

                await channel.BasicConsumeAsync(InventoryTopology.OrderCreatedQueue, autoAck: false, consumer, stoppingToken);

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
                    "Failed to start consuming '{Queue}', retrying in {BackoffMilliseconds} ms",
                    InventoryTopology.OrderCreatedQueue, backoffMilliseconds);

                await Task.Delay(backoffMilliseconds, stoppingToken);
            }
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken stoppingToken)
    {
        await channel.ExchangeDeclareAsync(InventoryTopology.TopicExchange, ExchangeType.Topic,
            durable: true, autoDelete: false, cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(InventoryTopology.DeadLetterExchange, ExchangeType.Fanout,
            durable: true, autoDelete: false, cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(InventoryTopology.OrderCreatedQueue, durable: true, exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = InventoryTopology.DeadLetterExchange,
            },
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(InventoryTopology.DeadLetterQueue, durable: true, exclusive: false,
            autoDelete: false, cancellationToken: stoppingToken);

        await channel.QueueBindAsync(InventoryTopology.OrderCreatedQueue, InventoryTopology.TopicExchange,
            OrderCreatedEvent.RoutingKey, cancellationToken: stoppingToken);

        await channel.QueueBindAsync(InventoryTopology.DeadLetterQueue, InventoryTopology.DeadLetterExchange,
            string.Empty, cancellationToken: stoppingToken);

        logger.LogInformation(
            "Declared queue '{Queue}' bound to '{TopicExchange}' on '{RoutingKey}' with x-dead-letter-exchange '{DeadLetterExchange}', and dead-letter queue '{DeadLetterQueue}' bound to '{DeadLetterExchange}'",
            InventoryTopology.OrderCreatedQueue, InventoryTopology.TopicExchange,
            OrderCreatedEvent.RoutingKey, InventoryTopology.DeadLetterExchange,
            InventoryTopology.DeadLetterQueue, InventoryTopology.DeadLetterExchange);
    }

    private async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs message,
        CancellationToken stoppingToken)
    {
        if (_options.SimulatedProcessingDelayEnabled)
        {
            var min = Math.Max(0, _options.MinProcessingDelayMilliseconds);
            var max = Math.Max(min, _options.MaxProcessingDelayMilliseconds);

            try
            {
                await Task.Delay(Random.Shared.Next(min, max), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        OrderCreatedEvent? orderCreated;
        try
        {
            orderCreated = JsonSerializer.Deserialize<OrderCreatedEvent>(message.Body.Span, SerializerOptions);
        }
        catch (JsonException)
        {
            orderCreated = null;
        }

        if (orderCreated is null || orderCreated.OrderId == Guid.Empty
            || string.IsNullOrWhiteSpace(orderCreated.Sku) || orderCreated.Quantity <= 0)
        {
            logger.LogWarning(
                "Malformed order.created message (delivery tag {DeliveryTag}) failed schema validation, dead-lettering via exchange '{DeadLetterExchange}'",
                message.DeliveryTag, InventoryTopology.DeadLetterExchange);

            await channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            return;
        }

        // Idempotency tradeoff: the processed-order mark is written only
        // AFTER the outcome event is published, not before mutating state.
        // A crash after reserve/publish but before the mark can replay this
        // delivery (duplicate stock decrement and/or duplicate outcome);
        // marking first would instead suppress the replay and silently lose
        // the order outcome entirely. Losing an outcome is the worse failure
        // for this system, so the duplicate window is the accepted risk.
        if (processedOrders.IsProcessed(orderCreated.OrderId))
        {
            logger.LogInformation(
                "Duplicate order.created for order {OrderId} (delivery tag {DeliveryTag}) already processed, acknowledging without reprocessing",
                orderCreated.OrderId, message.DeliveryTag);

            await channel.BasicAckAsync(message.DeliveryTag, multiple: false, stoppingToken);
            return;
        }

        var outcome = stockTable.Reserve(orderCreated.Sku, orderCreated.Quantity);

        switch (outcome.Status)
        {
            case ReserveStatus.Reserved:
                await publisher.PublishAsync(new OrderReservedEvent
                {
                    EventId = Guid.NewGuid(),
                    EventType = OrderReservedEvent.EventTypeValue,
                    OccurredAt = DateTimeOffset.UtcNow,
                    OrderId = orderCreated.OrderId,
                    Sku = orderCreated.Sku,
                    Quantity = orderCreated.Quantity,
                    RemainingStock = outcome.RemainingStock,
                }, OrderReservedEvent.RoutingKey, stoppingToken);

                logger.LogInformation("Order {OrderId} reserved {Sku} x{Quantity}, {RemainingStock} left in stock",
                    orderCreated.OrderId, orderCreated.Sku, orderCreated.Quantity, outcome.RemainingStock);
                break;

            case ReserveStatus.UnknownSku:
            case ReserveStatus.InsufficientStock:
                var reason = outcome.Status == ReserveStatus.UnknownSku
                    ? OrderRejectedEvent.ReasonUnknownSku
                    : OrderRejectedEvent.ReasonInsufficientStock;

                await publisher.PublishAsync(new OrderRejectedEvent
                {
                    EventId = Guid.NewGuid(),
                    EventType = OrderRejectedEvent.EventTypeValue,
                    OccurredAt = DateTimeOffset.UtcNow,
                    OrderId = orderCreated.OrderId,
                    Sku = orderCreated.Sku,
                    Reason = reason,
                }, OrderRejectedEvent.RoutingKey, stoppingToken);

                logger.LogInformation("Order {OrderId} rejected ({Reason}) for {Sku} x{Quantity}",
                    orderCreated.OrderId, reason, orderCreated.Sku, orderCreated.Quantity);
                break;
        }

        processedOrders.TryMark(orderCreated.OrderId);

        await channel.BasicAckAsync(message.DeliveryTag, multiple: false, stoppingToken);
    }
}
