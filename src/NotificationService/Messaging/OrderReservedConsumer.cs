using System.Text.Json;
using Microsoft.Extensions.Options;
using NotificationService.Models;
using NotificationService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Messaging;

public sealed class OrderReservedConsumer(
    IRabbitMqConnectionFactory connectionFactory,
    INotificationLog notificationLog,
    IProcessedEventTable processedEvents,
    IOptions<ConsumingOptions> consumingOptions,
    ILogger<OrderReservedConsumer> logger)
    : RabbitMqConsumerBase(connectionFactory, consumingOptions, logger)
{
    protected override string QueueName => NotificationsTopology.OrderReservedQueue;

    protected override string RoutingKey => OrderReservedEvent.RoutingKey;

    protected override async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs message,
        CancellationToken stoppingToken)
    {
        OrderReservedEvent? reserved = null;
        try
        {
            reserved = JsonSerializer.Deserialize<OrderReservedEvent>(message.Body.Span, SerializerOptions);
        }
        catch (JsonException)
        {
        }

        if (reserved is null || reserved.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(reserved.Sku))
        {
            logger.LogError(
                "Discarding malformed {EventType} message (delivery tag {DeliveryTag}): deserialization or schema validation failed",
                OrderReservedEvent.EventTypeValue, message.DeliveryTag);

            await channel.BasicAckAsync(message.DeliveryTag, multiple: false, stoppingToken);
            return;
        }

        if (!processedEvents.TryMark(reserved.EventId))
        {
            logger.LogInformation(
                "Duplicate {EventType} event {EventId} (delivery tag {DeliveryTag}) already processed, acknowledging without recording",
                OrderReservedEvent.EventTypeValue, reserved.EventId, message.DeliveryTag);

            await channel.BasicAckAsync(message.DeliveryTag, multiple: false, stoppingToken);
            return;
        }

        logger.LogInformation("[NOTIFY] order {OrderId} reserved for {Sku} x{Quantity}, {RemainingStock} left in stock",
            reserved.OrderId, reserved.Sku, reserved.Quantity, reserved.RemainingStock);

        notificationLog.Add(new NotificationRecord
        {
            NotifiedAt = DateTimeOffset.UtcNow,
            Status = NotificationRecord.StatusReserved,
            OrderId = reserved.OrderId,
            Sku = reserved.Sku,
            Quantity = reserved.Quantity,
            RemainingStock = reserved.RemainingStock,
        });

        await channel.BasicAckAsync(message.DeliveryTag, multiple: false, stoppingToken);
    }
}
