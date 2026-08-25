using System.Text.Json;
using NotificationService.Models;
using NotificationService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Messaging;

public sealed class OrderRejectedConsumer(
    IRabbitMqConnectionFactory connectionFactory,
    INotificationLog notificationLog,
    ILogger<OrderRejectedConsumer> logger)
    : RabbitMqConsumerBase(connectionFactory, logger)
{
    protected override string QueueName => NotificationsTopology.OrderRejectedQueue;

    protected override string RoutingKey => OrderRejectedEvent.RoutingKey;

    protected override async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs message,
        CancellationToken stoppingToken)
    {
        OrderRejectedEvent? rejected = null;
        try
        {
            rejected = JsonSerializer.Deserialize<OrderRejectedEvent>(message.Body.Span, SerializerOptions);
        }
        catch (JsonException)
        {
        }

        if (rejected is null || rejected.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(rejected.Sku))
        {
            logger.LogError(
                "Discarding malformed {EventType} message (delivery tag {DeliveryTag}): deserialization or schema validation failed",
                OrderRejectedEvent.EventTypeValue, message.DeliveryTag);

            await channel.BasicAckAsync(message.DeliveryTag, multiple: false, stoppingToken);
            return;
        }

        logger.LogInformation("[NOTIFY] order {OrderId} rejected ({Reason}) for {Sku}",
            rejected.OrderId, rejected.Reason, rejected.Sku);

        notificationLog.Add(new NotificationRecord
        {
            NotifiedAt = DateTimeOffset.UtcNow,
            Status = NotificationRecord.StatusRejected,
            OrderId = rejected.OrderId,
            Sku = rejected.Sku,
            Quantity = 0,
            Reason = rejected.Reason,
        });

        await channel.BasicAckAsync(message.DeliveryTag, multiple: false, stoppingToken);
    }
}
