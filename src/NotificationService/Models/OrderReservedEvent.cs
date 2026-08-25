using Shared.Messaging;

namespace NotificationService.Models;

public sealed record OrderReservedEvent : EventEnvelope
{
    public const string EventTypeValue = "order.reserved";
    public const string RoutingKey = EventTypeValue;

    public required Guid OrderId { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required int RemainingStock { get; init; }
}
