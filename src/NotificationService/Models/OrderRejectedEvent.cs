using Shared.Messaging;

namespace NotificationService.Models;

public sealed record OrderRejectedEvent : EventEnvelope
{
    public const string EventTypeValue = "order.rejected";
    public const string RoutingKey = EventTypeValue;
    public const string ReasonUnknownSku = "unknown_sku";
    public const string ReasonInsufficientStock = "insufficient_stock";

    public required Guid OrderId { get; init; }
    public required string Sku { get; init; }
    public required string Reason { get; init; }
}
