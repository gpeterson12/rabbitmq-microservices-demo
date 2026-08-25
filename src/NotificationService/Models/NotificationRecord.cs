namespace NotificationService.Models;

public sealed record NotificationRecord
{
    public const string StatusReserved = "reserved";
    public const string StatusRejected = "rejected";

    public required DateTimeOffset NotifiedAt { get; init; }
    public required string Status { get; init; }
    public required Guid OrderId { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public int? RemainingStock { get; init; }
    public string? Reason { get; init; }
}
