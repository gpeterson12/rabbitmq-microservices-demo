namespace OrderService.Models;

public sealed record CreateOrderRequest
{
    public string Sku { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string CustomerEmail { get; init; } = string.Empty;
}

public sealed record Order
{
    public required Guid OrderId { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required string CustomerEmail { get; init; }
    public required string Status { get; init; }
}

public sealed record OrderAcceptedResponse(Guid OrderId, string Status);
