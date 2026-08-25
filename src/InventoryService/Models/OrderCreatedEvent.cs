using Shared.Messaging;

namespace InventoryService.Models;

public sealed record OrderCreatedEvent : EventEnvelope
{
    public const string EventTypeValue = "order.created";
    public const string RoutingKey = EventTypeValue;

    public required Guid OrderId { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
}
